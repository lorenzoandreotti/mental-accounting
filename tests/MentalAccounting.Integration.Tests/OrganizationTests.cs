using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MentalAccounting.Integration.Tests;

public sealed class OrganizationTests
{
    private static HttpClient Client(AppFactory factory)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-User", "admin"); return client;
    }
    private static async Task<Ledger> Seed(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope(); var start = new DateOnly(2026, 1, 1);
        var ledger = new Ledger(start, 1000, 0, 0, DayBasis.Actual365, new(),
            [new(Guid.NewGuid(), new(2026, 1, 10), MovementKind.Deposit, 100, "Versamento di prova"), new(Guid.NewGuid(), new(2026, 1, 15), MovementKind.Withdrawal, 50)],
            [new(start, .0365m)], [new(start, .26m)]);
        await scope.ServiceProvider.GetRequiredService<AccountingService>().Commit(new(1, ledger, []), 0, new(2026, 10, 22));
        return ledger;
    }
    [Fact] public async Task LegacyAddressesRedirectAndPreserveEditAndDownloadParameters()
    {
        await using var factory = new AppFactory(); using var client = Client(factory); await Seed(factory);
        var id = Guid.NewGuid();
        var movements = await client.GetAsync($"/Movements?edit={id}");
        Assert.Equal(HttpStatusCode.Redirect, movements.StatusCode); Assert.Contains($"edit={id}", movements.Headers.Location!.ToString());
        Assert.DoesNotContain("Movements", movements.Headers.Location.ToString());
        var settings = await client.GetAsync("/Settings"); Assert.Equal("/Settings/Conditions", settings.Headers.Location!.ToString());
        var transfer = await client.GetAsync("/Transfer?handler=Export&format=json");
        Assert.Equal(HttpStatusCode.Redirect, transfer.StatusCode); Assert.Equal("/Settings/Data?handler=Export&format=json", transfer.Headers.Location!.ToString());
        var download = await client.GetAsync(transfer.Headers.Location); Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/json", download.Content.Headers.ContentType!.MediaType);
        Assert.Contains("attachment", download.Content.Headers.ContentDisposition!.ToString());
    }
    [Fact] public async Task SettingsHaveSharedNavigationAndAppearanceNeedsNoAccountMutation()
    {
        await using var factory = new AppFactory(); using var client = Client(factory); await Seed(factory);
        foreach (var route in new[] { "/Settings/Conditions", "/Settings/Appearance", "/Settings/Data" })
        {
            var html = await client.GetStringAsync(route);
            Assert.Contains("aria-label=\"Impostazioni\"", html); Assert.Contains("/Settings/Appearance", html);
            Assert.Contains("/Settings/Data", html); Assert.Contains("/Settings/Conditions", html);
        }
        var appearance = await client.GetStringAsync("/Settings/Appearance"); Assert.Contains("data-theme-mode-select", appearance);
        var login = await client.GetStringAsync("/Account/Login"); Assert.Contains("data-theme-toggle", login); Assert.DoesNotContain("data-theme-mode-select", login);
        using var scope = factory.Services.CreateScope(); Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load()).Version);
    }
    [Fact] public async Task HomeIncludesEveryActualCreditReadOnlyAndHistoryReconciles()
    {
        await using var factory = new AppFactory(); using var client = Client(factory); var ledger = await Seed(factory);
        var html = await client.GetStringAsync("/"); var calculation = new InterestCalculator().Calculate(ledger, new(2026, 10, 22));
        var rows = Regex.Matches(html, "<tr data-activity-type=\"Interest\".*?</tr>", RegexOptions.Singleline);
        Assert.Equal(calculation.Settlements.Count(x => x.Credited), rows.Count);
        foreach (Match row in rows)
        {
            Assert.DoesNotContain("<form", row.Value); Assert.DoesNotContain("Modifica", row.Value); Assert.DoesNotContain("Elimina", row.Value);
            Assert.Contains("PeriodStart=", row.Value); Assert.Contains("Lordo", row.Value); Assert.Contains("Imposta", row.Value);
        }
        Assert.Contains("data-activity-type=\"Withdrawal\"", html); Assert.Contains("-50,00", html);
        var json = Regex.Match(html, "<script type=\"application/json\" data-history>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        using var history = JsonDocument.Parse(json); var last = history.RootElement.EnumerateArray().Last();
        Assert.Equal(calculation.Balance, last.GetProperty("available").GetDecimal());
        Assert.Equal(calculation.Balance + calculation.AccruedNet, last.GetProperty("total").GetDecimal());
        Assert.Contains("data-auto-open=\"false\"", Regex.Match(html, "<dialog id=\"movement-form\"[^>]*>").Value);
        var id = ledger.Movements[0].Id; var edit = await client.GetStringAsync($"/?edit={id}");
        Assert.Contains("data-auto-open=\"true\"", Regex.Match(edit, "<dialog id=\"movement-form\"[^>]*>").Value);
        Assert.Equal("100", WebTests.Field(edit, "Amount"));
    }
    [Fact] public async Task InterestLinkShowsOnlySelectedPeriodAndCorrectCreditedState()
    {
        await using var factory = new AppFactory(); using var client = Client(factory); await Seed(factory);
        var html = await client.GetStringAsync("/Interest?PeriodStart=2026-01-01&Through=2026-01-31");
        Assert.Contains("Netto del periodo", html); Assert.Contains("Accreditato", html); Assert.Contains("Mostra tutti i periodi", html);
        Assert.DoesNotContain("data-value=\"2026-02-28\"", html);
        var invalid = await client.GetStringAsync("/Interest?PeriodStart=2026-01-02&Through=2026-01-31");
        Assert.Contains("Periodo di calcolo non trovato", invalid);
    }
    [Fact] public async Task InvalidMovementKeepsExpandedFormAndDerivedRowsCannotBeDeleted()
    {
        await using var factory = new AppFactory(); using var client = Client(factory); await Seed(factory);
        var html = await client.GetStringAsync("/");
        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"] = WebTests.Field(html, "__RequestVerificationToken"), ["Version"] = "1", ["Date"] = "2026-01-20", ["Kind"] = "Withdrawal", ["Amount"] = "2000", ["Note"] = "Da correggere" }));
        var invalid = await response.Content.ReadAsStringAsync(); Assert.Contains("Saldo negativo", invalid);
        Assert.Contains("data-auto-open=\"true\"", Regex.Match(invalid, "<dialog id=\"movement-form\"[^>]*>").Value); Assert.Equal("Da correggere", WebTests.Field(invalid, "Note"));
        response = await client.PostAsync("/?handler=Delete", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"] = WebTests.Field(invalid, "__RequestVerificationToken"), ["Version"] = "1", ["id"] = Guid.Empty.ToString() }));
        Assert.Contains("non modificabile", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load()).Version);
    }
}
