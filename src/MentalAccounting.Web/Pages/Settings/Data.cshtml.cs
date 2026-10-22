using System.Text;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages.Settings;
public sealed class DataModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time, IWorkbookReader workbook) : LedgerPage(service,protection,time)
{
    [BindProperty] public IFormFile? Upload { get; set; }
    public ImportPreview? Import { get; set; }
    public async Task OnGetAsync()=>await Load();
    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        await Load();
        if(format=="json")return File(Encoding.UTF8.GetBytes(BackupFormat.Write(State.Document)),"application/json","accantonamento.json");
        if(State.Document.Ledger is not {} ledger)return RedirectToPage("/Settings/Conditions");
        var text=format=="settlements"?AccountingService.SettlementsCsv(Service.Calculate(ledger,Today)):AccountingService.MovementsCsv(ledger);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),"text/csv; charset=utf-8",format=="settlements"?"liquidazioni.csv":"movimenti.csv");
    }
    public async Task<IActionResult> OnPostWorkbookAsync()
    {
        var version=Version;await Load();
        try
        {
            CheckUpload(".xlsx");
            using var stream=new MemoryStream();await Upload!.CopyToAsync(stream);stream.Position=0;
            Import=workbook.Read(stream);
            if(Import.Errors.Count>0)return Page();
            if(Import.Movements.Count==0)throw new BusinessException("Nessun movimento importabile.");
            return await Preview(State.Document with { Ledger=RequireLedger() with { Movements=Import.Movements } },version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException){return Failure(e);}
    }
    public async Task<IActionResult> OnPostRestoreAsync()
    {
        var version=Version;await Load();
        try
        {
            CheckUpload(".json");using var reader=new StreamReader(Upload!.OpenReadStream());
            return await Preview(BackupFormat.Read(await reader.ReadToEndAsync()),version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException){return Failure(e);}
    }
    private void CheckUpload(string extension)
    {
        if(Upload is null || Upload.Length is <=0 or >5_000_000 || !Path.GetExtension(Upload.FileName).Equals(extension,StringComparison.OrdinalIgnoreCase))
            throw new BusinessException($"Selezionare un file {extension} di massimo 5 MB.");
    }
}
