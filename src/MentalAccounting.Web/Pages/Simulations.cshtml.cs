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
    [BindProperty] public List<MovementInput> FutureMovements { get; set; } = [];
    [BindProperty] public List<ConditionInput> FutureRates { get; set; } = [];
    [BindProperty] public List<ConditionInput> FutureTaxes { get; set; } = [];
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
        await Load();
        GridInputValidation.Conditions(FutureRates,nameof(FutureRates),ModelState,Today);
        GridInputValidation.Conditions(FutureTaxes,nameof(FutureTaxes),ModelState,Today);
        foreach(var row in FutureMovements.Where(x=>x.Date<=Today)) ModelState.AddModelError($"FutureMovements[{row.Key}].Date","Usare una data successiva a oggi.");
        foreach(var row in FutureMovements.Where(x=>x.Amount.HasValue && InterestCalculator.Money(x.Amount.Value)!=x.Amount.Value)) ModelState.AddModelError($"FutureMovements[{row.Key}].Amount","Inserire un importo in centesimi.");
        if(FutureMovements.Count>1000 || FutureMovements.Select(x=>x.Key).Distinct().Count()!=FutureMovements.Count) ModelState.AddModelError(nameof(FutureMovements),"Troppe righe o identificativi duplicati.");
        if(!ModelState.IsValid)return Page();
        try
        {
            if(Through<Today)throw new BusinessException("Scegli una data finale uguale o successiva a oggi.");
            var ledger=RequireLedger();
            var moves=FutureMovements.Select(x=>new Movement(Guid.NewGuid(),x.Date!.Value,x.Kind!.Value,x.Amount!.Value,x.Note)).ToList();
            var rates=FutureRates.Select(x=>new RateChange(x.From!.Value,x.Percentage!.Value/100)).ToList();
            var taxes=FutureTaxes.Select(x=>new TaxChange(x.From!.Value,x.Percentage!.Value/100)).ToList();
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

