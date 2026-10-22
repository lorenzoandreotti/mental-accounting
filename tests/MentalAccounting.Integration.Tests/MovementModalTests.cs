using System.Net;
using System.Text.Json;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using MentalAccounting.Web.Pages;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace MentalAccounting.Integration.Tests;

public sealed class MovementModalTests
{
    private static async Task<(HttpClient Client, Guid Id)> Setup(AppFactory factory)
    {
        using var scope=factory.Services.CreateScope();
        var start=new DateOnly(2026,1,1); var id=Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<AccountingService>().Commit(new(1,
            new(start,100,0,0,DayBasis.Actual365,new(),[new(id,start,MovementKind.Deposit,200,"Da eliminare")],[new(start,.0365m)],[new(start,.26m)]),[]),0,new(2026,10,22));
        var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("https://localhost")});
        client.DefaultRequestHeaders.Add("X-Test-User","admin"); return(client,id);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client,string handler,Dictionary<string,string> fields)
    {
        fields["__RequestVerificationToken"]=WebTests.Field(await client.GetStringAsync("/"),"__RequestVerificationToken");
        return await client.PostAsync("/"+handler,new FormUrlEncodedContent(fields));
    }
    private static async Task<AccountSnapshot> State(AppFactory factory)
    {
        using var scope=factory.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load();
    }
    private static Dictionary<string,string> Movement(Guid? id=null,string amount="50,25",string version="1") => new()
    { ["Version"]=version,["Date"]="2026-01-02",["Kind"]="Withdrawal",["Amount"]=amount,["Note"]="Nota conservata",["Id"]=id?.ToString()??"" };

    [Fact] public async Task SaveAndEditAreImmediateAndInvalidInputIsPreserved()
    {
        await using var factory=new AppFactory();var(client,id)=await Setup(factory);using var owned=client;
        var response=await Post(client,"",Movement(id));Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var state=await State(factory);Assert.Equal(2,state.Version);Assert.Equal(50.25m,Assert.Single(state.Document.Ledger!.Movements).Amount);
        response=await Post(client,"",Movement(amount:"non valido",version:"2"));var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("data-auto-open=\"true\"",html);Assert.Equal("non valido",WebTests.Field(html,"Amount"));Assert.Equal("Nota conservata",WebTests.Field(html,"Note"));
        Assert.Equal(2,(await State(factory)).Version);
        response=await Post(client,"",Movement(Guid.NewGuid(),version:"2"));Assert.Contains("Movimento non trovato",await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task StaleSaveCannotOverwriteOrAcquireANewerVersion()
    {
        await using var factory=new AppFactory();var(client,id)=await Setup(factory);using var owned=client;
        await Post(client,"",Movement(id));
        var response=await Post(client,"",Movement(id));var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("Ricarica i dati",html);Assert.Equal("1",WebTests.Field(html,"Version"));Assert.Equal("Nota conservata",WebTests.Field(html,"Note"));
        Assert.Equal(2,(await State(factory)).Version);
    }
    [Fact] public async Task DeletePreviewDoesNotWriteAndConfirmationMatchesCalculation()
    {
        await using var factory=new AppFactory();var(client,id)=await Setup(factory);using var owned=client;
        var response=await Post(client,"?handler=Delete",new(){["id"]=id.ToString(),["Version"]="1"});var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("Dopo l'eliminazione",WebUtility.HtmlDecode(html));Assert.Contains("Differenza",html);Assert.DoesNotContain("Conferma la modifica",html);
        Assert.Contains("Da eliminare",html);Assert.Equal(1,(await State(factory)).Version);
        var ticket=WebTests.Field(html,"DeleteTicket");
        await client.GetStringAsync("/");Assert.Equal(1,(await State(factory)).Version);
        response=await Post(client,"?handler=DeleteConfirm",new(){["DeleteTicket"]=ticket});Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var state=await State(factory);Assert.Empty(state.Document.Ledger!.Movements);Assert.Equal(2,state.Version);
        var expected=new InterestCalculator().Calculate(state.Document.Ledger,new(2026,10,22));
        Assert.Contains(expected.Balance.ToString("C",System.Globalization.CultureInfo.GetCultureInfo("it-IT")),WebUtility.HtmlDecode(html));
        response=await Post(client,"?handler=DeleteConfirm",new(){["DeleteTicket"]=ticket});Assert.Contains("altro dispositivo",await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task DeletionThatWouldMakeBalanceNegativeCannotBeConfirmed()
    {
        await using var factory=new AppFactory();var(client,id)=await Setup(factory);using var owned=client;
        await Post(client,"",Movement(amount:"250"));
        var response=await Post(client,"?handler=Delete",new(){["id"]=id.ToString(),["Version"]="2"});var html=await response.Content.ReadAsStringAsync();
        Assert.Contains("Saldo negativo",html);Assert.DoesNotContain("Conferma eliminazione",html);Assert.Equal(2,(await State(factory)).Version);
    }
    [Fact] public async Task AlteredExpiredAndStaleDeleteTicketsCannotSave()
    {
        await using var factory=new AppFactory();var(client,id)=await Setup(factory);using var owned=client;
        var preview=await Post(client,"?handler=Delete",new(){["id"]=id.ToString(),["Version"]="1"});var html=await preview.Content.ReadAsStringAsync();var ticket=WebTests.Field(html,"DeleteTicket");
        var response=await Post(client,"?handler=DeleteConfirm",new(){["DeleteTicket"]=ticket+"altered"});Assert.Contains("Anteprima non valida",await response.Content.ReadAsStringAsync());
        using var scope=factory.Services.CreateScope();var protector=scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Accounting.ChangeReview.v1").CreateProtector("DeleteMovement.v1");
        var value=JsonSerializer.Deserialize<ReviewTicket>(protector.Unprotect(ticket),BackupFormat.Options)!;
        var expired=protector.Protect(JsonSerializer.Serialize(value with { Expires=new(2026,10,21,0,0,0,TimeSpan.Zero) },BackupFormat.Options));
        response=await Post(client,"?handler=DeleteConfirm",new(){["DeleteTicket"]=expired});Assert.Contains("Anteprima scaduta",await response.Content.ReadAsStringAsync());
        await Post(client,"",Movement(id));
        response=await Post(client,"?handler=DeleteConfirm",new(){["DeleteTicket"]=ticket});html=await response.Content.ReadAsStringAsync();Assert.Contains("altro dispositivo",html);Assert.DoesNotContain("Conferma eliminazione",html);
        Assert.Equal(2,(await State(factory)).Version);
    }
}
