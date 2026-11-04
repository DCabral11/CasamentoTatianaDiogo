using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

var initializeDatabase = args.Contains("--initialize-database");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--initialize-database").ToArray());

// A instalação local pode guardar os segredos fora do repositório e da pasta publicada.
var localSettingsPath = Environment.GetEnvironmentVariable("CASAMENTO_SETTINGS_PATH");
if (!string.IsNullOrWhiteSpace(localSettingsPath))
{
    builder.Configuration.AddJsonFile(localSettingsPath, optional: false, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddWeddingDatabase(builder.Configuration, initializeDatabase);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

var isVercel = builder.Configuration["VERCEL"] == "1" || builder.Configuration.GetValue<bool>("Hosting:Vercel");
var protection = builder.Services.AddDataProtection().SetApplicationName("CasamentoTatianaDiogo");
var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    dataProtectionKeysPath = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath, "data", "protection-keys");
}

if (!isVercel && !string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    protection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
else
    protection.PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Vercel's managed ingress is the only public entry point to the container.
    if (isVercel)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WeddingClock>();

builder.Services.AddScoped<IWeddingSettingsService, WeddingSettingsService>();
builder.Services.AddScoped<IRsvpService, RsvpService>();
builder.Services.AddScoped<IRsvpEmailNotificationService, RsvpEmailNotificationService>();
builder.Services.AddSingleton<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddScoped<IPhotoUploadService, PhotoUploadService>();
builder.Services.AddScoped<ResumablePhotoUploadService>();
builder.Services.AddScoped<ISiteContentService, SiteContentService>();
builder.Services.AddSingleton<IAppMessageService, AppMessageService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

if (initializeDatabase || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    // Local SQL Server keeps its development bootstrap; PostgreSQL setup always uses the explicit command.
    if (initializeDatabase || scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
        await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    if (initializeDatabase)
    {
        app.Logger.LogInformation("Database initialized successfully.");
        return;
    }
}

app.UseForwardedHeaders();
// The same friendly recovery screen is used locally and after publishing.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();

public partial class Program;
