using ApiInadimplencia.Api.Endpoints;
using ApiInadimplencia.Api.Middleware;
using ApiInadimplencia.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

// Add configuration for IsDevelopment flag
builder.Configuration["IsDevelopment"] = builder.Environment.IsDevelopment().ToString();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "api-inadimplencia",
        Version = "v1",
        Description = "Migracao do modulo inadimplencia para C#/.NET com Clean Architecture + CQRS.",
    });
    
    // Include XML comments for Swagger documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

// SQL Server Health Check (configured via connection string)
var sqlServerConnectionString = builder.Configuration["SqlServer:ConnectionString"];
if (!string.IsNullOrEmpty(sqlServerConnectionString))
{
    builder.Services.AddHealthChecks()
        .AddSqlServer(connectionString: sqlServerConnectionString, name: "sqlserver");
}

// RabbitMQ Health Check (configured via connection string)
var rabbitMQConnectionString = builder.Configuration["RabbitMQ:ConnectionString"];
if (!string.IsNullOrEmpty(rabbitMQConnectionString))
{
    builder.Services.AddHealthChecks()
        .AddRabbitMQ(rabbitMQConnectionString, name: "rabbitmq");
}

// External Integration Health Checks (Fluig, RM, Serasa)
var fluigBaseUrl = builder.Configuration["Fluig:BaseUrl"];
if (!string.IsNullOrEmpty(fluigBaseUrl))
{
    builder.Services.AddHealthChecks()
        .AddUrlGroup(new Uri($"{fluigBaseUrl}/j_security_check"), name: "fluig");
}

var rmBaseUrl = builder.Configuration["Rm:BaseUrl"];
if (!string.IsNullOrEmpty(rmBaseUrl))
{
    builder.Services.AddHealthChecks()
        .AddUrlGroup(new Uri($"{rmBaseUrl}/dataset-handle/search"), name: "rm");
}

var serasaBaseUrl = builder.Configuration["SerasaPefin:BaseUrl"];
if (!string.IsNullOrEmpty(serasaBaseUrl))
{
    builder.Services.AddHealthChecks()
        .AddUrlGroup(new Uri($"{serasaBaseUrl}/oauth/token"), name: "serasa");
}

builder.Services.AddInfrastructure(builder.Configuration);

// OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource
            .AddService("api-inadimplencia")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("user.id", request.HttpContext.User?.Identity?.Name);
                };
            })
            .AddSource("MassTransit")
            .AddSource("ApiInadimplencia")
            .AddOtlpExporter(); // Export to OTLP endpoint (Jaeger/Zipkin/Tempo)
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter("MassTransit")
            .AddMeter("ApiInadimplencia")
            .AddPrometheusExporter(); // Export to Prometheus
    });

// Configure logging levels based on environment
builder.Services.AddLogging(logging =>
{
    logging.AddSerilog(dispose: true);
    
    if (builder.Environment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
    else
    {
        logging.SetMinimumLevel(LogLevel.Information);
    }
});

var app = builder.Build();

// Add Sensitive Data Masking Middleware to pipeline (early in pipeline)
app.UseMiddleware<SensitiveDataMaskingMiddleware>();

app.UseExceptionHandler();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// Map health check endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Map Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint("/metrics");

app.MapInadimplenciaEndpoints();

try
{
    Log.Information("Starting api-inadimplencia");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
