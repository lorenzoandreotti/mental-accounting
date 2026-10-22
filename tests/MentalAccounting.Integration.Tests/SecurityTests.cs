using System.Net;
using MentalAccounting.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MentalAccounting.Application;
using MentalAccounting.Domain;
namespace MentalAccounting.Integration.Tests;
[Collection("Postgres")]
public sealed class SecurityTests
{
    [PostgresFact] public async Task RealIdentityLoginCookieLogoutAndLockout()
    {
        await using var factory=new IdentityFactory();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AccountingDbContext>();await db.Database.MigrateAsync();
            var store=scope.ServiceProvider.GetRequiredService<IAccountStore>();var state=await store.Load();
            if(state.Document.Ledger is null)
            { var date=new DateOnly(2026,1,1);await store.Save(new(1,new(date,1000,0,0,DayBasis.Actual365,new(),[],[new(date,.02m)],[new(date,.26m)]),[]),state.Version); }
            var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var existing=await users.FindByNameAsync("identity-test");if(existing is not null)await users.DeleteAsync(existing);
            var result=await users.CreateAsync(new IdentityUser("identity-test"),"Test-Only-Password-2026!");Assert.True(result.Succeeded);
        }
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("https://localhost")});
        var login=await client.GetStringAsync("/Account/Login");
        var response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{
            {"Username","identity-test"},{"Password","Test-Only-Password-2026!"},{"__RequestVerificationToken",WebTests.Field(login,"__RequestVerificationToken")}}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var cookie=response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("MentalAccounting.Auth="));
        Assert.Contains("secure",cookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("httponly",cookie,StringComparison.OrdinalIgnoreCase);
        var home=await client.GetStringAsync("/");Assert.Contains("Saldo disponibile",home);
        response=await client.PostAsync("/Account/Logout",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",WebTests.Field(home,"__RequestVerificationToken")}}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/")).StatusCode);
        for(var i=0;i<5;i++)
        {
            login=await client.GetStringAsync("/Account/Login");
            await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Username","identity-test"},{"Password","wrong"},{"__RequestVerificationToken",WebTests.Field(login,"__RequestVerificationToken")}}));
        }
        using var checkScope=factory.Services.CreateScope();var manager=checkScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await manager.IsLockedOutAsync((await manager.FindByNameAsync("identity-test"))!));
    }

    [Fact] public async Task UntrustedForwardedHeaderDoesNotBypassHttps()
    {
        await using var factory=new AppFactory();using var client=factory.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new("http://localhost")});
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto","https");
        var result=await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.TemporaryRedirect,result.StatusCode);Assert.Equal("https",result.Headers.Location!.Scheme);
    }
    [Fact] public async Task OnlyAllowlistedProxyCanForwardHttps()
    {
        await using var factory=new AppFactory(true);_=factory.CreateClient();
        var trusted=await factory.Server.SendAsync(context=>
        {
            context.Connection.RemoteIpAddress=IPAddress.Loopback;context.Request.Scheme="http";context.Request.Host=new("localhost");
            context.Request.Path="/Account/Login";context.Request.Headers["X-Forwarded-Proto"]="https";
        });
        Assert.Equal(200,trusted.Response.StatusCode);Assert.Equal("https",trusted.Request.Scheme);
        var untrusted=await factory.Server.SendAsync(context=>
        {
            context.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.1");context.Request.Scheme="http";context.Request.Host=new("localhost");
            context.Request.Path="/Account/Login";context.Request.Headers["X-Forwarded-Proto"]="https";
        });
        Assert.Equal(307,untrusted.Response.StatusCode);
    }
}
public sealed class IdentityFactory:WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection=Environment.GetEnvironmentVariable("TEST_POSTGRES")!;
        if(!new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.EndsWith("_test",StringComparison.Ordinal))throw new InvalidOperationException("Database di test richiesto.");
        builder.UseEnvironment("Development");builder.ConfigureLogging(o=>o.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Accounting",connection}}));
        builder.ConfigureServices(services=>
        {
            services.RemoveAll<DbContextOptions<AccountingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AccountingDbContext>>();
            services.AddDbContext<AccountingDbContext>(o=>o.UseNpgsql(connection));
            services.RemoveAll<IDataProtectionProvider>();services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}
