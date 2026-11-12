using System;
using System.Globalization;
using PrismCodingAgent.CodingShell.Localizations;

namespace PrismCodingAgent.CodingShell.Services.Integrations;

internal static class QuotaTimeFormatter
{
    internal static string FormatWindowDuration(decimal windowMinutes)
    {
        if (windowMinutes >= 1440) return Lang.Format(Lang.Current.QuotaWindowDays, decimal.Floor(windowMinutes / 1440));
        if (windowMinutes >= 60) return Lang.Format(Lang.Current.QuotaWindowHours, decimal.Floor(windowMinutes / 60));
        return Lang.Format(Lang.Current.QuotaWindowMinutes, decimal.Floor(windowMinutes));
    }

    internal static string FormatResetTime(DateTimeOffset? resetAt, DateTimeOffset now)
    {
        if (resetAt is not { } reset) return Lang.Current.QuotaUnknown;
        TimeSpan remaining = reset - now;
        string countdown;
        if (remaining <= TimeSpan.Zero) countdown = Lang.Current.QuotaResetDue;
        else if (remaining.TotalMinutes < 1) countdown = Lang.Current.QuotaResetUnderMinute;
        else
        {
            string duration;
            if (remaining.TotalDays >= 1)
                duration = JoinUnits(Lang.Format(Lang.Current.QuotaCountdownDays, remaining.Days), remaining.Hours, Lang.Format(Lang.Current.QuotaCountdownHours, remaining.Hours));
            else if (remaining.TotalHours >= 1)
                duration = JoinUnits(Lang.Format(Lang.Current.QuotaCountdownHours, remaining.Hours), remaining.Minutes, Lang.Format(Lang.Current.QuotaCountdownMinutes, remaining.Minutes));
            else duration = Lang.Format(Lang.Current.QuotaCountdownMinutes, remaining.Minutes);
            countdown = Lang.Format(Lang.Current.QuotaCountdownRemaining, duration);
        }
        return Lang.Format(Lang.Current.QuotaResetRemaining,
            reset.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), countdown);
    }

    private static string JoinUnits(string first, int second, string secondText)
        => second == 0 ? first : Lang.Format(Lang.Current.QuotaCountdownUnits, first, secondText);
}
