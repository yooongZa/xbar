using Xunit;

namespace XBar.Core.Tests;

public sealed class ImpressionSurgeTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-13T00:00:00Z");
    private static ContentPost Post(string impressions, string id = "2111348825839611989", string rt = "0") => new(id, "Test", impressions, "0", rt);

    [Fact] public void ImpressionBurstTriggersWithZeroReposts()
    {
        var detector = new ImpressionDetector();
        Assert.Empty(detector.Observe([Post("100")], Start));
        var signal = Assert.Single(detector.Observe([Post("1100")], Start.AddMinutes(10)));
        Assert.Equal(1000, signal.Delta); Assert.Equal(10, signal.Minutes); Assert.False(signal.Approximate);
        var repostOnly = new ImpressionDetector(); repostOnly.Observe([Post("100")], Start);
        Assert.Empty(repostOnly.Observe([Post("100", rt: "500")], Start.AddMinutes(10)));
    }
    [Theory] [InlineData(1999, false)] [InlineData(2000, true)]
    public void FivefoldGrowthUsesRecentNormalHistory(int delta, bool expected)
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("10000")], Start);
        Assert.Empty(detector.Observe([Post("10400")], Start.AddMinutes(10)));
        Assert.Equal(expected, detector.Observe([Post((10400 + delta).ToString())], Start.AddMinutes(20)).Count == 1);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void PeerReferenceIsIndependentOfPostOrder(bool reverse)
    {
        var ids = new[] { "2111348825839611989", "2111348825839611988", "2111348825839611987" };
        var order = reverse ? new[] { 2, 1, 0 } : [0, 1, 2];
        var detector = new ImpressionDetector(); detector.Observe(order.Select(i => Post("10000", ids[i])), Start);
        var signal = Assert.Single(detector.Observe(order.Select(i => Post(i == 0 ? "13000" : "10500", ids[i])), Start.AddMinutes(10)));
        Assert.Equal(ids[0], signal.Post.Id);
    }
    [Fact] public void SustainedBurstKeepsNormalBaselineAndThirtyMinuteCooldown()
    {
        var detector = new ImpressionDetector(); const string other = "2111348825839611988";
        detector.Observe([Post("10000"), Post("10000", other)], Start);
        for (int tick = 1; tick <= 4; tick++)
        {
            var signals = detector.Observe([Post((10000 + tick * 8000).ToString()), Post((10000 + tick * 200).ToString(), other)], Start.AddMinutes(tick * 10));
            Assert.Equal(tick is 1 or 4 ? 1 : 0, signals.Count);
        }
    }
    [Fact] public void RoundedImpressionsAllowLargeGrowthButIgnoreDisplayBoundary()
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("868K")], Start);
        Assert.Empty(detector.Observe([Post("869K")], Start.AddMinutes(10)));
        var signal = Assert.Single(detector.Observe([Post("879K")], Start.AddMinutes(20)));
        Assert.Equal(11000, signal.Delta); Assert.Equal(20, signal.Minutes); Assert.True(signal.Approximate);
        var millions = new ImpressionDetector(); millions.Observe([Post("1.0M")], Start);
        Assert.Empty(millions.Observe([Post("1.1M")], Start.AddMinutes(10)));
        Assert.Single(millions.Observe([Post("1.4M")], Start.AddMinutes(20)));
    }
    [Fact] public void CoarseCountsAccumulateAcrossConsecutiveCaptures()
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("1.0M")], Start);
        Assert.Empty(detector.Observe([Post("1.1M")], Start.AddMinutes(10)));
        Assert.Empty(detector.Observe([Post("1.2M")], Start.AddMinutes(20)));
        var signal = Assert.Single(detector.Observe([Post("1.3M")], Start.AddMinutes(30)));
        Assert.Equal(300000, signal.Delta); Assert.Equal(30, signal.Minutes);
    }
    [Fact] public void CoarseAccumulationResetsAfterGapsCorrectionsAndOneHour()
    {
        var gap = new ImpressionDetector(); gap.Observe([Post("1.0M")], Start);
        gap.Observe([Post("1.1M")], Start.AddSeconds(600));
        Assert.Empty(gap.Observe([Post("1.4M")], Start.AddSeconds(1501)));
        var correction = new ImpressionDetector(); correction.Observe([Post("1.0M")], Start);
        correction.Observe([Post("1.2M")], Start.AddMinutes(10));
        correction.Observe([Post("1.1M")], Start.AddMinutes(20));
        Assert.Empty(correction.Observe([Post("1.3M")], Start.AddMinutes(30)));
        var quiet = new ImpressionDetector();
        for (int tick = 0; tick <= 6; tick++) Assert.Empty(quiet.Observe([Post("1.0M")], Start.AddMinutes(tick * 10)));
        quiet.Observe([Post("1.1M")], Start.AddMinutes(70));
        quiet.Observe([Post("1.2M")], Start.AddMinutes(80));
        Assert.Equal(30, Assert.Single(quiet.Observe([Post("1.3M")], Start.AddMinutes(90))).Minutes);
    }
    [Fact] public void ManualRefreshKeepsTheFiveMinuteObservationWindow()
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("100")], Start);
        Assert.Empty(detector.Observe([Post("1100")], Start.AddMinutes(1)));
        Assert.Empty(detector.Observe([Post("1100")], Start.AddMinutes(4)));
        Assert.Equal(5, Assert.Single(detector.Observe([Post("1100")], Start.AddMinutes(5))).Minutes);
    }
    [Theory]
    [InlineData(300, 500, true)] [InlineData(600, 999, false)] [InlineData(600, 1000, true)]
    [InlineData(900, 1499, false)] [InlineData(900, 1500, true)] [InlineData(901, 100000, false)]
    public void GrowthRateUsesElapsedTimeAndResetsAfterLongGaps(int seconds, int delta, bool expected)
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("10000")], Start);
        Assert.Equal(expected, detector.Observe([Post((10000 + delta).ToString())], Start.AddSeconds(seconds)).Count == 1);
    }
    [Fact] public void CounterCorrectionAndClockRollbackStartNewBaselines()
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("10000")], Start);
        Assert.Empty(detector.Observe([Post("100")], Start.AddSeconds(600)));
        Assert.Empty(detector.Observe([Post("1100")], Start.AddSeconds(599)));
        Assert.Empty(detector.Observe([Post("1150")], Start.AddSeconds(1199)));
    }
    [Fact] public void CooldownSurvivesLeavingList()
    {
        var detector = new ImpressionDetector(); detector.Observe([Post("10000")], Start);
        Assert.Single(detector.Observe([Post("20000")], Start.AddMinutes(10)));
        detector.Observe([], Start.AddMinutes(11)); detector.Observe([Post("50000")], Start.AddMinutes(12));
        Assert.Empty(detector.Observe([Post("60000")], Start.AddMinutes(17)));
        detector.Observe([], Start.AddMinutes(30)); detector.Observe([Post("70000")], Start.AddMinutes(35));
        Assert.Single(detector.Observe([Post("80000")], Start.AddMinutes(40)));
    }
    [Fact] public void AccountPeriodAndDayChangesDiscardHistory()
    {
        var ledger = new PostLedger(); var zone = TimeZoneInfo.Utc;
        ledger.Ingest([Post("100")], "@one", ContentPeriod.Today, Start, zone);
        Assert.Single(ledger.Ingest([Post("1100")], "@one", ContentPeriod.Today, Start.AddMinutes(10), zone));
        Assert.Empty(ledger.Ingest([Post("100000")], "@two", ContentPeriod.Today, Start.AddMinutes(20), zone));
        Assert.Empty(ledger.Ingest([Post("200000")], "@two", ContentPeriod.Week, Start.AddMinutes(30), zone));
        Assert.Empty(ledger.Ingest([Post("300000")], "@two", ContentPeriod.Week, Start.AddDays(1), zone));
    }
}
