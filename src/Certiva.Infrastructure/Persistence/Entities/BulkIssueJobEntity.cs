namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the BulkIssueJobs table.
/// Tracks the lifecycle and progress of a bulk certificate issuance job.
/// </summary>
public class BulkIssueJobEntity
{
    public Guid JobId { get; set; }
    public Guid TenantId { get; set; }
    public Guid IssuerId { get; set; }
    public Guid TemplateId { get; set; }

    /// <summary>One of: Queued, Processing, Completed, Failed. Defaults to Queued.</summary>
    public string Status { get; set; } = "Queued";

    /// <summary>Total number of Professional entries in the batch.</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of entries processed so far (success + failure). Defaults to 0.</summary>
    public int ProcessedCount { get; set; } = 0;

    /// <summary>Number of successfully issued certificates. Defaults to 0.</summary>
    public int SuccessCount { get; set; } = 0;

    /// <summary>Number of failed entries. Defaults to 0.</summary>
    public int FailureCount { get; set; } = 0;

    /// <summary>
    /// JSONB result report containing success count, failure count, and per-entry failure details.
    /// Null until the job reaches Completed or Failed status.
    /// </summary>
    public string? ResultReport { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>UTC timestamp when the job reached Completed or Failed status. Null while in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
