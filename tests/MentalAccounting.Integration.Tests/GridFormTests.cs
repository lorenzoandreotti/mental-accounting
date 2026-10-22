using System.Net;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MentalAccounting.Integration.Tests;

public sealed class GridFormTests
{
    private static HttpClient Client(AppFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        return client;
    }
    private static List<KeyValuePair<string, string>> Settings(string token) => new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token, ["Version"] = "0", ["Start"] = "2026-01-01", ["Balance"] = "1000,00",
        ["Gross"] = "0", ["Tax"] = "0", ["Basis"] = "Actual365", ["Months"] = "1", ["Civil"] = "true",
        ["Timing"] = "NextMonthFirst", ["CreditDay"] = "1"
    }.ToList();
    private static void Add(List<KeyValuePair<string, string>> form, string prefix, string key, string date, string percentage)
    {
        form.Add(new($"{prefix}.Index", key)); form.Add(new($"{prefix}[{key}].Key", key));
        form.Add(new($"{prefix}[{key}].From", date)); form.Add(new($"{prefix}[{key}].Percentage", percentage));
    }
    [Fact]
    public async Task AllThirteenRowsAreSavedWithStableKeysAndItalianPercentages()
    {
        await using var factory = new AppFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Settings"); var form = Settings(WebTests.Field(html, "__RequestVerificationToken"));
        for (var i = 13; i >= 1; i--) Add(form, "Rates", $"row-{i * 7}", $"2026-01-{i:00}", "2,5");
        Add(form, "Taxes", "tax-gap-91", "2026-01-01", "26");
        var response = await client.PostAsync("/Settings", new FormUrlEncodedContent(form)); html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Conferma la modifica", html);
        using (var before = factory.Services.CreateScope()) Assert.Null((await before.ServiceProvider.GetRequiredService<IAccountStore>().Load()).Document.Ledger);
        response = await client.PostAsync("/Settings?handler=Confirm", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebTests.Field(html, "__RequestVerificationToken"), ["Ticket"] = WebTests.Field(html, "Ticket") }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope(); var saved = (await scope.ServiceProvider.GetRequiredService<IAccountStore>().Load()).Document.Ledger!;
        Assert.Equal(13, saved.Rates.Count); Assert.All(saved.Rates, rate => Assert.Equal(.025m, rate.Rate));
        Assert.Equal(.26m, saved.Taxes.Single().Rate);
    }
    [Theory]
    [InlineData("abc", "2026-01-02", "abc")]
    [InlineData("", "2026-01-02", "Inserire la percentuale")]
    [InlineData("3", "2026-01-01", "Decorrenza duplicata")]
    public async Task InvalidRowsKeepOtherValuesAndStableKeys(string percentage, string date, string error)
    {
        await using var factory = new AppFactory(); using var client = Client(factory);
        var html = await client.GetStringAsync("/Settings"); var form = Settings(WebTests.Field(html, "__RequestVerificationToken"));
        Add(form, "Rates", "first", "2026-01-01", "2,75"); Add(form, "Rates", "last", date, percentage); Add(form, "Taxes", "tax", "2026-01-01", "26");
        var response = await client.PostAsync("/Settings", new FormUrlEncodedContent(form)); html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.DoesNotContain("Conferma la modifica", html);
        Assert.Contains(error, WebUtility.HtmlDecode(html));
        Assert.Equal("2,75", WebTests.Field(html, "Rates[first].Percentage"));
        Assert.Equal(percentage, WebTests.Field(html, "Rates[last].Percentage"));
        Assert.Contains("field-validation-error", html);
    }
    [Fact]
    public async Task DashboardIncludesMoreThanEightMovementsAndExportStillIncludesAll()
    {
        await using var factory = new AppFactory(); using var client = Client(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var date = new DateOnly(2026, 1, 1);
            var moves = Enumerable.Range(1, 15).Select(i => new Movement(Guid.NewGuid(), date.AddDays(i), MovementKind.Deposit, i, $"Riga-{i:00}")).ToList();
            await scope.ServiceProvider.GetRequiredService<AccountingService>().Commit(new(1, new(date, 0, 0, 0, DayBasis.Actual365, new(), moves, [new(date, .02m)], [new(date, .26m)]), []), 0, new(2026, 10, 22));
        }
        var html = await client.GetStringAsync("/"); Assert.Contains("Riga-01", html); Assert.Contains("Riga-15", html);
        Assert.Contains("data-grid", html); Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
        var csv = await client.GetStringAsync("/Transfer?handler=Export&format=csv"); Assert.Contains("Riga-01", csv); Assert.Contains("Riga-15", csv);
    }
}
