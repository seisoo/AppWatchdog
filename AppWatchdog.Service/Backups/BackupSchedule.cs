using AppWatchdog.Shared;
using System.Globalization;

namespace AppWatchdog.Service.Backups;

/// <summary>
/// Provides scheduling helpers for backup plans.
/// </summary>
public static class BackupSchedule
{
    /// <summary>
    /// Computes the next planned run time in UTC.
    /// </summary>
    /// <param name="cfg">Schedule configuration.</param>
    /// <param name="nowLocal">Current local time.</param>
    /// <returns>The next planned UTC time.</returns>
    public static DateTimeOffset ComputeNextPlannedUtc(BackupScheduleConfig cfg, DateTimeOffset nowLocal)
    {
        if (!TryParseTime(cfg.TimeLocal, out var time))
            time = new TimeSpan(2, 0, 0);

        var days = (cfg.Days?.Count ?? 0) > 0
            ? new HashSet<DayOfWeek>(cfg.Days)
            : new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>());

        var start = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);

        for (int i = 0; i <= 14; i++)
        {
            var day = start.AddDays(i);
            if (!days.Contains(day.DayOfWeek))
                continue;

            var candidate = day.Add(time);
            var candidateLocal = new DateTimeOffset(candidate, nowLocal.Offset);

            if (candidateLocal > nowLocal.AddSeconds(1))
                return candidateLocal.ToUniversalTime();
        }

        return nowLocal.AddDays(1).ToUniversalTime();
    }

    /// <summary>
    /// Determines whether a job is due to run.
    /// </summary>
    /// <param name="nowLocal">Current local time.</param>
    /// <param name="plannedUtc">Planned UTC time.</param>
    /// <returns><c>true</c> when due.</returns>
    public static bool IsDue(DateTimeOffset nowLocal, DateTimeOffset plannedUtc)
        => nowLocal.ToUniversalTime() >= plannedUtc;

    /// <summary>
    /// Tries to parse a time string into a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="s">Time string.</param>
    /// <param name="t">Parsed time span.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    private static bool TryParseTime(string s, out TimeSpan t)
    {
        if (TimeSpan.TryParseExact(s, "hh\\:mm", CultureInfo.InvariantCulture, out t))
            return true;
        if (TimeSpan.TryParseExact(s, "h\\:mm", CultureInfo.InvariantCulture, out t))
            return true;
        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out t))
            return true;
        t = default;
        return false;
    }
}
