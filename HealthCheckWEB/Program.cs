using DatabaseContext;
using DatabaseContext.Models;
using HealthChecks.UI.Client;
using HealthCheckWEB.HealthCheckers;
using HealthCheckWEB.Models;
using HealthCheckWEB.Models.Azure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", reloadOnChange: true, optional: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

builder.Configuration.AddConfiguration(configBuilder.Build());

var redisConfig = builder.Configuration.GetSection("Redis").Get<RedisConfig>();

// INIT IDENTITY DB
var healthCheckDbConnection = builder.Configuration.GetSection("HealthCheckerDatabaseConnection").Value;
builder.Services.AddDbContext<ApplicationDatabaseContext>(options => options.UseNpgsql(healthCheckDbConnection));
builder.Services.AddIdentity<User, Role>()
    .AddEntityFrameworkStores<ApplicationDatabaseContext>();
//

// Add services to the container.
builder.Services.AddControllersWithViews();

var apiDbConnection = builder.Configuration.GetSection("APIDatabaseConnection").Value;
var elasticConnection = builder.Configuration.GetSection("ElasticConfiguration:Uri").Value;
var healthCheckUIUri = builder.Configuration.GetSection("HealthCheckUIUri").Value;
var storageConfig = builder.Configuration.GetSection("Azure").Get<AzureConfig>();

var workerDbConnection = builder.Configuration.GetSection("WorkerDatabaseConnection").Value;

builder.Services.AddDaprClient();

// HEALTH CHECKER
builder.Services.AddHealthChecks()
                // .AddElasticsearch(elasticConnection)
                .AddRedis($"{redisConfig.Connection},password={redisConfig.Password}")
                .AddNpgSql(apiDbConnection, name: "X Notes Database")
                .AddNpgSql(healthCheckDbConnection, name: "Health check Database")
                .AddNpgSql(workerDbConnection, name: "Worker Database")
                .AddCheck<AzureBlobStorageHealthChecker>("AzureBlobStorageChecker");


builder.Services.AddHealthChecksUI(setup =>
                {
                    setup.SetEvaluationTimeInSeconds(5);
                    setup.AddHealthCheckEndpoint("APPS", healthCheckUIUri);
                })
                .AddInMemoryStorage();

// AZURE STORAGE
builder.Services.AddSingleton(x => storageConfig);
builder.Services.AddAzureClients(builder =>
{
    builder.AddBlobServiceClient(storageConfig.StorageConnection);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// HEALTH CHECK
    // API URL /healthchecks-ui
app.MapHealthChecksUI().RequireAuthorization();

app.MapHealthChecks("/app-health", new HealthCheckOptions
{
    Predicate = registration => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();