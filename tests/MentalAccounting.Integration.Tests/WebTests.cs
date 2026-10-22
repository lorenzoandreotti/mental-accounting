using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using MentalAccounting.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using MentalAccounting.Domain;

namespace MentalAccounting.Integration.Tests;
public sealed class WebTests
{
    [Fact] public async Task AnonymousPagesRequireLoginAndLoginContainsAntiforgery()
    {
        await using var factory=new AppFactory();using var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("https://localhost")});
        foreach(var path in new[]{"/","/Settings","/Movements","/Interest","/Simulations","/Transfer"})
        {var response=await client.GetAsync(path);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("/Account/Login",response.Headers.Location!.ToString());}
        var login=await client.GetStringAsync("/Account/Login");Assert.Contains("__RequestVerificationToken",login);Assert.Contains("Accedi",login);
        var invalid=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Username","admin"},{"Password","wrong"}}));
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }
    [Fact] public async Task ConfigurationReviewCommitAndMovementFlowWorkInItalian()
    {
        await using var factory=new AppFactory();using var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("https://localhost")});
        client.DefaultRequestHeaders.Add("X-Test-User","admin");
        var settings=await client.GetStringAsync("/Settings");
        var data=new Dictionary<string,string>{{"__RequestVerificationToken",Field(settings,"__RequestVerificationToken")},{"Version","0"},{"Start","2026-01-01"},
            {"Balance","1000,00"},{"Gross","0"},{"Tax","0"},{"Basis","Actual365"},{"Months","1"},{"Civil","true"},{"Timing","NextMonthFirst"},{"CreditDay","1"},
            {"Rates","2026-01-01;2,5"},{"Taxes","2026-01-01;26"}};
        var response=await client.PostAsync("/Settings",new FormUrlEncodedContent(data));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var review=await response.Content.ReadAsStringAsync();Assert.Contains("Conferma la modifica",review);
        response=await Confirm(client,"/Settings",review);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var home=await client.GetStringAsync("/");Assert.Contains("Saldo disponibile",home);
        var moves=await client.GetStringAsync("/Movements");
        var movement=await client.PostAsync("/Movements",new FormUrlEncodedContent(new Dictionary<string,string>{
            {"__RequestVerificationToken",Field(moves,"__RequestVerificationToken")},{"Version","1"},{"Date","2026-02-10"},{"Kind","Deposit"},{"Amount","50,25"},{"Note","Vacanze"}}));
        review=await movement.Content.ReadAsStringAsync();Assert.Contains("Conferma la modifica",review);
        Assert.Equal(HttpStatusCode.Redirect,(await Confirm(client,"/Movements",review)).StatusCode);
        Assert.Contains("Vacanze",await client.GetStringAsync("/Movements"));
        foreach(var path in new[]{"/Interest","/Simulations","/Transfer","/Settings"})Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(path)).StatusCode);
        var json=await client.GetStringAsync("/Transfer?handler=Export&format=json");Assert.Contains("50.25",json);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Movements",new FormUrlEncodedContent(data.Where(x=>x.Key!="__RequestVerificationToken")))).StatusCode);
    }
    public static string Field(string html,string name)
    {
        foreach(Match match in Regex.Matches(html,"<input[^>]+>"))
            if(Regex.IsMatch(match.Value,$"name=\"{Regex.Escape(name)}\""))
                return WebUtility.HtmlDecode(Regex.Match(match.Value,"value=\"([^\"]*)\"").Groups[1].Value);
        throw new InvalidOperationException($"Campo {name} non trovato: {html}");
    }
    [Fact] public async Task SimulationSaveAndRestoreRequireConfirmationAndDoNotChangeBalance()
    {
        await using var factory=new AppFactory();
        using(var scope=factory.Services.CreateScope())
        {
            var date=new DateOnly(2026,1,1);var service=scope.ServiceProvider.GetRequiredService<AccountingService>();
            await service.Commit(new(1,new(date,1000,0,0,DayBasis.Actual365,new(),[],[new(date,.02m)],[new(date,.26m)]),[]),0,new(2026,10,22));
        }
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("https://localhost")});client.DefaultRequestHeaders.Add("X-Test-User","admin");
        var page=await client.GetStringAsync("/Simulations");
        var response=await client.PostAsync("/Simulations",new FormUrlEncodedContent(new Dictionary<string,string>{
            {"__RequestVerificationToken",Field(page,"__RequestVerificationToken")},{"Through","2027-10-22"},{"Name","Vacanze"},{"Method","Simple"},
            {"FutureMovements","2027-01-01;versamento;100"},{"FutureRates",""},{"FutureTaxes",""}}));
        var simulation=await response.Content.ReadAsStringAsync();Assert.Contains("Saldo con il metodo selezionato",simulation);
        using(var scope=factory.Services.CreateScope())Assert.Empty((await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load()).Document.Scenarios);
        response=await client.PostAsync("/Simulations?handler=Save",new FormUrlEncodedContent(new Dictionary<string,string>{
            {"__RequestVerificationToken",Field(simulation,"__RequestVerificationToken")},{"ScenarioTicket",Field(simulation,"ScenarioTicket")}}));
        var review=await response.Content.ReadAsStringAsync();Assert.Contains("Conferma la modifica",review);
        Assert.Equal(HttpStatusCode.Redirect,(await Confirm(client,"/Simulations",review)).StatusCode);
        string backup;
        using(var scope=factory.Services.CreateScope())
        {
            var state=await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load();Assert.Empty(state.Document.Ledger!.Movements);Assert.Single(state.Document.Scenarios);
            backup=BackupFormat.Write(state.Document);
        }
        page=await client.GetStringAsync("/Transfer");using var upload=new MultipartFormDataContent();
        upload.Add(new StringContent(Field(page,"__RequestVerificationToken")),"__RequestVerificationToken");upload.Add(new StringContent("2"),"Version");
        upload.Add(new StringContent(backup),"Upload","backup.json");
        response=await client.PostAsync("/Transfer?handler=Restore",upload);review=await response.Content.ReadAsStringAsync();Assert.Contains("Conferma la modifica",review);
        Assert.Equal(HttpStatusCode.Redirect,(await Confirm(client,"/Transfer",review)).StatusCode);
    }
    private static Task<HttpResponseMessage> Confirm(HttpClient client,string page,string html)=>client.PostAsync(page+"?handler=Confirm",new FormUrlEncodedContent(new Dictionary<string,string>
    {{"__RequestVerificationToken",Field(html,"__RequestVerificationToken")},{"Ticket",Field(html,"Ticket")}}));
}
public sealed class AppFactory(bool trustProxy=false):WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("https_port","443");
        if(trustProxy)builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{{"ReverseProxy:KnownProxies:0","127.0.0.1"}}));
        builder.ConfigureLogging(logging=>logging.ClearProviders().AddConsole());
        builder.ConfigureServices(services=>
        {
            services.RemoveAll<IAccountStore>();services.AddSingleton<IAccountStore,MemoryAccountStore>();
            services.RemoveAll<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            services.RemoveAll<TimeProvider>();services.AddSingleton<TimeProvider>(new FixedTime());
            services.AddAuthentication(o=>o.DefaultAuthenticateScheme="Test").AddScheme<AuthenticationSchemeOptions,TestAuthentication>("Test",_=>{});
        });
    }
}
public sealed class FixedTime:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,22,12,0,0,TimeSpan.Zero); }
public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(Request.Headers.ContainsKey("X-Test-User")?
        AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"test"),new Claim(ClaimTypes.Name,"admin" )],"Test")),"Test")):AuthenticateResult.NoResult());
}
public sealed class MemoryAccountStore:IAccountStore
{
    private AccountSnapshot snapshot=new(0,new(1,null,[]));
    public Task<AccountSnapshot> Load(CancellationToken ct=default)=>Task.FromResult(snapshot);
    public Task Save(AccountDocument document,long expectedVersion,CancellationToken ct=default)
    {
        if(snapshot.Version!=expectedVersion)throw new ConcurrencyException();
        snapshot=new(expectedVersion+1,BackupFormat.Read(BackupFormat.Write(document)));return Task.CompletedTask;
    }
}
