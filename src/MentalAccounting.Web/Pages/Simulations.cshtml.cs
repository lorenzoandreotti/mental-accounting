using System.Text.Json;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages;
public sealed class SimulationsModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : LedgerPage(service, protection, time)
{
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public DateOnly Through { get; set; }
    [BindProperty] public InterestMethod Method { get; set; }
    [BindProperty] public string FutureMovements { get; set; } = "";
    [BindProperty] public string FutureRates { get; set; } = "";
    [BindProperty] public string FutureTaxes { get; set; } = "";
    [BindProperty] public string ScenarioTicket { get; set; } = "";
    public CalculationResult? Result { get; set; }
    public CalculationResult? Comparison { get; set; }
    public string? ResultName { get; set; }
    public async Task OnGetAsync(Guid? id)
    {
        await Load(); Through=Today.AddYears(1);
        if(id is {} value && State.Document.Scenarios.SingleOrDefault(x=>x.Id==value) is {} scenario)
        { Result=Service.Calculate(scenario.Ledger,scenario.Through,scenario.Method); ResultName=scenario.Name; Method=scenario.Method; }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        await Load(); if(!ModelState.IsValid)return Page();
        try
        {
            if(Through<Today)throw new BusinessException("Scegli una data finale uguale o successiva a oggi.");
            var ledger=RequireLedger();
            var moves=EntryParser.Movements(FutureMovements); var rates=EntryParser.Rates(FutureRates); var taxes=EntryParser.Taxes(FutureTaxes);
            if(moves.Any(x=>x.Date<=Today)||rates.Any(x=>x.From<=Today)||taxes.Any(x=>x.From<=Today))
                throw new BusinessException("Le ipotesi aggiuntive devono avere date successive a oggi.");
            var forecast=ledger with { Movements=[..ledger.Movements,..moves], Rates=[..ledger.Rates,..rates], Taxes=[..ledger.Taxes,..taxes] };
            var scenario=new Scenario(Guid.NewGuid(),string.IsNullOrWhiteSpace(Name)?"Nuova simulazione":Name,Through,Method,forecast);
            Service.ValidateDocument(State.Document with { Scenarios=[scenario] },Today);
            Result=Service.Calculate(forecast,Through,Method);
            Comparison=Service.Calculate(forecast,Through,Method==InterestMethod.Simple?InterestMethod.Compound:InterestMethod.Simple);
            ResultName=scenario.Name;
            ScenarioTicket=Protector.Protect(JsonSerializer.Serialize(new ReviewTicket(State.Version,State.Document with { Scenarios=[..State.Document.Scenarios,scenario] },Clock.GetUtcNow().AddMinutes(30)),BackupFormat.Options));
            return Page();
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException){return Failure(e);}
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        await Load();
        try
        {
            if(string.IsNullOrWhiteSpace(ScenarioTicket))throw new BusinessException("Calcolare prima la simulazione.");
            var ticket=JsonSerializer.Deserialize<ReviewTicket>(Protector.Unprotect(ScenarioTicket),BackupFormat.Options)!;
            if(ticket.Expires<Clock.GetUtcNow())throw new BusinessException("Simulazione scaduta. Ripetere il calcolo.");
            return await Preview(ticket.Document,ticket.Version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException){return Failure(e);}
        catch(System.Security.Cryptography.CryptographicException){return Failure(new BusinessException("Simulazione non valida."));}
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var version=Version;await Load();
        try{return await Preview(State.Document with { Scenarios=State.Document.Scenarios.Where(x=>x.Id!=id).ToList() },version);}
        catch(Exception e) when(e is BusinessException or ConcurrencyException){return Failure(e);}
    }
}

