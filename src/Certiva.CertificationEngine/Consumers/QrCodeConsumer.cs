using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace Certiva.CertificationEngine.Consumers;

/// <summary>
/// Subscribes to <see cref="CertificateIssued"/> and generates a QR code PNG (≥200×200 px)
/// encoding the public verification URL. Stores URL and base64 PNG on the Certificate record,
/// then publishes <see cref="QrCodeGenerated"/>.
/// </summary>
public sealed class QrCodeConsumer : IConsumer<CertificateIssued>
{
    private readonly CertivaDbContext _db;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<QrCodeConsumer> _logger;
    private readonly string _domain;
    private readonly ICertificateRepository _certificates;

    public QrCodeConsumer(
        CertivaDbContext db,
        IOutboxWriter outboxWriter,
        IConfiguration config,
        ILogger<QrCodeConsumer> logger,
        ICertificateRepository certificates)
    {
        _db = db;
        _outboxWriter = outboxWriter;
        _logger = logger;
        _domain = config["App:Domain"]?.TrimEnd('/') ?? "https://certiva.app";
        _certificates = certificates;
    }

    public async Task Consume(ConsumeContext<CertificateIssued> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var certificate = await _certificates.FindByIdAsync(msg.CertificateId, msg.TenantId, ct);

        if (certificate is null)
        {
            _logger.LogWarning("QrCodeConsumer: Certificate {CertificateId} not found. Skipping.", msg.CertificateId);
            return;
        }

        // Already generated — idempotency guard
        if (!string.IsNullOrEmpty(certificate.QRCodeBase64))
        {
            _logger.LogDebug("QrCodeConsumer: QR code already generated for {CertificateId}.", msg.CertificateId);
            return;
        }

        var verifyUrl = $"{_domain}/verify/{msg.CertificateId}";

        // Generate QR PNG — ECCLevel.M, pixel size 5 → at least 200×200 for typical payloads
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(verifyUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        byte[] png = qrCode.GetGraphic(5); // each module = 5px; 40-module QR ≥ 200px

        var base64Png = Convert.ToBase64String(png);

        // Persist on the Certificate record
        certificate.QRCodeUrl = verifyUrl;
        certificate.QRCodeBase64 = base64Png;
        certificate.UpdatedAt = DateTimeOffset.UtcNow;

        // Publish QrCodeGenerated via outbox (atomic with Certificate update)
        await _outboxWriter.WriteAsync(
            msg.TenantId,
            EventTypes.QrCodeGenerated,
            new QrCodeGenerated
            {
                EventId = Guid.NewGuid(),
                CertificateId = msg.CertificateId,
                TenantId = msg.TenantId,
                QrCodeUrl = verifyUrl,
                QrCodeBase64 = base64Png,
                OccurredAt = DateTimeOffset.UtcNow
            },
            ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "QrCodeConsumer: QR code generated for CertificateId={CertificateId}.",
            msg.CertificateId);
    }
}

/// <summary>
/// Consumer definition that configures the specific retry and dead-letter policy for
/// QR code generation: 3 retries with 5s, 25s, 125s intervals.
/// </summary>
public sealed class QrCodeConsumerDefinition : ConsumerDefinition<QrCodeConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<QrCodeConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(25),
                TimeSpan.FromSeconds(125)));
    }
}
