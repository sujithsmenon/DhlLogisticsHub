using System.Text;
using DhlLogistics.Web.Api;
using DhlLogistics.Web.Components;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Hub;
using DhlLogistics.Web.Service;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    "Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXped3RVQmheU0R3V0VWYEo=");

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(
               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// ── Identity (cookie auth for Blazor) ────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT Bearer (for mobile/API) ───────────────────────────────────────────────
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwt["Key"]!)),
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("MobileApi", policy =>
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
              .RequireAuthenticatedUser()));

// ── Razor Pages (login / logout form handlers) ────────────────────────────────
builder.Services.AddRazorPages();

// ── Configure Identity cookie paths ──────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath        = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan   = TimeSpan.FromDays(7);
});

// ── Blazor + Syncfusion ───────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddScoped<Syncfusion.Blazor.Popups.SfDialogService>();
builder.Services.AddScoped<DhlLogistics.Web.Components.Common.Toast.IToastService,
                          DhlLogistics.Web.Components.Common.Toast.ToastService>();
builder.Services.AddScoped<DhlLogistics.Web.Components.Common.Spinner.SpinnerService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<LogisticsService>();
builder.Services.AddScoped<EmailReaderService>();
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<JobAssignmentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AwbShipmentService>();
builder.Services.AddScoped<ExportJobService>();
builder.Services.AddHostedService<EmailPollingService>();

// ── M2 master CRUD services ──────────────────────────────────────────────────
builder.Services.AddScoped<CountryService>();
builder.Services.AddScoped<RegionService>();
builder.Services.AddScoped<StateService>();
builder.Services.AddScoped<PortService>();
builder.Services.AddScoped<SezLocationService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<SacService>();
builder.Services.AddScoped<ChargeCodeService>();
builder.Services.AddScoped<ContainerSizeService>();
builder.Services.AddScoped<CommodityService>();
builder.Services.AddScoped<VesselService>();
builder.Services.AddScoped<VehicleDriverService>();
builder.Services.AddScoped<VehicleDocumentTypeService>();
builder.Services.AddScoped<VehicleDocumentService>();
builder.Services.AddScoped<DriverDocumentTypeService>();
builder.Services.AddScoped<StaffDepartmentService>();
builder.Services.AddScoped<StaffDesignationService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<CompanyBranchService>();
builder.Services.AddScoped<ShipmentActivityService>();

// ── M3 Permission service ────────────────────────────────────────────────────
builder.Services.AddScoped<PermissionService>();

// ── M4 Job Order service ─────────────────────────────────────────────────────
builder.Services.AddScoped<JobOrderService>();

var app = builder.Build();

// ── Firebase Admin SDK (FCM) ──────────────────────────────────────────────────
// Place your firebase-adminsdk.json (downloaded from Firebase console) in the
// project root and set "Firebase:CredentialFile" in appsettings.json.
var firebaseCredFile = builder.Configuration["Firebase:CredentialFile"];
if (!string.IsNullOrEmpty(firebaseCredFile) && File.Exists(firebaseCredFile))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromFile(firebaseCredFile),
    });
}

// ── Seed roles + default admin ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Manager", "Executive", "Viewer" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    const string adminEmail = "admin@dhl.com";
    const string adminPassword = "Admin@1234";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new AppUser
        {
            UserName   = adminEmail,
            Email      = adminEmail,
            FullName   = "Administrator",
            Role       = "Admin",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // M2 master seed data (idempotent — skipped per-entity if rows already exist)
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Database.AppDbContext>();
        await DhlLogistics.Web.Database.M2SeedData.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[M2 Seed] skipped: {ex.Message}");
    }

    // M3 default permission grants (idempotent — skipped if already seeded)
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Database.AppDbContext>();
        await DhlLogistics.Web.Database.PermissionSeed.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Permission Seed] skipped: {ex.Message}");
    }
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ── API endpoints ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapJobEndpoints();
app.MapGpsEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationEndpoints();

// ── SignalR hubs ──────────────────────────────────────────────────────────────
app.MapHub<GpsHub>("/gpshub");
app.MapHub<NotificationHub>("/notificationhub");

// ── Razor Pages (auth) ───────────────────────────────────────────────────────
app.MapRazorPages();

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
