using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MentalAccounting.Domain;

namespace MentalAccounting.Application;

public sealed record Scenario(Guid Id, string Name, DateOnly Through, InterestMethod Method, Ledger Ledger);
public sealed record AccountDocument(int SchemaVersion, Ledger? Ledger, List<Scenario> Scenarios);
public sealed record AccountSnapshot(long Version, AccountDocument Document);
public sealed record ChangePreview(long Version, AccountDocument Document, CalculationResult? Before, CalculationResult After);
public sealed record ImportPreview(List<Movement> Movements, List<string> Warnings, List<string> Errors);
public interface IAccountStore
{
    Task<AccountSnapshot> Load(CancellationToken ct = default);
    Task Save(AccountDocument document, long expectedVersion, CancellationToken ct = default);
}
public interface IWorkbookReader { ImportPreview Read(Stream stream); }
public sealed class ConcurrencyException() : Exception("I dati sono cambiati su un altro dispositivo. Ricaricare e ripetere l'operazione.");

public static class BackupFormat
{
    public static readonly JsonSerializerOptions Options = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static string Write(AccountDocument value) => JsonSerializer.Serialize(value, Options);
    public static AccountDocument Read(string value)
    {
        try
        {
            var document = JsonSerializer.Deserialize<AccountDocument>(value, Options)
                ?? throw new BusinessException("Backup vuoto.");
            if (document.SchemaVersion != 1 || document.Scenarios is null)
                throw new BusinessException("Versione del backup non supportata.");
            return document;
        }
        catch (JsonException) { throw new BusinessException("Formato JSON non valido."); }
    }
}

public sealed class AccountingService(IAccountStore store, InterestCalculator calculator)
{
    public Task<AccountSnapshot> Load(CancellationToken ct = default) => store.Load(ct);
    public CalculationResult Calculate(Ledger ledger, DateOnly through, InterestMethod method = InterestMethod.Simple, bool includeHistory = false)
        => calculator.Calculate(ledger, through, method, includeHistory);

    public async Task<ChangePreview> Preview(AccountDocument proposed, long version, DateOnly today, CancellationToken ct = default)
    {
        var current = await store.Load(ct);
        if (current.Version != version) throw new ConcurrencyException();
        ValidateDocument(proposed, today);
        var ledger = proposed.Ledger ?? throw new BusinessException("Configurare l'accantonamento.");
        return new(version, proposed, current.Document.Ledger is { } old ? calculator.Calculate(old, today) : null,
            calculator.Calculate(ledger, today));
    }

    public async Task Commit(AccountDocument proposed, long version, DateOnly today, CancellationToken ct = default)
    {
        ValidateDocument(proposed, today);
        await store.Save(proposed, version, ct);
    }

    public void ValidateDocument(AccountDocument document, DateOnly today)
    {
        if (document.SchemaVersion != 1 || document.Scenarios is null || document.Scenarios.Count > 100 || document.Scenarios.Any(x => x is null || x.Ledger is null))
            throw new BusinessException("Documento non valido o troppi scenari (massimo 100).");
        if (document.Ledger is not { } ledger) throw new BusinessException("Configurare il saldo iniziale.");
        CheckCollections(ledger);
        if (ledger.Start > today || ledger.Movements.Any(x => x.Date > today) || ledger.Rates.Any(x => x.From > today) || ledger.Taxes.Any(x => x.From > today))
            throw new BusinessException("Date future ammesse solo nelle simulazioni.");
        calculator.Calculate(ledger, today);
        if (document.Scenarios.Select(x => x.Id).Distinct().Count() != document.Scenarios.Count)
            throw new BusinessException("Identificativi degli scenari duplicati.");
        foreach (var scenario in document.Scenarios)
        {
            if (string.IsNullOrWhiteSpace(scenario.Name) || scenario.Name.Length > 100 || !Enum.IsDefined(scenario.Method))
                throw new BusinessException("Nome o metodo dello scenario non valido.");
            CheckCollections(scenario.Ledger);
            calculator.Calculate(scenario.Ledger, scenario.Through, scenario.Method);
            if (scenario.Ledger.Movements.Any(x => x.Date > scenario.Through))
                throw new BusinessException("Lo scenario contiene movimenti oltre la data finale.");
        }
    }

    private static void CheckCollections(Ledger ledger)
    {
        if (ledger.Calendar is null || ledger.Movements is null || ledger.Rates is null || ledger.Taxes is null ||
            ledger.Movements.Count > 10000 || ledger.Rates.Count > 1000 || ledger.Taxes.Count > 1000)
            throw new BusinessException("Dati mancanti o limite di registrazioni superato.");
    }

    public static string MovementsCsv(Ledger ledger)
    {
        var csv = new StringBuilder("Data;Tipo;Importo;Nota\r\n");
        foreach (var m in ledger.Movements.OrderBy(m => m.Date))
            csv.AppendLine($"{m.Date:yyyy-MM-dd};{m.Kind};{m.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};{Cell(m.Note ?? "")}");
        return csv.ToString();
    }
    public static string SettlementsCsv(CalculationResult result)
    {
        var csv = new StringBuilder("Dal;Al;Accredito;Lordo;Imposta;Netto;Accreditato\r\n");
        foreach (var s in result.Settlements)
            csv.AppendLine(FormattableString.Invariant($"{s.From:yyyy-MM-dd};{s.Through:yyyy-MM-dd};{s.CreditDate:yyyy-MM-dd};{s.Gross:0.00};{s.Tax:0.00};{s.Net:0.00};{s.Credited}"));
        return csv.ToString();
    }
    private static string Cell(string value)
    {
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
