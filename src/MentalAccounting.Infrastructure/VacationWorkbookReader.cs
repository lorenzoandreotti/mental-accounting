using System.IO.Compression;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using MentalAccounting.Application;
using MentalAccounting.Domain;

namespace MentalAccounting.Infrastructure;

public sealed class VacationWorkbookReader : IWorkbookReader
{
    // Pinned ClosedXML 0.105.0 embeds this metric-only font. Import needs no installed fonts.
    private static readonly Lazy<IXLGraphicEngine> ImportGraphics = new(() =>
    {
        using var font = typeof(XLWorkbook).Assembly.GetManifestResourceStream("ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf")
            ?? throw new InvalidOperationException("Font incorporato ClosedXML non disponibile.");
        return DefaultGraphicEngine.CreateOnlyWithFonts(font);
    });
    public ImportPreview Read(Stream stream)
    {
        var movements = new List<Movement>();
        var warnings = new List<string>();
        var errors = new List<string>();
        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
                if (zip.Entries.Sum(x => x.Length) > 30_000_000 || zip.Entries.Count > 2000)
                    throw new BusinessException("Il contenuto del file supera i limiti di importazione.");
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream, new LoadOptions { GraphicEngine = ImportGraphics.Value });
            if (!workbook.TryGetWorksheet("Sheet1", out var sheet))
                return new([], [], ["Manca il foglio Sheet1 previsto dalla mappatura."]);
            var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (last > 10001) return new([], [], ["Massimo 10000 righe importabili."]);
            for (var row = 2; row <= last; row++)
            {
                var dateCell = sheet.Cell(row, 3);
                var typeCell = sheet.Cell(row, 4);
                var amountCell = sheet.Cell(row, 5);
                if (typeCell.HasFormula) { errors.Add($"Riga {row}: tipo calcolato non ammesso."); continue; }
                var type = typeCell.GetString().Trim();
                if (string.IsNullOrEmpty(type) && dateCell.IsEmpty() && amountCell.IsEmpty()) continue;
                if (type is "Interessi" or "Consolidamento")
                { warnings.Add($"Riga {row}: {type} ignorato; verrà ricalcolato."); continue; }
                var kind = type.ToLowerInvariant() switch
                { "carico" => MovementKind.Deposit, "prelievo" => MovementKind.Withdrawal, "addebito" => MovementKind.Charge, _ => (MovementKind?)null };
                if (kind is null) { errors.Add($"Riga {row}: tipo '{type}' non riconosciuto."); continue; }
                if (dateCell.HasFormula || amountCell.HasFormula)
                { errors.Add($"Riga {row}: data o importo derivano da formula; sostituirli con valori verificati."); continue; }
                if (!dateCell.TryGetValue<DateTime>(out var date) || !amountCell.TryGetValue<decimal>(out var amount))
                { errors.Add($"Riga {row}: data o importo non valido."); continue; }
                if (kind != MovementKind.Deposit && amount < 0)
                { amount = -amount; warnings.Add($"Riga {row}: uscita negativa convertita in importo positivo del prelievo/addebito."); }
                var rounded = InterestCalculator.Money(amount);
                if (amount <= 0 || Math.Abs(amount - rounded) > .00000001m)
                { errors.Add($"Riga {row}: importo non valido o frazioni reali di centesimo."); continue; }
                amount = rounded; // Remove only the insignificant binary Excel storage residue.
                movements.Add(new(Guid.NewGuid(), DateOnly.FromDateTime(date), kind.Value, amount, $"Importazione Sheet1, riga {row}"));
            }
        }
        catch (BusinessException) { throw; }
        catch (Exception e) when (e is not OutOfMemoryException)
        { errors.Add("File XLSX non leggibile o non compatibile con la mappatura."); }
        return new(movements, warnings, errors);
    }
}
