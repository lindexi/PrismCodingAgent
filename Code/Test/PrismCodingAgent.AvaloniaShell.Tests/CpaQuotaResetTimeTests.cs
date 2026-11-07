using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.ViewModels;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaResetTimeTests
{
    [TestMethod]
    [DataRow(49, 59, 2, 1)]
    [DataRow(23, 59, 0, 23)]
    [DataRow(0, 59, 0, 0)]
    [DataRow(-1, 0, 0, 0)]
    public void RemainingTimeUsesWholeDaysAndHours(int hours, int minutes, int days, int remainingHours)
    {
        Lang.SetCurrent("zh-Hans");
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(hours).AddMinutes(minutes);
        string expected = $"{reset.ToLocalTime():yyyy-MM-dd HH:mm} （剩余 {days}天{remainingHours}小时）";

        Assert.AreEqual(expected, CpaQuotaServiceViewModel.FormatReset(reset, now));
    }

    [TestMethod]
    public void MissingResetDateDoesNotProduceCountdown()
    {
        Lang.SetCurrent("zh-Hans");
        Assert.AreEqual(Lang.Current.QuotaUnknown, CpaQuotaServiceViewModel.FormatReset(null, DateTimeOffset.Now));
    }
}
