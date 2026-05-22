using Asp.Versioning;
using Certiva.AuditLog;
using Certiva.CertificateWallet;
using Certiva.CertificationEngine;
using Certiva.CertificationEngine.Consumers;
using Certiva.IdentityRegistry;
using Certiva.Infrastructure.Authorization;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Idempotency;
using Certiva.Infrastructure.MultiTenancy;
using Certiva.Infrastructure.Persistence.Repositories;
using Certiva.Infrastructure.Observability;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Redis;
using Certiva.IssuerPortal;
using Certiva.NotificationSystem;
using Certiva.NotificationSystem.Consumers;
using Certiva.VerificationEngine;
using Certiva.VerificationEngine.Consumers;
using Certiva.VerificationEngine.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

// ── Startup environment variable validation ──────────────────────────────────
ValidateRequiredConfiguration();

// ── Host builder ─────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console(new CompactJsonFormatter()));

// ── Infrastructure services ──────────────────────────────────────────────────
builder.Services.AddDbContext<CertivaDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddRedisInfrastructure(builder.Configuration);
builder.Services.AddOutboxInfrastructure();
builder.Services.AddMultiTenancy();
builder.Services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
builder.Services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
builder.Services.AddScoped<IIssuerRepository, IssuerRepository>();
builder.Services.AddScoped<ICertificateTemplateRepository, CertificateTemplateRepository>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IVerificationLogRepository, VerificationLogRepository>();
builder.Services.AddScoped<IBulkIssueJobRepository, BulkIssueJobRepository>();
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddCertivaJwtAuthentication(builder.Configuration);
builder.Services.AddCertivaAuthorization();
builder.Services.AddCertivaObservability(builder.Configuration);

// ── MassTransit (RabbitMQ + all consumers) ───────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<QrCodeConsumer, QrCodeConsumerDefinition>();
    x.AddConsumer<PdfConsumer, PdfConsumerDefinition>();
    x.AddConsumer<BulkIssueJobConsumer>();
    x.AddConsumer<VerificationCertificateIssuedConsumer>();
    x.AddConsumer<VerificationCertificateRevokedConsumer>();
    x.AddConsumer<VerificationCertificateExpiredConsumer>();
    x.AddConsumer<CertificateIssuedNotificationConsumer>();
    x.AddConsumer<CertificateRevokedNotificationConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(
            builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        cfg.UseMessageRetry(r =>
            r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1)));

        cfg.Message<ProfessionalRegistered>(m => m.SetEntityName("certiva.professional-registered"));
        cfg.Message<IssuerApproved>(m => m.SetEntityName("certiva.issuer-approved"));
        cfg.Message<IssuerRejected>(m => m.SetEntityName("certiva.issuer-rejected"));
        cfg.Message<CertificateIssued>(m => m.SetEntityName("certiva.certificate-issued"));
        cfg.Message<CertificateRevoked>(m => m.SetEntityName("certiva.certificate-revoked"));
        cfg.Message<CertificateExpired>(m => m.SetEntityName("certiva.certificate-expired"));
        cfg.Message<QrCodeGenerated>(m => m.SetEntityName("certiva.qr-code-generated"));
        cfg.Message<PdfGenerated>(m => m.SetEntityName("certiva.pdf-generated"));
        cfg.Message<BulkIssueJobEnqueued>(m => m.SetEntityName("certiva.bulk-issue-job-enqueued"));

        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Module services ──────────────────────────────────────────────────────────
builder.Services.AddIdentityRegistry();
builder.Services.AddCertificationEngine();
builder.Services.AddVerificationEngine();
builder.Services.AddCertificateWallet();
builder.Services.AddIssuerPortal();
builder.Services.AddNotificationSystem();
builder.Services.AddAuditLog();

// ── Health checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Controllers + API versioning ─────────────────────────────────────────────
builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Build the app ────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ──────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UsePrometheusMetrics();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// ── Health probes (infrastructure — not versioned API routes) ────────────────
app.MapGet("/health/live", async (CertivaDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        return Results.Ok(new { status = "live" });
    }
    catch
    {
        return Results.Problem(statusCode: 503, title: "Database unavailable");
    }
}).AllowAnonymous();

app.MapGet("/health/ready", async (IVerificationStoreRepository verifyStore, CancellationToken ct) =>
{
    var redisOk = await verifyStore.IsAvailableAsync(ct);
    if (!redisOk)
        return Results.Problem(statusCode: 503, title: "Redis not available");
    return Results.Ok(new { status = "ready" });
}).AllowAnonymous();

// ── API version discovery ────────────────────────────────────────────────────
app.MapGet("/api/versions", () => Results.Ok(new
{
    versions = new[]
    {
        new { version = "1.0", status = "current" }
    }
})).AllowAnonymous();

// ── Controllers ──────────────────────────────────────────────────────────────
app.MapControllers();

app.Run();

// ── Startup validation ───────────────────────────────────────────────────────
static void ValidateRequiredConfiguration()
{
    var required = new[]
    {
        "ConnectionStrings__DefaultConnection",
        "Redis__ConnectionString",
        "RabbitMQ__Host",
        "Jwt__SigningKey",
        "Encryption__Key"
    };

    var missing = required.Where(k => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(k))).ToList();

    if (missing.Count > 0)
    {
        foreach (var key in missing)
            Console.Error.WriteLine($"[STARTUP] Required environment variable missing: {key}");

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
        {
            Console.Error.WriteLine("[STARTUP] Aborting: required configuration is missing in Production.");
            Environment.Exit(1);
        }
    }
}
