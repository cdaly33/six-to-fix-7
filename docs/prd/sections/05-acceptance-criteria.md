# Section 5 — Acceptance Criteria & Test Strategy

> **Author:** Milhouse (Tester)
> **Date:** 2026-05-10
> **Status:** Draft — for review before implementation begins

---

> **Framing note:** This document treats the system as not yet built. Every criterion below is a
> pass/fail delivery gate. "Passes" means an independent tester can verify the outcome without
> asking the developer what was intended. If any criterion is ambiguous, it must be refined before
> the feature enters development.

---

## 1. Multi-Tenancy & Isolation

#### AC-1.1: Tenant data is strictly isolated at the database layer
**Given:** Two tenants (Tenant A, Tenant B) each have at least one client, audit, and document in the system
**When:** Any API request is made using a token scoped to Tenant A
**Then:** The response contains only records whose `tenant_id` matches Tenant A; zero records from Tenant B appear in any list, detail, or search response

**Edge cases:**
- Authenticated request with no `tenant_id` claim on the JWT: returns 403 with error code `TENANT_CLAIM_MISSING`
- Direct database row from Tenant B with a manipulated query param (e.g., `?clientId=<tenant-B-id>`): returns 404, not the record
- Super Admin token with no explicit tenant scope: may not read tenant-specific data without explicitly passing a valid `tenantId` parameter

---

#### AC-1.2: Tenant Admin manages users within their own tenant only
**Given:** A user with the Tenant Admin role for Tenant A is authenticated
**When:** The Tenant Admin attempts to create, edit, or deactivate a user
**Then:** The operation succeeds only if the target user belongs to Tenant A; attempts targeting Tenant B users return 403 with error code `CROSS_TENANT_FORBIDDEN`

**Edge cases:**
- Tenant Admin attempts to assign the Super Admin role to a new user: returns 422 with error code `ROLE_ASSIGNMENT_FORBIDDEN`
- Tenant Admin deactivates themselves: returns 422 with error code `SELF_DEACTIVATION_FORBIDDEN`

---

#### AC-1.3: Super Admin has read/write access across all tenants
**Given:** A user with the Super Admin role is authenticated
**When:** The Super Admin requests data for any tenant by passing a valid `tenantId`
**Then:** The response returns the requested data regardless of which tenant owns it; audit trail records the Super Admin's identity and the tenant accessed

**Edge cases:**
- Super Admin request with an invalid (non-existent) `tenantId`: returns 404 with error code `TENANT_NOT_FOUND`
- Super Admin attempt to create a tenant record with a duplicate slug: returns 409 with error code `TENANT_SLUG_CONFLICT`

---

#### AC-1.4: Users without a tenant assignment cannot access any tenant-scoped resource
**Given:** A valid user account exists that has not been assigned to any tenant
**When:** The user authenticates and requests any tenant-scoped endpoint
**Then:** Every response is 403 with error code `NO_TENANT_ASSIGNMENT`; the user can only reach the "pending assignment" landing page

**Edge cases:**
- User is removed from all tenants while a session is active: the next API call within that session returns 403 (session is not grandfathered)

---

## 2. Authentication & Authorization

#### AC-2.1: Valid credentials produce an authenticated session
**Given:** A registered user account with correct credentials exists
**When:** The user submits correct username and password
**Then:** Authentication succeeds; a session token is issued; the user is redirected to the role-appropriate home page; the response does not include the password or password hash in any form

**Edge cases:**
- Credentials contain leading/trailing whitespace: server trims and authenticates successfully
- Username is submitted in a different case: case-insensitive match succeeds

---

#### AC-2.2: Invalid credentials are rejected without leaking information
**Given:** A registered user account exists
**When:** The user submits an incorrect password
**Then:** Authentication fails with HTTP 401; the response body contains only a generic message ("Invalid credentials"); the response does not indicate whether the username exists

**Edge cases:**
- Non-existent username: response is identical in structure and timing to a wrong-password response (no user-enumeration side channel)
- SQL-injection or script payload in the username field: input is rejected with HTTP 400; no database error surfaces to the client

---

#### AC-2.3: Account lockout after 5 consecutive failures
**Given:** A registered user account has not been locked
**When:** 5 consecutive authentication failures occur for the same username
**Then:** On the 5th failure the account is locked; all subsequent authentication attempts return 401 with error code `ACCOUNT_LOCKED` for 15 minutes; the lockout timer resets if a successful login occurs before the 15-minute window expires

**Edge cases:**
- 4 failures, then a successful login, then 1 more failure: lockout count resets to 1 (not 5); no lockout occurs
- Lockout timer expires exactly at the 15-minute mark: the next attempt is treated as a fresh attempt (no residual count)

---

#### AC-2.4: Role-based access enforces route-level and action-level restrictions
**Given:** Users exist with Reviewer and Tenant Admin roles respectively
**When:** A Reviewer attempts to access a Tenant Admin route (e.g., user management), or a Tenant Admin attempts to perform a Reviewer-only action (e.g., submit a review decision)
**Then:** The server returns 403 with error code `INSUFFICIENT_ROLE`; no partial data from the restricted resource is included in the response

**Edge cases:**
- Auditor attempts to approve a Reviewer Queue item: 403 with `INSUFFICIENT_ROLE`
- Reviewer attempts to publish an audit: 403 with `INSUFFICIENT_ROLE`

---

#### AC-2.5: Blazor Server session is invalidated on token expiry
**Given:** An authenticated user has an active Blazor Server session and their session token has expired
**When:** The user interacts with any Blazor component that triggers a server-side operation
**Then:** The Blazor circuit redirects the user to the login page within one component render cycle; no stale data from the expired session is returned; the expired token cannot be replayed to gain access

**Edge cases:**
- Token expires while a long-running skill chain is in progress: in-flight skill chain completes (it was authorized at start); no new authorized actions are permitted post-expiry

---

## 3. Client & Document Management

#### AC-3.1: Client can be created with required fields
**Given:** A Tenant Admin or Auditor is authenticated for Tenant X
**When:** A POST request is made to create a client with a valid name, industry, and revenue band
**Then:** A client record is persisted with a generated UUID; the record is scoped to Tenant X; the response returns HTTP 201 with the created client including its `id`

**Edge cases:**
- Name field is empty or whitespace-only: returns 422 with field-level validation error `name: REQUIRED`
- Revenue band value is not in the allowed enum: returns 422 with `revenue_band: INVALID_VALUE`
- Duplicate client name within the same tenant: succeeds (names are not unique per tenant); duplicate names are allowed

---

#### AC-3.2: Document upload enforces type and size limits
**Given:** A valid client record exists and the user is authenticated with Auditor or higher role
**When:** A file is uploaded via the document upload endpoint
**Then:** Files with MIME types `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, and `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (PDF, DOCX, XLSX) up to 10 MB are accepted with HTTP 201; the blob is stored in Azure Blob Storage; a document record is persisted in the database referencing the blob URL

**Edge cases:**
- File size is exactly 10,000,000 bytes (10 MB boundary): accepted
- File size is 10,000,001 bytes: rejected with 413 and error code `FILE_TOO_LARGE`
- File with `.pdf` extension but non-PDF MIME type (content sniffing): rejected with 422 and error code `UNSUPPORTED_FILE_TYPE`
- Upload of a zero-byte file: rejected with 422 and error code `FILE_EMPTY`

---

#### AC-3.3: Uploaded document is searchable in Azure AI Search within 30 seconds
**Given:** A document has been successfully uploaded and the blob stored
**When:** 30 seconds have elapsed after the HTTP 201 response is received
**Then:** A search query containing a term known to exist in the document's content returns that document in the results from the Azure AI Search index; the search result is scoped to the uploading tenant only

**Edge cases:**
- Azure AI Search indexer is temporarily unavailable at upload time: the upload still returns 201 (blob and DB record created); indexing is retried asynchronously; document becomes searchable within 30 seconds of the indexer recovering (not 30 seconds of upload)

---

#### AC-3.4: Document deletion clears blob and search index entry
**Given:** A document exists with a corresponding blob and Azure AI Search index entry
**When:** A DELETE request is made for that document
**Then:** HTTP 204 is returned; the database record is marked deleted (soft delete); the Azure Blob Storage blob is deleted; the Azure AI Search document entry is removed within 30 seconds; subsequent search queries do not return the deleted document

**Edge cases:**
- Deletion request for a document already deleted: returns 404
- Deletion request while a skill chain referencing that document is in progress: document is flagged as pending-delete; deletion completes after the skill chain terminates

---

#### AC-3.5: Client list is scoped to the authenticated tenant
**Given:** Multiple tenants each have clients in the database
**When:** An authenticated user for Tenant A requests the client list
**Then:** The response contains only clients whose `tenant_id` = Tenant A's ID; the total count matches only Tenant A's clients

---

## 4. Audit Creation & Skill Chain

#### AC-4.1: AuditRun is created and transitions to IN_PROGRESS
**Given:** A valid client with at least one uploaded document exists
**When:** A POST request creates an AuditRun for that client
**Then:** An AuditRun record is persisted with status `PENDING`; the skill chain is triggered; AuditRun status transitions to `IN_PROGRESS` within 5 seconds; the response to the POST includes the AuditRun ID

---

#### AC-4.2: Skills execute sequentially and each SkillRun is persisted
**Given:** An AuditRun is IN_PROGRESS
**When:** The skill chain executes
**Then:** Each of the 6 skills (brand, customer, offering, communications, sales, management) executes in sequence; a SkillRun record is created for each skill before execution begins (status `RUNNING`) and updated on completion (status `COMPLETED` or `FAILED`); no two skills execute concurrently within the same AuditRun

**Edge cases:**
- One skill fails mid-chain: remaining skills are not executed; AuditRun status is set to `FAILED`; all completed SkillRuns retain their `COMPLETED` status

---

#### AC-4.3: Stale SkillRun detection
**Given:** A SkillRun has status `RUNNING` and its `updated_at` timestamp is more than 120 seconds in the past
**When:** A background health check or AuditRun status query runs
**Then:** The SkillRun is marked `STALE`; the AuditRun is marked `FAILED`; an alert entry is written to the structured log with severity WARNING

---

#### AC-4.4: Missing prerequisites return 422
**Given:** An AuditRun creation request references a client that has no uploaded documents
**When:** The POST request is submitted
**Then:** HTTP 422 is returned with error code `MISSING_PREREQUISITES` and a message listing which prerequisites are absent; no AuditRun record is created

---

#### AC-4.5: Schema validation failure returns 502 with no retry
**Given:** An AI skill returns a response payload that fails JSON schema validation
**When:** The response is received and validated
**Then:** HTTP 502 is returned to the caller with error code `SCHEMA_VALIDATION_FAILED`; the SkillRun is marked `FAILED`; no retry is attempted (schema failure is not a transient error); the raw invalid payload is stored in the SkillRun debug field for investigation

---

#### AC-4.6: Polly retry fires 3 times on 429 or 5xx responses
**Given:** An AI skill endpoint returns HTTP 429 or 5xx on the first call
**When:** Polly retry policy is active
**Then:** The platform retries up to 3 times with exponential backoff (1s, 2s, 4s delays); if all 3 retries fail, the SkillRun is marked `FAILED` and the AuditRun is marked `FAILED`; the retry count and each attempt's status code are logged in structured JSON

**Edge cases:**
- 2nd retry succeeds: SkillRun is marked `COMPLETED`; the 2 failed attempts are logged but do not affect the outcome

---

#### AC-4.7: Circuit breaker opens at 50% failure rate and returns 503
**Given:** The circuit breaker is configured with a 50% failure threshold and 60-second break duration
**When:** The failure rate of AI skill calls reaches or exceeds 50% within the sampling window
**Then:** The circuit breaker opens; subsequent calls during the 60-second break window return HTTP 503 with a `Retry-After: 60` header and error code `CIRCUIT_OPEN`; after 60 seconds the circuit enters half-open state and allows one probe request

---

## 5. Policy Engine

#### AC-5.1: LOW_CONFIDENCE rule fires when confidence < 0.6
**Given:** A CategoryPayload is produced with `confidence` = 0.59
**When:** The Policy Engine evaluates the payload
**Then:** A `LOW_CONFIDENCE` flag of severity WARNING is attached to the payload; the flag is persisted; the category is auto-escalated to the AI Council

**Edge cases:**
- Confidence exactly 0.6: LOW_CONFIDENCE does not fire
- Confidence is null/missing: treated as 0.0 → LOW_CONFIDENCE fires

---

#### AC-5.2: MISSING_EVIDENCE rule fires when evidence list is empty
**Given:** A CategoryPayload is produced with an empty evidence array (`[]`)
**When:** The Policy Engine evaluates the payload
**Then:** A `MISSING_EVIDENCE` flag of severity WARNING is attached; the category is not blocked from proceeding (informational only)

---

#### AC-5.3: BENCHMARK_OUTLIER rule fires when score deviates > 2 from industry median
**Given:** The industry median score for the "brand" category is 6.0 and the payload's `activity_score` is 8.1
**When:** The Policy Engine evaluates the payload
**Then:** A `BENCHMARK_OUTLIER` flag of severity TRIGGER is attached; the category is queued for the Reviewer Queue

**Edge cases:**
- Score is exactly 2.0 above the median (median 6.0, score 8.0): does not fire
- Score is 2.1 above the median (median 6.0, score 8.1): fires

---

#### AC-5.4: INSUFFICIENT_EVIDENCE rule fires when evidence list has fewer than 2 items
**Given:** A CategoryPayload with an evidence list containing exactly 1 item
**When:** The Policy Engine evaluates the payload
**Then:** An `INSUFFICIENT_EVIDENCE` flag of severity WARNING is attached; the category is not blocked

**Edge cases:**
- Evidence list has exactly 2 items: INSUFFICIENT_EVIDENCE does not fire
- Evidence list has 0 items: both MISSING_EVIDENCE and INSUFFICIENT_EVIDENCE fire (both are recorded)

---

#### AC-5.5: SCORE_STRATEGY_MISMATCH rule fires when score > 7 and strategy is "none"
**Given:** A CategoryPayload with `activity_score` = 7.1 and `documented_strategy` = `"none"`
**When:** The Policy Engine evaluates the payload
**Then:** A `SCORE_STRATEGY_MISMATCH` flag of severity TRIGGER is attached; the category is queued for the Reviewer Queue

**Edge cases:**
- `activity_score` = 7.0 exactly: does not fire (rule is `> 7`, not `>= 7`)
- `documented_strategy` is null: treated as `"none"` → rule fires if score > 7

---

#### AC-5.6: No flags → category auto-approved
**Given:** A CategoryPayload that passes all 5 policy rules (no flags of any kind)
**When:** The Policy Engine evaluation completes
**Then:** The category status is set to `APPROVED` automatically; it does not enter the Reviewer Queue; no AI Council session is triggered

---

#### AC-5.7: Warning-only flags are informational and do not block publish
**Given:** A CategoryPayload has only WARNING-severity flags (MISSING_EVIDENCE, INSUFFICIENT_EVIDENCE, or LOW_CONFIDENCE) and no TRIGGER-severity flags
**When:** The audit proceeds toward publish
**Then:** The category status is set to `APPROVED` (warnings do not block); the warning flags are recorded on the payload and visible in the audit report; the publish is not blocked by the presence of warnings alone

---

#### AC-5.8: Multiple flags combine and the highest severity determines routing
**Given:** A CategoryPayload has both a WARNING flag (INSUFFICIENT_EVIDENCE) and a TRIGGER flag (BENCHMARK_OUTLIER)
**When:** The Policy Engine evaluates the payload
**Then:** Both flags are persisted; the category is routed as TRIGGER (Reviewer Queue); all flags are visible to the reviewer

---

## 6. AI Council

#### AC-6.1: Council is triggered only for TRIGGER-severity flags
**Given:** A CategoryPayload has only WARNING-severity flags
**When:** The Policy Engine evaluation completes
**Then:** No AI Council session is initiated; the category proceeds per AC-5.7

**Given:** A CategoryPayload has at least one TRIGGER-severity flag
**When:** The Policy Engine evaluation completes
**Then:** An AI Council session is initiated for that category before the category enters the Reviewer Queue

---

#### AC-6.2: CouncilDecision record has the required shape
**Given:** An AI Council session completes for a flagged category
**When:** The CouncilDecision is persisted
**Then:** The record contains: `decision_type` (one of `ACCEPT`, `ADJUST`, `REJECT`), `final_scores` (all 6 fields matching CategoryPayload schema), `rationale` (non-empty string ≥ 20 characters), `council_session_id`, and `created_at` timestamp; any missing field causes the record to be marked `MALFORMED` and the category remains in queue with original payload

---

#### AC-6.3: Council-adjusted score is applied before reviewer sees the payload
**Given:** A CouncilDecision with `decision_type` = `ADJUST` and modified `final_scores` is produced
**When:** The category enters the Reviewer Queue
**Then:** The reviewer sees the council-adjusted scores, not the original AI skill scores; the original scores are preserved in the SkillRun record for audit trail purposes; the UI clearly labels the displayed score as "Council-Adjusted"

---

#### AC-6.4: Council AI error keeps category in queue with original payload
**Given:** The AI Council call fails (timeout, 5xx, or schema-invalid response)
**When:** The error is detected
**Then:** The category is placed in the Reviewer Queue with the original (unadjusted) SkillRun payload; the council failure is recorded as a flag on the payload with severity INFO and code `COUNCIL_UNAVAILABLE`; the reviewer can act without waiting for a council retry

---

## 7. Reviewer Queue & Actions

#### AC-7.1: Reviewer Queue displays all pending categories
**Given:** Multiple categories across multiple audits have TRIGGER-severity flags and are awaiting review
**When:** An authenticated Reviewer loads the Reviewer Queue
**Then:** All pending categories scoped to the reviewer's tenant are displayed; each entry shows: client name, audit ID, category name, flags present, council decision (if any), and current status; categories with status other than `PENDING_REVIEW` are not shown in the default queue view

---

#### AC-7.2: Approve action marks category as approved
**Given:** A category is in `PENDING_REVIEW` status
**When:** The Reviewer clicks Approve
**Then:** Category status transitions to `APPROVED`; the action is recorded with the reviewer's user ID and timestamp; no CalibrationDelta is created; the category is removed from the pending queue

---

#### AC-7.3: Edit action requires override reason code, non-empty notes, and creates CalibrationDelta
**Given:** A category is in `PENDING_REVIEW` status
**When:** The Reviewer submits an edit with a modified score
**Then:** The request is rejected with 422 if: `override_reason_code` is absent, or `notes` is empty or whitespace-only; when all fields are valid, the category status transitions to `APPROVED` with the edited score; a CalibrationDelta record is created capturing: original score, new score, override_reason_code, notes, reviewer_id, and timestamp

**Edge cases:**
- Score is edited to the same value as the original: edit is still recorded as a CalibrationDelta (no equality shortcut)
- Score is edited outside the valid range [0, 10]: rejected with 422 and error code `SCORE_OUT_OF_RANGE`

---

#### AC-7.4: Rerun action triggers a new SkillRun for the category
**Given:** A category is in `PENDING_REVIEW` status
**When:** The Reviewer clicks Rerun
**Then:** A new SkillRun is created for that category within the same AuditRun; the category status transitions to `RUNNING`; when the new SkillRun completes, the Policy Engine re-evaluates the new payload; the original SkillRun is retained with status `SUPERSEDED`

---

#### AC-7.5: Escalate action records escalation and notifies
**Given:** A category is in `PENDING_REVIEW` status
**When:** The Reviewer clicks Escalate
**Then:** Category status transitions to `ESCALATED`; an escalation record is created with reviewer_id, timestamp, and escalation notes; the category is removed from the standard Reviewer Queue and appears in the escalation view; a notification (structured log entry at INFO level) is emitted

---

#### AC-7.6: Reviewer lockout triggers after 3 rejections in 24 hours
**Given:** A single Reviewer has had 3 categories they reviewed subsequently rejected (by Tenant Admin or AI re-evaluation) within a 24-hour rolling window
**When:** The Reviewer attempts a 4th review action
**Then:** HTTP 409 is returned with error code `REVIEWER_REJECTION_LOCKOUT`; the Reviewer cannot approve, edit, rerun, or escalate any category until the 24-hour window expires

**Edge cases:**
- 2 rejections in 24h window, then the window expires and 1 new rejection: count resets to 1; no lockout
- Lockout is active for Reviewer A: Reviewer B (different user, same tenant) can act on all categories in the queue without restriction

---

## 8. Publishing

#### AC-8.1: All 6 categories must be approved before publish
**Given:** An AuditRun has 5 categories with status `APPROVED` and 1 with status `PENDING_REVIEW`
**When:** A publish request is submitted
**Then:** HTTP 422 is returned with error code `PUBLISH_INCOMPLETE` and a list of unapproved category names; no publish artifact is created

---

#### AC-8.2: Published audit is immutable
**Given:** An audit has been published (status = `PUBLISHED`)
**When:** Any request attempts to modify category scores, flags, or SkillRun data within that audit
**Then:** HTTP 409 is returned with error code `AUDIT_IMMUTABLE`; no data changes are persisted

---

#### AC-8.3: Publish increments version and computes composite score
**Given:** All 6 categories are `APPROVED` for an AuditRun
**When:** A publish request is submitted
**Then:** HTTP 201 is returned; the published audit artifact is persisted with: an auto-incremented version number (integer, starting at 1), a composite score computed as the mean of the 6 category `activity_score` values (rounded to 2 decimal places), and a tier recommendation derived from the composite score using the defined tier table

**Edge cases:**
- A second publish of the same audit after an admin override: version increments to 2; prior version 1 artifact is retained and accessible

---

#### AC-8.4: Tier recommendation is included in published artifact
**Given:** A publish completes with a composite score
**When:** The published artifact is retrieved
**Then:** The `tier_recommendation` field is present and is one of the defined tier values (e.g., Tier 1, Tier 2, Tier 3, Tier 4); the tier boundary rules are applied deterministically (same score always yields the same tier)

---

## 9. Calibration & Telemetry

#### AC-9.1: Every reviewer score edit creates a CalibrationDelta
**Given:** A Reviewer submits an approved edit with a score change
**When:** The edit is persisted
**Then:** Exactly one CalibrationDelta record is created containing: `audit_run_id`, `category`, `original_score`, `new_score`, `override_reason_code`, `reviewer_id`, `created_at`; no CalibrationDelta is created for approve-only or escalate actions

---

#### AC-9.2: Calibration dashboard displays all deltas for the tenant
**Given:** CalibrationDelta records exist for Tenant A
**When:** A Tenant Admin or Super Admin queries the calibration dashboard
**Then:** All CalibrationDelta records for the tenant are returned, sortable by date and category; the dashboard does not return records from other tenants

---

#### AC-9.3: RunMetricsSample is created for every completed AuditRun
**Given:** An AuditRun transitions to `COMPLETED` status
**When:** The completion event is processed
**Then:** A RunMetricsSample record is persisted containing: `audit_run_id`, `total_duration_ms`, per-skill durations, total policy flags count, council sessions count, reviewer actions count, and `created_at`

---

#### AC-9.4: Telemetry dashboard reflects accurate counts
**Given:** A known set of AuditRuns, CalibrationDeltas, and policy flags exist in the database
**When:** The telemetry dashboard endpoint is queried
**Then:** The returned counts for audits run, flags raised, council sessions, and reviewer edits match the exact counts in the database; no caching artifact returns stale counts more than 60 seconds old

---

## 10. HubSpot Integration

#### AC-10.1: Client creation triggers HubSpot company upsert
**Given:** A new client is created in the platform
**When:** The creation event is processed
**Then:** A HubSpot API call is made to upsert a company record using the client's name and industry; the HubSpot company ID (if returned) is stored on the client record; the upsert is attempted within 10 seconds of the client creation response

**Edge cases:**
- HubSpot is unavailable: the client is created successfully; the failed upsert is logged at WARNING level with error code `HUBSPOT_SYNC_FAILED`; no 500 is returned to the caller; the sync is queued for retry

---

#### AC-10.2: Audit publish pushes tier and composite score to HubSpot
**Given:** An audit is published with a composite score and tier recommendation
**When:** The publish event is processed
**Then:** A HubSpot API call updates the associated company record with `audit_tier` and `audit_composite_score` fields; the update is attempted within 10 seconds of publish

**Edge cases:**
- HubSpot company ID was never stored (initial upsert failed): a new upsert is attempted before the score update; failure is logged but publish is not rolled back

---

#### AC-10.3: HubSpot webhook HMAC signature is verified
**Given:** An inbound HubSpot webhook request arrives
**When:** The platform processes the request
**Then:** The request's HMAC-SHA256 signature (using the configured secret) is verified before any payload is processed; requests with invalid or missing signatures return 401 and no data is acted on; the verification uses constant-time comparison to prevent timing attacks

---

#### AC-10.4: HubSpot failure does not block platform operations
**Given:** HubSpot is completely unavailable (DNS failure, 500 responses)
**When:** Any platform operation that triggers a HubSpot sync runs
**Then:** The platform operation (client create, audit publish) completes successfully and returns its normal success response; the HubSpot failure is logged at WARNING level; no error is surfaced to the end user from the HubSpot failure

---

## 11. Document Search

#### AC-11.1: Uploaded document content is searchable within 30 seconds
**Given:** A document has been uploaded and HTTP 201 received
**When:** 30 seconds have elapsed
**Then:** A search query using a term extracted from the document's body text returns that document in the Azure AI Search results; the result includes the document's `id` and at least one relevant excerpt

---

#### AC-11.2: Skill executions receive relevant document excerpts as evidence
**Given:** An AuditRun is executing and the tenant has relevant documents uploaded
**When:** A skill is executed for a category
**Then:** The skill's prompt includes at least one document excerpt retrieved from Azure AI Search relevant to that category; the excerpt is drawn only from documents belonging to the same tenant as the AuditRun's client

---

#### AC-11.3: Search results are strictly scoped to the requesting tenant
**Given:** Tenant A and Tenant B each have documents with overlapping content (e.g., both mention "brand strategy")
**When:** Tenant A's skill chain performs a document search
**Then:** Only Tenant A's documents appear in the search results; Tenant B's documents do not appear in any result, excerpt, or count

---

## 12. Resilience & Error Handling

#### AC-12.1: All 4xx and 5xx responses use the structured JSON error envelope
**Given:** Any error condition occurs in the API (validation failure, auth failure, not found, server error)
**When:** The API returns an error response
**Then:** The response body conforms to the defined error envelope schema: `{ "error": { "code": string, "message": string, "traceId": string } }`; no raw exception messages, stack traces, or unstructured text appear in the response body

---

#### AC-12.2: Structured logs contain no PII
**Given:** The platform is running and processing real user data
**When:** Any log entry is emitted to the structured log sink
**Then:** No personally identifiable information (user email, full name, phone, address, document content, client financial data) appears in log fields; `traceId` and `userId` (opaque identifiers) are acceptable; log entries are validated against a PII-check rule in CI

---

#### AC-12.3: PostgreSQL unavailability returns 503 with Retry-After
**Given:** The Azure PostgreSQL Flexible Server is unreachable (simulated connection timeout)
**When:** Any API request requires a database read or write
**Then:** HTTP 503 is returned with a `Retry-After: 30` header and error code `DATABASE_UNAVAILABLE`; no partial data is returned; the Polly circuit breaker for the database connection activates after repeated failures

---

#### AC-12.4: Blob Storage unavailability returns 503 for uploads; other features are unaffected
**Given:** Azure Blob Storage is unreachable
**When:** A document upload request is submitted
**Then:** HTTP 503 is returned with error code `BLOB_UNAVAILABLE`; the database record for the document is not created (atomic failure); all non-upload API operations (client reads, audit queries, reviewer actions) continue to function normally

---

#### AC-12.5: Azure AI Search unavailability causes skills to degrade gracefully
**Given:** Azure AI Search is unreachable
**When:** A skill execution begins and attempts to retrieve document evidence
**Then:** The skill proceeds without document evidence (evidence array is `[]`); the `MISSING_EVIDENCE` policy flag is set; the skill does not fail due to the search outage; the search failure is logged at WARNING level with error code `SEARCH_UNAVAILABLE`

---

## 13. Performance Targets

#### AC-13.1: Audit Dashboard page loads in under 2 seconds
**Given:** A tenant with 50 active audits in various states
**When:** An authenticated user loads the Audit Dashboard
**Then:** The time from request initiation to full page render (server-side rendered content visible) is under 2,000 ms at p95 under normal load conditions (≤ 10 concurrent users)

---

#### AC-13.2: Individual skill execution completes within 60 seconds (Polly timeout)
**Given:** A SkillRun is initiated for any of the 6 categories
**When:** The AI skill call is made
**Then:** If the AI service does not respond within 60 seconds, Polly's timeout policy cancels the request; the SkillRun is marked `FAILED` with error code `SKILL_TIMEOUT`; the Polly retry policy then applies (3 retries) before the AuditRun is failed

---

#### AC-13.3: Reviewer Queue loads in under 1 second
**Given:** A tenant with up to 100 pending review items
**When:** An authenticated Reviewer loads the Reviewer Queue
**Then:** The full queue list (paginated at 25 items per page) renders in under 1,000 ms at p95 under normal load

---

#### AC-13.4: Document upload completes in under 10 seconds for files ≤ 10 MB
**Given:** A valid file of size ≤ 10 MB is submitted for upload
**When:** The upload request is processed
**Then:** HTTP 201 is returned within 10,000 ms from the time the request is fully received by the server; this includes blob storage write and database record creation; Azure AI Search indexing is excluded from this 10-second window (async)

---

---

## Test Strategy

### Test Pyramid

#### Unit Tests
- **Scope:** Pure domain logic — policy rule evaluation (all 5 rules), composite score computation, tier recommendation lookup, state machine transitions (AuditRun, SkillRun, Reviewer Queue status), CalibrationDelta creation logic, HMAC verification
- **Framework:** xUnit with FluentAssertions; no database, no HTTP, no file I/O
- **Coverage gate:** Minimum 80% branch coverage of all domain logic classes; measured by Coverlet on every PR; PR is blocked if coverage drops below 80%
- **Required test cases per policy rule:** at-threshold (boundary), below-threshold, above-threshold, null/missing input

#### Integration Tests
- **Scope:** Service layer interacting with a real PostgreSQL instance; all database seams covered (repositories, EF Core queries, migrations)
- **Framework:** xUnit + Testcontainers (PostgreSQL container spun up per test run); migrations applied fresh before each test session
- **AI calls:** All AI service calls are replaced with fakes/stubs returning fixture payloads; no real AI calls in integration tests
- **Required coverage:** Every repository method, every EF Core query that includes a `WHERE tenant_id = ?` clause; every state transition that writes to the database

#### Component Tests (Blazor / React)
- **Scope:** All interactive Blazor Server components and React components (web/src)
- **Framework:** bUnit for Blazor; Vitest + testing-library/react for React components
- **Required coverage:** All interactive components — queue list, approve/edit/rerun/escalate actions, document upload form, client create form, login form, calibration dashboard
- **AI calls:** Mocked at the service boundary; no real API calls in component tests

#### API / Contract Tests
- **Scope:** All REST endpoints tested against their documented request/response shapes
- **Framework:** xUnit + WebApplicationFactory (ASP.NET Core in-process test host)
- **Required tests:** Every endpoint's happy path; every documented error case (401, 403, 404, 409, 422, 503); schema validation of response envelope shape on all error responses
- **Auth:** Test tokens with controlled role claims; lockout state injected via test helpers

#### End-to-End Tests
- **Scope:** Full system running against a test environment (real database, real blob storage, AI mocked at the network boundary)
- **Framework:** Playwright
- **Required scenarios:**
  1. **Happy path:** Create client → upload document → create AuditRun → all 6 skills complete → no flags → all auto-approved → publish → verify artifact
  2. **Reviewer lockout:** 3 reviewer rejections in 24h → 4th attempt → verify 409 lockout → verify second reviewer can still act
  3. **Policy Engine flags:** Inject a payload that triggers BENCHMARK_OUTLIER → verify AI Council session → verify reviewer queue entry → approve → publish
  4. **Circuit breaker:** Simulate AI 5xx responses exceeding 50% → verify 503 with Retry-After header
- **Execution:** E2E tests run on merge to `main` only (not on every PR)

---

### Test Data Strategy

- **Fixture seed set:** One sample tenant, one sample Tenant Admin user, one sample Auditor user, one sample Reviewer user, one sample client, one sample AuditRun in each status (PENDING, IN_PROGRESS, COMPLETED, FAILED), fixture SkillRun outputs covering all policy rule combinations
- **AI mocking:** All AI service calls are intercepted and replaced with fixture responses at every test layer — unit, integration, component, API, E2E. No real AI calls are permitted in any automated test
- **Test database isolation:** A separate PostgreSQL database (or Testcontainers instance) is used for all automated tests; it is reset to a known seed state before each test session; it shares no data with development or production environments
- **HubSpot mocking:** HubSpot API calls are intercepted with a WireMock or equivalent stub; tests verify outbound call payloads without hitting the real HubSpot API

---

### CI Integration

| Test Layer | Trigger | Merge-blocking |
|---|---|---|
| Unit tests | Every PR | Yes |
| Integration tests | Every PR | Yes |
| Component tests | Every PR | Yes |
| API/contract tests | Every PR | Yes |
| E2E tests | Merge to `main` | Yes (post-merge gate) |

- Test results are published as CI artifacts; a PR cannot be merged if any unit, integration, component, or API test fails
- E2E test failure on `main` triggers an automated rollback alert and blocks the next deployment pipeline stage

---

### Quality Gates (PR Merge Requirements)

All of the following must be satisfied before a PR is eligible for merge:

1. **All tests pass:** Zero failing tests across unit, integration, component, and API layers
2. **Coverage threshold met:** Domain logic branch coverage ≥ 80% (measured by Coverlet, reported in CI)
3. **No new compiler warnings:** `dotnet build --warnaserror` must exit 0
4. **No new policy rule violations:** Custom Roslyn analyzers and linting rules pass with zero new violations
5. **Structured log PII check:** CI step scans log output from integration test run for known PII patterns; any hit is a blocker
6. **Schema validation:** All API responses in contract tests validated against the defined JSON schema; any mismatch is a blocker

---

*End of Section 5.*
