namespace MentalAccounting.Domain;

public sealed class InterestCalculator
{
    public CalculationResult Calculate(Ledger ledger, DateOnly asOf, InterestMethod method = InterestMethod.Simple, bool includeHistory = false)
    {
        if (!Enum.IsDefined(method)) throw new BusinessException("Metodo di calcolo non valido.");
        Validate(ledger, asOf);
        var balance = ledger.OpeningBalance;
        var gross = ledger.OpeningGross;
        var tax = ledger.OpeningTax;
        var periodStart = ledger.Start;
        var end = AccrualCalendar.PeriodEnd(periodStart, ledger.Calendar);
        var segments = new List<CalculationSegment>();
        var settlements = new List<Settlement>();
        var history = includeHistory ? new List<BalanceHistoryPoint>() : null;
        decimal pendingGross = 0, pendingTax = 0;
        var movements = ledger.Movements.GroupBy(x => x.Date).ToDictionary(x => x.Key, x => x.Sum(m => m.Kind == MovementKind.Deposit ? m.Amount : -m.Amount));
        var rates = ledger.Rates.OrderBy(x => x.From).ToArray();
        var taxes = ledger.Taxes.OrderBy(x => x.From).ToArray();
        var rateIndex = 0;
        var taxIndex = 0;
        var roots = new Dictionary<(decimal, int), decimal>();
        for (var day = ledger.Start; day <= asOf; day = day.AddDays(1))
        {
            for (var i = 0; i < settlements.Count; i++)
                if (!settlements[i].Credited && settlements[i].CreditDate <= day)
                {
                    balance += settlements[i].Net;
                    pendingGross -= settlements[i].Gross;
                    pendingTax -= settlements[i].Tax;
                    settlements[i] = settlements[i] with { Credited = true };
                }
            if (movements.TryGetValue(day, out var delta)) balance += delta;
            if (balance < 0) throw new BusinessException($"Saldo negativo il {day:dd/MM/yyyy}.");
            if (balance > 1_000_000_000_000_000_000_000_000m || gross > 1_000_000_000_000_000_000_000_000m)
                throw new BusinessException("La proiezione supera il limite numerico supportato. Ridurre durata o importi.");
            while (rateIndex + 1 < rates.Length && rates[rateIndex + 1].From <= day) rateIndex++;
            while (taxIndex + 1 < taxes.Length && taxes[taxIndex + 1].From <= day) taxIndex++;
            var rate = rates[rateIndex].Rate;
            var taxRate = taxes[taxIndex].Rate;
            var denominator = ledger.Basis switch { DayBasis.Actual360 => 360, DayBasis.ActualYear => DateTime.IsLeapYear(day.Year) ? 366 : 365, _ => 365 };
            decimal interest;
            if (method == InterestMethod.Simple) interest = balance * rate / denominator;
            else
            {
                if (!roots.TryGetValue((rate, denominator), out var growth))
                    roots[(rate, denominator)] = growth = AnnualRoot(1 + rate, denominator) - 1;
                interest = (balance + gross) * growth;
            }
            var dailyTax = interest * taxRate;
            gross += interest;
            tax += dailyTax;
            if (segments.Count > 0 && segments[^1] is var last && last.Through.AddDays(1) == day &&
                last.Capital == balance && last.Rate == rate && last.TaxRate == taxRate && last.Denominator == denominator && day != periodStart)
                segments[^1] = last with { Through = day, Gross = last.Gross + interest, Tax = last.Tax + dailyTax };
            else segments.Add(new(day, day, balance, rate, taxRate, denominator, interest, dailyTax));
            if (day == end)
            {
                var roundedGross = Money(gross);
                var roundedTax = Money(tax);
                var credit = AccrualCalendar.CreditDate(end, ledger.Calendar);
                settlements.Add(new(periodStart, end, credit, roundedGross, roundedTax, roundedGross - roundedTax, credit <= day));
                if (credit <= day) balance += roundedGross - roundedTax;
                else { pendingGross += roundedGross; pendingTax += roundedTax; }
                gross = tax = 0;
                periodStart = day.AddDays(1);
                end = AccrualCalendar.PeriodEnd(periodStart, ledger.Calendar);
            }
            history?.Add(new(day, balance, gross - tax + pendingGross - pendingTax));
        }
        return new(asOf, balance, gross, tax, pendingGross, pendingTax, segments, settlements)
        { History = history ?? [] };
    }

    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal AnnualRoot(decimal value, int n)
    {
        decimal x = 1;
        for (var i = 0; i < 60; i++)
        {
            var next = ((n - 1) * x + value / Pow(x, n - 1)) / n;
            if (Math.Abs(next - x) < 0.000000000000000000000001m) return next;
            x = next;
        }
        return x;
    }
    private static decimal Pow(decimal value, int n)
    {
        decimal result = 1;
        while (n > 0) { if ((n & 1) == 1) result *= value; n >>= 1; if (n > 0) value *= value; }
        return result;
    }

    public static void Validate(Ledger x, DateOnly asOf)
    {
        if (x.Calendar is null || x.Movements is null || x.Rates is null || x.Taxes is null ||
            x.Movements.Any(m => m is null) || x.Rates.Any(r => r is null) || x.Taxes.Any(t => t is null))
            throw new BusinessException("Dati contabili incompleti.");
        if (asOf < x.Start || asOf.Year - x.Start.Year > 100 || x.Start.Year < 1900 || asOf.Year > 2200)
            throw new BusinessException("Intervallo non valido (1900–2200, massimo 100 anni).");
        if (!Enum.IsDefined(x.Basis) || !Enum.IsDefined(x.Calendar.Timing)) throw new BusinessException("Metodo o calendario non valido.");
        if (x.OpeningBalance < 0 || x.OpeningGross < 0 || x.OpeningTax < 0 || x.OpeningTax > x.OpeningGross)
            throw new BusinessException("Saldo e interessi iniziali non validi.");
        if (x.OpeningBalance > 1_000_000_000_000m || x.OpeningGross > 1_000_000_000_000m ||
            x.OpeningBalance != Money(x.OpeningBalance)) throw new BusinessException("Saldo iniziale non valido (massimo 1000 miliardi, importo in centesimi).");
        if (x.Rates.Count == 0 || x.Taxes.Count == 0 || x.Rates.Min(r => r.From) > x.Start || x.Taxes.Min(t => t.From) > x.Start)
            throw new BusinessException("Inserire tasso e aliquota validi alla data iniziale.");
        if (x.Rates.Any(r => r.Rate < 0 || r.Rate > 1) || x.Taxes.Any(t => t.Rate < 0 || t.Rate > 1))
            throw new BusinessException("Tassi e aliquote devono essere tra 0 e 100%.");
        if (x.Rates.GroupBy(r => r.From).Any(g => g.Count() > 1) || x.Taxes.GroupBy(t => t.From).Any(g => g.Count() > 1))
            throw new BusinessException("Una sola condizione per tipo e data.");
        if (x.Movements.Any(m => m.Amount <= 0 || m.Amount > 1_000_000_000_000m || m.Amount != Money(m.Amount) || m.Date < x.Start || m.Date.Year > 2200 || !Enum.IsDefined(m.Kind) || m.Note?.Length > 500))
            throw new BusinessException("Movimenti non validi: importi positivi in centesimi, date successive all'avvio, note entro 500 caratteri.");
        if (x.Movements.Select(m => m.Id).Distinct().Count() != x.Movements.Count || x.Movements.Any(m => m.Id == Guid.Empty))
            throw new BusinessException("Identificativi dei movimenti duplicati o mancanti.");
        _ = AccrualCalendar.PeriodEnd(x.Start, x.Calendar);
    }
}
