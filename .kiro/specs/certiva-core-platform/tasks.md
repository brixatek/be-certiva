# Implementation Plan: Certiva Core Platform

## Overview

Implement the Certiva Phase 1 Digital Certification & Verification Infrastructure as a modular monolith on .NET 8. The plan proceeds in layers: shared infrastructure and data models first, then module-by-module implementation (IdentityRegistry → CertificationEngine → VerificationEngine → CertificateWallet → IssuerPortal → NotificationSystem → AuditLog), followed by the API host, background workers, and cross-cutting concerns. Property-based tests using FsCheck are placed immediately after the code they validate.

---

## Tasks

- [ ] 1. Scaffold solution structure and shared infrastructure
  - [ ] 1.1 Create solution file and all project references
    - Create `Certiva.sln` and add projects: `Certiva.Api`, `Certiva.IdentityRegistry`, `Certiva.CertificationEngine`, `Certiva.VerificationEngine`, `Certiva.CertificateWallet`, `Certiva.IssuerPortal`, `Certiva.NotificationSystem`, `Certiva.AuditLog`, `Certiva.Infrastructure`, `Certiva.Tests.Unit`, `Certiva.Tests.Property`, `Certiva.Tests.Integration`
    - Add NuGet packages: EF Core 8, Npgsql EF provider, StackExchange.Redis, MassTransit, QuestPDF, QRCoder, ASP.NET Core Identity, Otp.NET, OpenTelemetry, Serilog, Asp.Versioning, FsCheck.NUnit (or FsCheck.Xunit)
    - Enforce project reference boundaries (each module references only `Certiva.Infrastructure`; no cross-module project references)
    - _Requirements: 20.1, 20.5_

  - [ ] 1.2 Define shared domain primitives and value objects
    - Create strongly-typed IDs: `ProfessionalId`, `IssuerId`, `CertificateId`, `TemplateId`, `TenantId`, `BulkJobId` (record structs wrapping `Guid`)
    - Create `DomainEvent` base record with `EventId`, `TenantId`, `SequenceNumber`, `OccurredAt`
    - Create `PagedResult<T>` and `Result<T>` types for consistent return values
    - _Requirements: 21.1_

  - [ ] 1.3 Implement `DbContext` and EF Core configuration
    - Create `CertivaDbContext` in `Certiva.Infrastructure` with `DbSet` for all entity tables
    - Configure entity mappings: `Professionals`, `Issuers`, `CertificateTemplates`, `Certificates`, `OutboxMessages`, `IdempotencyKeys`, `BulkIssueJobs`, `VerificationLogs`, `AuditLogs`, `NotificationLogs`
    - Apply all indexes and unique constraints from the schema (including `DEFERRABLE INITIALLY DEFERRED` on `uq_active_cert_professional_template`)
    - Configure `AuditLogs` entity as insert-only (no update/delete operations in EF)
    - _Requirements: 14.2, 21.1_

  - [ ] 1.4 Write and apply EF Core migrations
    - Generate initial migration covering all tables and indexes
    - Add database-level `RULE` or trigger on `AuditLogs` to reject `UPDATE`/`DELETE` as defense-in-depth
    - _Requirements: 14.2_

  - [ ] 1.5 Implement Redis connection and `IVerificationStoreRepository`
    - Configure `StackExchange.Redis` connection with health-check support
    - Implement `RedisVerificationStoreRepository`: `GetAsync`, `UpsertAsync` (with 1-hour TTL), `IsAvailableAsync`
    - Key format: `cert:verify:{tenantId}:{certificateId}`
    - _Requirements: 10.4, 9.1_

  - [ ] 1.6 Implement transactional outbox infrastructure
    - Implement `OutboxRelayWorker` (`BackgroundService`): polls `OutboxMessages WHERE Published = false ORDER BY CreatedAt`, publishes to MassTransit, marks `Published = true`
    - Implement `IOutboxWriter` helper used inside EF transactions
    - _Requirements: 1.6, 4.3, 17.4_

  - [ ] 1.7 Implement idempotency key middleware and repository
    - Implement `IdempotencyKeyRepository`: check-and-store with 24-hour TTL, using `IdempotencyKeys` table
    - Implement `IdempotencyMiddleware` or service decorator that intercepts requests with `Idempotency-Key` header
    - _Requirements: 4.5, 17.1_

  - [ ] 1.8 Configure MassTransit event bus and define all domain event contracts
    - Register all domain event message types: `ProfessionalRegistered`, `IssuerApproved`, `IssuerRejected`, `CertificateIssued`, `CertificateRevoked`, `CertificateExpired`, `QrCodeGenerated`, `PdfGenerated`, `BulkIssueJobEnqueued`
    - Configure retry policies (base 1s, max 60s, 3 retries) and dead-letter queue routing
    - _Requirements: 17.2, 4.9_

  - [ ] 1.9 Implement multi-tenancy middleware
    - Implement `TenantResolutionMiddleware`: extract `tenant_id` claim from JWT, set `TenantId` on `HttpContext`; return HTTP 400 if absent or unrecognized
    - _Requirements: 21.6_

  - [ ] 1.10 Configure OpenTelemetry, Serilog, and Prometheus metrics
    - Wire OpenTelemetry tracing across HTTP, EF Core, MassTransit, and Redis
    - Configure Serilog JSON formatter for structured logs
    - Expose `/metrics` Prometheus scrape endpoint
    - _Requirements: 18.1, 18.2, 18.3, 18.5_


- [ ] 2. Implement IdentityRegistry module
  - [ ] 2.1 Implement `INationalIdMaskingService`
    - Implement `NationalIdMaskingService.Mask(string nationalId)`: if `length <= 4` return all asterisks; otherwise return `(length-4)` asterisks + last 4 chars
    - Apply masking in every response mapper that includes Professional data
    - _Requirements: 1.7, 16.3_

  - [ ]* 2.2 Write property test for NationalId masking (P1)
    - **Property 1: NationalId Masking Is Applied Universally**
    - Generate random strings of length 0–50; assert last-4 rule and all-asterisk rule for short strings
    - **Validates: Requirements 1.7, 16.3**
    - `// Feature: certiva-core-platform, Property 1: NationalId Masking Is Applied Universally`

  - [ ] 2.3 Implement Professional registration
    - Implement `RegisterProfessionalAsync`: validate Name (≤100 chars, non-empty), NationalId (6–20 alphanumeric), Phone (E.164), Email (RFC 5322), at least one of Phone/Email present
    - Encrypt NationalId and contact fields with AES-256; store `NationalId_Hash` (SHA-256) for dedup
    - Insert Professional + OutboxMessage (`ProfessionalRegistered`) in one transaction
    - Return HTTP 409 with existing `ProfessionalId` on duplicate `NationalId_Hash + TenantId`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 16.2_

  - [ ]* 2.4 Write property test for Professional registration deduplication (P2)
    - **Property 2: Professional Registration Deduplication**
    - Generate random valid NationalIds; register twice; assert HTTP 409 and record count = 1
    - **Validates: Requirements 1.2**
    - `// Feature: certiva-core-platform, Property 2: Professional Registration Deduplication`

  - [ ]* 2.5 Write property test for Professional registration field validation (P3)
    - **Property 3: Professional Registration Field Validation**
    - Generate invalid field combinations; assert validation error returned and no record persisted
    - **Validates: Requirements 1.3, 1.4, 1.5**
    - `// Feature: certiva-core-platform, Property 3: Professional Registration Field Validation`

  - [ ] 2.6 Implement Issuer onboarding and approval/rejection
    - Implement `OnboardIssuerAsync`: create Issuer with `VerificationStatus = Pending`; enforce case-insensitive `OrganizationName` uniqueness per tenant (HTTP 409 on duplicate)
    - Implement `ApproveIssuerAsync`: transition to Verified, write AuditLog, publish `IssuerApproved` via outbox; return HTTP 409 if already Verified
    - Implement `RejectIssuerAsync`: transition to Rejected, write AuditLog, publish `IssuerRejected` via outbox; return HTTP 409 if already Rejected
    - _Requirements: 2.1, 2.2, 2.3, 2.5, 2.6, 2.7, 2.8_

  - [ ]* 2.7 Write property test for Issuer name uniqueness (P5)
    - **Property 5: Issuer Organization Name Uniqueness (Case-Insensitive)**
    - Generate names with varying case; assert HTTP 409 on second attempt and no duplicate record
    - **Validates: Requirements 2.5, 2.6**
    - `// Feature: certiva-core-platform, Property 5: Issuer Organization Name Uniqueness`

  - [ ]* 2.8 Write property test for Issuer state transition idempotency (P6)
    - **Property 6: Issuer State Transition Idempotency**
    - Approve already-Verified issuer; reject already-Rejected issuer; assert HTTP 409 and no duplicate AuditLog entry
    - **Validates: Requirements 2.7**
    - `// Feature: certiva-core-platform, Property 6: Issuer State Transition Idempotency`

  - [ ] 2.9 Implement authentication service (JWT, refresh tokens, MFA)
    - Implement `AuthenticateAsync`: validate credentials, check MFA TOTP if enabled (±30s window), issue JWT (15-min expiry) + refresh token (7-day expiry)
    - Implement `RefreshTokenAsync`: validate refresh token, issue new pair, invalidate old refresh token
    - Implement `RevokeSessionAsync`: invalidate refresh token
    - Record failed attempts in AuditLog; apply rate limiting (10 attempts / 15-min / IP) via Redis counter
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.10, 15.11, 15.12_

  - [ ]* 2.10 Write property test for authentication error conditions (P40)
    - **Property 40: Authentication Error Conditions**
    - Generate invalid credentials/tokens; assert HTTP 401 and no JWT issued
    - **Validates: Requirements 15.2, 15.4, 15.5**
    - `// Feature: certiva-core-platform, Property 40: Authentication Error Conditions`

  - [ ] 2.11 Implement RBAC authorization policies
    - Define `AdminPolicy`, `IssuerPolicy`, `WorkerPolicy` using JWT role claims
    - Apply policies to all protected endpoints
    - _Requirements: 15.6, 15.7, 15.8, 15.9_

  - [ ]* 2.12 Write property test for RBAC enforcement (P41)
    - **Property 41: RBAC Enforcement**
    - Generate users with wrong roles; assert HTTP 403 on protected endpoints
    - **Validates: Requirements 15.7, 15.8, 15.9**
    - `// Feature: certiva-core-platform, Property 41: RBAC Enforcement`


- [ ] 3. Checkpoint — IdentityRegistry
  - Ensure all IdentityRegistry unit, property, and integration tests pass. Verify Professional registration, Issuer onboarding, auth, and RBAC. Ask the user if questions arise.

- [ ] 4. Implement CertificationEngine module
  - [ ] 4.1 Implement `ICanonicalSerializationService`
    - Serialize `CertificateFields` as alphabetically ordered JSON, no whitespace, UTF-8
    - Fields: `certificateId`, `expiryDate`, `issuerId`, `issuerName`, `issueDate`, `name`, `professionalId`, `tenantId`
    - _Requirements: 19.2_

  - [ ]* 4.2 Write property test for canonical serialization determinism (P13)
    - **Property 13: Canonical Serialization Determinism**
    - Generate random field sets; assert same output regardless of field insertion order; assert round-trip equality
    - **Validates: Requirements 19.2**
    - `// Feature: certiva-core-platform, Property 13: Canonical Serialization Determinism`

  - [ ] 4.3 Implement `ICertificateHashService`
    - Implement `ComputeHash(fields, previousHash)`: `SHA256(Canonical(fields) + previousHash)`
    - Implement `GetGenesisHash()`: 64 hex zeros
    - Implement `VerifyChain(chain)`: recompute each hash and compare to stored value
    - _Requirements: 4.2, 19.1, 19.4_

  - [ ]* 4.4 Write property test for certificate hash chain integrity (P12)
    - **Property 12: Certificate Hash Chain Integrity**
    - Generate random sequences of certificates; assert recomputed hashes match stored hashes; assert field modification breaks subsequent hashes
    - **Validates: Requirements 4.2, 19.1, 19.2, 19.4**
    - `// Feature: certiva-core-platform, Property 12: Certificate Hash Chain Integrity`

  - [ ] 4.5 Implement individual certificate issuance
    - Implement `IssueCertificateAsync`: validate ProfessionalId (HTTP 404), TemplateId (HTTP 404), Issuer ownership (HTTP 403), Issuer verified (HTTP 403), no duplicate Active cert (HTTP 409)
    - Compute `ExpiryDate = IssueDate + ValidityPeriodDays` (null if `ValidityPeriodDays = 0`)
    - Compute `CertificateHash` using `ICertificateHashService`
    - Write Certificate + OutboxMessage (`CertificateIssued`) in one transaction; check idempotency key before insert
    - Validate all entities share the same `TenantId` (HTTP 403 on mismatch)
    - _Requirements: 4.1, 4.2, 4.3, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 21.3_

  - [ ]* 4.6 Write property test for certificate issuance field correctness (P11)
    - **Property 11: Certificate Issuance Creates Correct Record**
    - Generate random `ValidityPeriodDays`; assert `ExpiryDate` computation and all required fields present
    - **Validates: Requirements 4.1**
    - `// Feature: certiva-core-platform, Property 11: Certificate Issuance Creates Correct Record`

  - [ ]* 4.7 Write property test for issuance idempotency (P14)
    - **Property 14: Issuance Idempotency**
    - Repeat issuance with same `Idempotency-Key`; assert HTTP 200 with original cert and record count = 1
    - **Validates: Requirements 4.5, 17.1**
    - `// Feature: certiva-core-platform, Property 14: Issuance Idempotency`

  - [ ]* 4.8 Write property test for duplicate active certificate prevention (P15)
    - **Property 15: Duplicate Active Certificate Prevention**
    - Issue twice for same Professional+Template; assert HTTP 409 with existing `CertificateId` and record count = 1
    - **Validates: Requirements 4.10**
    - `// Feature: certiva-core-platform, Property 15: Duplicate Active Certificate Prevention`

  - [ ]* 4.9 Write property test for unverified issuer rejection (P4)
    - **Property 4: Unverified Issuer Cannot Issue or Manage Templates**
    - Generate issuers in Pending/Rejected states; assert HTTP 403 on issuance
    - **Validates: Requirements 2.4, 3.5**
    - `// Feature: certiva-core-platform, Property 4: Unverified Issuer Cannot Issue or Manage Templates`

  - [ ] 4.10 Implement certificate revocation
    - Implement `RevokeCertificateAsync`: validate certificate exists (HTTP 404), Issuer ownership (HTTP 403), status not already Revoked/Expired (HTTP 409), revocation reason present (HTTP 422)
    - Update Status to Revoked, write AuditLog, publish `CertificateRevoked` via outbox in one transaction
    - _Requirements: 6.1, 6.2, 6.4, 6.5, 6.6, 6.7, 6.10_

  - [ ]* 4.11 Write property test for revocation state transition validation (P20)
    - **Property 20: Revocation State Transition Validation**
    - Attempt revocation on already-Revoked/Expired certs and without reason; assert HTTP 409/422 and no duplicate AuditLog
    - **Validates: Requirements 6.5, 6.10**
    - `// Feature: certiva-core-platform, Property 20: Revocation State Transition Validation`

  - [ ]* 4.12 Write property test for cross-issuer revocation authorization (P21)
    - **Property 21: Cross-Issuer Revocation Authorization**
    - Generate revocation requests from non-owning issuers; assert HTTP 403 and no status change
    - **Validates: Requirements 6.6**
    - `// Feature: certiva-core-platform, Property 21: Cross-Issuer Revocation Authorization`

  - [ ] 4.13 Implement certificate expiry scheduler
    - Implement `CertificateExpiryScheduler` (`BackgroundService`): runs every ≤60 minutes, queries certificates where `ExpiryDate <= NOW() AND Status = 'Active'`, updates Status to Expired, writes OutboxMessage (`CertificateExpired`) per certificate in batched transactions
    - _Requirements: 6.8_

  - [ ] 4.14 Implement hash verification endpoint logic
    - Implement `VerifyCertificateHashAsync`: recompute hash from stored fields + preceding hash; return match/mismatch status, stored hash, recomputed hash
    - If mismatch: update Status to Tampered, write AuditLog entry with `CertificateId`, stored hash, recomputed hash, detection timestamp
    - _Requirements: 19.3, 19.4, 19.5_

  - [ ]* 4.15 Write property test for tampered certificate detection (P45)
    - **Property 45: Tampered Certificate Detection**
    - Modify stored fields of a certificate; assert Status updated to Tampered and AuditLog entry written
    - **Validates: Requirements 19.5**
    - `// Feature: certiva-core-platform, Property 45: Tampered Certificate Detection`

  - [ ]* 4.16 Write property test for soft deletion invariant (P19)
    - **Property 19: Soft Deletion Invariant**
    - Apply lifecycle operations; assert Certificate record still exists with updated Status
    - **Validates: Requirements 6.4**
    - `// Feature: certiva-core-platform, Property 19: Soft Deletion Invariant`

  - [ ]* 4.17 Write property test for cross-tenant entity validation (P47)
    - **Property 47: Cross-Tenant Entity Validation**
    - Generate issuance requests with cross-tenant ProfessionalId/IssuerId/TemplateId; assert HTTP 403
    - **Validates: Requirements 21.3**
    - `// Feature: certiva-core-platform, Property 47: Cross-Tenant Entity Validation`


- [ ] 5. Implement QR code and PDF generation workers
  - [ ] 5.1 Implement QR code generation consumer
    - Implement `QrCodeGeneratedConsumer`: subscribes to `CertificateIssued`; generates QR PNG (≥200×200 px, auto-version) encoding `https://{domain}/verify/{CertificateId}`; stores URL string and base64 PNG on Certificate record; publishes `QrCodeGenerated`
    - Retry up to 3 times with exponential backoff (5s, 25s, 125s); on exhaustion write AuditLog and move to dead-letter
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

  - [ ]* 5.2 Write property test for QR code URL format and uniqueness (P22)
    - **Property 22: QR Code URL Format and Uniqueness**
    - Generate random `CertificateId`s; assert URL format `https://{domain}/verify/{id}` and uniqueness across certificates
    - **Validates: Requirements 7.1, 7.3**
    - `// Feature: certiva-core-platform, Property 22: QR Code URL Format and Uniqueness`

  - [ ]* 5.3 Write property test for QR code image minimum resolution (P23)
    - **Property 23: QR Code Image Minimum Resolution**
    - Generate random verification URLs; assert generated PNG dimensions ≥ 200×200 and full URL encodable
    - **Validates: Requirements 7.7**
    - `// Feature: certiva-core-platform, Property 23: QR Code Image Minimum Resolution`

  - [ ] 5.4 Implement PDF generation consumer
    - Implement `PdfGeneratedConsumer`: subscribes to `QrCodeGenerated`; generates PDF via QuestPDF containing Professional Name, certificate Name, IssueDate, ExpiryDate (or "Does not expire"), IssuerName, embedded QR image; stores PDF in blob storage; publishes `PdfGenerated`
    - Retry rendering errors up to 3 times (5s, 25s, 125s); on exhaustion write AuditLog and dead-letter
    - Retry infrastructure/timeout errors indefinitely with same backoff (no dead-letter for infrastructure failures)
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 5.5 Write property test for PDF required fields (P24)
    - **Property 24: PDF Contains Required Fields**
    - Generate random certificate data; assert PDF document contains all required fields and is associated with correct `CertificateId`
    - **Validates: Requirements 8.1**
    - `// Feature: certiva-core-platform, Property 24: PDF Contains Required Fields`

- [ ] 6. Checkpoint — CertificationEngine and workers
  - Ensure all CertificationEngine and worker unit, property, and integration tests pass. Verify issuance, revocation, hash chain, QR, and PDF. Ask the user if questions arise.

- [ ] 7. Implement VerificationEngine module
  - [ ] 7.1 Implement verification resolution with Redis/PostgreSQL fallback
    - Implement `VerifyCertificateAsync`: check `IVerificationStoreRepository.IsAvailableAsync()`; if available, attempt Redis lookup; on miss or Redis unavailable, fall back to PostgreSQL; repopulate Redis on fallback hit (1-hour TTL)
    - Return `valid: false` for Revoked/Expired; return HTTP 404 for non-existent CertificateId (write AuditLog)
    - Record `VerificationLog` entry on every request
    - Apply rate limiting: 100 req/min/IP via Redis counter (`ratelimit:verify:{ip}`, 1-min TTL); return HTTP 429 on breach
    - Emit latency-exceeded log entries: >50ms from Redis, >500ms from PostgreSQL (independently)
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 10.1, 18.2, 18.4_

  - [ ]* 7.2 Write property test for revoked/expired certificates return valid=false (P27)
    - **Property 27: Revoked and Expired Certificates Return valid=false**
    - Generate Revoked/Expired certificates; assert `valid: false` and correct Status in response
    - **Validates: Requirements 9.7**
    - `// Feature: certiva-core-platform, Property 27: Revoked and Expired Certificates Return valid=false`

  - [ ]* 7.3 Write property test for non-existent certificate returns 404 (P28)
    - **Property 28: Non-Existent Certificate Verification Returns 404**
    - Generate random non-existent `CertificateId`s; assert HTTP 404 with error indicator and queried ID
    - **Validates: Requirements 9.5**
    - `// Feature: certiva-core-platform, Property 28: Non-Existent Certificate Verification Returns 404`

  - [ ]* 7.4 Write property test for contact details masked in verification responses (P42)
    - **Property 42: Contact Details Masked in Verification Responses**
    - Generate random Professional data; assert verification response contains only Name, CertificateId, IssueDate, ExpiryDate — no Phone, Email, or NationalId
    - **Validates: Requirements 16.4**
    - `// Feature: certiva-core-platform, Property 42: Contact Details Masked in Verification Responses`

  - [ ] 7.5 Implement `CertificateIssued`/`CertificateRevoked`/`CertificateExpired` consumers in VerificationEngine
    - Implement consumers that upsert `CertificateVerificationView` in Redis on each event
    - Enforce sequence number ordering: apply event only if `event.SequenceNumber == lastApplied + 1`; discard and write AuditLog for out-of-order events
    - Track `LastAppliedSequence` per `CertificateId` in Redis (`seq:{tenantId}:{certificateId}`)
    - _Requirements: 4.4, 6.3, 6.9, 17.3, 17.6_

  - [ ]* 7.6 Write property test for ordered event processing (P43)
    - **Property 43: Ordered Event Processing**
    - Generate out-of-order event sequences; assert only in-order events applied and discarded events recorded in AuditLog
    - **Validates: Requirements 17.3**
    - `// Feature: certiva-core-platform, Property 43: Ordered Event Processing`

  - [ ] 7.7 Implement Verification_Store resynchronization
    - Implement `ResynchronizeVerificationStoreAsync`: on Redis restoration (health check), bulk-upsert all `CertificateVerificationView` records from PostgreSQL within 5-minute deadline; resume Redis reads after deadline regardless of completion; retry every 30 minutes on failure
    - _Requirements: 10.2, 10.3, 10.5_

  - [ ]* 7.8 Write property test for multi-tenant data isolation (P46)
    - **Property 46: Multi-Tenant Data Isolation**
    - Generate data for two tenants; query as one tenant; assert no records from other tenant returned
    - **Validates: Requirements 21.2, 21.7**
    - `// Feature: certiva-core-platform, Property 46: Multi-Tenant Data Isolation`

  - [ ]* 7.9 Write property test for missing/invalid TenantId handling (P48)
    - **Property 48: Missing or Invalid TenantId Handling**
    - Generate requests without resolvable TenantId; assert HTTP 400; generate cross-tenant access; assert HTTP 403
    - **Validates: Requirements 21.6**
    - `// Feature: certiva-core-platform, Property 48: Missing or Invalid TenantId Handling`


- [ ] 8. Implement CertificateWallet module
  - [ ] 8.1 Implement wallet certificate list and detail
    - Implement `GetCertificatesAsync`: query all certificates for `ProfessionalId + TenantId`; group by Status (`[Active, Expired, Revoked]`), sort by `IssueDate DESC` within each group; set `expiryWarning: true` where `ExpiryDate != null AND ExpiryDate <= today + 30 days`
    - Implement `GetCertificateDetailAsync`: return full detail including `QRCodeValue` and PDF download link; return HTTP 403 if `ProfessionalId` mismatch
    - Return HTTP 200 with empty list when no certificates exist
    - _Requirements: 11.1, 11.2, 11.4, 11.5, 11.6, 11.7_

  - [ ]* 8.2 Write property test for wallet grouping and sorting (P29)
    - **Property 29: Certificate Wallet Returns All Certificates Grouped and Sorted**
    - Generate random certificate sets; assert grouping order `[Active, Expired, Revoked]` and `IssueDate DESC` sort within groups; assert no cross-professional leakage
    - **Validates: Requirements 11.1, 11.4**
    - `// Feature: certiva-core-platform, Property 29: Certificate Wallet Returns All Certificates Grouped and Sorted`

  - [ ]* 8.3 Write property test for expiry warning indicator (P30)
    - **Property 30: Expiry Warning Indicator**
    - Generate certificates with varying `ExpiryDate` values; assert warning set iff `ExpiryDate != null AND ExpiryDate <= today + 30`
    - **Validates: Requirements 11.5**
    - `// Feature: certiva-core-platform, Property 30: Expiry Warning Indicator`

  - [ ]* 8.4 Write property test for cross-professional wallet access authorization (P31)
    - **Property 31: Cross-Professional Wallet Access Authorization**
    - Generate requests from a Professional for a certificate belonging to a different Professional; assert HTTP 403
    - **Validates: Requirements 11.6**
    - `// Feature: certiva-core-platform, Property 31: Cross-Professional Wallet Access Authorization`

  - [ ] 8.5 Implement signed PDF download URL generation
    - Implement `GetPdfDownloadUrlAsync`: generate HMAC-SHA256 signed URL over `{CertificateId}:{ExpiresAt}` with 15-minute expiry; return HTTP 202 with job state if PDF not yet generated
    - Implement URL validation endpoint: reject requests after expiry time
    - _Requirements: 8.4, 8.5_

  - [ ]* 8.6 Write property test for signed URL expiry (P26)
    - **Property 26: Signed URL Expiry**
    - Generate signed URLs at various times; assert URL valid within 15 minutes and rejected after expiry
    - **Validates: Requirements 8.5**
    - `// Feature: certiva-core-platform, Property 26: Signed URL Expiry`

  - [ ]* 8.7 Write property test for PDF download authorization (P25)
    - **Property 25: PDF Download Authorization**
    - Generate PDF download requests from non-owning Professionals; assert HTTP 403
    - **Validates: Requirements 8.4**
    - `// Feature: certiva-core-platform, Property 25: PDF Download Authorization`

  - [ ] 8.6 Implement shareable link generation
    - Implement `GetShareableLinkAsync`: return `https://{domain}/verify/{CertificateId}`
    - _Requirements: 11.3_

  - [ ]* 8.7 Write property test for shareable link format (P32)
    - **Property 32: Shareable Link Format**
    - Generate random `CertificateId`s; assert link matches `https://{domain}/verify/{id}`
    - **Validates: Requirements 11.3**
    - `// Feature: certiva-core-platform, Property 32: Shareable Link Format`

- [ ] 9. Implement IssuerPortal module
  - [ ] 9.1 Implement certification template management
    - Implement `CreateTemplateAsync`: validate Name (≤100 chars), `ValidityPeriodDays` (non-negative), `IssuerId` present; enforce case-insensitive name uniqueness per Issuer+Tenant (HTTP 409); reject if Issuer not Verified (HTTP 403)
    - Implement `UpdateTemplateAsync`: apply update prospectively only; do not alter existing certificates
    - Implement `DeactivateTemplateAsync`: mark `IsActive = false`; prevent selection for new issuance
    - _Requirements: 3.1, 3.2, 3.4, 3.5, 3.6, 3.7_

  - [ ]* 9.2 Write property test for template required field validation (P7)
    - **Property 7: Template Required Field Validation**
    - Generate invalid template fields; assert validation error and no record persisted
    - **Validates: Requirements 3.2**
    - `// Feature: certiva-core-platform, Property 7: Template Required Field Validation`

  - [ ]* 9.3 Write property test for template name uniqueness per issuer (P10)
    - **Property 10: Template Name Uniqueness Per Issuer (Case-Insensitive)**
    - Generate template names with varying case for same Issuer; assert HTTP 409 on second attempt
    - **Validates: Requirements 3.6**
    - `// Feature: certiva-core-platform, Property 10: Template Name Uniqueness Per Issuer`

  - [ ]* 9.4 Write property test for template scoped to creating issuer (P8)
    - **Property 8: Template Scoped to Creating Issuer**
    - Generate issuance requests from non-owning issuers referencing a template; assert HTTP 403
    - **Validates: Requirements 3.3, 4.8**
    - `// Feature: certiva-core-platform, Property 8: Template Scoped to Creating Issuer`

  - [ ]* 9.5 Write property test for template updates do not retroactively alter certificates (P9)
    - **Property 9: Template Updates Do Not Retroactively Alter Certificates**
    - Issue certificate; update template; assert certificate fields unchanged
    - **Validates: Requirements 3.4**
    - `// Feature: certiva-core-platform, Property 9: Template Updates Do Not Retroactively Alter Certificates`

  - [ ] 9.6 Implement bulk issuance job enqueueing and status polling
    - Implement `EnqueueBulkIssueAsync`: validate list size 1–1000 (HTTP 422 otherwise); persist `BulkIssueJob` with status Queued; publish `BulkIssueJobEnqueued` via outbox; return `JobId`
    - Implement `GetBulkJobStatusAsync`: return current status, `TotalCount`, `ProcessedCount`, `SuccessCount`, `FailureCount`; support lookup by `IssuerId + submittedAt` as alternative
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 9.7 Write property test for bulk issuance boundary validation (P16)
    - **Property 16: Bulk Issuance Boundary Validation**
    - Generate lists of size 0, 1, 1000, 1001; assert HTTP 422 for 0 and >1000; assert acceptance for 1–1000
    - **Validates: Requirements 5.2**
    - `// Feature: certiva-core-platform, Property 16: Bulk Issuance Boundary Validation`

  - [ ] 9.8 Implement bulk issuance job processor consumer
    - Implement `BulkIssueJobConsumer`: subscribes to `BulkIssueJobEnqueued`; updates job to Processing; iterates entries calling `IssueCertificateAsync` with same idempotency rules; accumulates success/failure counts; on completion update to Completed with result report; on full failure update to Failed and write AuditLog
    - Continue processing remaining entries on individual failure (no batch abort)
    - _Requirements: 5.5, 5.6, 5.7, 5.8_

  - [ ]* 9.9 Write property test for bulk issuance partial failure isolation (P17)
    - **Property 17: Bulk Issuance Partial Failure Isolation**
    - Generate mixed valid/invalid entries; assert valid entries succeed and invalid entries fail without aborting batch
    - **Validates: Requirements 5.7**
    - `// Feature: certiva-core-platform, Property 17: Bulk Issuance Partial Failure Isolation`

  - [ ]* 9.10 Write property test for bulk issuance applies individual idempotency rules (P18)
    - **Property 18: Bulk Issuance Applies Individual Idempotency Rules**
    - Submit same bulk job twice; assert no duplicate Certificate records created
    - **Validates: Requirements 5.5**
    - `// Feature: certiva-core-platform, Property 18: Bulk Issuance Applies Individual Idempotency Rules`

  - [ ] 9.11 Implement analytics queries
    - Implement `GetAnalyticsAsync`: certificate counts by Status; monthly issuance for trailing 12 months; all queries scoped to `IssuerId + TenantId`
    - Implement `SearchProfessionalsAsync`: case-insensitive partial Name match or exact NationalId match; scoped to requesting Issuer's certificates; return empty list (HTTP 200) when no matches
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

  - [ ]* 9.12 Write property test for analytics data isolation (P34)
    - **Property 34: Analytics Data Isolation**
    - Generate multi-issuer data sets; assert analytics response contains only requesting Issuer's data
    - **Validates: Requirements 13.4**
    - `// Feature: certiva-core-platform, Property 34: Analytics Data Isolation`

  - [ ]* 9.13 Write property test for analytics counts correctness (P35)
    - **Property 35: Analytics Counts Correctness**
    - Generate random certificate distributions; assert counts match actual Status distribution and monthly breakdown is correct
    - **Validates: Requirements 13.1, 13.2**
    - `// Feature: certiva-core-platform, Property 35: Analytics Counts Correctness`

  - [ ]* 9.14 Write property test for professional search returns matching results (P36)
    - **Property 36: Professional Search Returns Matching Results**
    - Generate random search queries; assert all and only matching certificates from requesting Issuer returned
    - **Validates: Requirements 13.3**
    - `// Feature: certiva-core-platform, Property 36: Professional Search Returns Matching Results`


- [ ] 10. Implement NotificationSystem module
  - [ ] 10.1 Implement notification dispatch consumers
    - Implement `CertificateIssuedNotificationConsumer`: subscribes to `CertificateIssued`; check idempotency key (`SHA256(eventId + "Issuance")`); if Professional has Email, dispatch issuance alert within 5 minutes; if no Email, skip and write AuditLog
    - Implement `CertificateRevokedNotificationConsumer`: same pattern for revocation alerts
    - Retry up to 3 times with exponential backoff (5s initial, capped at 60s); on exhaustion write single AuditLog failure entry
    - _Requirements: 12.1, 12.2, 12.5, 12.6, 12.7_

  - [ ]* 10.2 Write property test for notification idempotency (P33)
    - **Property 33: Notification Idempotency**
    - Generate duplicate events with same `EventId + NotificationType`; assert at most one dispatch per combination
    - **Validates: Requirements 12.8**
    - `// Feature: certiva-core-platform, Property 33: Notification Idempotency`

  - [ ] 10.3 Implement expiry reminder scheduler
    - Implement `ExpiryReminderScheduler` (`BackgroundService`): runs daily; query certificates where `ExpiryDate = today + 30` and `ExpiryDate = today + 7`; dispatch reminders with idempotency keys; skip if no Email
    - _Requirements: 12.3, 12.4_

- [ ] 11. Implement AuditLog module
  - [ ] 11.1 Implement `IAuditLogService`
    - Implement `WriteAsync`: compute `RecordHash = SHA256(actionType + entityId + timestamp.ToString("O") + actor + metadataJson)`; insert record (insert-only, no update/delete); reject originating action if write fails (for security events, always reject; for non-security, reject per Req 14.7)
    - Implement `QueryAsync`: filter by `ActionType`, `EntityId`, `Actor`, date range; paginate at ≤100 records per page
    - Implement `ExportCsvAsync`: stream CSV with `ActionType`, `EntityId`, `Timestamp`, `Actor`, `Metadata` columns
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.7_

  - [ ]* 11.2 Write property test for audit log append-only invariant (P37)
    - **Property 37: Audit Log Append-Only Invariant**
    - Attempt modifications/deletions on AuditLog records; assert record count monotonically non-decreasing and no record modified
    - **Validates: Requirements 14.2**
    - `// Feature: certiva-core-platform, Property 37: Audit Log Append-Only Invariant`

  - [ ]* 11.3 Write property test for audit log record hash integrity (P38)
    - **Property 38: Audit Log Record Hash Integrity**
    - Generate random audit entries; recompute hash from stored fields; assert equals stored `RecordHash`; modify a field; assert hash differs
    - **Validates: Requirements 14.3**
    - `// Feature: certiva-core-platform, Property 38: Audit Log Record Hash Integrity`

  - [ ]* 11.4 Write property test for audit log records all significant actions (P39)
    - **Property 39: Audit Log Records All Significant Actions**
    - Generate random action sequences (issuance, revocation, expiry, verification, approval, rejection); assert AuditLog entry created for each with required fields and metadata ≤10 KB
    - **Validates: Requirements 14.1**
    - `// Feature: certiva-core-platform, Property 39: Audit Log Records All Significant Actions`

- [ ] 12. Checkpoint — VerificationEngine, Wallet, IssuerPortal, Notifications, AuditLog
  - Ensure all module unit, property, and integration tests pass. Verify verification fallback, wallet grouping, bulk issuance, notifications, and audit log integrity. Ask the user if questions arise.

- [ ] 13. Implement API host, versioning, and health probes
  - [ ] 13.1 Configure ASP.NET Core host and register all module services
    - Register all module services, repositories, and consumers via DI in `Certiva.Api`
    - Configure `Asp.Versioning` for `/api/v{major}` routing; register `/api/versions` endpoint returning supported versions, status, and sunset dates
    - Return HTTP 410 for removed versions; include `Deprecation` and `Sunset` headers for deprecated versions
    - _Requirements: 22.1, 22.2, 22.3, 22.4, 22.5, 22.6_

  - [ ]* 13.2 Write property test for deprecated API version headers (P49)
    - **Property 49: Deprecated API Version Headers**
    - Call deprecated version endpoints; assert `Deprecation` and `Sunset` headers present
    - **Validates: Requirements 22.3**
    - `// Feature: certiva-core-platform, Property 49: Deprecated API Version Headers`

  - [ ]* 13.3 Write property test for removed API version returns 410 (P50)
    - **Property 50: Removed API Version Returns 410**
    - Call removed version endpoints; assert HTTP 410 with message referencing current version
    - **Validates: Requirements 22.5**
    - `// Feature: certiva-core-platform, Property 50: Removed API Version Returns 410`

  - [ ] 13.4 Implement all API controllers/minimal API endpoints
    - Wire all endpoint groups to their module services: `/api/v1/professionals`, `/api/v1/issuers`, `/api/v1/auth`, `/api/v1/certificates`, `/api/v1/certificates/bulk`, `/api/v1/verify/{id}`, `/api/v1/wallet`, `/api/v1/templates`, `/api/v1/analytics`, `/api/v1/audit`
    - Apply `TenantResolutionMiddleware`, auth policies, and rate limiting middleware to appropriate endpoints
    - _Requirements: 9.4, 15.6, 15.7, 15.8, 15.9, 21.6_

  - [ ] 13.5 Implement liveness and readiness health probes
    - Implement `/health/live`: return HTTP 200 if PostgreSQL reachable; HTTP 503 otherwise
    - Implement `/health/ready`: return HTTP 200 only after startup initialization complete (Redis check, config validation) within 60-second timeout; HTTP 503 otherwise
    - _Requirements: 20.3, 20.4_

  - [ ] 13.6 Implement environment variable validation at startup
    - On startup, validate all required environment variables (DB connection string, Redis endpoint, Event Bus connection, JWT signing key, blob storage config); log descriptive error and exit with non-zero code if any missing
    - _Requirements: 20.5, 20.6_

  - [ ] 13.7 Implement structured log emission for certificate operations
    - Ensure every certificate issuance, revocation, and expiry transition emits a JSON-formatted log entry with `CertificateId`, `ProfessionalId`, `IssuerId`, `Timestamp`, and `operationType` fields via Serilog
    - _Requirements: 18.1_

  - [ ]* 13.8 Write property test for structured log fields (P44)
    - **Property 44: Structured Log Fields for Certificate Operations**
    - Generate random certificate operations; assert emitted log entries are JSON-formatted and contain all required named fields
    - **Validates: Requirements 18.1**
    - `// Feature: certiva-core-platform, Property 44: Structured Log Fields for Certificate Operations`


- [ ] 14. Write integration tests
  - [ ]* 14.1 Write integration test for transactional outbox
    - Assert certificate issuance writes both `Certificate` and `OutboxMessage` in the same transaction; assert rollback on outbox write failure leaves no partial state
    - _Requirements: 4.3, 17.4_

  - [ ]* 14.2 Write integration test for event flow (CertificateIssued → VerificationEngine)
    - Assert `CertificateIssued` event triggers `CertificateVerificationView` upsert in Redis within expected time
    - _Requirements: 4.4_

  - [ ]* 14.3 Write integration test for Redis fallback and resynchronization
    - Assert verification falls back to PostgreSQL when Redis unavailable; assert resynchronization repopulates Redis after restoration
    - _Requirements: 10.1, 10.2_

  - [ ]* 14.4 Write integration test for expiry scheduler
    - Assert certificates with past `ExpiryDate` are transitioned to Expired within scheduler interval
    - _Requirements: 6.8_

  - [ ]* 14.5 Write integration test for notification dispatch
    - Assert `CertificateIssued` event triggers email dispatch within 5 minutes for Professionals with registered Email
    - _Requirements: 12.1_

  - [ ]* 14.6 Write integration test for liveness and readiness probes
    - Assert `/health/live` returns 200 when PostgreSQL reachable and 503 when not; assert `/health/ready` returns non-2xx before initialization and 200 after
    - _Requirements: 20.3, 20.4_

  - [ ]* 14.7 Write integration test for rate limiting
    - Assert auth endpoint blocks after 10 failed attempts per 15-minute window per IP (HTTP 429)
    - Assert public verification endpoint blocks after 100 requests per minute per IP (HTTP 429)
    - _Requirements: 9.6, 15.12_

  - [ ]* 14.8 Write integration test for distributed trace propagation
    - Assert trace ID flows from issuance API request through Event Bus to workers and PostgreSQL writes
    - _Requirements: 18.5_

- [ ] 15. Final checkpoint — Full system
  - Ensure all unit, property, and integration tests pass across all modules. Verify end-to-end flows: issuance → QR → PDF → verification → wallet. Ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP delivery
- Each task references specific requirements for full traceability
- Property-based tests use FsCheck with a minimum of 100 iterations per property; each test is annotated with `// Feature: certiva-core-platform, Property {N}: {property_text}`
- All 50 correctness properties from the design document are covered by property test sub-tasks
- Checkpoints at tasks 3, 6, 12, and 15 ensure incremental validation at module boundaries
- The design document uses C# / .NET 8 throughout — no language selection step required
- All write operations use the transactional outbox pattern; no direct Event Bus calls from business logic
- TenantId isolation is enforced at the application layer on every database query (Req 21.7)
- AuditLog write failures reject the originating action for security events; for non-security events, the system also rejects per Req 14.7


## Task Dependency Graph

```json
{
  "waves": [
    {
      "id": 0,
      "tasks": ["1.1", "1.2"]
    },
    {
      "id": 1,
      "tasks": ["1.3", "1.4"]
    },
    {
      "id": 2,
      "tasks": ["1.5", "1.6", "1.7", "1.8", "1.9", "1.10"]
    },
    {
      "id": 3,
      "tasks": ["2.1", "2.3", "2.6", "2.9", "2.11"]
    },
    {
      "id": 4,
      "tasks": ["2.2", "2.4", "2.5", "2.7", "2.8", "2.10", "2.12"]
    },
    {
      "id": 5,
      "tasks": ["4.1", "4.3"]
    },
    {
      "id": 6,
      "tasks": ["4.2", "4.4", "4.5"]
    },
    {
      "id": 7,
      "tasks": ["4.6", "4.7", "4.8", "4.9", "4.10", "4.13", "4.14"]
    },
    {
      "id": 8,
      "tasks": ["4.11", "4.12", "4.15", "4.16", "4.17", "5.1"]
    },
    {
      "id": 9,
      "tasks": ["5.2", "5.3", "5.4"]
    },
    {
      "id": 10,
      "tasks": ["5.5", "7.1", "9.1", "9.6", "9.8", "9.11", "10.1", "10.3", "11.1"]
    },
    {
      "id": 11,
      "tasks": ["7.2", "7.3", "7.4", "7.5", "7.7", "9.2", "9.3", "9.4", "9.5", "9.7", "9.9", "9.10", "9.12", "9.13", "9.14", "10.2", "11.2", "11.3", "11.4"]
    },
    {
      "id": 12,
      "tasks": ["7.6", "7.8", "7.9", "8.1", "8.5", "8.6"]
    },
    {
      "id": 13,
      "tasks": ["8.2", "8.3", "8.4", "8.7"]
    },
    {
      "id": 14,
      "tasks": ["13.1", "13.4", "13.5", "13.6", "13.7"]
    },
    {
      "id": 15,
      "tasks": ["13.2", "13.3", "13.8"]
    },
    {
      "id": 16,
      "tasks": ["14.1", "14.2", "14.3", "14.4", "14.5", "14.6", "14.7", "14.8"]
    }
  ]
}
```
