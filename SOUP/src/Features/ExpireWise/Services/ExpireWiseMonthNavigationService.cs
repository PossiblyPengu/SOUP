using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Core.Entities.ExpireWise;
using SOUP.ViewModels;

namespace SOUP.Features.ExpireWise.Services;

public class ExpireWiseMonthNavigationService
{
    public DateTime CurrentMonth { get; private set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    public void NavigateNext() => CurrentMonth = CurrentMonth.AddMonths(1);
    public void NavigatePrevious() => CurrentMonth = CurrentMonth.AddMonths(-1);
    public void NavigateToToday() => CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public void NavigateToMonth(int year, int month) => CurrentMonth = new DateTime(year, month, 1);

    public (DateTime start, DateTime end) GetMonthRange() => (CurrentMonth, CurrentMonth.AddMonths(1).AddDays(-1));

    public string FormatMonthHeader(string dateFormat) => CurrentMonth.ToString(dateFormat);

    public List<ExpirationItem> GetItemsForCurrentMonth(IEnumerable<ExpirationItem> allItems)
    {
        var (start, end) = GetMonthRange();
        return allItems
            .Where(i => i.ExpiryDate >= start && i.ExpiryDate <= end)
            .OrderBy(i => i.ExpiryDate)
            .ThenBy(i => i.Description)
            .ToList();
    }

    public List<MonthGroup> BuildMonthGroups(IEnumerable<ExpirationItem> allItems, DateTime centerMonth, string dateFormat, int monthsBefore = 6, int monthsAfter = 6)
    {
        var groups = new List<MonthGroup>();

        var startMonth = new DateTime(centerMonth.Year, centerMonth.Month, 1).AddMonths(-monthsBefore);
        var endMonth = new DateTime(centerMonth.Year, centerMonth.Month, 1).AddMonths(monthsAfter);

        var itemsByMonth = allItems
            .GroupBy(i => (i.ExpiryDate.Year, i.ExpiryDate.Month))
            .ToDictionary(g => g.Key, g => g.ToList());

        var cursor = startMonth;
        while (cursor <= endMonth)
        {
            var key = (cursor.Year, cursor.Month);
            var itemsInMonth = itemsByMonth.TryGetValue(key, out var list) ? list : new List<ExpirationItem>();

            groups.Add(new MonthGroup
            {
                Month = cursor,
                DisplayName = cursor.ToString(dateFormat),
                ItemCount = itemsInMonth.Count,
                TotalUnits = itemsInMonth.Sum(i => i.Units),
                CriticalCount = itemsInMonth.Count(i => i.Status == ExpirationStatus.Critical),
                ExpiredCount = itemsInMonth.Count(i => i.Status == ExpirationStatus.Expired),
                IsCurrentMonth = cursor.Year == centerMonth.Year && cursor.Month == centerMonth.Month
            });

            cursor = cursor.AddMonths(1);
        }

        return groups;
    }
}
