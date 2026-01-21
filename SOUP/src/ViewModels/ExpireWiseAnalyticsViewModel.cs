using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SOUP.Core.Entities.ExpireWise;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for ExpireWise Analytics tab with chart data
/// </summary>
public partial class ExpireWiseAnalyticsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<MonthlyChartData> _monthlyData = new();

    [ObservableProperty]
    private ObservableCollection<StatusChartData> _statusDistribution = new();

    [ObservableProperty]
    private ObservableCollection<LocationChartData> _locationDistribution = new();

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _totalUnits;

    [ObservableProperty]
    private int _expiredItems;

    [ObservableProperty]
    private int _criticalItems;

    [ObservableProperty]
    private int _warningItems;

    [ObservableProperty]
    private int _goodItems;

    [ObservableProperty]

    [ObservableProperty]
    private string _mostCommonLocation = "N/A";

    [ObservableProperty]
    private string _nextExpiringItem = "None";

    [ObservableProperty]
    private int _nextExpiringDays;

    /// <summary>
    /// Updates all analytics data based on the provided items
    /// </summary>
    public void UpdateAnalytics(IEnumerable<ExpirationItem> items)
    {
        var itemList = items.ToList();

        // Basic stats
        TotalItems = itemList.Count;
        TotalUnits = itemList.Sum(i => i.Units);

        // Status counts
        ExpiredItems = itemList.Count(i => i.Status == ExpirationStatus.Expired);
        CriticalItems = itemList.Count(i => i.Status == ExpirationStatus.Critical);
        WarningItems = itemList.Count(i => i.Status == ExpirationStatus.Warning);
        GoodItems = itemList.Count(i => i.Status == ExpirationStatus.Good);

        // Average shelf life (only for non-expired items)
        var nonExpired = itemList.Where(i => i.DaysUntilExpiry > 0).ToList();
        AverageShelfLife = nonExpired.Count > 0 ? Math.Round(nonExpired.Average(i => i.DaysUntilExpiry), 1) : 0;

        // Most common location - optimized with MaxBy
        var locationGroups = itemList
            .Where(i => !string.IsNullOrEmpty(i.Location))
            .GroupBy(i => i.Location)
            .MaxBy(g => g.Count());
        MostCommonLocation = locationGroups?.Key ?? "N/A";

        // Next expiring item - optimized with MinBy
        var nextExpiring = itemList
            .Where(i => i.DaysUntilExpiry > 0)
            .MinBy(i => i.ExpiryDate);
        if (nextExpiring != null)
        {
            NextExpiringItem = $"{nextExpiring.ItemNumber} - {nextExpiring.Description}";
            NextExpiringDays = nextExpiring.DaysUntilExpiry;
        }
        else
        {
            NextExpiringItem = "None";
            NextExpiringDays = 0;
        }

        // Monthly distribution (next 6 months)
        UpdateMonthlyData(itemList);

        // Status distribution for pie chart
        UpdateStatusDistribution();

        // Location distribution
        UpdateLocationDistribution(itemList);
    }

    private void UpdateMonthlyData(List<ExpirationItem> items)
    {
        MonthlyData.Clear();

        var today = DateTime.Today;
        var startMonth = new DateTime(today.Year, today.Month, 1);

        for (int i = 0; i < 6; i++)
        {
            var month = startMonth.AddMonths(i);
            var monthItems = items.Where(item =>
                item.ExpiryDate.Year == month.Year &&
                item.ExpiryDate.Month == month.Month).ToList();

            MonthlyData.Add(new MonthlyChartData
            {
                Month = month.ToString("MMM yyyy"),
                ShortMonth = month.ToString("MMM"),
                ItemCount = monthItems.Count,
                UnitCount = monthItems.Sum(x => x.Units),
                CriticalCount = monthItems.Count(x => x.Status == ExpirationStatus.Critical || x.Status == ExpirationStatus.Expired)
            });
        }

        // Calculate max for scaling
        var maxItems = MonthlyData.Max(m => m.ItemCount);
        var maxUnits = MonthlyData.Max(m => m.UnitCount);
        foreach (var data in MonthlyData)
        {
            data.ItemPercentage = maxItems > 0 ? (double)data.ItemCount / maxItems * 100 : 0;
            data.UnitPercentage = maxUnits > 0 ? (double)data.UnitCount / maxUnits * 100 : 0;
        }
    }

    private void UpdateStatusDistribution()
    {
        StatusDistribution.Clear();

        var total = TotalItems;
        if (total == 0) total = 1; // Prevent division by zero

        StatusDistribution.Add(new StatusChartData
        {
            Status = "Expired",
            Count = ExpiredItems,
            Percentage = (double)ExpiredItems / total * 100,
            Color = "#9E9E9E"
        });

        StatusDistribution.Add(new StatusChartData
        {
            Status = "Critical",
            Count = CriticalItems,
            Percentage = (double)CriticalItems / total * 100,
            Color = "#EF4444"
        });

        StatusDistribution.Add(new StatusChartData
        {
            Status = "Warning",
            Count = WarningItems,
            Percentage = (double)WarningItems / total * 100,
            Color = "#F59E0B"
        });

        StatusDistribution.Add(new StatusChartData
        {
            Status = "Good",
            Count = GoodItems,
            Percentage = (double)GoodItems / total * 100,
            Color = "#10B981"
        });
    }

    private void UpdateLocationDistribution(List<ExpirationItem> items)
    {
        LocationDistribution.Clear();

        var locationGroups = items
            .Where(i => !string.IsNullOrEmpty(i.Location))
            .GroupBy(i => i.Location)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var colors = new[] { "#6366F1", "#8B5CF6", "#EC4899", "#F59E0B", "#10B981" };
        var maxCount = locationGroups.Count > 0 ? locationGroups[0].Count() : 1;

        for (int i = 0; i < locationGroups.Count; i++)
        {
            var group = locationGroups[i];
            LocationDistribution.Add(new LocationChartData
            {
                Location = group.Key ?? "Unknown",
                Count = group.Count(),
                Units = group.Sum(x => x.Units),
                Percentage = (double)group.Count() / maxCount * 100,
                Color = colors[i % colors.Length]
            });
        }
    }
}

/// <summary>
/// Data for monthly bar chart
/// </summary>
public class MonthlyChartData
{
    public string Month { get; set; } = string.Empty;
    public string ShortMonth { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public int UnitCount { get; set; }
    public int CriticalCount { get; set; }
    public double ItemPercentage { get; set; }
    public double UnitPercentage { get; set; }
}

/// <summary>
/// Data for status pie chart
/// </summary>
public class StatusChartData
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

/// <summary>
/// Data for location distribution
/// </summary>
public class LocationChartData
{
    public string Location { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Units { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}
