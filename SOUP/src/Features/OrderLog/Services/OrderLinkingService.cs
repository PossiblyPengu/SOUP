using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for linking order items into groups and managing linked group operations.
/// Handles validation, group unification, and timestamp synchronization for linked items.
/// </summary>
public class OrderLinkingService
{
    private readonly OrderTimestampSyncService _timestampSyncService;

    public OrderLinkingService(OrderTimestampSyncService timestampSyncService)
    {
        _timestampSyncService = timestampSyncService;
    }

    /// <summary>
    /// Result of a linking operation with details about what was linked.
    /// </summary>
    public class LinkResult
    {
        public bool Success { get; set; }
        public Guid? GroupId { get; set; }
        public List<OrderItem> LinkedItems { get; set; } = new();
        public string? FailureReason { get; set; }
    }

    /// <summary>
    /// Links the provided items together with the target into a single LinkedGroupId.
    /// Only links active (non-archived) items of the same NoteType.
    /// If any item already belongs to a group, groups are unified.
    /// </summary>
    /// <param name="itemsToLink">Items to link together</param>
    /// <param name="target">Target item to link to</param>
    /// <param name="activeItems">Collection of active (non-archived) items</param>
    /// <returns>Result of the linking operation</returns>
    public LinkResult LinkItems(
        List<OrderItem> itemsToLink,
        OrderItem? target,
        IEnumerable<OrderItem> activeItems)
    {
        // Validation
        var validationResult = ValidateForLinking(itemsToLink, target, activeItems);
        if (!validationResult.Success)
            return validationResult;

        var activeItemsById = activeItems.ToDictionary(i => i.Id);

        // Resolve target and candidates from active items
        var (resolvedTarget, candidates) = ResolveTargetAndCandidates(
            itemsToLink,
            target!,
            activeItemsById);

        if (resolvedTarget == null || candidates.Count == 0)
        {
            return new LinkResult
            {
                Success = false,
                FailureReason = "No valid items to link after resolution"
            };
        }

        // Determine the group ID for the linked items
        var groupId = DetermineGroupId(resolvedTarget, candidates);

        // Assign group ID to target and candidates
        AssignGroupId(resolvedTarget, candidates, groupId);

        // Unify any existing groups into the new group
        UnifyExistingGroups(activeItemsById.Values, candidates, resolvedTarget, groupId);

        // Get all items now in this group
        var allGroupItems = activeItemsById.Values
            .Where(i => i.LinkedGroupId == groupId)
            .ToList();

        // Sync timestamps across the linked group using target as reference
        if (allGroupItems.Count > 1)
        {
            _timestampSyncService.SyncLinkedGroupTimestamps(allGroupItems, resolvedTarget);
        }

        return new LinkResult
        {
            Success = true,
            GroupId = groupId,
            LinkedItems = allGroupItems
        };
    }

    /// <summary>
    /// Validates that the linking operation can proceed.
    /// </summary>
    private LinkResult ValidateForLinking(
        List<OrderItem>? itemsToLink,
        OrderItem? target,
        IEnumerable<OrderItem> activeItems)
    {
        if (itemsToLink == null || itemsToLink.Count == 0)
        {
            return new LinkResult
            {
                Success = false,
                FailureReason = "No items to link"
            };
        }

        if (target == null)
        {
            return new LinkResult
            {
                Success = false,
                FailureReason = "Target is null"
            };
        }

        if (!target.IsRenderable)
        {
            return new LinkResult
            {
                Success = false,
                FailureReason = "Target is not renderable"
            };
        }

        var activeItemsById = activeItems.ToDictionary(i => i.Id);
        if (!activeItemsById.ContainsKey(target.Id))
        {
            return new LinkResult
            {
                Success = false,
                FailureReason = "Target is not in active items (archived items cannot be linked)"
            };
        }

        return new LinkResult { Success = true };
    }

    /// <summary>
    /// Resolves the target and candidate items from the active items collection.
    /// Filters out archived items, non-renderable items, and items with mismatched NoteType.
    /// </summary>
    private (OrderItem? Target, List<OrderItem> Candidates) ResolveTargetAndCandidates(
        List<OrderItem> itemsToLink,
        OrderItem target,
        Dictionary<Guid, OrderItem> activeItemsById)
    {
        // Resolve target from active items
        if (!activeItemsById.TryGetValue(target.Id, out var actualTarget))
            return (null, new List<OrderItem>());

        // Resolve candidates from active items only
        var candidates = new List<OrderItem>();
        foreach (var item in itemsToLink)
        {
            if (item == null) continue;
            if (!activeItemsById.TryGetValue(item.Id, out var knownItem)) continue;
            if (!knownItem.IsRenderable) continue;

            // Enforce same NoteType as target
            if (knownItem.NoteType == actualTarget.NoteType)
            {
                candidates.Add(knownItem);
            }
        }

        return (actualTarget, candidates);
    }

    /// <summary>
    /// Determines the group ID to use for linking.
    /// Prefers target's existing group, then any candidate's group, then creates new.
    /// </summary>
    private Guid DetermineGroupId(OrderItem target, List<OrderItem> candidates)
    {
        if (target.LinkedGroupId != null)
            return target.LinkedGroupId.Value;

        var existingGroupId = candidates
            .Select(c => c.LinkedGroupId)
            .FirstOrDefault(g => g != null);

        return existingGroupId ?? Guid.NewGuid();
    }

    /// <summary>
    /// Assigns the group ID to the target and all candidate items.
    /// </summary>
    private void AssignGroupId(OrderItem target, List<OrderItem> candidates, Guid groupId)
    {
        target.LinkedGroupId = groupId;

        foreach (var candidate in candidates)
        {
            candidate.LinkedGroupId = groupId;
        }
    }

    /// <summary>
    /// Unifies any existing groups by pulling in all active items that belonged
    /// to the same groups as the candidates or target.
    /// </summary>
    private void UnifyExistingGroups(
        IEnumerable<OrderItem> allActiveItems,
        List<OrderItem> candidates,
        OrderItem target,
        Guid newGroupId)
    {
        // Collect all group IDs that need to be unified
        var groupsToUnify = new HashSet<Guid>(
            candidates
                .Select(c => c.LinkedGroupId ?? Guid.Empty)
                .Where(g => g != Guid.Empty));

        if (target.LinkedGroupId != null)
        {
            groupsToUnify.Add(target.LinkedGroupId.Value);
        }

        // Assign new group ID to all items in the groups being unified
        foreach (var item in allActiveItems)
        {
            if (item.LinkedGroupId != null &&
                groupsToUnify.Contains(item.LinkedGroupId.Value) &&
                item.IsRenderable)
            {
                item.LinkedGroupId = newGroupId;
            }
        }
    }

    /// <summary>
    /// Expands a partial selection to include all items in the linked group.
    /// Useful for ensuring operations affect all linked items.
    /// </summary>
    /// <param name="items">Items to expand</param>
    /// <param name="allActiveItems">All active items to search within</param>
    /// <returns>Full set of linked items</returns>
    public List<OrderItem> ExpandToFullLinkedGroups(
        IEnumerable<OrderItem> items,
        IEnumerable<OrderItem> allActiveItems)
    {
        var groupIds = items
            .Where(i => i.LinkedGroupId != null)
            .Select(i => i.LinkedGroupId!.Value)
            .Distinct()
            .ToHashSet();

        if (groupIds.Count == 0)
            return items.ToList();

        var result = new List<OrderItem>();
        var addedIds = new HashSet<Guid>();

        foreach (var item in allActiveItems)
        {
            if (item.LinkedGroupId != null && groupIds.Contains(item.LinkedGroupId.Value))
            {
                if (addedIds.Add(item.Id))
                {
                    result.Add(item);
                }
            }
        }

        // Add any items that weren't in a linked group
        foreach (var item in items)
        {
            if (item.LinkedGroupId == null && addedIds.Add(item.Id))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
