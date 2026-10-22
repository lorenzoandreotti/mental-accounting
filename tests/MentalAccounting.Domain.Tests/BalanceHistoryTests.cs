using MentalAccounting.Domain;

namespace MentalAccounting.Domain.Tests;

public sealed class BalanceHistoryTests
{
    private static readonly DateOnly Start = new(2024, 1, 1);
    private static Ledger Ledger() => new(Start, 1000, 0, 0, DayBasis.Actual365, new(), [], [new(Start, .0365m)], [new(Start, .26m)]);
    private readonly InterestCalculator engine = new();
    private static void Near(decimal expected, decimal actual) => Assert.InRange(actual, expected - .000000001m, expected + .000000001m);

    [Fact] public void HistoryIsOptionalAndIncludesOpeningBalanceAndAccrual()
    {
        Assert.Empty(engine.Calculate(Ledger(), Start).History);
        var point = engine.Calculate(Ledger() with { OpeningGross = 10, OpeningTax = 2.6m }, Start, includeHistory: true).History.Single();
        Assert.Equal(Start, point.Date); Assert.Equal(1000m, point.Available); Near(7.474m, point.AccruedNet); Near(1007.474m, point.Total);
    }
    [Fact] public void SameDayMovementsAndTaxesAppearInEndOfDayBalance()
    {
        var ledger = Ledger() with { Movements = [new(Guid.NewGuid(), Start, MovementKind.Deposit, 200), new(Guid.NewGuid(), Start, MovementKind.Withdrawal, 100)] };
        var point = engine.Calculate(ledger, Start, includeHistory: true).History.Single();
        Assert.Equal(1100, point.Available); Near(.0814m, point.AccruedNet);
    }
    [Fact] public void UnpaidClosedInterestRemainsInTotalUntilPaymentWithoutDoubleCounting()
    {
        var ledger = Ledger() with { Calendar = new(Timing: CreditTiming.NextMonthDay, CreditDay: 15) };
        var result = engine.Calculate(ledger, new(2024, 2, 15), includeHistory: true);
        var jan31 = result.History.Single(x => x.Date == new DateOnly(2024, 1, 31));
        Assert.Equal(1000, jan31.Available); Assert.Equal(2.29m, jan31.AccruedNet);
        var feb14 = result.History.Single(x => x.Date == new DateOnly(2024, 2, 14));
        Assert.Equal(1000, feb14.Available); Near(3.326m, feb14.AccruedNet);
        var feb15 = result.History[^1]; Assert.Equal(1002.29m, feb15.Available); Near(1.11016946m, feb15.AccruedNet);
    }
    [Theory]
    [InlineData(CreditTiming.NextMonthFirst)]
    [InlineData(CreditTiming.PeriodEnd)]
    [InlineData(CreditTiming.NextMonthDay)]
    public void LastPointReconcilesAndDatesIncludeLeapDay(CreditTiming timing)
    {
        var result = engine.Calculate(Ledger() with { Calendar = new(Timing: timing, CreditDay: 15) }, new(2024, 3, 31), includeHistory: true);
        Assert.Equal(91, result.History.Count); Assert.Equal(91, result.History.Select(x => x.Date).Distinct().Count());
        Assert.Contains(result.History, x => x.Date == new DateOnly(2024, 2, 29));
        Assert.Equal(result.Balance, result.History[^1].Available); Assert.Equal(result.AccruedNet, result.History[^1].AccruedNet);
        if (timing == CreditTiming.PeriodEnd)
        { var jan31 = result.History.Single(x => x.Date == new DateOnly(2024, 1, 31)); Assert.Equal(1002.29m, jan31.Available); Assert.Equal(0, jan31.AccruedNet); }
    }
    [Fact] public void RetroactiveChangesRebuildHistory()
    {
        var before = engine.Calculate(Ledger(), new(2024, 2, 1), includeHistory: true);
        var after = engine.Calculate(Ledger() with { Rates = [new(Start, .073m)] }, new(2024, 2, 1), includeHistory: true);
        Assert.Equal(1002.29m, before.History[^1].Available); Assert.Equal(1004.59m, after.History[^1].Available);
        Assert.True(after.History[0].AccruedNet > before.History[0].AccruedNet);
    }
}
