using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certiva.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // Professionals
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Professionals",
                columns: table => new
                {
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NationalId_Encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    NationalId_Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Phone_Encrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    Email_Encrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professionals", x => x.ProfessionalId);
                    table.UniqueConstraint("uq_professional_nationalid_tenant", x => new { x.NationalId_Hash, x.TenantId });
                });

            migrationBuilder.CreateIndex(
                name: "idx_professionals_tenant",
                table: "Professionals",
                column: "TenantId");

            // ---------------------------------------------------------------
            // Issuers
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Issuers",
                columns: table => new
                {
                    IssuerId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issuers", x => x.IssuerId);
                });

            // Case-insensitive unique constraint on OrganizationName per tenant
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_issuer_name_tenant ON \"Issuers\" (LOWER(\"OrganizationName\"), \"TenantId\");");

            // ---------------------------------------------------------------
            // CertificateTemplates
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "CertificateTemplates",
                columns: table => new
                {
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValidityPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateTemplates", x => x.TemplateId);
                    table.CheckConstraint("CK_CertificateTemplates_ValidityPeriodDays", "\"ValidityPeriodDays\" >= 0");
                    table.ForeignKey(
                        name: "FK_CertificateTemplates_Issuers_IssuerId",
                        column: x => x.IssuerId,
                        principalTable: "Issuers",
                        principalColumn: "IssuerId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Case-insensitive unique constraint on template Name per issuer+tenant
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_template_name_issuer ON \"CertificateTemplates\" (LOWER(\"Name\"), \"IssuerId\", \"TenantId\");");

            // ---------------------------------------------------------------
            // Certificates
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QRCodeUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QRCodeBase64 = table.Column<string>(type: "text", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.CertificateId);
                    table.ForeignKey(
                        name: "FK_Certificates_Professionals_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Professionals",
                        principalColumn: "ProfessionalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Certificates_Issuers_IssuerId",
                        column: x => x.IssuerId,
                        principalTable: "Issuers",
                        principalColumn: "IssuerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Certificates_CertificateTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "CertificateTemplates",
                        principalColumn: "TemplateId",
                        onDelete: ReferentialAction.Restrict);
                });

            // DEFERRABLE INITIALLY DEFERRED unique constraint for active cert per professional+template
            // EF Core does not support DEFERRABLE constraints natively, so we use raw SQL.
            migrationBuilder.Sql(
                "ALTER TABLE \"Certificates\" ADD CONSTRAINT uq_active_cert_professional_template " +
                "UNIQUE (\"ProfessionalId\", \"TemplateId\", \"TenantId\", \"Status\") DEFERRABLE INITIALLY DEFERRED;");

            migrationBuilder.CreateIndex(
                name: "idx_certificates_tenant",
                table: "Certificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "idx_certificates_professional",
                table: "Certificates",
                columns: new[] { "ProfessionalId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "idx_certificates_issuer",
                table: "Certificates",
                columns: new[] { "IssuerId", "TenantId" });

            // Partial index: only rows where ExpiryDate IS NOT NULL
            migrationBuilder.Sql(
                "CREATE INDEX idx_certificates_expiry ON \"Certificates\" (\"ExpiryDate\", \"Status\") " +
                "WHERE \"ExpiryDate\" IS NOT NULL;");

            // ---------------------------------------------------------------
            // OutboxMessages
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.MessageId);
                });

            // Partial index: only unpublished messages
            migrationBuilder.Sql(
                "CREATE INDEX idx_outbox_unpublished ON \"OutboxMessages\" (\"Published\", \"CreatedAt\") " +
                "WHERE \"Published\" = FALSE;");

            // ---------------------------------------------------------------
            // IdempotencyKeys
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResultPayload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.IdempotencyKey);
                });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expiry",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            // ---------------------------------------------------------------
            // BulkIssueJobs
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "BulkIssueJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailureCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ResultReport = table.Column<string>(type: "jsonb", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkIssueJobs", x => x.JobId);
                });

            // ---------------------------------------------------------------
            // VerificationLogs
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "VerificationLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestingIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    StatusReturned = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationLogs", x => x.LogId);
                });

            migrationBuilder.CreateIndex(
                name: "idx_verificationlogs_cert",
                table: "VerificationLogs",
                columns: new[] { "CertificateId", "TenantId" });

            // ---------------------------------------------------------------
            // AuditLogs
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    RecordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditId);
                });

            migrationBuilder.CreateIndex(
                name: "idx_auditlogs_tenant",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_auditlogs_action",
                table: "AuditLogs",
                columns: new[] { "ActionType", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "idx_auditlogs_entity",
                table: "AuditLogs",
                columns: new[] { "EntityId", "TenantId" });

            // ---------------------------------------------------------------
            // AuditLogs — defense-in-depth: PostgreSQL RULE to reject UPDATE/DELETE
            // Requirement 14.2: The Audit_Log SHALL be append-only; no record SHALL
            // be updated or deleted after creation.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
CREATE OR REPLACE RULE audit_logs_no_update AS
    ON UPDATE TO ""AuditLogs""
    DO INSTEAD NOTHING;

CREATE OR REPLACE RULE audit_logs_no_delete AS
    ON DELETE TO ""AuditLogs""
    DO INSTEAD NOTHING;
");

            // ---------------------------------------------------------------
            // NotificationLogs
            // ---------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.NotificationId);
                    table.UniqueConstraint("uq_notificationlogs_idempotencykey", x => x.IdempotencyKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop AuditLogs rules first (before dropping the table)
            migrationBuilder.Sql(@"
DROP RULE IF EXISTS audit_logs_no_update ON ""AuditLogs"";
DROP RULE IF EXISTS audit_logs_no_delete ON ""AuditLogs"";
");

            migrationBuilder.DropTable(name: "NotificationLogs");
            migrationBuilder.DropTable(name: "AuditLogs");
            migrationBuilder.DropTable(name: "VerificationLogs");
            migrationBuilder.DropTable(name: "BulkIssueJobs");
            migrationBuilder.DropTable(name: "IdempotencyKeys");
            migrationBuilder.DropTable(name: "OutboxMessages");
            migrationBuilder.DropTable(name: "Certificates");
            migrationBuilder.DropTable(name: "CertificateTemplates");
            migrationBuilder.DropTable(name: "Issuers");
            migrationBuilder.DropTable(name: "Professionals");
        }
    }
}
