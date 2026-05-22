# Certiva Backend API

A multi-tenant, event-driven certificate issuance and verification platform built with ASP.NET Core 8, Entity Framework Core, MassTransit/RabbitMQ, Redis, and PostgreSQL.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Running Locally](#running-locally)
- [Authentication](#authentication)
- [API Reference](#api-reference)
  - [Auth](#auth-apiv1auth)
  - [Professionals](#professionals-apiv1professionals)
  - [Issuers](#issuers-apiv1issuers)
  - [Templates](#templates-apiv1templates)
  - [Certificates](#certificates-apiv1certificates)
  - [Wallet](#wallet-apiv1wallet)
  - [Verification](#verification-apiv1verify)
  - [Analytics](#analytics-apiv1analytics)
  - [Audit Log](#audit-log-apiv1audit)
- [Event-Driven Flow](#event-driven-flow)
- [Error Responses](#error-responses)
- [Health Endpoints](#health-endpoints)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Certiva.Api  (HTTP)                          │
│  AuthController  ProfessionalsController  CertificatesController ...  │
└───────────────────────────┬──────────────────────────────────────────┘
                            │  Scoped Services
         ┌──────────────────┼─────────────────────────────────────────┐
         │                  │                                         │
  IdentityRegistry  CertificationEngine   IssuerPortal   ...modules   │
         │                  │                                         │
         └──────────────────┴─────────────────────────────────────────┘
                            │  Repository Pattern
                   ┌────────┴────────┐
                   │  Infrastructure  │
                   │  PostgreSQL (EF) │
                   │  Redis           │
                   │  RabbitMQ        │
                   └─────────────────┘
```

### Modules

| Module | Responsibility |
|---|---|
| `Certiva.IdentityRegistry` | Professional & Issuer onboarding, JWT auth, MFA/TOTP |
| `Certiva.CertificationEngine` | Certificate issuance, revocation, hash verification, bulk jobs |
| `Certiva.IssuerPortal` | Template CRUD, analytics, professional search |
| `Certiva.CertificateWallet` | Professional's certificate wallet, PDF/share links |
| `Certiva.VerificationEngine` | Public certificate verification (Redis-first, PostgreSQL fallback) |
| `Certiva.AuditLog` | Immutable append-only audit log with CSV export |
| `Certiva.NotificationSystem` | Email dispatch on issuance and revocation events |
| `Certiva.Infrastructure` | Entities, repositories, events, outbox, encryption, multi-tenancy |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 14+
- Redis 7+
- RabbitMQ 3.12+ (or Docker Compose stack below)

---

## Configuration

All configuration is in `src/Certiva.Api/appsettings.json`. The following environment variables are **required** in Production and validated at startup:

| Environment Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Redis__ConnectionString` | Redis connection string |
| `RabbitMQ__Host` | RabbitMQ host |
| `Jwt__SigningKey` | HS256 signing key (≥32 chars) |
| `Encryption__Key` | Base64-encoded 32-byte AES-256 key |

### Full `appsettings.json` reference

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=certiva;Username=certiva;Password=secret"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  },
  "Jwt": {
    "Issuer": "certiva",
    "Audience": "certiva-api",
    "SigningKey": "your-super-secret-signing-key-min-32-chars",
    "ExpiryMinutes": 60,
    "RefreshExpiryDays": 30
  },
  "Encryption": {
    "Key": "<base64-of-32-random-bytes>"
  },
  "App": {
    "Domain": "https://certiva.app",
    "SignedUrlHmacKey": "your-hmac-secret-for-pdf-signed-urls"
  },
  "Storage": {
    "BasePath": "/var/certiva/pdfs"
  }
}
```

---

## Running Locally

```bash
# 1. Start infrastructure
docker compose up -d   # PostgreSQL, Redis, RabbitMQ

# 2. Apply migrations
dotnet ef database update --project src/Certiva.Infrastructure --startup-project src/Certiva.Api

# 3. Run the API
dotnet run --project src/Certiva.Api
```

The API starts on `https://localhost:5001` / `http://localhost:5000`.

---

## Authentication

The API uses **JWT Bearer** tokens with optional **TOTP/MFA**.

1. Call `POST /api/v1/auth/login` with credentials → receive `accessToken` + `refreshToken`.
2. Include the access token on all subsequent requests:
   ```
   Authorization: Bearer <accessToken>
   ```
3. When the access token expires, call `POST /api/v1/auth/refresh` with the refresh token.

### Authorization Policies

| Policy | Who |
|---|---|
| `Admin` | Platform administrators — issuer approval, professional registration |
| `Issuer` | Verified issuers — templates, certificates, bulk jobs, analytics |
| `Worker` | Professionals — wallet access |
| _(anonymous)_ | Login, refresh, public verification, PDF download |

---

## API Reference

### Auth `/api/v1/auth`

#### POST `/api/v1/auth/login`

Authenticate and receive tokens.

**Request**
```json
{
  "email": "alice@example.com",
  "password": "P@ssword123",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totpCode": "123456"
}
```

> `totpCode` is only required if the professional has MFA enabled.

**Response `200 OK`**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4e5f6...",
  "expiresAt": "2026-05-22T13:00:00Z"
}
```

**Errors**

| Status | Meaning |
|---|---|
| 400 | Missing or malformed fields |
| 401 | Wrong credentials or TOTP code |

---

#### POST `/api/v1/auth/refresh`

Exchange a refresh token for new token pair.

**Request**
```json
{
  "refreshToken": "a1b2c3d4e5f6...",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response `200 OK`** — same shape as `/login`.

---

#### DELETE `/api/v1/auth/session`

Revoke the current refresh token (logout). Requires `Authorization` header.

**Request**
```json
{
  "refreshToken": "a1b2c3d4e5f6...",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response `204 No Content`**

---

### Professionals `/api/v1/professionals`

#### POST `/api/v1/professionals`

Register a new professional. Requires **Admin** policy.

**Request**
```json
{
  "name": "Alice Banda",
  "nationalId": "NRC123456",
  "phone": "+260971000001",
  "email": "alice@example.com"
}
```

> `phone` and `email` are optional. `nationalId` is AES-256 encrypted; a SHA-256 hash is stored for duplicate detection.

**Response `201 Created`**
```json
{
  "professionalId": "a1b2c3d4-0000-0000-0000-000000000001",
  "isNew": true
}
```

> `isNew: false` means a professional with the same national ID already exists — the existing ID is returned (idempotent upsert).

---

#### GET `/api/v1/professionals/search?q={query}`

Search professionals by name. Requires **Issuer** policy.

**Query Parameters**

| Param | Type | Required | Description |
|---|---|---|---|
| `q` | string | Yes | Partial name search (case-insensitive) |

**Response `200 OK`**
```json
[
  {
    "professionalId": "a1b2c3d4-0000-0000-0000-000000000001",
    "name": "Alice Banda",
    "certificateId": "b2c3d4e5-0000-0000-0000-000000000002",
    "certificateName": "Nursing License",
    "status": "Active",
    "issueDate": "2025-01-15"
  }
]
```

---

### Issuers `/api/v1/issuers`

All endpoints require **Admin** policy.

#### POST `/api/v1/issuers`

Onboard a new issuer organization.

**Request**
```json
{
  "organizationName": "Health Professionals Council",
  "contactEmail": "admin@hpc.gov",
  "issuerType": "RegulatoryBody"
}
```

> `contactEmail` and `issuerType` are optional. Organization names are unique per tenant.

**Response `201 Created`**
```json
{
  "issuerId": "c3d4e5f6-0000-0000-0000-000000000003"
}
```

---

#### POST `/api/v1/issuers/{id}/approve`

Approve a pending issuer.

**Response `204 No Content`**

---

#### POST `/api/v1/issuers/{id}/reject`

Reject a pending issuer with a reason.

**Request**
```json
{
  "reason": "Insufficient documentation provided."
}
```

**Response `204 No Content`**

---

### Templates `/api/v1/templates`

All endpoints require **Issuer** policy.

#### POST `/api/v1/templates`

Create a certificate template.

**Request**
```json
{
  "name": "Annual Nursing License",
  "validityPeriodDays": 365
}
```

> `validityPeriodDays: 0` means the certificate never expires.

**Response `201 Created`**
```json
{
  "templateId": "d4e5f6a7-0000-0000-0000-000000000004"
}
```

---

#### PUT `/api/v1/templates/{id}`

Update an existing template.

**Request**
```json
{
  "name": "Annual Nursing License v2",
  "validityPeriodDays": 730
}
```

**Response `204 No Content`**

---

#### DELETE `/api/v1/templates/{id}`

Deactivate (soft-delete) a template.

**Response `204 No Content`**

---

### Certificates `/api/v1/certificates`

All endpoints require **Issuer** policy unless noted.

#### POST `/api/v1/certificates`

Issue a certificate to a professional.

**Headers**

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes | `Bearer <accessToken>` |
| `X-Idempotency-Key` | Yes | UUID — prevents duplicate issuance on retry |

**Request**
```json
{
  "professionalId": "a1b2c3d4-0000-0000-0000-000000000001",
  "templateId": "d4e5f6a7-0000-0000-0000-000000000004"
}
```

**Response `201 Created`**
```json
{
  "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
  "isNew": true
}
```

> `isNew: false` is returned when the same `X-Idempotency-Key` is replayed — the original certificate ID is returned safely.

**Async side effects** (triggered via outbox → RabbitMQ):
1. QR code generated and stored on the certificate
2. PDF certificate rendered and stored
3. Email notification dispatched to the professional
4. Verification store (Redis) updated

---

#### POST `/api/v1/certificates/{id}/revoke`

Revoke an issued certificate.

**Request**
```json
{
  "revocationReason": "Licence expired due to non-renewal."
}
```

**Response `204 No Content`**

---

#### POST `/api/v1/certificates/{id}/verify-hash`

Recompute and compare the certificate's tamper-evident hash.

**Response `200 OK`**
```json
{
  "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
  "isValid": true,
  "storedHash": "a1b2c3d4e5f6...",
  "recomputedHash": "a1b2c3d4e5f6..."
}
```

> `isValid: false` means the certificate record was tampered with outside the API. The status is updated to `Tampered`.

---

#### POST `/api/v1/certificates/bulk`

Enqueue a bulk issuance job (up to 1000 professionals per job).

**Request**
```json
{
  "templateId": "d4e5f6a7-0000-0000-0000-000000000004",
  "professionalIds": [
    "a1b2c3d4-0000-0000-0000-000000000001",
    "a1b2c3d4-0000-0000-0000-000000000002"
  ]
}
```

**Response `202 Accepted`**
```json
{
  "jobId": "f6a7b8c9-0000-0000-0000-000000000006"
}
```

---

#### GET `/api/v1/certificates/bulk/{jobId}`

Poll bulk issuance job status.

**Response `200 OK`**
```json
{
  "jobId": "f6a7b8c9-0000-0000-0000-000000000006",
  "status": "Processing",
  "totalCount": 100,
  "processedCount": 47,
  "successCount": 45,
  "failureCount": 2,
  "submittedAt": "2026-05-22T08:00:00Z",
  "completedAt": null
}
```

> `status` values: `Queued`, `Processing`, `Completed`, `Failed`

---

### Wallet `/api/v1/wallet`

All endpoints require **Worker** policy (professional's own JWT).

#### GET `/api/v1/wallet/certificates`

Retrieve all certificates in the professional's wallet, grouped by status.

**Response `200 OK`**
```json
{
  "active": [
    {
      "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
      "name": "Annual Nursing License",
      "issuerName": "Health Professionals Council",
      "issueDate": "2025-01-15",
      "expiryDate": "2026-01-15"
    }
  ],
  "expired": [],
  "revoked": []
}
```

---

#### GET `/api/v1/wallet/certificates/{id}`

Get full detail for a single certificate.

**Response `200 OK`**
```json
{
  "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
  "name": "Annual Nursing License",
  "status": "Active",
  "issuerName": "Health Professionals Council",
  "issueDate": "2025-01-15",
  "expiryDate": "2026-01-15",
  "expiryWarning": false,
  "qrCodeValue": "https://certiva.app/verify/e5f6a7b8-...",
  "qrCodeBase64": "iVBORw0KGgoAAAANSUhEUgAA...",
  "pdfDownloadLink": "/api/v1/wallet/certificates/e5f6a7b8-.../pdf-url",
  "shareableLink": "https://certiva.app/verify/e5f6a7b8-..."
}
```

---

#### GET `/api/v1/wallet/certificates/{id}/share`

Get the public shareable verification link for a certificate.

**Response `200 OK`**
```json
"https://certiva.app/verify/e5f6a7b8-0000-0000-0000-000000000005"
```

---

#### GET `/api/v1/wallet/certificates/{id}/pdf-url`

Get an HMAC-signed PDF download URL valid for 15 minutes.

**Response `200 OK`** (PDF ready)
```json
{
  "signedUrl": "/api/v1/wallet/certificates/e5f6a7b8-.../pdf/download?expires=1716393600&sig=a1b2c3..."
}
```

**Response `202 Accepted`** (PDF still generating)
```json
{
  "message": "PdfGenerating"
}
```

---

#### GET `/api/v1/wallet/certificates/{id}/pdf/download`

Download the PDF certificate. **No authentication required** — access is controlled by the HMAC-signed URL.

**Query Parameters**

| Param | Type | Required | Description |
|---|---|---|---|
| `expires` | long | Yes | Unix timestamp (URL expiry) |
| `sig` | string | Yes | HMAC-SHA256 signature |

**Response `200 OK`** — `Content-Type: application/pdf`

> Use the signed URL returned by `/pdf-url` directly. Do not construct this URL manually.

**Errors**

| Status | Meaning |
|---|---|
| 403 | Invalid or expired signature |
| 404 | PDF not yet generated or certificate not found |

---

### Verification `/api/v1/verify`

Public endpoint — no authentication required.

#### GET `/api/v1/verify/{id}`

Verify a certificate by ID (e.g., after scanning the QR code).

**Response `200 OK`** (valid certificate)
```json
{
  "valid": true,
  "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
  "status": "Active",
  "professionalName": "Alice Banda",
  "certificateName": "Annual Nursing License",
  "issuerName": "Health Professionals Council",
  "issueDate": "2025-01-15",
  "expiryDate": "2026-01-15",
  "qrCodeUrl": "https://certiva.app/verify/e5f6a7b8-...",
  "source": "Cache"
}
```

> `source` is either `"Cache"` (Redis hit) or `"Database"` (fallback). All verification attempts are logged.

**Response `200 OK`** (invalid / not found)
```json
{
  "valid": false,
  "certificateId": "e5f6a7b8-0000-0000-0000-000000000005",
  "status": null
}
```

---

### Analytics `/api/v1/analytics`

Requires **Issuer** policy.

#### GET `/api/v1/analytics`

Summary statistics for the current issuer's tenant.

**Response `200 OK`**
```json
{
  "totalActive": 1204,
  "totalExpired": 87,
  "totalRevoked": 12,
  "monthlyIssuance": [
    { "year": 2026, "month": 1, "count": 45 },
    { "year": 2026, "month": 2, "count": 62 },
    { "year": 2026, "month": 3, "count": 51 }
  ]
}
```

---

### Audit Log `/api/v1/audit`

Requires **Admin** policy.

#### GET `/api/v1/audit`

Query paginated audit log entries.

**Query Parameters**

| Param | Type | Default | Description |
|---|---|---|---|
| `actionType` | string | — | Filter by action (e.g., `CertificateIssued`) |
| `entityId` | string | — | Filter by entity ID (GUID as string) |
| `actor` | string | — | Filter by actor (user ID or system name) |
| `from` | ISO 8601 | — | Start of date range |
| `to` | ISO 8601 | — | End of date range |
| `page` | int | `1` | Page number |
| `pageSize` | int | `50` | Records per page (max 100) |

**Example**
```
GET /api/v1/audit?actionType=CertificateIssued&from=2026-01-01&page=1&pageSize=20
```

**Response `200 OK`**
```json
{
  "items": [
    {
      "auditId": "a7b8c9d0-0000-0000-0000-000000000007",
      "actionType": "CertificateIssued",
      "entityId": "e5f6a7b8-0000-0000-0000-000000000005",
      "timestamp": "2026-05-22T08:30:00Z",
      "actor": "issuer:c3d4e5f6-...",
      "metadata": "{\"professionalId\":\"a1b2c3d4-...\",\"templateId\":\"d4e5f6a7-...\"}",
      "recordHash": "3c9f2a1b4d8e..."
    }
  ],
  "total": 1204,
  "page": 1,
  "pageSize": 20
}
```

---

#### GET `/api/v1/audit/export`

Export audit log as a CSV file.

**Query Parameters** — same filters as `GET /api/v1/audit` (no pagination).

**Response `200 OK`** — `Content-Type: text/csv`

```csv
AuditId,ActionType,EntityId,Timestamp,Actor,Metadata,RecordHash
a7b8c9d0-...,CertificateIssued,e5f6a7b8-...,2026-05-22T08:30:00Z,issuer:c3d4e5f6-...,...,3c9f2a1b...
```

---

## Event-Driven Flow

Domain events are written atomically to an **outbox table** within the same database transaction as the business operation. A background worker polls the outbox and publishes to RabbitMQ. All events use MassTransit.

```
POST /certificates (IssueCertificate)
        │
        ▼
  CertificateIssued ──────┬──► QrCodeConsumer       → stores QR on Certificate
  (via Outbox → RabbitMQ) │                                    │
                          │                              QrCodeGenerated
                          │                                    │
                          │                                    ▼
                          │                           PdfConsumer
                          │                             → renders PDF (QuestPDF)
                          │                             → stores file on disk
                          │                             → publishes PdfGenerated
                          │
                          ├──► CertificateIssuedNotificationConsumer
                          │      → dispatches email to professional
                          │
                          └──► VerificationCertificateIssuedConsumer
                                 → writes entry to Redis verification store

POST /certificates/{id}/revoke
        │
        ▼
  CertificateRevoked ─────┬──► CertificateRevokedNotificationConsumer
  (via Outbox → RabbitMQ) │      → dispatches revocation email
                          │
                          └──► VerificationCertificateRevokedConsumer
                                 → updates Redis verification store

Expiry Scheduler (background)
        │
        ▼
  CertificateExpired ─────► VerificationCertificateExpiredConsumer
  (via Outbox → RabbitMQ)      → updates Redis verification store

POST /certificates/bulk
        │
        ▼
  BulkIssueJobEnqueued ───► BulkIssueJobConsumer
  (via Outbox → RabbitMQ)     → iterates professionalIds
                               → issues each certificate
                               → each triggers CertificateIssued chain above
```

### Retry Policy

All consumers are configured with **3 retries** at 5 s, 25 s, and 125 s intervals. On exhaustion, messages are dead-lettered in RabbitMQ. A global exponential retry (1 s → 60 s, 3 attempts) applies at the transport level.

---

## Error Responses

All errors follow the RFC 7807 Problem Details format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "Certificate not found.",
  "traceId": "00-abc123-def456-00"
}
```

### Common Status Codes

| Code | Meaning |
|---|---|
| 200 | Success |
| 201 | Resource created |
| 202 | Accepted (async job enqueued) |
| 204 | Success, no content |
| 400 | Validation error — check `errors` field for field-level detail |
| 401 | Missing or invalid JWT |
| 403 | Insufficient permissions or invalid signed URL |
| 404 | Resource not found |
| 409 | Conflict (e.g., issuer name already exists) |
| 422 | Business rule violation (e.g., revoking an already-revoked certificate) |
| 503 | Dependency unavailable (database or Redis) |

---

## Health Endpoints

| Endpoint | Auth | Description |
|---|---|---|
| `GET /health/live` | None | Returns `200` when the database is reachable |
| `GET /health/ready` | None | Returns `200` when Redis is available |
| `GET /api/versions` | None | Lists supported API versions |

**`/health/live` response**
```json
{ "status": "live" }
```

**`/health/ready` response**
```json
{ "status": "ready" }
```

---

## Quick Testing Reference

### 1. Login and capture token

```bash
curl -s -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@certiva.app",
    "password": "P@ssword123",
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }' | jq '.accessToken'
```

### 2. Register a professional (Admin)

```bash
TOKEN="eyJhbGci..."
curl -X POST https://localhost:5001/api/v1/professionals \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Alice Banda",
    "nationalId": "NRC123456",
    "email": "alice@example.com"
  }'
```

### 3. Issue a certificate (Issuer)

```bash
curl -X POST https://localhost:5001/api/v1/certificates \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: $(uuidgen)" \
  -d '{
    "professionalId": "a1b2c3d4-0000-0000-0000-000000000001",
    "templateId": "d4e5f6a7-0000-0000-0000-000000000004"
  }'
```

### 4. Verify a certificate (public)

```bash
curl https://localhost:5001/api/v1/verify/e5f6a7b8-0000-0000-0000-000000000005
```

### 5. Get wallet (Professional / Worker)

```bash
curl https://localhost:5001/api/v1/wallet/certificates \
  -H "Authorization: Bearer $TOKEN"
```

### 6. Download PDF (signed URL — no auth needed)

```bash
# First get the signed URL
SIGNED=$(curl -s https://localhost:5001/api/v1/wallet/certificates/{id}/pdf-url \
  -H "Authorization: Bearer $TOKEN" | jq -r '.signedUrl')

# Then download
curl -o certificate.pdf "https://localhost:5001$SIGNED"
```
