using MigrationDashboard.Services;
using MigrationDashboard.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register Options
builder.Services.Configure<OnPremiseAppOptions>(builder.Configuration.GetSection("OnPremiseApp"));

builder.Services.AddScoped<IMigrationService, MigrationService>();
builder.Services.AddScoped<ICloudResourceService, CloudResourceService>();
builder.Services.AddScoped<IPreMigrationAssessmentService, PreMigrationAssessmentService>();
builder.Services.AddScoped<IMigrationPackageService, MigrationPackageService>();
builder.Services.AddScoped<IMigrationReportService, MigrationReportService>();
builder.Services.AddScoped<IRestoreSimulationService, RestoreSimulationService>();
builder.Services.AddScoped<IMonitoringService, MonitoringService>();
builder.Services.AddScoped<ISecurityComplianceService, SecurityComplianceService>();
builder.Services.AddScoped<IDatabaseExportService, DatabaseExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
