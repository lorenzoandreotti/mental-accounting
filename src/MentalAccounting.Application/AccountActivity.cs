using MentalAccounting.Domain;

namespace MentalAccounting.Application;

public sealed record AccountActivity(DateOnly Date, string Type, decimal Amount, string? Note,
    Guid? MovementId = null, Settlement? Interest = null)
{
    public static IReadOnlyList<AccountActivity> Build(Ledger ledger, CalculationResult result)
    {
        var entries = ledger.Movements.Where(x => x.Date <= result.AsOf).Select(x =>
            new AccountActivity(x.Date, x.Kind.ToString(), x.Kind == MovementKind.Deposit ? x.Amount : -x.Amount, x.Note, x.Id));
        var credits = result.Settlements.Where(x => x.Credited).Select(x =>
            new AccountActivity(x.CreditDate, "Interest", x.Net, $"Interessi dal {x.From:dd/MM/yyyy} al {x.Through:dd/MM/yyyy}", Interest: x));
        return entries.Concat(credits).OrderByDescending(x => x.Date).ToArray();
    }
}
