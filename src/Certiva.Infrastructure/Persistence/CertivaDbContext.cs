using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Resources;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Certiva platform.
/// Covers all 10 entity tables defined in the InitialCreate migration.
/// </summary>
public class CertivaDbContext : DbContext
{
    public CertivaDbContext(DbContextOptions<CertivaDbContext> options) : base(options) { }

    // -----------------------------------------------------------------------
    // DbSets
    // -----------------------------------------------------------------------
    public DbSet<ProfessionalEntity> Professionals => Set<ProfessionalEntity>();
    public DbSet<IssuerEntity> Issuers => Set<IssuerEntity>();
    public DbSet<CertificateTemplateEntity> CertificateTemplates => Set<CertificateTemplateEntity>();
    public DbSet<CertificateEntity> Certificates => Set<CertificateEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();
    public DbSet<BulkIssueJobEntity> BulkIssueJobs => Set<BulkIssueJobEntity>();
    public DbSet<VerificationLogEntity> VerificationLogs => Set<VerificationLogEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<NotificationLogEntity> NotificationLogs => Set<NotificationLogEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    // -----------------------------------------------------------------------
    // Model configuration
    // -----------------------------------------------------------------------
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureProfessionals(modelBuilder);
        ConfigureIssuers(modelBuilder);
        ConfigureCertificateTemplates(modelBuilder);
        ConfigureCertificates(modelBuilder);
        ConfigureOutboxMessages(modelBuilder);
        ConfigureIdempotencyKeys(modelBuilder);
        ConfigureBulkIssueJobs(modelBuilder);
        ConfigureVerificationLogs(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureNotificationLogs(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
    }

    // -----------------------------------------------------------------------
    // Append-only enforcement for AuditLogs (Requirement 14.2, 21.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if any <see cref="AuditLogEntity"/>
    /// entry is in <see cref="EntityState.Modified"/> or <see cref="EntityState.Deleted"/> state,
    /// enforcing the append-only constraint at the application layer.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfAuditLogMutated();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc cref="SaveChangesAsync(CancellationToken)"/>
    public override int SaveChanges()
    {
        ThrowIfAuditLogMutated();
        return base.SaveChanges();
    }

    /// <inheritdoc cref="SaveChangesAsync(CancellationToken)"/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ThrowIfAuditLogMutated();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc cref="SaveChangesAsync(CancellationToken)"/>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ThrowIfAuditLogMutated();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ThrowIfAuditLogMutated()
    {
        var illegalEntries = ChangeTracker.Entries<AuditLogEntity>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (illegalEntries.Count > 0)
        {
            throw new InvalidOperationException(
                InfrastructureMessages.AuditLogImmutable(illegalEntries.Count));
        }
    }

    // -----------------------------------------------------------------------
    // Per-entity configuration helpers
    // -----------------------------------------------------------------------

    private static void ConfigureProfessionals(ModelBuilder b)
    {
        b.Entity<ProfessionalEntity>(e =>
        {
            e.ToTable("Professionals");
            e.HasKey(x => x.ProfessionalId);

            e.Property(x => x.ProfessionalId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.Name)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.NationalId_Encrypted)
                .HasColumnType("bytea")
                .IsRequired();

            e.Property(x => x.NationalId_Hash)
                .HasColumnType("character varying(64)")
                .HasMaxLength(64)
                .IsRequired();

            e.Property(x => x.Phone_Encrypted)
                .HasColumnType("bytea");

            e.Property(x => x.Email_Encrypted)
                .HasColumnType("bytea");

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.PasswordHash)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            e.Property(x => x.TotpSecret)
                .HasColumnType("character varying(64)")
                .HasMaxLength(64);

            // Unique constraint: one NationalId per tenant
            e.HasAlternateKey("NationalId_Hash", "TenantId")
                .HasName("uq_professional_nationalid_tenant");

            // Index: tenant scoping
            e.HasIndex(x => x.TenantId)
                .HasDatabaseName("idx_professionals_tenant");

            // Navigation: one Professional → many Certificates
            e.HasMany(x => x.Certificates)
                .WithOne(c => c.Professional)
                .HasForeignKey(c => c.ProfessionalId)
                .HasConstraintName("FK_Certificates_Professionals_ProfessionalId")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIssuers(ModelBuilder b)
    {
        b.Entity<IssuerEntity>(e =>
        {
            e.ToTable("Issuers");
            e.HasKey(x => x.IssuerId);

            e.Property(x => x.IssuerId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.OrganizationName)
                .HasColumnType("character varying(200)")
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Type)
                .HasColumnType("character varying(50)")
                .HasMaxLength(50)
                .IsRequired();

            e.Property(x => x.VerificationStatus)
                .HasColumnType("character varying(20)")
                .HasMaxLength(20)
                .HasDefaultValue("Pending")
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Note: uq_issuer_name_tenant is a case-insensitive unique index created via raw SQL
            // in the migration (LOWER(OrganizationName), TenantId). EF cannot express this natively.

            // Navigation: one Issuer → many Templates
            e.HasMany(x => x.Templates)
                .WithOne(t => t.Issuer)
                .HasForeignKey(t => t.IssuerId)
                .HasConstraintName("FK_CertificateTemplates_Issuers_IssuerId")
                .OnDelete(DeleteBehavior.Restrict);

            // Navigation: one Issuer → many Certificates
            e.HasMany(x => x.Certificates)
                .WithOne(c => c.Issuer)
                .HasForeignKey(c => c.IssuerId)
                .HasConstraintName("FK_Certificates_Issuers_IssuerId")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCertificateTemplates(ModelBuilder b)
    {
        b.Entity<CertificateTemplateEntity>(e =>
        {
            e.ToTable("CertificateTemplates");
            e.HasKey(x => x.TemplateId);

            e.Property(x => x.TemplateId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.IssuerId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.Name)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.Description)
                .HasColumnType("character varying(500)")
                .HasMaxLength(500);

            e.Property(x => x.ValidityPeriodDays)
                .HasColumnType("integer")
                .IsRequired();

            e.Property(x => x.IsActive)
                .HasColumnType("boolean")
                .HasDefaultValue(true)
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.UpdatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Check constraint: ValidityPeriodDays >= 0
            e.ToTable(t => t.HasCheckConstraint(
                "CK_CertificateTemplates_ValidityPeriodDays",
                "\"ValidityPeriodDays\" >= 0"));

            // Note: uq_template_name_issuer is a case-insensitive unique index created via raw SQL
            // in the migration (LOWER(Name), IssuerId, TenantId). EF cannot express this natively.

            // Navigation: one Template → many Certificates
            e.HasMany(x => x.Certificates)
                .WithOne(c => c.Template)
                .HasForeignKey(c => c.TemplateId)
                .HasConstraintName("FK_Certificates_CertificateTemplates_TemplateId")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCertificates(ModelBuilder b)
    {
        b.Entity<CertificateEntity>(e =>
        {
            e.ToTable("Certificates");
            e.HasKey(x => x.CertificateId);

            e.Property(x => x.CertificateId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.ProfessionalId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.IssuerId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.TemplateId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.Name)
                .HasColumnType("character varying(200)")
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Status)
                .HasColumnType("character varying(20)")
                .HasMaxLength(20)
                .HasDefaultValue("Active")
                .IsRequired();

            e.Property(x => x.IssueDate)
                .HasColumnType("date")
                .IsRequired();

            e.Property(x => x.ExpiryDate)
                .HasColumnType("date");

            e.Property(x => x.CertificateHash)
                .HasColumnType("character varying(64)")
                .HasMaxLength(64)
                .IsRequired();

            e.Property(x => x.QRCodeUrl)
                .HasColumnType("character varying(500)")
                .HasMaxLength(500);

            e.Property(x => x.QRCodeBase64)
                .HasColumnType("text");

            e.Property(x => x.PdfStoragePath)
                .HasColumnType("character varying(500)")
                .HasMaxLength(500);

            e.Property(x => x.RevocationReason)
                .HasColumnType("character varying(500)")
                .HasMaxLength(500);

            e.Property(x => x.RevokedAt)
                .HasColumnType("timestamptz");

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.UpdatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Note: uq_active_cert_professional_template is a DEFERRABLE INITIALLY DEFERRED
            // unique constraint added via raw SQL in the migration. EF Core does not support
            // DEFERRABLE constraints natively, so it is not expressed here.

            // Indexes
            e.HasIndex(x => x.TenantId)
                .HasDatabaseName("idx_certificates_tenant");

            e.HasIndex(x => new { x.ProfessionalId, x.TenantId })
                .HasDatabaseName("idx_certificates_professional");

            e.HasIndex(x => new { x.IssuerId, x.TenantId })
                .HasDatabaseName("idx_certificates_issuer");

            // Note: idx_certificates_expiry is a partial index (WHERE ExpiryDate IS NOT NULL)
            // created via raw SQL in the migration. EF Core does not support partial indexes
            // natively in this version, so it is not expressed here.
        });
    }

    private static void ConfigureOutboxMessages(ModelBuilder b)
    {
        b.Entity<OutboxMessageEntity>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.MessageId);

            e.Property(x => x.MessageId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.EventType)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.Payload)
                .HasColumnType("jsonb")
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.PublishedAt)
                .HasColumnType("timestamptz");

            e.Property(x => x.Published)
                .HasColumnType("boolean")
                .HasDefaultValue(false)
                .IsRequired();

            // Note: idx_outbox_unpublished is a partial index (WHERE Published = FALSE)
            // created via raw SQL in the migration.
        });
    }

    private static void ConfigureIdempotencyKeys(ModelBuilder b)
    {
        b.Entity<IdempotencyKeyEntity>(e =>
        {
            e.ToTable("IdempotencyKeys");
            e.HasKey(x => x.IdempotencyKey);

            e.Property(x => x.IdempotencyKey)
                .HasColumnType("character varying(256)")
                .HasMaxLength(256)
                .IsRequired();

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.OperationType)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.ResultPayload)
                .HasColumnType("jsonb")
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.ExpiresAt)
                .HasColumnType("timestamptz")
                .IsRequired();

            e.HasIndex(x => x.ExpiresAt)
                .HasDatabaseName("idx_idempotency_expiry");
        });
    }

    private static void ConfigureBulkIssueJobs(ModelBuilder b)
    {
        b.Entity<BulkIssueJobEntity>(e =>
        {
            e.ToTable("BulkIssueJobs");
            e.HasKey(x => x.JobId);

            e.Property(x => x.JobId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.IssuerId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.TemplateId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.Status)
                .HasColumnType("character varying(20)")
                .HasMaxLength(20)
                .HasDefaultValue("Queued")
                .IsRequired();

            e.Property(x => x.TotalCount)
                .HasColumnType("integer")
                .IsRequired();

            e.Property(x => x.ProcessedCount)
                .HasColumnType("integer")
                .HasDefaultValue(0)
                .IsRequired();

            e.Property(x => x.SuccessCount)
                .HasColumnType("integer")
                .HasDefaultValue(0)
                .IsRequired();

            e.Property(x => x.FailureCount)
                .HasColumnType("integer")
                .HasDefaultValue(0)
                .IsRequired();

            e.Property(x => x.ResultReport)
                .HasColumnType("jsonb");

            e.Property(x => x.SubmittedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.CompletedAt)
                .HasColumnType("timestamptz");
        });
    }

    private static void ConfigureVerificationLogs(ModelBuilder b)
    {
        b.Entity<VerificationLogEntity>(e =>
        {
            e.ToTable("VerificationLogs");
            e.HasKey(x => x.LogId);

            e.Property(x => x.LogId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.CertificateId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.RequestingIp)
                .HasColumnType("character varying(45)")
                .HasMaxLength(45)
                .IsRequired();

            e.Property(x => x.StatusReturned)
                .HasColumnType("character varying(20)")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.Timestamp)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.HasIndex(x => new { x.CertificateId, x.TenantId })
                .HasDatabaseName("idx_verificationlogs_cert");
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder b)
    {
        b.Entity<AuditLogEntity>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.AuditId);

            e.Property(x => x.AuditId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.ActionType)
                .HasColumnType("character varying(100)")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.EntityId)
                .HasColumnType("character varying(200)")
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Actor)
                .HasColumnType("character varying(200)")
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Timestamp)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            e.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            e.Property(x => x.RecordHash)
                .HasColumnType("character varying(64)")
                .HasMaxLength(64)
                .IsRequired();

            // Composite index: tenant + timestamp descending (for audit log queries)
            e.HasIndex(x => new { x.TenantId, x.Timestamp })
                .IsDescending(false, true)
                .HasDatabaseName("idx_auditlogs_tenant");

            e.HasIndex(x => new { x.ActionType, x.TenantId })
                .HasDatabaseName("idx_auditlogs_action");

            e.HasIndex(x => new { x.EntityId, x.TenantId })
                .HasDatabaseName("idx_auditlogs_entity");
        });
    }

    private static void ConfigureNotificationLogs(ModelBuilder b)
    {
        b.Entity<NotificationLogEntity>(e =>
        {
            e.ToTable("NotificationLogs");
            e.HasKey(x => x.NotificationId);

            e.Property(x => x.NotificationId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.ProfessionalId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.IdempotencyKey)
                .HasColumnType("character varying(64)")
                .HasMaxLength(64)
                .IsRequired();

            e.Property(x => x.NotificationType)
                .HasColumnType("character varying(50)")
                .HasMaxLength(50)
                .IsRequired();

            e.Property(x => x.EventId)
                .HasColumnType("character varying(200)")
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Status)
                .HasColumnType("character varying(20)")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.DispatchedAt)
                .HasColumnType("timestamptz");

            e.Property(x => x.FailedAt)
                .HasColumnType("timestamptz");

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Unique constraint on IdempotencyKey
            e.HasAlternateKey(x => x.IdempotencyKey)
                .HasName("uq_notificationlogs_idempotencykey");
        });
    }

    private static void ConfigureRefreshTokens(ModelBuilder b)
    {
        b.Entity<RefreshTokenEntity>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.TokenId);

            e.Property(x => x.TokenId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.UserId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.TenantId)
                .HasColumnType("uuid")
                .IsRequired();

            e.Property(x => x.Token)
                .HasColumnType("character varying(128)")
                .HasMaxLength(128)
                .IsRequired();

            e.Property(x => x.ExpiresAt)
                .HasColumnType("timestamptz")
                .IsRequired();

            e.Property(x => x.IsRevoked)
                .HasColumnType("boolean")
                .HasDefaultValue(false)
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Unique index on Token value for fast lookup
            e.HasIndex(x => x.Token)
                .IsUnique()
                .HasDatabaseName("uq_refreshtokens_token");

            // Index for tenant-scoped queries
            e.HasIndex(x => new { x.UserId, x.TenantId })
                .HasDatabaseName("idx_refreshtokens_user_tenant");
        });
    }
}
