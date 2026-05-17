using System.Text;
using DhlLogistics.Web.Api;
using DhlLogistics.Web.Components;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Hub;
using DhlLogistics.Web.Service;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    "Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXped3RVQmheU0R3V0VWYEo=");

// ── Forwarded headers (Render's load balancer terminates TLS) ───────────────
// MUST be configured via the options pattern so the *internal* allowlists
// (including .NET 10's new KnownIPNetworks) are all cleared — clearing only
// KnownNetworks/KnownProxies in the inline middleware options misses one in
// .NET 10 and the headers get silently ignored, breaking the Blazor circuit.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                              | ForwardedHeaders.XForwardedProto
                              | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Database (Supabase Postgres via Npgsql) ──────────────────────────────────
// Connection string lives in User Secrets in Development:
//   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=6543;Database=postgres;Username=postgres.<projectref>;Password=...;SSL Mode=Require;Trust Server Certificate=true"
// In production, set env var ConnectionStrings__DefaultConnection.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
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
{
    options.AddPolicy("MobileApi", policy =>
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
              .RequireAuthenticatedUser());

    // Mirrors the web dashboard's read-only views to the mobile app.
    // Restricted to Admin/Manager because those screens show every master,
    // job order, AWB and export across the whole business.
    options.AddPolicy("MobileAdminApi", policy =>
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
              .RequireAuthenticatedUser()
              .RequireRole("Admin", "Manager"));
});

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

// ── CORS — open policy (matches CBM AllowAll). Needed so the mobile app
// (Maui) on a different origin can hit the API endpoints; for the Blazor
// dashboard itself it's same-origin so this is essentially a no-op there.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
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

// ── Admin Activity Report service ────────────────────────────────────────────
builder.Services.AddScoped<ReportService>();

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
// Forwarded headers MUST come before everything else so subsequent middleware
// (auth, antiforgery, Blazor SignalR hub) see the real public scheme/host.
app.UseForwardedHeaders();

// Explicit WebSockets — Blazor Server's circuit relies on this. Auto-wired by
// MapRazorComponents but enabling explicitly avoids any race with hub mapping.
app.UseWebSockets();

// Render terminates TLS at the edge and forwards plain HTTP to the container,
// so UseHttpsRedirection() inside the container would loop. Only enable it
// outside containers (i.e. local dev).
if (!app.Environment.IsProduction() || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
    app.UseHttpsRedirection();

// Serve wwwroot/* + _framework/* (Blazor runtime) + _content/* (RCL assets).
// Using UseStaticFiles (the .NET 8 / CBM-compatible pattern) instead of
// MapStaticAssets because MapStaticAssets's fingerprint manifest doesn't
// load reliably in our Docker publish, causing _framework/blazor.web.js
// to 404 and breaking the Blazor circuit.
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ── Diagnostic endpoint (TEMP — remove after deployment is debugged) ────────
// Lists what files actually exist on disk in the deployed container so we can
// compare against the local publish output and isolate why static files 404.
app.MapGet("/_diag/files", () =>
{
    var contentRoot = AppContext.BaseDirectory;
    var webRoot = Path.Combine(contentRoot, "wwwroot");
    string Tree(string dir, int depth = 0, int max = 3)
    {
        if (!Directory.Exists(dir) || depth > max) return "";
        var sb = new System.Text.StringBuilder();
        try
        {
            foreach (var f in Directory.GetFiles(dir).OrderBy(f => f))
                sb.AppendLine(new string(' ', depth * 2) + Path.GetFileName(f));
            foreach (var d in Directory.GetDirectories(dir).OrderBy(d => d))
            {
                sb.AppendLine(new string(' ', depth * 2) + "[" + Path.GetFileName(d) + "/]");
                sb.Append(Tree(d, depth + 1, max));
            }
        }
        catch (Exception ex) { sb.AppendLine($"<error: {ex.Message}>"); }
        return sb.ToString();
    }
    var report = new System.Text.StringBuilder();
    report.AppendLine($"ContentRoot:   {contentRoot}");
    report.AppendLine($"WebRootPath:   {webRoot} (exists: {Directory.Exists(webRoot)})");
    report.AppendLine($"CWD:           {Directory.GetCurrentDirectory()}");
    report.AppendLine();
    report.AppendLine("=== Content root ===");
    report.Append(Tree(contentRoot, 0, 1));
    report.AppendLine();
    report.AppendLine("=== wwwroot tree (depth 3) ===");
    report.Append(Tree(webRoot, 0, 3));
    return Results.Text(report.ToString(), "text/plain");
}).AllowAnonymous();

// ── API endpoints ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapJobEndpoints();
app.MapGpsEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationEndpoints();

// Mirror the web dashboard to the mobile app (Admin/Manager only).
app.MapMasterEndpoints();
app.MapJobOrderEndpoints();
app.MapShipmentEndpoints();
app.MapAdminEndpoints();
app.MapReportEndpoints();

// ── SignalR hubs ──────────────────────────────────────────────────────────────
app.MapHub<GpsHub>("/gpshub");
app.MapHub<NotificationHub>("/notificationhub");

// ── Razor Pages (auth) ───────────────────────────────────────────────────────
app.MapRazorPages();

// ── Blazor ────────────────────────────────────────────────────────────────────
// MapStaticAssets registers the endpoint-based static asset routes that
// MapRazorComponents/AddInteractiveServerRenderMode depends on in .NET 10.
// UseStaticFiles (above) is the middleware fallback. Having both is safe.
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
