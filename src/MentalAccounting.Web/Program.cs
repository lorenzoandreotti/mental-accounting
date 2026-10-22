using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using MentalAccounting.Infrastructure;
using MentalAccounting.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var connection = builder.Configuration.GetConnectionString("Accounting")
    ?? "Host=localhost;Database=mental_accounting;Username=mental_accounting";
builder.Services.AddDbContext<AccountingDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsAssembly("MentalAccounting.Infrastructure")));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AccountingDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.Cookie.Name = "MentalAccounting.Auth";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
}).AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddSingleton<InterestCalculator>();
builder.Services.AddScoped<IAccountStore, PostgresAccountStore>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddSingleton<IWorkbookReader, VacationWorkbookReader>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
    builder.Configuration["DataProtection:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "keys")))
    .SetApplicationName("MentalAccounting");
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear(); o.KnownProxies.Clear(); o.ForwardLimit = 1;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(IPAddress.Parse(proxy));
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("login-form"));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 6_000_000);
var app = builder.Build();
if (args.FirstOrDefault() is "migrate" or "create-user" or "reset-password")
{ await AdminCommands.Run(app.Services, args[0]); return; }
var culture = CultureInfo.GetCultureInfo("it-IT");
app.UseRequestLocalization(new RequestLocalizationOptions { DefaultRequestCulture = new(culture), SupportedCultures = [culture], SupportedUICultures = [culture] });
// An empty allowlist would trust every proxy: do not activate forwarding without explicit trusted IPs.
if ((builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? []).Length > 0) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
public partial class Program;
