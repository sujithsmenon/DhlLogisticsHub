using DhlLogistics.Web.Components;
using DhlLogistics.Web.Components.Pages;
using DhlLogistics.Web.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Blazor + Syncfusion
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY");
builder.Services.AddSyncfusionBlazor();

// SignalR (for GPS)
builder.Services.AddSignalR();

// App services
builder.Services.AddScoped<LogisticsService>();
builder.Services.AddScoped<EmailReaderService>();
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<JobAssignmentService>();
builder.Services.AddHostedService<EmailPollingService>(); // runs every 5 min

var app = builder.Build();

// Seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Manager", "Executive", "Viewer" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<GpsHub>("/gpshub");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

//using DhlLogistics.Web.Components;

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.
//builder.Services.AddRazorComponents()
//    .AddInteractiveServerComponents();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Error", createScopeForErrors: true);
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}
//app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
//app.UseHttpsRedirection();

//app.UseAntiforgery();

//app.MapStaticAssets();
//app.MapRazorComponents<App>()
//    .AddInteractiveServerRenderMode();

//app.Run();
