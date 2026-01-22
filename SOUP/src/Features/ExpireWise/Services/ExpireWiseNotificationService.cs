using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;

namespace SOUP.Features.ExpireWise.Services;

public class ExpireWiseNotificationService
{
    private readonly HashSet<Guid> _notifiedItems = new();
    private readonly ILogger<ExpireWiseNotificationService>? _logger;

    public ExpireWiseNotificationService(ILogger<ExpireWiseNotificationService>? logger = null)
    {
        _logger = logger;
    }

    public void CheckAndNotify(IEnumerable<ExpirationItem> items, int daysWarningThreshold, bool enabled)
    {
        if (!enabled) return;

        var today = DateTime.Today;
        var warningDate = today.AddDays(daysWarningThreshold);

        var expiring = items
            .Where(i => !_notifiedItems.Contains(i.Id))
            .Where(i => i.ExpiryDate >= today && i.ExpiryDate <= warningDate)
            .ToList();

        foreach (var item in expiring)
        {
            var daysLeft = (item.ExpiryDate - today).Days;
            // Placeholder: integrate with real notifications (toast/tray)
            _logger?.LogInformation("Notify: {Item} expires in {Days} day(s)", item.ItemNumber, daysLeft);
            _notifiedItems.Add(item.Id);
        }
    }

    public async Task<IEnumerable<ExpirationItem>> GetNotificationsAsync(IEnumerable<ExpirationItem> items, int daysWarningThreshold, bool enabled)
    {
        if (!enabled) return Array.Empty<ExpirationItem>();

        var today = DateTime.Today;
        var warningDate = today.AddDays(daysWarningThreshold);

        var expiring = items
            .Where(i => !_notifiedItems.Contains(i.Id))
            .Where(i => i.ExpiryDate >= today && i.ExpiryDate <= warningDate)
            .ToList();

        // Mark as notified to avoid duplicate notifications
        foreach (var item in expiring)
            _notifiedItems.Add(item.Id);

        return await Task.FromResult<IEnumerable<ExpirationItem>>(expiring);
    }

    public void ResetNotificationHistory() => _notifiedItems.Clear();
}
