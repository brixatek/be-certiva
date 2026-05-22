namespace Certiva.IssuerPortal.Services;

public sealed record CreateTemplateCommand
{
    public Guid IssuerId { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ValidityPeriodDays { get; init; }
}

public sealed record UpdateTemplateCommand
{
    public Guid TemplateId { get; init; }
    public Guid IssuerId { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ValidityPeriodDays { get; init; }
}

public sealed record EnqueueBulkIssueCommand
{
    public Guid IssuerId { get; init; }
    public Guid TemplateId { get; init; }
    public Guid TenantId { get; init; }
    public List<Guid> ProfessionalIds { get; init; } = [];
    public string? IdempotencyKey { get; init; }
}

public sealed record BulkJobStatusResult
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public int ProcessedCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}

public sealed record IssuerAnalyticsResult
{
    public int TotalActive { get; init; }
    public int TotalExpired { get; init; }
    public int TotalRevoked { get; init; }
    public IReadOnlyList<MonthlyIssuancePoint> MonthlyIssuance { get; init; } = [];
}

public sealed record MonthlyIssuancePoint
{
    public int Year { get; init; }
    public int Month { get; init; }
    public int Count { get; init; }
}

public sealed record ProfessionalSearchResult
{
    public Guid ProfessionalId { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid CertificateId { get; init; }
    public string CertificateName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
}
