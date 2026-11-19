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

try
{
    builder.Services.AddWeddingDatabase(builder.Configuration, initializeDatabase);
}
catch (InvalidOperationException exception) when (initializeDatabase)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
    return;
}
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

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(ApplicationLanguages.Configure);
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<SiteMusicFilter>();
        options.ModelMetadataDetailsProviders.Add(new LocalizedDisplayMetadataProvider());
        options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) => T("O valor «{0}» não é válido para {1}.", value, T(field)));
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => T("Este campo é obrigatório."));
        options.ModelBindingMessageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => T("Não foi possível interpretar o valor deste campo."));
        options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => T("O valor «{0}» não é válido.", value));
        options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(field => T("O campo {0} deve conter um número.", T(field)));
    })
    .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = (_, _) => new UiTextLocalizer());
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddHealthChecks().AddCheck<ApplicationReadinessHealthCheck>("database", timeout: TimeSpan.FromSeconds(15));

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
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PublicContentCache>();
builder.Services.AddSingleton<CarouselPhotoService>();
builder.Services.AddScoped<PublicContentCacheInvalidationInterceptor>();
builder.Services.ConfigureDbContext<ApplicationDbContext>((services, options) =>
    options.AddInterceptors(services.GetRequiredService<PublicContentCacheInvalidationInterceptor>()));
builder.Services.ConfigureDbContext<PostgreSqlApplicationDbContext>((services, options) =>
    options.AddInterceptors(services.GetRequiredService<PublicContentCacheInvalidationInterceptor>()));
builder.Services.AddScoped<SiteThemeService>();
builder.Services.AddScoped<IRsvpService, RsvpService>();
builder.Services.AddScoped<IRsvpEmailNotificationService, RsvpEmailNotificationService>();
builder.Services.AddScoped<RsvpVerificationService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<FaqService>();
builder.Services.AddScoped<IRsvpVerificationSender, RsvpVerificationSender>();
builder.Services.AddSingleton<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddScoped<IPhotoUploadService, PhotoUploadService>();
builder.Services.AddScoped<ResumablePhotoUploadService>();
builder.Services.AddScoped<ISiteContentService, SiteContentService>();
builder.Services.AddScoped<SiteTranslationService>();
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
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = StaticAssetCache.PrepareResponse });
app.UseRequestLocalization();
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
