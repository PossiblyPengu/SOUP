using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Core.Entities.ExpireWise;

namespace SOUP.Features.ExpireWise.Services;

public class ExpireWiseSearchService
{
    public IEnumerable<ExpirationItem> Search(IEnumerable<ExpirationItem> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return items;

        var lower = query.ToLowerInvariant();
        return items.Where(i =>
            (!string.IsNullOrEmpty(i.ItemNumber) && i.ItemNumber.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(i.Description) && i.Description.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(i.Location) && i.Location.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(i.Notes) && i.Notes.Contains(lower, StringComparison.OrdinalIgnoreCase)));
    }

    public bool Matches(ExpirationItem item, string query)
    {
        if (item == null) return false;
        if (string.IsNullOrWhiteSpace(query)) return true;

        var lower = query.ToLowerInvariant();
        return (!string.IsNullOrEmpty(item.ItemNumber) && item.ItemNumber.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(item.Description) && item.Description.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(item.Location) && item.Location.Contains(lower, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(item.Notes) && item.Notes.Contains(lower, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<ExpirationItem> FilterByCategory(IEnumerable<ExpirationItem> items, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return items;
        return items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<ExpirationItem> FilterByDateRange(IEnumerable<ExpirationItem> items, DateTime? start, DateTime? end)
    {
        var filtered = items;
        if (start.HasValue)
            filtered = filtered.Where(i => i.ExpiryDate >= start.Value);
        if (end.HasValue)
            filtered = filtered.Where(i => i.ExpiryDate <= end.Value);
        return filtered;
    }

    public IEnumerable<ExpirationItem> FilterByExpirationStatus(IEnumerable<ExpirationItem> items, ExpirationStatus? status, int daysWarningThreshold)
    {
        if (status == null) return items;

        var today = DateTime.Today;
        var warningDate = today.AddDays(daysWarningThreshold);

        return status switch
        {
            ExpirationStatus.Expired => items.Where(i => i.ExpiryDate < today),
            ExpirationStatus.Warning => items.Where(i => i.ExpiryDate >= today && i.ExpiryDate <= warningDate),
            ExpirationStatus.Critical => items.Where(i => (i.ExpiryDate - today).TotalDays <= 7),
            ExpirationStatus.Good => items.Where(i => i.ExpiryDate > warningDate),
            _ => items
        };
    }
}
