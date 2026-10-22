using MentalAccounting.Application;
using MentalAccounting.Domain;

namespace MentalAccounting.Application.Tests;

public sealed class AccountActivityTests
{
    [Fact] public void TimelineContainsSignedManualMovementsAndOnlyActualCredits()
    {
        var date = new DateOnly(2024, 1, 1);
        var ledger = new Ledger(date, 1000, 0, 0, DayBasis.Actual365, new(Timing: CreditTiming.NextMonthDay, CreditDay: 15),
            [new(Guid.NewGuid(), date, MovementKind.Deposit, 100), new(Guid.NewGuid(), date, MovementKind.Withdrawal, 50), new(Guid.NewGuid(), date, MovementKind.Charge, 10)],
            [new(date, .0365m)], [new(date, .26m)]);
        var engine = new InterestCalculator();
        Assert.Equal(3, AccountActivity.Build(ledger, engine.Calculate(ledger, new(2024, 2, 14))).Count);
        var result = engine.Calculate(ledger, new(2024, 2, 15)); var entries = AccountActivity.Build(ledger, result);
        Assert.Equal(4, entries.Count); Assert.Equal(3, ledger.Movements.Count);
        Assert.Equal(100, entries.Single(x => x.Type == "Deposit").Amount);
        Assert.Equal(-50, entries.Single(x => x.Type == "Withdrawal").Amount);
        Assert.Equal(-10, entries.Single(x => x.Type == "Charge").Amount);
        var credit = entries.Single(x => x.Type == "Interest");
        Assert.Null(credit.MovementId); Assert.Equal(new(2024, 2, 15), credit.Date); Assert.Equal(result.Settlements[0].Net, credit.Amount);
        Assert.NotNull(credit.Interest);
        Assert.Equal(entries, AccountActivity.Build(ledger, engine.Calculate(ledger, new(2024, 2, 15))));
    }
}
