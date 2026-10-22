namespace MentalAccounting.Domain;

public enum MovementKind { Deposit, Withdrawal, Charge }
public enum DayBasis { Actual365, Actual360, ActualYear }
public enum CreditTiming { NextMonthFirst, PeriodEnd, NextMonthDay }
public enum InterestMethod { Simple, Compound }
public sealed record Movement(Guid Id, DateOnly Date, MovementKind Kind, decimal Amount, string? Note = null);
public sealed record RateChange(DateOnly From, decimal Rate);
public sealed record TaxChange(DateOnly From, decimal Rate);
public sealed record CalendarRule(int Months = 1, bool Civil = true, DateOnly? Anchor = null,
    CreditTiming Timing = CreditTiming.NextMonthFirst, int CreditDay = 1);
public sealed record Ledger(DateOnly Start, decimal OpeningBalance, decimal OpeningGross,
    decimal OpeningTax, DayBasis Basis, CalendarRule Calendar,
    List<Movement> Movements, List<RateChange> Rates, List<TaxChange> Taxes);
public sealed record CalculationSegment(DateOnly From, DateOnly Through, decimal Capital, decimal Rate,
    decimal TaxRate, int Denominator, decimal Gross, decimal Tax)
{ public int Days => Through.DayNumber - From.DayNumber + 1; }
public sealed record Settlement(DateOnly From, DateOnly Through, DateOnly CreditDate,
    decimal Gross, decimal Tax, decimal Net, bool Credited);
public sealed record CalculationResult(DateOnly AsOf, decimal Balance, decimal OpenGross, decimal OpenTax,
    decimal PendingGross, decimal PendingTax, IReadOnlyList<CalculationSegment> Segments,
    IReadOnlyList<Settlement> Settlements)
{
    public decimal AccruedGross => OpenGross + PendingGross;
    public decimal AccruedTax => OpenTax + PendingTax;
    public decimal AccruedNet => AccruedGross - AccruedTax;
}
public sealed class BusinessException(string message) : Exception(message);
