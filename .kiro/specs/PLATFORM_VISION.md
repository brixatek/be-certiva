# Certiva — Full Platform Vision & Phase Roadmap

> **Status:** Phase 1 in progress. This document preserves the full product vision so subsequent phases can be picked up without losing context.

---

## What Certiva Really Is

Not just a certification system.

> "A national-grade trust registry for workforce credentials" — configurable enough to become a Workforce Compliance & Certification Platform-as-a-Service (PaaS) for regulated industries.

Core design principle that drives every decision:
> **"Every certificate must be verifiable even if the system is partially down."**

---

## Target Industries

| Industry | Key Compliance Needs |
|---|---|
| Oil & Gas | HSE, BOSIET, offshore medical, contractor approval |
| Healthcare | Medical license, annual renewal, malpractice insurance, CPD hours |
| Aviation | Simulator hours, recurrent checks, FAA/CAA compliance, route authorization |
| Construction | Site safety, equipment certification, contractor vetting |
| Maritime | STCW, port authority compliance |

---

## Architecture Principles (All Phases)

- **Modular Monolith** — fast development, easy debugging, strong domain boundaries, extractable later
- **Event-Driven Backbone** — every important action emits an event (CertificateIssued, CertificateRevoked, etc.)
- **Read-Optimized Verification Layer** — verification must be O(1); QR scan → Redis → ≤50ms response
- **Immutability** — append-only audit logs, soft deletes only, hash chaining for tamper evidence
- **Idempotency** — every write operation is safe to retry without duplication
- **Stateless API Layer** — horizontal scaling behind a load balancer
- **Transactional Outbox** — event publication is atomic with the database write

---

## Architecture Decision Records (ADRs)

| Decision | Choice | Rationale |
|---|---|---|
| Event Bus | RabbitMQ (Phase 1) → Kafka (Phase 2+) | RabbitMQ is operationally simpler, excellent for background jobs, well-supported in .NET. Kafka for analytics and national-scale streaming later. |
| Tenancy Model | Shared DB + TenantId (Phase 1) → Dedicated DB per enterprise tenant (Phase 3+) | Faster to build, cheaper to operate early. TenantId on every table and every query means extraction is possible without a rewrite. |
| Verification Cache TTL | 1 hour (not 24 hours) | Trust systems prioritize correctness. Explicit event-driven invalidation (CertificateRevoked, CertificateExpired) is the primary consistency mechanism. TTL is a safety net only. |
| API Versioning | `/api/v1/` prefix from day one | Government integrations never die. 12-month deprecation window minimum. |
| Search Strategy | PostgreSQL full-text search (Phase 1) → OpenSearch/Elasticsearch (Phase 3+) | Sufficient for early scale. Migration path needed when professional counts hit millions. |
| DB Partitioning | Document strategy now, implement Phase 2+ | AuditLogs and VerificationLogs will be the first tables to explode. Partition by date. Archive strategy required. |
| Offline Verification | Signed JWT QR payload (Phase 4) | QR codes contain signed minimal claims for limited offline verification. Critical for remote oil rigs and low-connectivity areas. |
| Compliance Standards | NDPR, ISO 27001 readiness, SOC2 direction, GDPR considerations | Required for government adoption. Retention policies must be defined. |
| Disaster Recovery | RPO: 5 minutes, RTO: 30 minutes | Government systems expect explicit SLA targets. Daily backups + point-in-time recovery from day one. |

---

## Bounded Contexts (Domain Boundaries)

| Context | Responsibility | Key Entities |
|---|---|---|
| **Identity** | Professionals, Issuers, authentication, RBAC | Professional, Issuer, User, Role |
| **Certification** | Issuance lifecycle, templates, hash chaining, QR, PDF | Certificate, CertificateTemplate, CertificateHash |
| **Verification** | Read-optimized lookups, public API, verification logs | CertificateVerificationView, VerificationLog |
| **Audit** | Immutable logging, tamper evidence, regulatory export | AuditLog |
| **Notifications** | Async communication, expiry reminders, issuance alerts | NotificationLog, NotificationJob |

Each context owns its data. Cross-context communication happens only via events on the Event Bus, never via direct database joins across context boundaries.

---

## Domain Event Catalog

| Event | Producer | Consumers |
|---|---|---|
| `ProfessionalRegistered` | Identity | Notifications |
| `IssuerApproved` | Identity | Audit |
| `IssuerRejected` | Identity | Audit |
| `CertificateIssued` | Certification | Verification, Notifications, Audit, QR Worker, PDF Worker |
| `CertificateRevoked` | Certification | Verification, Notifications, Audit |
| `CertificateExpired` | Certification (scheduler) | Verification, Notifications, Audit |
| `CertificateTampered` | Certification | Audit, Notifications (Admin alert) |
| `QRCodeGenerated` | QR Worker | Certification (update record) |
| `PDFGenerated` | PDF Worker | Certification (update record) |
| `VerificationLogged` | Verification | Audit |

---

## Phase Roadmap

---

### ✅ PHASE 1 — Digital Certification & Verification Infrastructure
**Status: IN PROGRESS**
**Spec:** `.kiro/specs/certiva-core-platform/`

**Goal:** Build the trust layer. Every certificate issued is tamper-evident, instantly verifiable, and lifecycle-managed.

**Core Modules:**
- Identity & Professional Registry
- Certification Engine (issuance, lifecycle, hash chaining)
- Verification Engine (public QR verification, Redis read store, ≤50ms)
- Certificate Wallet (worker-facing: list, share, download)
- Issuer Portal (templates, bulk issuance, analytics)
- Notification System (issuance alerts, expiry reminders)
- Audit Logging (append-only, tamper-evident, regulatory export)
- Auth & RBAC (JWT, refresh tokens, MFA, roles: Admin/Issuer/Worker/Verifier)

**Tech Stack:**
- .NET 8 Web API (modular monolith)
- PostgreSQL (write model / source of truth)
- Redis (verification read store)
- Event Bus (async processing)
- Docker + Kubernetes (AKS/EKS)

**Government Readiness Targets:**
- Millions of professionals
- Nationwide verification traffic
- Audit queries and regulatory reporting
- Zero downtime expectations

---

### 🔜 PHASE 2 — Workflow & Compliance Engine
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-workflow-engine/` *(to be created)*

**Goal:** Make compliance workflows configurable. Organizations define their own approval chains, assessment stages, and renewal rules — without engineering involvement.

**Core Modules:**
- **Workflow Engine** — configurable steps, approvals, conditions, escalation rules, expirations
- **Rules Engine** — dynamic compliance rules (e.g. IF certification expired THEN revoke site access)
- **Dynamic Forms Engine** — custom forms, conditional fields, uploads, signatures
- **Assessment Engine** — scoring logic, pass/fail thresholds, retake policies
- **Renewal Engine** — automated renewal workflows triggered by expiry events

**Example Workflow (Oil & Gas):**
```
Worker Registration
  ↓ Upload Documents
  ↓ Medical Verification
  ↓ Training Assessment
  ↓ Supervisor Approval
  ↓ Certification Issuance
```

**Key Design Decisions:**
- Build vertical-first (Oil & Gas workflows) then extract reusable engine
- Rules stored dynamically, not hardcoded
- Forms engine supports conditional fields and file uploads

---

### 🔜 PHASE 3 — Multi-Tenant & Industry Packs
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-multi-tenant/` *(to be created)*

**Goal:** Transform Certiva into a configurable PaaS. Multiple organizations run isolated instances with their own branding, workflows, and RBAC.

**Core Modules:**
- **Multi-Tenant Architecture** — tenant isolation, tenant configs, tenant branding
- **Tenant RBAC** — per-tenant role definitions and permission sets
- **Industry Packs** — pre-built templates for Oil & Gas, Healthcare, Construction, Maritime
- **Tenant Onboarding** — self-service or admin-assisted tenant provisioning
- **Database Strategy** — Shared DB + TenantId (MVP) → Dedicated DB per enterprise tenant (later)

**Industry Pack Contents:**
- Certification templates
- Compliance rules
- Workflow definitions
- Report templates

---

### 🔜 PHASE 4 — Identity Verification & Digital Wallet
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-identity-verification/` *(to be created)*

**Goal:** Strengthen identity trust and give workers a portable digital credential wallet.

**Core Modules:**
- **Enhanced Identity Verification** — NIN integration, passport verification, employee ID verification
- **Biometric Verification** (optional) — facial verification
- **Mobile Wallet** — workers carry digital certifications on mobile
- **Verifiable Credentials** — W3C-compatible digital credential format (future)
- **Offline Verification** — QR codes verifiable without internet connectivity

---

### 🔜 PHASE 5 — Enterprise Integrations & API Platform
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-api-platform/` *(to be created)*

**Goal:** Allow enterprise HR systems, access control systems, and government portals to integrate with Certiva programmatically.

**Core Modules:**
- **Public API Platform** — versioned REST API for enterprise integrations
- **Webhook System** — push events to external systems on certificate state changes
- **HR System Integration** — sync worker profiles from SAP, Workday, etc.
- **Access Control Integration** — automatically approve/deny site access based on certification status
- **Request Signing** — HMAC-based request signing for enterprise API consumers
- **API Key Management** — per-tenant API keys with scoped permissions

---

### 🔜 PHASE 6 — Government & Regulatory Layer
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-government-layer/` *(to be created)*

**Goal:** Support government adoption with regulatory dashboards, national registries, and compliance reporting.

**Core Modules:**
- **Regulatory Dashboards** — government visibility into industry-wide compliance
- **National Registry API** — read-only government access to aggregate certification data
- **Compliance Reporting** — scheduled regulatory reports (PDF/CSV) per industry
- **Multi-Region Replication** — data sovereignty compliance for different jurisdictions
- **Disaster Recovery** — point-in-time recovery, multi-region failover
- **SLA Monitoring** — uptime guarantees, incident response tracking

---

### 🔜 PHASE 7 — AI & Analytics Layer
**Status: NOT STARTED**
**Spec:** `.kiro/specs/certiva-analytics/` *(to be created)*

**Goal:** Surface intelligence from certification data to help organizations stay ahead of compliance risk.

**Core Modules:**
- **AI Compliance Assistant** — "Which workers are at risk of non-compliance next month?"
- **Predictive Expiry Analytics** — forecast certification gaps before they happen
- **Workforce Compliance Scoring** — per-organization and per-worker compliance health scores
- **Anomaly Detection** — flag unusual verification patterns (potential fraud)
- **Executive Dashboards** — C-suite visibility into workforce compliance posture

---

## Competitive Moat

Once organizations build their workflows on Certiva:
- Switching becomes painful (compliance history stays, integrations stay, workforce profiles stay)
- If regulators adopt it → it becomes infrastructure-level software
- Network effect: more issuers → more workers → more verifications → more trust

---

## Key Scalability Decisions (Applies to All Phases)

| Concern | Solution |
|---|---|
| Write operations | PostgreSQL (source of truth) |
| Read-heavy verification | Redis / read store |
| Async tasks | Event Bus |
| Business logic | Modular monolith |
| Scale pressure | Stateless APIs + horizontal scaling |
| Tenant isolation | TenantId scoping → dedicated DB later |
| Audit/legal | Append-only logs + hash chaining |

---

## Naming System

- **Certiva Verify** — public verification product
- **Certiva Workforce** — enterprise workforce compliance
- **Certiva Compliance** — regulatory/government product
- **Certiva Registry** — national registry product
- **Certiva ID** — identity verification product

---

*Last updated: Phase 1 requirements complete. Proceed to Phase 1 technical design.*
