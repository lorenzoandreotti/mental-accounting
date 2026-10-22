using System.Globalization;
using MentalAccounting.Domain;
namespace MentalAccounting.Application;

public static class EntryParser
{
    public static decimal Number(string value)
    {
        if (!decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var result)) throw new BusinessException($"Numero non valido: {value}.");
        return result;
    }
    public static DateOnly Date(string value) => DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var result) ? result : throw new BusinessException("Usare date nel formato AAAA-MM-GG.");
    public static List<RateChange> Rates(string text) => Lines(text, 2).Select(x => new RateChange(Date(x[0]), Number(x[1])/100)).ToList();
    public static List<TaxChange> Taxes(string text) => Lines(text, 2).Select(x => new TaxChange(Date(x[0]), Number(x[1])/100)).ToList();
    public static List<Movement> Movements(string text) => Lines(text, 3).Select(x => new Movement(Guid.NewGuid(), Date(x[0]),
        x[1].ToLowerInvariant() switch { "versamento" => MovementKind.Deposit, "prelievo" => MovementKind.Withdrawal, "addebito" => MovementKind.Charge,
            _ => throw new BusinessException("Tipi ammessi: versamento, prelievo, addebito.") }, Number(x[2]), x.Length > 3 ? x[3] : null)).ToList();
    private static IEnumerable<string[]> Lines(string? text, int columns)
    {
        var lines = (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1000) throw new BusinessException("Massimo 1000 righe per campo.");
        foreach (var line in lines)
        {
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length < columns || fields.Take(columns).Any(string.IsNullOrWhiteSpace)) throw new BusinessException("Riga incompleta: separare i campi con punto e virgola.");
            yield return fields;
        }
    }
    public static string Format(IEnumerable<RateChange> values) => string.Join('\n', values.OrderBy(x=>x.From).Select(x=>FormattableString.Invariant($"{x.From:yyyy-MM-dd};{x.Rate*100:0.######}")));
    public static string Format(IEnumerable<TaxChange> values) => string.Join('\n', values.OrderBy(x=>x.From).Select(x=>FormattableString.Invariant($"{x.From:yyyy-MM-dd};{x.Rate*100:0.######}")));
    public static string Format(IEnumerable<Movement> values) => string.Join('\n', values.OrderBy(x=>x.Date).Select(x=>FormattableString.Invariant($"{x.Date:yyyy-MM-dd};{(x.Kind==MovementKind.Deposit?"versamento":x.Kind==MovementKind.Withdrawal?"prelievo":"addebito")};{x.Amount:0.00};{x.Note}")));
}
