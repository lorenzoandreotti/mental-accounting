using MentalAccounting.Domain;
namespace MentalAccounting.Domain.Tests;
public sealed class InterestTests
{
    private readonly InterestCalculator engine=new();
    private static readonly DateOnly Jan1=new(2024,1,1);
    private static Ledger Ledger(decimal balance=1000m) => new(Jan1,balance,0,0,DayBasis.Actual365,new(),[],[new(Jan1,.0365m)],[new(Jan1,.26m)]);
    private static void Near(decimal expected,decimal actual)=>Assert.InRange(actual,expected-0.000000001m,expected+0.000000001m);
    [Fact] public void DepositAndWithdrawalApplyFromTheirValueDate()
    {
        var ledger=Ledger() with {Movements=[new(Guid.NewGuid(),new(2024,1,11),MovementKind.Deposit,1000),new(Guid.NewGuid(),new(2024,1,21),MovementKind.Withdrawal,500)]};
        var result=engine.Calculate(ledger,new(2024,1,30));
        Near(4.5m,result.OpenGross);Near(1.17m,result.OpenTax);Assert.Equal(1500,result.Balance);
        Assert.Equal(new[]{10,10,10},result.Segments.Select(s=>s.Days));
    }
    [Fact] public void RateAndTaxChangesUseTheirOwnDates()
    {
        var ledger=Ledger() with {Rates=[new(Jan1,.0365m),new(new(2024,1,11),.073m)],Taxes=[new(Jan1,.26m),new(new(2024,1,21),.30m)]};
        var result=engine.Calculate(ledger,new(2024,1,30));Near(5,result.OpenGross);Near(1.38m,result.OpenTax);
    }
    [Theory][InlineData(DayBasis.Actual365)][InlineData(DayBasis.Actual360)][InlineData(DayBasis.ActualYear)]
    public void BasesHandleLeapYears(DayBasis basis)
    {
        var result=engine.Calculate(Ledger() with {Basis=basis,Calendar=new(12)},new(2024,12,30));
        var denominator=basis==DayBasis.Actual360?360:basis==DayBasis.ActualYear?366:365;
        Near(1000m*.0365m*365/denominator,result.OpenGross);
    }
    [Fact] public void ActualYearSplitsAtJanuary()
    {
        var start=new DateOnly(2023,12,31);
        var l=Ledger() with {Start=start,Rates=[new(start,.1m)],Taxes=[new(start,0)],Basis=DayBasis.ActualYear,
            Calendar=new(12,false,new(2023,7,1))};
        var r=engine.Calculate(l,new(2024,1,1));Near(100m/365+100m/366,r.OpenGross);Assert.Equal(2,r.Segments.Count);
    }
    [Fact] public void CreditCapitalizesNetOnceAndRecalculationIsDeterministic()
    {
        var jan=engine.Calculate(Ledger(),new(2024,1,31));Assert.Equal(1000,jan.Balance);Assert.Equal(3.10m,jan.PendingGross);Assert.Equal(.81m,jan.PendingTax);
        var feb=engine.Calculate(Ledger(),new(2024,2,1));Assert.Equal(1002.29m,feb.Balance);Near(.100229m,feb.OpenGross);Assert.True(feb.Settlements.Single().Credited);
        Assert.Equal(feb.Balance,engine.Calculate(Ledger(),new(2024,2,1)).Balance);
    }
    [Fact] public void DelayedCreditDoesNotEarnBeforePayment()
    {
        var l=Ledger() with {Calendar=new(Timing:CreditTiming.NextMonthDay,CreditDay:15)};
        var before=engine.Calculate(l,new(2024,2,14));Assert.Equal(1000,before.Balance);Near(1.4m,before.OpenGross);
        var payment=engine.Calculate(l,new(2024,2,15));Assert.Equal(1002.29m,payment.Balance);Near(1.500229m,payment.OpenGross);
    }
    [Fact] public void PeriodEndCreditEarnsOnlyFollowingDay()
    {
        var l=Ledger() with {Calendar=new(Timing:CreditTiming.PeriodEnd)};
        var r=engine.Calculate(l,new(2024,1,31));Assert.Equal(1002.29m,r.Balance);Assert.Equal(0,r.AccruedGross);
        Near(.100229m,engine.Calculate(l,new(2024,2,1)).OpenGross);
    }
    [Fact] public void OpeningAccrualIsSettledWithFirstPeriod()
    {
        var r=engine.Calculate(Ledger() with {OpeningGross=10,OpeningTax=2.6m},new(2024,2,1));
        Assert.Equal(1009.69m,r.Balance);
    }
    [Fact] public void RetroactiveChangesRebuildSettlements()
    {
        var old=engine.Calculate(Ledger(),new(2024,2,1));
        var changed=engine.Calculate(Ledger() with {Rates=[new(Jan1,.073m)]},new(2024,2,1));
        Assert.Equal(1004.59m,changed.Balance);Assert.True(changed.Balance>old.Balance);
    }
    [Fact] public void AccruedInterestCannotFundWithdrawal()
    {
        var l=Ledger() with {Movements=[new(Guid.NewGuid(),new(2024,1,30),MovementKind.Withdrawal,1001)]};
        Assert.Throws<BusinessException>(()=>engine.Calculate(l,new(2024,1,30)));
    }
    [Fact] public void SameDateMovementsAreAggregated()
    {
        var l=Ledger(0) with {Movements=[new(Guid.NewGuid(),Jan1,MovementKind.Withdrawal,100),new(Guid.NewGuid(),Jan1,MovementKind.Deposit,100)]};
        Assert.Equal(0,engine.Calculate(l,Jan1).Balance);
    }
    [Fact] public void CompoundAnnualGrowthUsesDecimalRoot()
    {
        var l=Ledger() with {Basis=DayBasis.ActualYear,Rates=[new(Jan1,.1m)],Taxes=[new(Jan1,0)],Calendar=new(12)};
        var r=engine.Calculate(l,new(2024,12,31),InterestMethod.Compound);
        Assert.Equal(100m,r.PendingGross);Assert.Equal(1000,r.Balance);
    }
    [Fact] public void RejectsMissingAndDuplicateConditionsAndInvalidAmounts()
    {
        Assert.Throws<BusinessException>(()=>engine.Calculate(Ledger() with {Rates=[]},Jan1));
        Assert.Throws<BusinessException>(()=>engine.Calculate(Ledger() with {Taxes=[new(Jan1,.26m),new(Jan1,.2m)]},Jan1));
        Assert.Throws<BusinessException>(()=>engine.Calculate(Ledger() with {Movements=[new(Guid.NewGuid(),Jan1,MovementKind.Deposit,.001m)]},Jan1));
    }
    [Fact] public void RoundingOnlyAtCloseUsesAwayFromZero()
    {
        var l=Ledger() with {Start=new(2024,1,31),Rates=[new(Jan1,.001825m)],Taxes=[new(Jan1,0)]};
        Assert.Equal(.01m,engine.Calculate(l,new(2024,1,31)).PendingGross);
    }
    [Fact] public void CustomCalendarDoesNotDriftAfterFebruary()
    {
        var c=new CalendarRule(1,false,new(2024,1,31));
        Assert.Equal(new DateOnly(2024,2,28),AccrualCalendar.PeriodEnd(new(2024,2,1),c));
        Assert.Equal(new DateOnly(2024,3,30),AccrualCalendar.PeriodEnd(new(2024,3,1),c));
        Assert.Equal(new DateOnly(2024,2,29),AccrualCalendar.CreditDate(new(2024,1,31),new(Timing:CreditTiming.NextMonthDay,CreditDay:31)));
    }
}
