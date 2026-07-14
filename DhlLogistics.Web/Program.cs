using System.Security.Claims;
using System.Text;
using DhlLogistics.Shared.Models;
using DhlLogistics.Web.Api;
using DhlLogistics.Web.Components;
using DhlLogistics.Web.Database;
using DhlLogistics.Web.Hub;
using DhlLogistics.Web.Service;
using DhlLogistics.Web.Service.Search;
using DhlLogistics.Web.Service.Search.Providers;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion license registration and JWT key validation run after the host is
// built (see below) so they can use the DI ILogger rather than Console.WriteLine.

// ── Forwarded headers (upstream proxy / load balancer terminates TLS) ───────
// On AWS the ALB (or an IIS HTTPS binding acting as reverse proxy) forwards
// X-Forwarded-Proto/Host. Clearing the KnownNetworks / KnownProxies allowlists is
// required or the forwarded headers are silently dropped — which breaks both the
// Blazor circuit and HTTPS scheme detection behind the proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                              | ForwardedHeaders.XForwardedProto
                              | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Database (Amazon RDS PostgreSQL via Npgsql) ──────────────────────────────
// Provider-agnostic Npgsql — any PostgreSQL endpoint works (RDS, or a self-managed/hosted Postgres).
// Connection string lives in User Secrets in Development:
//   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<instance>.<region>.rds.amazonaws.com;Port=5432;Database=dhllogistics;Username=<db-user>;Password=...;SSL Mode=Require;Trust Server Certificate=true"
// In production, set env var ConnectionStrings__DefaultConnection.
// Single source of options: register the factory (configures Npgsql once) and derive
// the scoped AppDbContext from it. This serves BOTH the interactive user-management
// grids/trees (which call IDbContextFactory<AppDbContext>.CreateDbContext directly) and
// the rest of the app + Identity stores (which inject a scoped AppDbContext). Avoids the
// fragile dual AddDbContext + AddDbContextFactory registration, which could leave the
// scoped context without a connection string.
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(dbConnectionString));
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// ── Navigation menu ──────────────────────────────────────────────────────────
// The sidebar is data-driven from a Menus table. It lives in the main application
// PostgreSQL DB (AppDbContext) — previously a separate local SQL Server store, which
// AWS Elastic Beanstalk could not reach. NavMenu / permission trees open a short-lived
// AppDbContext via the factory registered above.

// ── Identity (cookie auth for Blazor) ────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
// Keep "Permission" claims OUT of the auth cookie (they're evaluated live from the DB).
// Without this the cookie overflows the HTTP header limit → 431 locally / 502 on the ALB.
.AddClaimsPrincipalFactory<DhlLogistics.Web.Service.AppUserClaimsPrincipalFactory>()
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
    options.LoginPath = "/Account/Login";
    // AccessDenied must NOT point at the login page. An already-authenticated user who is denied would be
    // sent to login, which sees a valid session and sends them straight back — an infinite redirect loop
    // (ERR_TOO_MANY_REDIRECTS on /usermanagement for any non-Admin). Denied means "you are signed in but not
    // allowed", which is exactly what /no-permission says.
    options.AccessDeniedPath = "/no-permission";
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
// Light/Dark theme engine (per-circuit; mirrors the localStorage-driven <html> attr).
builder.Services.AddScoped<DhlLogistics.Web.Service.ThemeService>();
// Shared notifier for live dashboard counters (singleton — fires across all circuits).
builder.Services.AddSingleton<DhlLogistics.Web.Service.DashboardState>();
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

// ── Workflow Engine (centralized Add/Edit/Delete pipeline) ───────────────────
// Orchestrator + reusable steps + per-module handlers. Every module's Application Service
// builds a WorkflowContext and calls WorkflowOrchestrator.RunAsync instead of saving inline.
builder.Services.AddScoped<DhlLogistics.Web.Workflow.WorkflowOrchestrator>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.ValidateStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.GenerateNumberStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.PersistEntityStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.GenerateBillingStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.TimelineStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.ActivityLogStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.AuditLogStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.DashboardRefreshStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.SearchIndexStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Steps.NotificationStep>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Handlers.JobOrderWorkflowHandler>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Handlers.BillWorkflowHandler>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Handlers.AwbWorkflowHandler>();
builder.Services.AddScoped<DhlLogistics.Web.Workflow.Handlers.ExportWorkflowHandler>();

// ── M4 Job Order service ─────────────────────────────────────────────────────
builder.Services.AddScoped<JobOrderService>();

// ── Job Operations foundation (new standalone tracking entity) ────────────────
builder.Services.AddScoped<DhlLogistics.Web.Repository.IJobOperationRepository,
                           DhlLogistics.Web.Repository.JobOperationRepository>();
builder.Services.AddScoped<JobOperationService>();

builder.Services.AddScoped<DhlLogistics.Web.Repository.ICompanyDetailsRepository,
                           DhlLogistics.Web.Repository.CompanyDetailsRepository>();
builder.Services.AddScoped<CompanyDetailsService>();

// ── M4 Billing + Accounts services ───────────────────────────────────────────
builder.Services.AddScoped<BillService>();
// Maps any shipment type (AWB / Export / JobOrder) into a prepared Transportation bill that then flows
// through the same BillService workflow + accounting. No separate billing module.
builder.Services.AddScoped<TransportationBillService>();

// ── Universal Smart Search — orchestrator + one provider per searchable module ───────────────
// A NEW searchable module only needs another `AddScoped<ISearchProvider, XxxSearchProvider>()` line here;
// nothing else (search bar, orchestrator, permissions, audit) changes.
builder.Services.AddScoped<GlobalSearchService>();
builder.Services.AddScoped<ISearchProvider, JobOrderSearchProvider>();
builder.Services.AddScoped<ISearchProvider, AwbSearchProvider>();
builder.Services.AddScoped<ISearchProvider, ExportJobSearchProvider>();
builder.Services.AddScoped<ISearchProvider, BillSearchProvider>();
builder.Services.AddScoped<ISearchProvider, TransportationBillSearchProvider>();
builder.Services.AddScoped<ISearchProvider, CustomerInvoiceSearchProvider>();
builder.Services.AddScoped<ISearchProvider, DocumentSearchProvider>();
builder.Services.AddScoped<ISearchProvider, VoucherSearchProvider>();
builder.Services.AddScoped<ISearchProvider, ClientSearchProvider>();
builder.Services.AddScoped<ISearchProvider, TransporterSearchProvider>();
builder.Services.AddScoped<ISearchProvider, VehicleSearchProvider>();
builder.Services.AddScoped<ISearchProvider, DriverSearchProvider>();
builder.Services.AddScoped<ISearchProvider, StaffSearchProvider>();
builder.Services.AddScoped<ISearchProvider, CommoditySearchProvider>();
builder.Services.AddScoped<ISearchProvider, PortSearchProvider>();
builder.Services.AddScoped<ISearchProvider, BranchSearchProvider>();
builder.Services.AddScoped<ISearchProvider, ContainerSearchProvider>();
builder.Services.AddScoped<ISearchProvider, AccountHeadSearchProvider>();
// Invoice layer over Bill: issue (generate customer-invoice PDF) + upload vendor/credit/debit docs.
builder.Services.AddScoped<InvoiceService>();
// Billing Group = the documents sharing one CustomerInvoiceNumber (virtual — nothing is stored to form it).
// A future module joins a group by registering an IBillingGroupContributor here; BillingGroupService itself
// does not change.
builder.Services.AddScoped<BillingGroupService>();
builder.Services.AddScoped<CustomerInvoiceService>();
// Advisory duplicate-billing detection (compares operational-record IDs, never bill numbers). Never blocks
// generation and never modifies a bill — the user's selection stays authoritative.
builder.Services.AddScoped<DuplicateBillingService>();
builder.Services.AddScoped<VoucherService>();
builder.Services.AddScoped<AccountHeadService>();
// Automatic accounting engine: bill approval → revenue voucher, job approval → expense/payable,
// customer/vendor payments → receipt/payment. Reuses VoucherService; no manual posting.
builder.Services.AddScoped<AccountingService>();
// Cross-cutting Activity/Audit log + notifications for status transitions that run outside the
// WorkflowOrchestrator (Verify/Approve/Reject/Submit/Close/Post).
builder.Services.AddScoped<WorkflowLogService>();

// ── One-time (repeatable) billing backfill for existing jobs ─────────────────
builder.Services.AddScoped<BillingSyncService>();

// ── Finance reports (read-only over the above) ───────────────────────────────
builder.Services.AddScoped<FinanceReportService>();

// ── Admin Activity Report service ────────────────────────────────────────────
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// ── Syncfusion license registration (Task 2) ──────────────────────────────────
// Read from configuration. Reading the key "Syncfusion:LicenseKey" automatically
// covers BOTH forms: the appsettings.json key "Syncfusion:LicenseKey" AND the
// environment variable "Syncfusion__LicenseKey" (ASP.NET Core maps __ → :). A
// missing key only downgrades the UI to a trial banner — it must never crash.
var syncfusionKey = app.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
    app.Logger.LogInformation("Syncfusion license registered.");
}
else
{
    app.Logger.LogWarning("Syncfusion license key missing. Components will render with a trial banner.");
}

// ── JWT signing key validation (Task 3) ───────────────────────────────────────
// The repo ships a placeholder key; production MUST override it via the Jwt__Key
// environment variable. A short or placeholder key lets anyone forge bearer tokens
// and impersonate any role. Logged as Critical (never thrown) so a misconfigured
// deploy still boots and the warning is visible in the IIS stdout / EB logs.
var jwtKey = app.Configuration["Jwt:Key"];
var jwtKeyIsPlaceholder = !string.IsNullOrEmpty(jwtKey)
    && jwtKey.StartsWith("CHANGE-THIS", StringComparison.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 || jwtKeyIsPlaceholder)
{
    app.Logger.LogCritical(
        "JWT signing key is missing, shorter than 32 characters, or still the committed placeholder. " +
        "Mobile/API tokens are INSECURE until Jwt__Key is set to a strong secret. The application will continue running.");
}

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
    // Seed admin credentials come from configuration, not source. Override in
    // production via env vars Seed__AdminEmail / Seed__AdminPassword (e.g. from
    // Secrets Manager) and change the password after first login.
    var adminEmail    = builder.Configuration["Seed:AdminEmail"]    ?? "admin@dhl.com";
    var adminPassword = builder.Configuration["Seed:AdminPassword"] ?? "Admin@1234";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new AppUser
        {
            UserName       = adminEmail,
            Email          = adminEmail,
            FullName       = "Administrator",
            Role           = "Admin",
            IsActive       = true,
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Ensure a RegisterdUser profile exists for the admin (CBM user-management parity).
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Database.AppDbContext>();
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is not null && !await db.RegisterdUsers.AnyAsync(r => r.AspNetUserId == admin.Id))
        {
            db.RegisterdUsers.Add(new DhlLogistics.Shared.Models.RegisterdUser
            {
                AspNetUserId = admin.Id,
                UserName     = admin.UserName,
                Email        = admin.Email,
                FullName     = admin.FullName,
            });
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Admin RegisterdUser Seed] skipped: {ex.Message}");
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

    // Chart of Accounts seed (idempotent — inserts any standard head missing by code).
    // Runs before the billing backfill so auto-posting has heads to resolve.
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Database.AppDbContext>();
        await DhlLogistics.Web.Database.AccountSeed.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Account Seed] skipped: {ex.Message}");
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

    // Navigation menu seed (Postgres, idempotent — schema from migration, seed inserts
    // only when the Menus table is empty).
    try
    {
        var menuFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<DhlLogistics.Web.Database.AppDbContext>>();
        await DhlLogistics.Web.Database.MenuSeed.SeedAsync(menuFactory);
        // Repoint legacy Users/Roles menu entries to the new /usermanagement page.
        await DhlLogistics.Web.Database.MenuSeed.FixupAsync(menuFactory);
        // Additively insert the ERP finance-report + payments menu leaves on already-seeded installs.
        await DhlLogistics.Web.Database.MenuSeed.EnsureFinanceMenusAsync(menuFactory);
        // Additively insert the Operations Dashboard leaf on already-seeded installs.
        await DhlLogistics.Web.Database.MenuSeed.EnsureOperationsMenusAsync(menuFactory);
        // Additively insert the Masters → Company Details leaf on already-seeded installs.
        await DhlLogistics.Web.Database.MenuSeed.EnsureCompanyDetailsMenuAsync(menuFactory);
        await DhlLogistics.Web.Database.MenuSeed.EnsureCustomerInvoiceMenuAsync(menuFactory);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Menu Seed] skipped: {ex.Message}");
    }

    // Billing backfill (idempotent — create-only): existing Clearance/Forwarding jobs get their
    // 1:1 bill on first boot, so Billing pages show synchronized records immediately. Jobs that
    // already have a bill are skipped, so repeated boots don't churn. A full header re-sync is
    // available on demand from the Billing Sync admin page.
    try
    {
        var sync = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Service.BillingSyncService>();
        var result = await sync.SyncAllAsync(updateExisting: false);
        Console.WriteLine($"[Billing Backfill] {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Billing Backfill] skipped: {ex.Message}");
    }

    // Invoice-document backfill (idempotent): copies any legacy on-disk invoice PDFs into the DB
    // Content column so they're served from the shared DB on every environment. Only affects rows
    // with null content whose file is readable on THIS host (e.g. dev), so prod boots are a no-op.
    try
    {
        var inv = scope.ServiceProvider.GetRequiredService<DhlLogistics.Web.Service.InvoiceService>();
        var migrated = await inv.BackfillContentFromDiskAsync();
        if (migrated > 0) Console.WriteLine($"[Invoice Backfill] migrated {migrated} document(s) to DB storage.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Invoice Backfill] skipped: {ex.Message}");
    }
}

// ── Middleware ────────────────────────────────────────────────────────────────
// Forwarded headers MUST come before everything else so subsequent middleware
// (auth, antiforgery, Blazor SignalR hub) see the real public scheme/host.
app.UseForwardedHeaders();

// Explicit WebSockets — Blazor Server's circuit relies on this. Auto-wired by
// MapRazorComponents but enabling explicitly avoids any race with hub mapping.
app.UseWebSockets();

// ── HTTPS enforcement (Task 1) ────────────────────────────────────────────────
// HSTS in every non-Development environment. The Strict-Transport-Security header
// is ignored by browsers when received over plain HTTP, so emitting it is safe even
// while the environment is still HTTP-only — it simply activates once TLS is live.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Config-driven HTTPS redirect. Default false (appsettings.Production.json) so the
// current HTTP-only environment keeps working; enabling it before an HTTPS listener
// (ALB+ACM or IIS binding) exists would 307-loop to an unreachable https:// URL.
// Set the EB env var Security__RequireHttps=true the moment TLS is in front.
var requireHttps = app.Configuration.GetValue<bool>("Security:RequireHttps");
if (requireHttps)
{
    app.UseHttpsRedirection();
}

// Serve wwwroot/* + _framework/* (Blazor runtime) + _content/* (RCL assets).
// Using UseStaticFiles (the .NET 8 / CBM-compatible pattern) instead of
// MapStaticAssets because MapStaticAssets's fingerprint manifest doesn't
// load reliably in our Docker publish, causing _framework/blazor.web.js
// to 404 and breaking the Blazor circuit.
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();


// ── Diagnostic endpoints (Development only) ─────────────────────────────────
// These expose the on-disk file layout and must NOT be reachable in production
// (information disclosure). Gated to the Development environment so they vanish
// from the deployed app entirely.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/diag", () =>
    {
        var root = AppContext.BaseDirectory;

        return Results.Ok(new
        {
            Root = root,
            FrameworkExists =
                Directory.Exists(Path.Combine(root, "_framework")),
            Files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly)
        });
    });
}

// ── Warm-up / liveness ping ─────────────────────────────────────────────────
// The mobile app calls this anonymously on launch to warm the app before the
// first data-loading screen. Also serves as a lightweight liveness probe for the
// EB / ALB health check (returns instantly once the worker process is up).
app.MapGet("/api/ping", () => Results.Ok(new { ok = true, at = DateTime.UtcNow }))
   .AllowAnonymous();
app.MapGet("/health", () => Results.Ok("OK"));

// ── Invoice / document download (authenticated AND permission-checked) ──────
// Serves generated customer-invoice PDFs and uploaded vendor/credit/debit docs.
// ?dl=true forces an attachment download; otherwise the file opens inline (for print).
//
// SECURITY: RequireAuthorization() alone is NOT enough here. It only proves the caller is *some* logged-in
// user — so any authenticated user could enumerate /invoices/doc/1,2,3… and pull down every customer's tax
// invoice, regardless of role. The Linked Documents panel is permission-scoped, but that scoping is cosmetic
// if the URL it links to is not: the UI can simply be bypassed.
//
// The document is therefore resolved to the bill it belongs to, and the caller must hold View on the page
// that bill lives on. An unauthorised caller gets 404, not 403 — a 403 would confirm the document exists and
// still leak which ids are real.
app.MapGet("/invoices/doc/{id:long}", async (
        long id, bool? dl,
        InvoiceService invoices,
        DhlLogistics.Web.Service.PermissionService perms,
        IDbContextFactory<AppDbContext> dbf,
        ClaimsPrincipal user) =>
{
    await using var db = await dbf.CreateDbContextAsync();

    // Which billing page does this document belong to?
    var owner = await db.InvoiceDocuments.AsNoTracking()
        .Where(d => d.Id == id)
        .Select(d => new { Mode = (BillMode?)d.Bill!.Mode, d.CustomerInvoiceId })
        .FirstOrDefaultAsync();

    if (owner is null) return Results.NotFound();

    var page = owner.CustomerInvoiceId is not null
        ? "bills/customer-invoices"
        : owner.Mode switch
        {
            BillMode.Forwarding     => "bills/forwarding",
            BillMode.Transportation => "bills/transportation",
            _                       => "bills/clearance",
        };

    if (!await perms.CheckPermissionAsync(user, page, Permission.View))
        return Results.NotFound();          // deliberately not 403 — do not confirm the id exists

    var file = await invoices.GetFileAsync(id);
    if (file is null) return Results.NotFound();
    return (dl == true)
        ? Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName)
        : Results.File(file.Value.Bytes, file.Value.ContentType);
}).RequireAuthorization();
// ── File-tree diagnostic (Development only) ─────────────────────────────────
// Lists what files exist on disk so the local publish output can be compared
// against the deployed app. AllowAnonymous + full path disclosure means this must
// never run in production; gated to Development so it is not mapped there at all.
if (app.Environment.IsDevelopment())
{
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
}

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
// Static assets are served by UseStaticFiles (above). .NET 8 has no
// MapStaticAssets endpoint-routing API, so MapRazorComponents is mapped directly.
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
