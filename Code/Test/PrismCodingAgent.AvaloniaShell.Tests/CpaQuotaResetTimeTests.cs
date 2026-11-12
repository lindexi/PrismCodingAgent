using System.Globalization;
using PrismCodingAgent.CodingShell.Localizations;
using PrismCodingAgent.CodingShell.Services.Integrations;

namespace PrismCodingAgent.AvaloniaShell.Tests;

[TestClass]
public sealed class CpaQuotaResetTimeTests
{
    [TestMethod]
    [DataRow(300, "5 小时")]
    [DataRow(330, "5 小时")]
    [DataRow(7980, "5 天")]
    [DataRow(10080, "7 天")]
    [DataRow(59, "59 分钟")]
    [DataRow(60, "1 小时")]
    [DataRow(1440, "1 天")]
    public void WindowDurationUsesLargestWholeUnit(int minutes, string expected)
    {
        Lang.SetCurrent("zh-Hans");
        Assert.AreEqual(expected, QuotaTimeFormatter.FormatWindowDuration(minutes));
    }

    [TestMethod]
    [DataRow(179940, "剩余2天1小时")]
    [DataRow(86400, "剩余1天")]
    [DataRow(86399, "剩余23小时59分钟")]
    [DataRow(15780, "剩余4小时23分钟")]
    [DataRow(3600, "剩余1小时")]
    [DataRow(3599, "剩余59分钟")]
    [DataRow(60, "剩余1分钟")]
    [DataRow(59, "剩余不足1分钟")]
    [DataRow(1, "剩余不足1分钟")]
    [DataRow(0, "已到重置时间")]
    [DataRow(-1, "已到重置时间")]
    public void ResetTimeUsesWholeUnitsAndOmitsZero(int seconds, string countdown)
    {
        Lang.SetCurrent("zh-Hans");
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddSeconds(seconds);
        string date = TimeZoneInfo.ConvertTime(reset, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        Assert.AreEqual($"{date}（{countdown}）", QuotaTimeFormatter.FormatResetTime(reset, now));
    }

    [TestMethod]
    public void EquivalentOffsetsHaveIdenticalResetText()
    {
        Lang.SetCurrent("zh-Hans");
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(4);
        Assert.AreEqual(QuotaTimeFormatter.FormatResetTime(reset, now),
            QuotaTimeFormatter.FormatResetTime(reset.ToOffset(TimeSpan.FromHours(8)), now));
    }

    [TestMethod]
    public void MissingResetDateDoesNotProduceCountdown()
    {
        Lang.SetCurrent("zh-Hans");
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        Assert.AreEqual("未知", QuotaTimeFormatter.FormatResetTime(null, now));
    }
}
