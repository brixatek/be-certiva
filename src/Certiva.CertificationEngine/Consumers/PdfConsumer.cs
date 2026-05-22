using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Certiva.CertificationEngine.Consumers;

/// <summary>
/// Subscribes to <see cref="QrCodeGenerated"/> and generates a PDF certificate via QuestPDF.
/// The PDF includes: Professional Name, certificate Name, IssueDate, ExpiryDate (or "Does not expire"),
/// IssuerName, and the embedded QR image.
/// Stores the PDF path on the Certificate record and publishes <see cref="PdfGenerated"/>.
/// </summary>
public sealed class PdfConsumer : IConsumer<QrCodeGenerated>
{
    private readonly CertivaDbContext _db;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<PdfConsumer> _logger;
    private readonly string _storageBasePath;
    private readonly ICertificateRepository _certificates;

    public PdfConsumer(
        CertivaDbContext db,
        IOutboxWriter outboxWriter,
        IConfiguration config,
        ILogger<PdfConsumer> logger,
        ICertificateRepository certificates)
    {
        _db = db;
        _outboxWriter = outboxWriter;
        _logger = logger;
        _storageBasePath = config["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "certiva-pdfs");
        _certificates = certificates;
    }

    public async Task Consume(ConsumeContext<QrCodeGenerated> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var certificate = await _certificates.FindByIdWithNavigationsAsync(msg.CertificateId, ct);

        if (certificate is null || certificate.TenantId != msg.TenantId)
        {
            _logger.LogWarning("PdfConsumer: Certificate {CertificateId} not found. Skipping.", msg.CertificateId);
            return;
        }

        // Idempotency guard
        if (!string.IsNullOrEmpty(certificate.PdfStoragePath))
        {
            _logger.LogDebug("PdfConsumer: PDF already generated for {CertificateId}.", msg.CertificateId);
            return;
        }

        var professionalName = certificate.Professional.Name;
        var issuerName = certificate.Issuer.OrganizationName;
        var certName = certificate.Name;
        var issueDate = certificate.IssueDate.ToString("dd MMM yyyy");
        var expiryLabel = certificate.ExpiryDate.HasValue
            ? certificate.ExpiryDate.Value.ToString("dd MMM yyyy")
            : "Does not expire";
        var qrBase64 = msg.QrCodeBase64;

        // Render PDF bytes
        QuestPDF.Settings.License = LicenseType.Community;

        byte[] pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(12));

                page.Content().Column(col =>
                {
                    col.Spacing(16);

                    // Title
                    col.Item().AlignCenter().Text("CERTIFICATE OF COMPLETION")
                        .Bold().FontSize(22);

                    col.Item().AlignCenter().Text(certName)
                        .SemiBold().FontSize(16);

                    col.Item().LineHorizontal(1);

                    // Professional Name
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Awarded to:");
                        row.RelativeItem(2).Text(professionalName).Bold();
                    });

                    // Issuer
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Issued by:");
                        row.RelativeItem(2).Text(issuerName);
                    });

                    // Issue Date
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Issue Date:");
                        row.RelativeItem(2).Text(issueDate);
                    });

                    // Expiry Date
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Valid Until:");
                        row.RelativeItem(2).Text(expiryLabel);
                    });

                    col.Item().LineHorizontal(1);

                    // QR Code
                    if (!string.IsNullOrEmpty(qrBase64))
                    {
                        var qrBytes = Convert.FromBase64String(qrBase64);
                        col.Item().AlignCenter().Width(120).Image(qrBytes);
                        col.Item().AlignCenter().Text("Scan to verify this certificate")
                            .FontSize(9).FontColor("#888888");
                    }

                    col.Item().AlignCenter().Text($"Certificate ID: {msg.CertificateId}")
                        .FontSize(9).FontColor("#888888");
                });
            });
        }).GeneratePdf();

        // Store PDF to disk
        var relativePath = Path.Combine("pdfs", $"{msg.CertificateId}.pdf");
        var fullPath = Path.Combine(_storageBasePath, "pdfs");
        Directory.CreateDirectory(fullPath);
        var filePath = Path.Combine(fullPath, $"{msg.CertificateId}.pdf");
        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);

        // Update Certificate record
        certificate.PdfStoragePath = relativePath;
        certificate.UpdatedAt = DateTimeOffset.UtcNow;

        // Publish PdfGenerated via outbox
        await _outboxWriter.WriteAsync(
            msg.TenantId,
            EventTypes.PdfGenerated,
            new PdfGenerated
            {
                EventId = Guid.NewGuid(),
                CertificateId = msg.CertificateId,
                TenantId = msg.TenantId,
                PdfStoragePath = relativePath,
                OccurredAt = DateTimeOffset.UtcNow
            },
            ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PdfConsumer: PDF generated and stored for CertificateId={CertificateId}.",
            msg.CertificateId);
    }
}

/// <summary>
/// Consumer definition for PDF generation:
/// - Rendering errors: 3 retries at 5s, 25s, 125s → dead-letter on exhaustion.
/// - Infrastructure errors: retry indefinitely (handled by MassTransit's global policy).
/// </summary>
public sealed class PdfConsumerDefinition : ConsumerDefinition<PdfConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PdfConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(25),
                TimeSpan.FromSeconds(125)));
    }
}
