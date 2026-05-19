# Requirements Document

## Introduction

Certiva is a national-grade trust registry for workforce credentials. Phase 1 delivers the core Digital Certification & Verification Infrastructure — a modular monolith built on .NET 8, PostgreSQL, Redis, and an event bus. The platform enables Training Providers to issue tamper-evident digital certificates, Professionals to own and share their credentials, Employers to verify workers in real time, and Platform Admins to govern system integrity. The guiding design principle is: **every certificate must be verifiable even if the system is partially down.**

---

## Glossary

- **Certificate**: A digitally issued credential linking a Professional to a completed training or qualification, identified by a globally unique GUID/ULID.
- **Certification_Engine**: The subsystem responsible for defining certification types, issuing certificates, managing certificate lifecycle, generating QR codes, and producing PDF certificates.
- **Verification_Engine**: The subsystem responsible for resolving certificate validity via public verification pages, the verification API, and verification logs.
- **Identity_Registry**: The subsystem that manages centralized Professional identity records, ensuring each Professional has a unique ProfessionalId.
- **Certificate_Wallet**: The worker-facing subsystem that displays certificate status, enables QR sharing, supports PDF download, and delivers notifications.
- **Issuer_Portal**: The training-provider-facing subsystem for creating certification templates, managing trainees, issuing certificates individually or in bulk, and viewing analytics.
- **Notification_System**: The subsystem responsible for delivering expiry reminders and issuance alerts via asynchronous event processing.
- **Professional**: A worker who holds one or more certificates on the platform.
- **Issuer**: A verified Training Provider organization authorized to issue certificates.
- **Verifier**: An Employer or Company that queries certificate validity before hiring or granting site access.
- **Platform_Admin**: A privileged operator responsible for onboarding organizations and maintaining system integrity.
- **QR_Code**: A machine-readable code embedded in a certificate that encodes a unique verification URL or token.
- **Certificate_Hash**: A SHA-256 digest computed over the Canonical_Serialization of certificate fields concatenated with the Certificate_Hash of the immediately preceding certificate in the Professional's chain (or the genesis value of 64 hexadecimal zeros for the first certificate), forming a tamper-evident append-only chain.
- **Canonical_Serialization**: A deterministic, UTF-8 encoded JSON representation of certificate fields in a fixed, alphabetically ordered key sequence with no optional whitespace, ensuring the same certificate data always produces the same byte sequence for hashing.
- **Verification_Store**: The Redis-backed read model holding denormalized CertificateVerificationView records for O(1) verification lookups.
- **CertificateVerificationView**: A denormalized read model record stored in the Verification_Store, containing CertificateId, TenantId, Status, ExpiryDate, ProfessionalName, and IssuerName for a single certificate.
- **VerificationLog**: An immutable record of a single certificate verification lookup, capturing CertificateId, TenantId, Timestamp, requesting IP address, and the Status returned.
- **Audit_Log**: An append-only record of every significant action performed on the platform, including actor, timestamp, entity, and metadata.
- **RBAC**: Role-Based Access Control; the authorization model governing what each actor (Admin, Issuer, Worker, Verifier) may do.
- **Event_Bus**: The asynchronous messaging infrastructure used to decouple heavy operations such as PDF generation, notification dispatch, and audit processing.
- **Status**: The lifecycle state of a certificate; one of Active, Expired, Revoked, or Tampered.
- **ProfessionalId**: The platform-assigned unique identifier for a Professional, used as the anchor for all certificates belonging to that individual.
- **Idempotency_Key**: A client-supplied or system-generated token that prevents duplicate processing of the same operation. For notifications, the Idempotency_Key is derived by computing SHA-256(EventId concatenated with NotificationType).
- **TenantId**: A platform-assigned unique identifier for an organization (Issuer or enterprise customer) that scopes all data and queries to that organization's isolated partition within the shared database.
- **Tenant**: An organization operating on the Certiva platform with its own isolated data scope, branding, and configuration.
- **TOTP**: Time-based One-Time Password; a time-limited MFA token generated per RFC 6238, accepted within a ±30-second validity window to account for clock skew.
- **SequenceNumber**: A monotonically increasing integer assigned to each domain event for a given CertificateId, used by the Verification_Engine to enforce ordered, exactly-once event application.

---

## Requirements

---

### Requirement 1: Professional Identity Registration

**User Story:** As a Training Provider, I want to register a Professional in the Identity Registry before issuing a certificate, so that every certificate is anchored to a verified, deduplicated identity.

#### Acceptance Criteria

1. WHEN a registration request is received with a unique NationalId, THE Identity_Registry SHALL create a Professional record containing a system-generated ProfessionalId, Name, Phone, Email, NationalId, and CreatedAt timestamp; WHERE NationalId is defined as an alphanumeric string between 6 and 20 characters inclusive.
2. WHEN a registration request is received with a NationalId that already exists in the Identity_Registry for the same TenantId, THE Identity_Registry SHALL return an HTTP 409 conflict response containing the existing ProfessionalId and SHALL NOT create a duplicate Professional record.
3. THE Identity_Registry SHALL enforce that Name (non-null, non-empty, maximum 100 characters), NationalId (6–20 alphanumeric characters), and at least one of Phone or Email (each non-null and non-empty) are present in every Professional record; IF any required field is absent or invalid, THE Identity_Registry SHALL return a descriptive validation error identifying every invalid field before persisting any data.
4. IF a registration request contains a malformed NationalId format (outside 6–20 alphanumeric characters), THEN THE Identity_Registry SHALL return a descriptive validation error identifying the invalid field and the expected format.
5. IF a registration request contains a Phone value that does not match E.164 format or an Email value that does not conform to RFC 5322, THEN THE Identity_Registry SHALL return a descriptive validation error identifying the invalid field.
6. WHEN a Professional record is created, THE Identity_Registry SHALL atomically publish a ProfessionalRegistered event to the Event_Bus via the transactional outbox, containing the new ProfessionalId and TenantId.
7. THE Identity_Registry SHALL mask NationalId values in all API responses by replacing all characters except the last four with asterisks; IF the NationalId is fewer than four characters, THE Identity_Registry SHALL mask the entire value; this masking rule applies to every endpoint that returns Professional data, including search results and analytics.

---

### Requirement 2: Issuer Onboarding and Verification

**User Story:** As a Platform Admin, I want to onboard and verify Training Provider organizations, so that only authorized Issuers can issue certificates on the platform.

#### Acceptance Criteria

1. WHEN a Platform_Admin submits an onboarding request for an organization, THE Identity_Registry SHALL create an Issuer record containing OrganizationName, Type, TenantId, and a VerificationStatus of Pending.
2. WHEN a Platform_Admin approves an Issuer, THE Identity_Registry SHALL update the Issuer's VerificationStatus to Verified and SHALL record the approval action in the Audit_Log, capturing the Admin's ActorId, the IssuerId, and the approval timestamp.
3. WHEN a Platform_Admin rejects an Issuer, THE Identity_Registry SHALL update the Issuer's VerificationStatus to Rejected and SHALL record the rejection action in the Audit_Log, capturing the Admin's ActorId, the IssuerId, and the rejection timestamp.
4. WHILE an Issuer's VerificationStatus is not Verified, THE Certification_Engine SHALL reject any certificate issuance request from that Issuer with an HTTP 403 authorization error.
5. THE Identity_Registry SHALL enforce that OrganizationName is unique across all Issuer records within the same TenantId using a case-insensitive comparison.
6. IF an onboarding request is submitted for an OrganizationName that already exists within the same TenantId (case-insensitive), THEN THE Identity_Registry SHALL return an HTTP 409 conflict response containing the existing IssuerId and SHALL NOT create a duplicate Issuer record.
7. IF a Platform_Admin attempts to approve an Issuer whose VerificationStatus is already Verified, or attempts to reject an Issuer whose VerificationStatus is already Rejected, THEN THE Identity_Registry SHALL return an HTTP 409 conflict response indicating the invalid state transition and SHALL NOT create a duplicate Audit_Log entry.
8. WHEN an Issuer's VerificationStatus transitions to Verified, THE Identity_Registry SHALL publish an IssuerApproved event to the Event_Bus via the transactional outbox, containing the IssuerId and TenantId; WHEN an Issuer's VerificationStatus transitions to Rejected, THE Identity_Registry SHALL publish an IssuerRejected event containing the IssuerId and TenantId.

---

### Requirement 3: Certification Type Definition

**User Story:** As a Training Provider, I want to define certification templates in the Issuer Portal, so that I can issue consistent, structured certificates to my trainees.

#### Acceptance Criteria

1. WHEN a verified Issuer submits a certification template, THE Issuer_Portal SHALL persist the template with a unique TemplateId, Name (maximum 100 characters), Description (maximum 500 characters), ValidityPeriodDays (a non-negative integer), and the IssuerId of the creating Issuer.
2. THE Issuer_Portal SHALL enforce that Name, ValidityPeriodDays (non-negative integer), and IssuerId are present in every certification template; IF any required field is missing or ValidityPeriodDays is negative, THEN THE Issuer_Portal SHALL return a descriptive validation error identifying every invalid field before persisting any data.
3. WHEN a certification template is created, THE Issuer_Portal SHALL make the template available for selection during individual and bulk certificate issuance exclusively to the Issuer identified by the template's IssuerId.
4. WHEN a verified Issuer updates a certification template, THE Issuer_Portal SHALL apply the update only to issuance requests received after the update is persisted and SHALL NOT retroactively alter previously issued certificates.
5. IF an unverified Issuer attempts to create or update a certification template, THEN THE Issuer_Portal SHALL return an HTTP 403 authorization error.
6. IF a verified Issuer submits a template with a Name that already exists for that IssuerId (case-insensitive comparison), THEN THE Issuer_Portal SHALL return an HTTP 409 conflict response, SHALL NOT persist the template to the database, and SHALL NOT create a duplicate template record.
7. WHEN a verified Issuer deactivates a certification template, THE Issuer_Portal SHALL prevent the template from being selected for new issuance requests and SHALL NOT alter certificates previously issued using that template.

---

### Requirement 4: Individual Certificate Issuance

**User Story:** As a Training Provider, I want to issue a certificate to a specific Professional, so that the Professional's credential is recorded on the platform and immediately verifiable.

#### Acceptance Criteria

1. WHEN a verified Issuer submits an issuance request with a valid ProfessionalId and TemplateId, THE Certification_Engine SHALL create a Certificate record containing a system-generated GUID/ULID, ProfessionalId, IssuerId, TenantId, Name, IssueDate, and Status of Active; WHERE the template's ValidityPeriodDays is greater than zero, THE Certification_Engine SHALL set ExpiryDate to IssueDate plus ValidityPeriodDays; WHERE the template's ValidityPeriodDays is zero, THE Certification_Engine SHALL set ExpiryDate to null, indicating the certificate does not expire.
2. THE Certification_Engine SHALL compute the Certificate_Hash as SHA-256(Canonical_Serialization of certificate fields concatenated with the Certificate_Hash of the most recently issued certificate for that Professional within the same TenantId, or the genesis value of 64 hexadecimal zeros for the first certificate), establishing a tamper-evident chain.
3. WHEN a certificate is issued, THE Certification_Engine SHALL atomically write the certificate to the PostgreSQL write model and persist a corresponding outbound event to the transactional outbox table, ensuring the write and event publication are never split across a failure boundary.
4. WHEN a CertificateIssued event is received, THE Verification_Engine SHALL upsert a CertificateVerificationView record in the Verification_Store containing CertificateId, TenantId, Status, ExpiryDate, ProfessionalName, and IssuerName.
5. WHEN an issuance request is received with an Idempotency_Key that matches a previously completed operation within the 24-hour TTL window, THE Certification_Engine SHALL return the existing certificate with an HTTP 200 response and SHALL NOT create a duplicate record.
6. IF an issuance request references a ProfessionalId that does not exist in the Identity_Registry for the requesting Issuer's TenantId, THEN THE Certification_Engine SHALL return an HTTP 404 not-found error.
7. IF an issuance request references a TemplateId that does not exist for the requesting Issuer's TenantId, THEN THE Certification_Engine SHALL return an HTTP 404 not-found error.
8. IF an issuance request references a TemplateId that does not belong to the requesting Issuer, THEN THE Certification_Engine SHALL return an HTTP 403 authorization error.
9. WHEN a certificate is issued, THE Certification_Engine SHALL publish a CertificateIssued event that triggers QR code generation and PDF generation asynchronously via the Event_Bus, and these async operations SHALL NOT block the issuance API response.
10. IF a verified Issuer attempts to issue a certificate to a ProfessionalId using a TemplateId for which an Active certificate already exists for that Professional, THEN THE Certification_Engine SHALL return an HTTP 409 conflict response containing the existing CertificateId and SHALL NOT create a duplicate certificate record.

---

### Requirement 5: Bulk Certificate Issuance

**User Story:** As a Training Provider, I want to issue certificates to multiple Professionals in a single operation, so that I can efficiently certify an entire cohort after a training session.

#### Acceptance Criteria

1. WHEN a verified Issuer submits a bulk issuance request containing a list of 1 to 1,000 ProfessionalIds and a TemplateId, THE Issuer_Portal SHALL enqueue the request on the Event_Bus for asynchronous processing.
2. IF a bulk issuance request contains an empty ProfessionalId list or more than 1,000 entries, THEN THE Issuer_Portal SHALL return an HTTP 422 descriptive validation error and SHALL NOT enqueue the request.
3. WHEN a bulk issuance job is enqueued, THE Issuer_Portal SHALL return a job reference identifier to the requesting Issuer within the synchronous API response and SHALL record the job with an initial status of Queued.
4. THE Issuer_Portal SHALL expose a polling endpoint that accepts a job reference identifier and returns the current job status (Queued, Processing, Completed, or Failed) and a progress summary (total entries, processed count, success count, failure count); WHERE no job reference was received by the Issuer, the endpoint SHALL also accept a combination of IssuerId and submission timestamp to retrieve job status.
5. WHEN the bulk issuance job is processed, THE Certification_Engine SHALL update the job status to Processing and SHALL issue a certificate for each valid ProfessionalId in the list, applying the same idempotency rules as individual issuance.
6. WHEN a bulk issuance job completes, THE Certification_Engine SHALL update the job status to Completed and SHALL persist a result report containing the count of successful issuances, the count of failures, and a list of failed ProfessionalIds each with a failure category (NotFound, AlreadyIssued, ValidationError, or AuthorizationError).
7. IF a single entry in a bulk issuance request fails validation, THEN THE Certification_Engine SHALL continue processing the remaining entries and SHALL NOT abort the entire batch.
8. WHEN a bulk issuance job fails to process all entries after exhausting retries, THE Certification_Engine SHALL update the job status to Failed, SHALL persist the partial result report, and SHALL record the failure in the Audit_Log containing the IssuerId, TemplateId, total entry count, and failure timestamp.

---

### Requirement 6: Certificate Lifecycle Management

**User Story:** As a Training Provider, I want to revoke a certificate when a Professional's credential is no longer valid, so that Verifiers receive accurate status information.

#### Acceptance Criteria

1. WHEN a verified Issuer submits a revocation request for a certificate the Issuer originally issued, THE Certification_Engine SHALL update the certificate Status to Revoked in the PostgreSQL write model and SHALL record the revocation in the Audit_Log, including the revocation reason (a non-empty string of maximum 500 characters) as metadata.
2. WHEN a certificate is revoked, THE Certification_Engine SHALL atomically publish a CertificateRevoked event to the transactional outbox.
3. WHEN a CertificateRevoked event is received, THE Verification_Engine SHALL update the corresponding CertificateVerificationView in the Verification_Store to reflect Status Revoked within 5 seconds of event publication under normal operating conditions.
4. THE Certification_Engine SHALL use soft deletion only; no Certificate record SHALL be permanently deleted from the PostgreSQL write model.
5. IF a revocation request is submitted for a certificate whose Status is already Revoked or Expired, THEN THE Certification_Engine SHALL return an HTTP 409 conflict response indicating the invalid state transition and SHALL NOT create a duplicate Audit_Log entry.
6. IF a verified Issuer submits a revocation request for a certificate issued by a different Issuer, THEN THE Certification_Engine SHALL return an HTTP 403 authorization error.
7. IF a revocation request references a CertificateId that does not exist within the requesting Issuer's TenantId, THEN THE Certification_Engine SHALL return an HTTP 404 not-found error.
8. WHEN a certificate's ExpiryDate is reached, a scheduled process running at an interval of at most 60 minutes SHALL update the certificate Status to Expired in the PostgreSQL write model and SHALL atomically publish a CertificateExpired event to the transactional outbox; certificates whose ExpiryDate falls within the current scheduler interval SHALL be transitioned within that interval's execution.
9. WHEN a CertificateExpired event is received, THE Verification_Engine SHALL update the corresponding CertificateVerificationView in the Verification_Store to reflect Status Expired within 5 seconds of event publication under normal operating conditions.
10. IF a revocation request is submitted without a revocation reason, THEN THE Certification_Engine SHALL return an HTTP 422 validation error identifying the missing field and SHALL NOT process the revocation.

---

### Requirement 7: QR Code Generation

**User Story:** As a Professional, I want each of my certificates to have a unique QR code, so that Verifiers can scan and instantly verify my credential.

#### Acceptance Criteria

1. WHEN a CertificateIssued event is received, THE Certification_Engine SHALL generate a QR_Code encoding a unique verification URL in the format `https://{domain}/verify/{CertificateId}`.
2. THE Certification_Engine SHALL store the QRCodeValue on the Certificate record in the PostgreSQL write model as both the verification URL string and a base64-encoded PNG image of the QR code.
3. WHEN a QR_Code is generated, THE Certification_Engine SHALL ensure the QRCodeValue URL is unique across all Certificate records within the same TenantId.
4. THE Certification_Engine SHALL generate QR codes asynchronously via the Event_Bus and SHALL NOT block the issuance API response on QR code generation completion.
5. WHEN a QR code generation job completes, THE Certification_Engine SHALL update the Certificate record in the PostgreSQL write model with the generated QRCodeValue and SHALL update the CertificateVerificationView in the Verification_Store to include the QRCodeValue.
6. IF a QR code generation job fails after exhausting all retries (up to 3 retries with exponential backoff starting at a 5-second initial delay), THE Certification_Engine SHALL record the failure in the Audit_Log containing the CertificateId and final failure timestamp, and SHALL mark the job as failed in the dead-letter queue.
7. THE Certification_Engine SHALL generate QR code images as PNG files with a minimum resolution of 200×200 pixels, with QR version auto-selected to encode the full verification URL without truncation.

---

### Requirement 8: PDF Certificate Generation

**User Story:** As a Professional, I want to download a PDF version of my certificate, so that I can present it in offline or printed contexts.

#### Acceptance Criteria

1. WHEN a CertificateIssued event is received and the QR_Code for the certificate is available, THE Certification_Engine SHALL generate a PDF certificate containing the Professional's Name, certificate Name, IssueDate, ExpiryDate (or "Does not expire" if ExpiryDate is null), IssuerName, and an embedded QR_Code image, and SHALL store the generated PDF associated with the CertificateId.
2. THE Certification_Engine SHALL generate PDF certificates asynchronously via the Event_Bus and SHALL NOT block the issuance API response on PDF generation completion.
3. WHEN a PDF generation job fails due to a PDF rendering error, THE Certification_Engine SHALL retry the job up to 3 times with exponential backoff starting at a 5-second initial delay (yielding delays of approximately 5 s, 25 s, and 125 s); after exhausting all retries, THE Certification_Engine SHALL mark the job as failed and record the failure in the Audit_Log containing the CertificateId and final failure timestamp; WHEN a PDF generation job fails due to an infrastructure or timeout error, THE Certification_Engine SHALL retry the job indefinitely using the same backoff schedule until the infrastructure recovers, and SHALL NOT mark the job as failed.
4. WHEN an authenticated Professional requests a PDF download for a certificate belonging to their ProfessionalId, THE Certificate_Wallet SHALL return the most recently generated PDF file; IF the requesting Professional's ProfessionalId does not match the certificate's ProfessionalId, THEN THE Certificate_Wallet SHALL return an HTTP 403 authorization error.
5. WHEN a PDF has been generated for a certificate, THE Certificate_Wallet SHALL make the PDF available for download via a signed, time-limited URL expiring after 15 minutes; IF a PDF has not yet been generated, THEN THE Certificate_Wallet SHALL return an HTTP 202 response containing the job state (Pending, Processing, or Failed) and the CertificateId.

---

### Requirement 9: Public Certificate Verification

**User Story:** As an Employer, I want to verify a worker's certificate by scanning a QR code or entering a certificate ID, so that I can confirm credential validity before hiring or granting site access.

#### Acceptance Criteria

1. WHEN a verification request is received with a valid CertificateId, THE Verification_Engine SHALL resolve the CertificateVerificationView from the Verification_Store and SHALL return Status, ExpiryDate, ProfessionalName, and IssuerName within 50 milliseconds under a load of up to 50 concurrent requests.
2. WHEN a verification request is received and the CertificateVerificationView is not present in the Verification_Store, THE Verification_Engine SHALL fall back to the authoritative data source and SHALL return the result within 500 milliseconds.
3. WHEN a verification request is processed, THE Verification_Engine SHALL record a VerificationLog entry containing CertificateId, Timestamp, requesting IP address, and the returned Status.
4. THE Verification_Engine SHALL expose a public verification endpoint that does not require authentication, so that any Verifier can check certificate validity without a platform account.
5. IF a verification request references a CertificateId that does not exist, THEN THE Verification_Engine SHALL return an HTTP 404 not-found response containing an error indicator and the queried CertificateId, and SHALL record the attempted lookup in the Audit_Log.
6. THE Verification_Engine SHALL apply rate limiting of 100 requests per minute per IP address to the public verification endpoint; IF the rate limit is exceeded, THEN THE Verification_Engine SHALL return an HTTP 429 too-many-requests response.
7. WHEN a certificate's Status is Revoked or Expired, THE Verification_Engine SHALL return a response containing a `valid` field set to `false` and the current Status value, and SHALL NOT return a `valid` field set to `true`.

---

### Requirement 10: Verification Store Consistency

**User Story:** As a Platform Admin, I want the Redis verification store to remain consistent with the PostgreSQL write model, so that Verifiers always receive accurate certificate status.

#### Acceptance Criteria

1. WHEN the Verification_Store is unavailable, THE Verification_Engine SHALL fall back to the PostgreSQL write model for all verification requests and SHALL NOT return an error to the Verifier.
2. WHEN the Verification_Store is restored after an outage (confirmed by a successful health check), THE Verification_Engine SHALL begin resynchronizing all CertificateVerificationView records from the PostgreSQL write model; WHEN the 5-minute resynchronization deadline is met, THE Verification_Engine SHALL resume reads from the Verification_Store regardless of whether resynchronization has fully completed; IF resynchronization fails, THE Verification_Engine SHALL continue serving requests from the PostgreSQL write model and SHALL retry resynchronization periodically (every 30 minutes) until it succeeds.
3. WHEN a conflict is detected between a CertificateVerificationView record in the Verification_Store and the corresponding record in the PostgreSQL write model, THE Verification_Engine SHALL overwrite the Verification_Store record with the PostgreSQL write model value.
4. WHEN a CertificateVerificationView record is written or updated in the Verification_Store, THE Verification_Engine SHALL set or reset a time-to-live of 1 hour on the record; explicit event-driven invalidation (CertificateRevoked, CertificateExpired) is the primary consistency mechanism, and the TTL serves only as a safety net against stale data.
5. WHEN a CertificateVerificationView record expires from the Verification_Store, THE Verification_Engine SHALL repopulate the record from the PostgreSQL write model on the next verification request for that CertificateId.

---

### Requirement 11: Certificate Wallet

**User Story:** As a Professional, I want to view all my certificates in one place, see their current status, and share or download them, so that I can manage and present my credentials easily.

#### Acceptance Criteria

1. WHEN an authenticated Professional requests their certificate list, THE Certificate_Wallet SHALL return all certificates associated with the Professional's ProfessionalId, including Name, Status, IssueDate, ExpiryDate, and IssuerName for each certificate.
2. WHEN an authenticated Professional requests a specific certificate by CertificateId, THE Certificate_Wallet SHALL return the full certificate detail including Name, Status, IssueDate, ExpiryDate, IssuerName, QRCodeValue, and a PDF download link.
3. WHEN an authenticated Professional requests a shareable link for a certificate, THE Certificate_Wallet SHALL return the public verification URL for that certificate in the format `https://{domain}/verify/{CertificateId}`.
4. WHEN an authenticated Professional requests their certificate list, THE Certificate_Wallet SHALL return certificates grouped by Status (Active, Expired, Revoked) and sorted by IssueDate descending within each group.
5. WHILE a Professional has certificates with an ExpiryDate within 30 days inclusive of today, THE Certificate_Wallet SHALL include a distinct expiry warning indicator field on those certificates in the response.
6. IF an authenticated Professional requests a certificate by CertificateId that does not belong to their ProfessionalId, THEN THE Certificate_Wallet SHALL return an HTTP 403 authorization error.
7. WHEN an authenticated Professional requests their certificate list and no certificates exist for their ProfessionalId, THE Certificate_Wallet SHALL return an empty list with an HTTP 200 response; IF certificates exist for the Professional but cannot be returned due to system errors or filtering, THE Certificate_Wallet SHALL return an appropriate non-200 error status code rather than an empty list.

---

### Requirement 12: Notification System

**User Story:** As a Professional, I want to receive alerts when a certificate is issued to me and reminders before my certificates expire, so that I can stay informed about my credential status.

#### Acceptance Criteria

1. WHEN a CertificateIssued event is received for a Professional who has a registered Email, THE Notification_System SHALL dispatch an issuance alert to that Email within 5 minutes of the event.
2. IF a Professional does not have a registered Email at the time a notification is triggered, THEN THE Notification_System SHALL skip the dispatch and SHALL record a skipped-notification entry in the Audit_Log containing the ProfessionalId, event type, and timestamp.
3. WHEN a certificate's ExpiryDate is exactly 30 days from the current date (evaluated daily), THE Notification_System SHALL dispatch an expiry reminder to the Professional's registered Email.
4. WHEN a certificate's ExpiryDate is exactly 7 days from the current date (evaluated daily), THE Notification_System SHALL dispatch a second expiry reminder to the Professional's registered Email.
5. WHEN a CertificateRevoked event is received for a Professional who has a registered Email, THE Notification_System SHALL dispatch a revocation alert to that Email within 5 minutes of the event.
6. IF a notification dispatch fails, THEN THE Notification_System SHALL retry the dispatch up to 3 times with exponential backoff starting at a 5-second initial delay and capped at 60 seconds.
7. WHEN all retry attempts for a notification dispatch have been exhausted without success, THE Notification_System SHALL record a single failure entry in the Audit_Log containing the ProfessionalId, notification type, event identifier, and final failure timestamp.
8. THE Notification_System SHALL ensure that duplicate notifications for the same event and Professional are not dispatched, using an Idempotency_Key derived from the event identifier and notification type.

---

### Requirement 13: Issuer Portal Analytics

**User Story:** As a Training Provider, I want to view issuance analytics in the Issuer Portal, so that I can track certification activity and monitor my trainees' credential status.

#### Acceptance Criteria

1. WHEN an authenticated Issuer accesses the analytics dashboard, THE Issuer_Portal SHALL return the total count of certificates issued by that Issuer, broken down by Status (Active, Expired, Revoked).
2. WHEN an authenticated Issuer accesses the analytics dashboard, THE Issuer_Portal SHALL return the count of certificates issued per calendar month for the trailing 12 calendar months relative to the current date.
3. WHEN an authenticated Issuer searches for a Professional by Name (case-insensitive partial match) or NationalId (exact match), THE Issuer_Portal SHALL return all certificates issued by that Issuer to matching Professionals; WHEN no matching certificates exist, THE Issuer_Portal SHALL return an empty result set with an HTTP 200 response.
4. THE Issuer_Portal SHALL restrict all analytics data to certificates issued by the requesting Issuer and SHALL NOT expose data belonging to other Issuers; this data restriction SHALL be enforced on every request regardless of whether the request is ultimately rejected with an HTTP 403 response.
5. IF an unauthenticated or unauthorized request is made to the analytics dashboard, THEN THE Issuer_Portal SHALL return an HTTP 401 or HTTP 403 response respectively, and SHALL NOT include any analytics data in the error response.

---

### Requirement 14: Audit Logging

**User Story:** As a Platform Admin, I want a complete, tamper-evident audit trail of all significant actions, so that I can support regulatory reporting and investigate incidents.

#### Acceptance Criteria

1. THE Audit_Log SHALL record every certificate issuance, revocation, expiry transition, verification lookup, Issuer approval, and Issuer rejection, capturing ActionType, EntityId, Timestamp, Actor, and a Metadata JSON payload not exceeding 10 KB per record.
2. THE Audit_Log SHALL be append-only; no Audit_Log record SHALL be updated or deleted after creation.
3. THE Audit_Log SHALL attach a cryptographic hash to each record at write time, computed over the record's fields, such that any subsequent modification to the stored record is detectable by recomputing and comparing the hash.
4. WHEN a Platform_Admin queries the Audit_Log, THE Audit_Log SHALL support filtering by ActionType, EntityId, Actor, and a date range, and SHALL return results paginated in pages of at most 100 records per response.
5. WHEN a Platform_Admin exports audit records, THE Audit_Log SHALL return all matching records in CSV format containing at minimum the ActionType, EntityId, Timestamp, Actor, and Metadata fields.
6. THE Audit_Log SHALL record all failed authentication attempts, including the requesting IP address and timestamp.
7. IF writing an Audit_Log record fails, THEN THE system SHALL reject the originating action with an error response and SHALL NOT commit the originating action without a corresponding log entry.

---

### Requirement 15: Authentication and Authorization

**User Story:** As a Platform Admin, I want all platform actors to authenticate with JWT tokens and be authorized by role, so that access to sensitive operations is controlled and auditable.

#### Acceptance Criteria

1. WHEN a user submits valid credentials, THE Identity_Registry SHALL issue a JWT access token with a 15-minute expiry and a refresh token with a 7-day expiry.
2. IF a user submits invalid credentials (incorrect username or password), THEN THE Identity_Registry SHALL return an HTTP 401 unauthorized response and SHALL record the failed attempt in the Audit_Log including the requesting IP address and timestamp.
3. WHEN a JWT access token expires, THE Identity_Registry SHALL accept a valid refresh token and SHALL issue a new JWT access token and a new refresh token, invalidating the previous refresh token.
4. IF a refresh token is invalid or expired, THEN THE Identity_Registry SHALL return an HTTP 401 unauthorized response and SHALL invalidate all tokens associated with that session.
5. IF a request is received with an expired or invalid JWT, THEN THE Identity_Registry SHALL return an HTTP 401 unauthorized response and SHALL record the failed attempt in the Audit_Log.
6. IF a user with the Admin role attempts to access a non-Admin-restricted endpoint, THE Identity_Registry SHALL permit the request subject to other role checks.
7. IF a user without the Admin role attempts to approve or reject an Issuer, THEN THE Identity_Registry SHALL return an HTTP 403 forbidden response.
8. IF a user without the Issuer role attempts to issue or revoke a certificate, THEN THE Identity_Registry SHALL return an HTTP 403 forbidden response.
9. IF a user without the Worker role attempts to access the Certificate_Wallet, THEN THE Identity_Registry SHALL return an HTTP 403 forbidden response.
10. WHERE multi-factor authentication is enabled for an account, THE Identity_Registry SHALL require a valid time-based MFA token (accepted within a 30-second validity window) in addition to credentials before issuing a JWT.
11. IF a user with MFA enabled submits an invalid or expired MFA token, THEN THE Identity_Registry SHALL return an HTTP 401 unauthorized response and SHALL NOT issue a JWT.
12. THE Identity_Registry SHALL apply rate limiting of 10 failed authentication attempts per 15-minute window per source IP address; WHEN the limit is exceeded, THE Identity_Registry SHALL return an HTTP 429 too-many-requests response and SHALL block further attempts from that IP for the remainder of the 15-minute window.

---

### Requirement 16: Data Security and Privacy

**User Story:** As a Platform Admin, I want all sensitive data to be encrypted and masked appropriately, so that the platform meets security and privacy obligations.

#### Acceptance Criteria

1. THE Identity_Registry SHALL encrypt all data in transit using TLS 1.2 or higher.
2. THE Identity_Registry SHALL encrypt sensitive fields at rest, including NationalId and contact information, using AES-256.
3. THE Identity_Registry SHALL mask NationalId values in all API responses by replacing all characters except the last four with asterisks; IF the NationalId is fewer than four characters, THE Identity_Registry SHALL mask the entire value.
4. THE Certification_Engine SHALL mask full Professional contact details in verification API responses, returning only the Professional's Name, CertificateId, IssueDate, and ExpiryDate to Verifiers.
5. WHEN a security event such as 5 or more failed authentication attempts from the same IP within 60 seconds is detected, THE Identity_Registry SHALL record the event in the Audit_Log with the requesting IP address, timestamp, and attempted action.
6. IF writing a security event to the Audit_Log fails, THEN THE Identity_Registry SHALL reject the originating request with an error response and SHALL NOT proceed without a corresponding security event log entry; for non-security audit log write failures, THE system SHALL not reject the originating action.

---

### Requirement 17: System Reliability and Idempotency

**User Story:** As a Platform Admin, I want all write operations to be idempotent and the system to recover gracefully from partial failures, so that data integrity is maintained under load and during outages.

#### Acceptance Criteria

1. WHEN any certificate issuance, revocation, or status update request is received with an Idempotency_Key that matches a previously completed operation (within a 24-hour TTL window), THE Certification_Engine SHALL return the result of the original operation and SHALL NOT perform the operation again; IF the matching operation is still in progress, THE Certification_Engine SHALL return an HTTP 202 response with a pending status indicator.
2. WHEN a background worker job fails, THE Certification_Engine SHALL retry the job using exponential backoff with a 1-second base interval and a 60-second maximum delay, up to a maximum of 3 retries, before moving the job to a dead-letter queue and recording the failure in the Audit_Log.
3. WHEN a state-change event is received by the Verification_Engine, THE Verification_Engine SHALL apply the event only if its sequence number is the next expected value for that CertificateId, ensuring ordered processing.
4. WHEN the Event_Bus is temporarily unavailable, THE Certification_Engine SHALL persist outbound events to a local outbox table in PostgreSQL and SHALL replay them when the Event_Bus becomes available.
5. WHEN concurrent issuance requests for the same ProfessionalId and TemplateId on the same IssueDate are detected, THE Certification_Engine SHALL ensure exactly one certificate record is created, enforced through database-level uniqueness constraints; duplicate callers SHALL receive the existing certificate record with a conflict indicator.
6. IF an event is received with a sequence number that is not the next expected value for that CertificateId, THEN THE Verification_Engine SHALL discard the out-of-order event and SHALL record the discarded event in the Audit_Log.

---

### Requirement 18: Observability and Monitoring

**User Story:** As a Platform Admin, I want structured logs, metrics, and distributed traces, so that I can monitor system health and diagnose issues in production.

#### Acceptance Criteria

1. THE Certification_Engine SHALL emit JSON-formatted structured log entries for every certificate issuance, revocation, and expiry transition, including CertificateId, ProfessionalId, IssuerId, Timestamp, and operation type.
2. THE Verification_Engine SHALL record the verification response latency for every request and SHALL expose this metric via a Prometheus-compatible scrape endpoint.
3. THE Certification_Engine SHALL expose the following metrics via a Prometheus-compatible scrape endpoint: issuance rate (certificates per minute), verification latency (p50, p95, p99), and issuance and revocation API response time (p50, p95, p99).
4. WHEN a verification request exceeds 50 milliseconds from the Verification_Store, THE Verification_Engine SHALL emit a latency-exceeded log entry containing RequestId, measured latency, the 50ms threshold, and timestamp; WHEN a verification request exceeds 500 milliseconds from the PostgreSQL fallback, THE Verification_Engine SHALL emit a separate latency-exceeded log entry containing RequestId, measured latency, the 500ms threshold, and timestamp; these two log entries SHALL be emitted independently based on each store's individual threshold, regardless of the other store's availability or measurement state; WHEN a request exceeds both thresholds, two separate log entries SHALL be emitted.
5. THE Certification_Engine SHALL propagate a distributed trace identifier across all synchronous and asynchronous operations — from the initial issuance API request through the Event_Bus, background workers, Verification_Store writes, and PostgreSQL writes — so that a single issuance flow can be traced end-to-end.

---

### Requirement 19: Certificate Hash Integrity (Round-Trip and Tamper Evidence)

**User Story:** As a Platform Admin, I want certificate hashes to form a verifiable chain, so that any tampering with historical certificate data can be detected.

#### Acceptance Criteria

1. WHEN a certificate is issued, THE Certification_Engine SHALL compute Certificate_Hash as SHA-256(canonical serialization of certificate fields concatenated with the Certificate_Hash of the immediately preceding certificate for that Professional, or the genesis value of 64 hexadecimal zeros for the first certificate).
2. THE Certification_Engine SHALL serialize certificate fields in a fixed, deterministic field order with no optional whitespace or locale-dependent formatting when computing the Certificate_Hash, so that the same certificate data always produces the same hash.
3. WHEN a hash verification request is received for a CertificateId, THE Certification_Engine SHALL recompute the expected hash from stored data and SHALL return a response containing a match/mismatch status, the stored hash, and the recomputed hash.
4. THE Certification_Engine SHALL ensure that for every certificate in a Professional's chain, recomputing the Certificate_Hash from stored data and the preceding certificate's hash produces a value equal to the stored Certificate_Hash; tampering with any certificate in the chain invalidates the hashes of all subsequent certificates.
5. IF the stored Certificate_Hash for any certificate does not match the recomputed hash, THEN THE Certification_Engine SHALL update the certificate's Status to Tampered in the PostgreSQL write model and SHALL record a discrepancy entry in the Audit_Log containing the CertificateId, stored hash, recomputed hash, and detection timestamp.

---

### Requirement 20: Deployment and Operational Readiness

**User Story:** As a Platform Admin, I want the platform to be deployable via Docker and Kubernetes with CI/CD pipelines, so that releases are automated, repeatable, and support zero-downtime deployments.

#### Acceptance Criteria

1. THE Certification_Engine SHALL be packaged as a Docker container image and SHALL be deployable to a Kubernetes cluster without manual configuration steps beyond environment variable injection.
2. THE Certification_Engine SHALL support blue-green deployment with a connection-drain window of at least 30 seconds, such that a new version can be deployed and traffic shifted without dropping in-flight requests.
3. WHEN the Certification_Engine is operational and all critical dependencies including the PostgreSQL write model are reachable, THE liveness probe endpoint SHALL return an HTTP 2xx response; IF a critical dependency is unreachable, THE liveness probe endpoint SHALL return an HTTP 5xx response.
4. WHEN the Certification_Engine has not yet completed startup initialization including Verification_Store connectivity checks and has not verified that the engine is properly configured and operational, THE readiness probe endpoint SHALL return an HTTP non-2xx response; WHEN initialization is complete, all critical dependencies are reachable, and the engine is properly configured and operational within a 60-second startup timeout, THE readiness probe endpoint SHALL return an HTTP 2xx response.
5. THE Certification_Engine SHALL source all environment-specific parameters (database connection strings, Redis endpoints, Event_Bus connection strings, JWT signing keys) exclusively from environment variables or mounted Kubernetes secrets, with no hardcoded values in the container image.
6. IF a required environment variable or Kubernetes secret is absent at startup, THEN THE Certification_Engine SHALL log a descriptive error identifying the missing parameter and SHALL terminate with a non-zero exit code rather than starting in a misconfigured state.

---

### Requirement 21: Multi-Tenancy Data Isolation

**User Story:** As a Platform Admin, I want all platform data to be scoped to a TenantId, so that each organization's data is fully isolated and the system can support multiple tenants on a shared database without data leakage.

#### Acceptance Criteria

1. THE Identity_Registry, Certification_Engine, Verification_Engine, Issuer_Portal, Certificate_Wallet, Notification_System, and Audit_Log SHALL include a TenantId column on every entity table (Professionals, Certificates, Issuers, CertificateTemplates, AuditLogs, VerificationLogs, NotificationLogs).
2. WHEN any query is executed against a tenant-scoped table, THE system SHALL include a TenantId filter matching the authenticated actor's TenantId, and SHALL NOT return records belonging to a different TenantId.
3. WHEN a certificate issuance, revocation, or verification request is processed, THE Certification_Engine SHALL validate that the ProfessionalId, IssuerId, and TemplateId all belong to the same TenantId as the authenticated actor; IF any entity belongs to a different TenantId, THE Certification_Engine SHALL return an HTTP 403 authorization error.
4. THE Verification_Store SHALL include TenantId in the CertificateVerificationView key structure so that verification lookups are always tenant-scoped.
5. THE Audit_Log SHALL record the TenantId on every log entry so that audit queries can be filtered and exported per tenant.
6. IF a request is received without a resolvable TenantId (missing or unrecognized tenant context), THEN THE system SHALL return an HTTP 400 bad request response and SHALL NOT process the request; IF a request has valid tenant context but attempts to access data belonging to a different TenantId, THEN THE system SHALL return an HTTP 403 forbidden response.
7. THE system SHALL enforce TenantId isolation at the application layer on every database query; reliance on database-level row security alone is insufficient.

---

### Requirement 22: API Versioning

**User Story:** As a Platform Admin, I want all API endpoints to be versioned from day one, so that future changes can be made without breaking existing integrations with government systems or enterprise clients.

#### Acceptance Criteria

1. ALL API endpoints SHALL be prefixed with a version segment in the format `/api/v{major}`, beginning with `/api/v1/` for all Phase 1 endpoints.
2. WHEN a breaking change is introduced to an existing endpoint, THE system SHALL introduce the change under a new version prefix (e.g., `/api/v2/`) and SHALL continue serving the previous version for a minimum deprecation period of 12 months.
3. WHEN a deprecated API version is called, THE system SHALL include a `Deprecation` response header containing the deprecation date and a `Sunset` response header containing the planned removal date.
4. THE system SHALL maintain backward compatibility within a major version; additive changes (new optional fields, new endpoints) SHALL be made within the existing version without incrementing the major version.
5. IF a request is received for an API version that has been removed, THEN THE system SHALL return an HTTP 410 Gone response with a message indicating the version is no longer supported and referencing the current supported version.
6. THE system SHALL expose an `/api/versions` endpoint that returns the list of currently supported API versions, their status (Active or Deprecated), and their sunset dates.
