namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for calculating time durations during business/work hours.
/// Used for accurate time tracking that only counts time during operational hours.
/// </summary>
public class WorkTimeCalculationService
{
    // Default work hours: 8:00 AM to 4:30 PM
    private readonly TimeSpan _workStart = new TimeSpan(8, 0, 0);
    private readonly TimeSpan _workEnd = new TimeSpan(16, 30, 0);

    /// <summary>
    /// Creates a new WorkTimeCalculationService with default work hours (8:00 AM - 4:30 PM).
    /// </summary>
    public WorkTimeCalculationService()
    {
    }

    /// <summary>
    /// Creates a new WorkTimeCalculationService with custom work hours.
    /// </summary>
    /// <param name="workStart">Start of work day (e.g., 8:00 AM)</param>
    /// <param name="workEnd">End of work day (e.g., 4:30 PM)</param>
    public WorkTimeCalculationService(TimeSpan workStart, TimeSpan workEnd)
    {
        _workStart = workStart;
        _workEnd = workEnd;
    }

    /// <summary>
    /// Calculates the total work time between two timestamps, counting only business hours.
    /// Non-work hours (nights, before/after work) are excluded from the calculation.
    /// </summary>
    /// <param name="start">Start timestamp</param>
    /// <param name="end">End timestamp</param>
    /// <returns>Total work time elapsed</returns>
    public TimeSpan CalculateWorkTimeBetween(DateTime start, DateTime end)
    {
        if (end <= start)
            return TimeSpan.Zero;

        TimeSpan total = TimeSpan.Zero;

        var currentDay = start.Date;
        var lastDay = end.Date;

        // Iterate through each day in the range
        while (currentDay <= lastDay)
        {
            var dayWorkStart = currentDay + _workStart;
            var dayWorkEnd = currentDay + _workEnd;

            // Calculate the overlap between the time range and this day's work hours
            var segmentStart = start > dayWorkStart ? start : dayWorkStart;
            var segmentEnd = end < dayWorkEnd ? end : dayWorkEnd;

            // Only add time if there's actual overlap with work hours
            if (segmentEnd > segmentStart)
            {
                total += segmentEnd - segmentStart;
            }

            currentDay = currentDay.AddDays(1);
        }

        return total;
    }

    /// <summary>
    /// Checks if a given timestamp falls within work hours.
    /// </summary>
    /// <param name="timestamp">Timestamp to check</param>
    /// <returns>True if within work hours, false otherwise</returns>
    public bool IsWithinWorkHours(DateTime timestamp)
    {
        var timeOfDay = timestamp.TimeOfDay;
        return timeOfDay >= _workStart && timeOfDay <= _workEnd;
    }

    /// <summary>
    /// Gets the next work start time after the given timestamp.
    /// If the timestamp is already during work hours, returns the timestamp.
    /// If after work hours, returns the start of the next work day.
    /// </summary>
    /// <param name="timestamp">Reference timestamp</param>
    /// <returns>Next work start time</returns>
    public DateTime GetNextWorkStart(DateTime timestamp)
    {
        var timeOfDay = timestamp.TimeOfDay;

        // If before work hours today, return work start today
        if (timeOfDay < _workStart)
        {
            return timestamp.Date + _workStart;
        }

        // If during work hours, return current time
        if (timeOfDay >= _workStart && timeOfDay <= _workEnd)
        {
            return timestamp;
        }

        // If after work hours, return work start tomorrow
        return timestamp.Date.AddDays(1) + _workStart;
    }

    /// <summary>
    /// Gets the work hours configuration.
    /// </summary>
    /// <returns>Tuple of (workStart, workEnd)</returns>
    public (TimeSpan WorkStart, TimeSpan WorkEnd) GetWorkHours()
    {
        return (_workStart, _workEnd);
    }
}
