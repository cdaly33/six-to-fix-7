# Azure AI Search — Index Schema Specification

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  

---

## Overview

Six-to-Fix uses Azure AI Search to support two distinct workloads:

1. **Evidence Retrieval (pre-audit):** Retrieve evidence chunks from client documents to populate Skill 1's `evidence.*` input arrays. This is a semantic retrieval step performed by `AuditOrchestrator` before the skill chain starts.
2. **Skill Output Indexing (post-skill):** Index skill outputs, council decisions, and calibration notes for downstream search, dashboarding, and audit trail access.

Both workloads are tenant-scoped. All search calls include `tenantId` as a mandatory filter. This is enforced by `AzureSearchClient.SearchAsync`, which always applies `Filter = $"tenantId eq '{tenantId}'"`.

---

## Index 1: `six-to-fix-evidence`

**Purpose:** Evidence retrieval for Skill 1 (`6tofix-scorecard-rubric`). Stores chunked client document content indexed before the audit run starts.

### Index Schema

```json
{
  "name": "six-to-fix-evidence",
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "key": true,
      "filterable": true,
      "description": "Unique chunk identifier. Format: {tenantId}-{clientId}-{documentId}-{chunkIndex}"
    },
    {
      "name": "tenantId",
      "type": "Edm.String",
      "filterable": true,
      "sortable": false,
      "facetable": false,
      "description": "Tenant identifier. Mandatory filter on all search queries."
    },
    {
      "name": "clientId",
      "type": "Edm.String",
      "filterable": true,
      "description": "Client identifier. Used to scope evidence retrieval to a specific client."
    },
    {
      "name": "documentId",
      "type": "Edm.String",
      "filterable": true,
      "description": "Source document identifier (maps to documents.id in DB). Used for provenance."
    },
    {
      "name": "area",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "Marketing area this chunk is classified under. One of: brand, customer, offering, communications, sales, management. Set by the document classification pipeline."
    },
    {
      "name": "content",
      "type": "Edm.String",
      "searchable": true,
      "analyzer": "en.microsoft",
      "description": "The text content of the evidence chunk. This is the primary semantic search target."
    },
    {
      "name": "contentVector",
      "type": "Collection(Edm.Single)",
      "searchable": true,
      "dimensions": 1536,
      "vectorSearchProfile": "hnsw-profile",
      "description": "Ada-002 embedding of the content field. Used for vector/hybrid search."
    },
    {
      "name": "documentTitle",
      "type": "Edm.String",
      "searchable": true,
      "description": "Title or filename of the source document."
    },
    {
      "name": "chunkIndex",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true,
      "description": "Chunk sequence number within the source document."
    },
    {
      "name": "uploadedAt",
      "type": "Edm.DateTimeOffset",
      "filterable": true,
      "sortable": true,
      "description": "When the source document was uploaded."
    }
  ],
  "vectorSearch": {
    "profiles": [
      {
        "name": "hnsw-profile",
        "algorithm": "hnsw-config"
      }
    ],
    "algorithms": [
      {
        "name": "hnsw-config",
        "kind": "hnsw",
        "hnswParameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ]
  },
  "semantic": {
    "defaultConfiguration": "semantic-config",
    "configurations": [
      {
        "name": "semantic-config",
        "prioritizedFields": {
          "contentFields": [{"fieldName": "content"}],
          "keywordsFields": [{"fieldName": "documentTitle"}]
        }
      }
    ]
  }
}
```

### Evidence Retrieval Query Pattern

```csharp
// Called by AuditOrchestrator before Skill 1 — retrieves top-K evidence per area
var result = await _searchClient.SearchAsync(
    indexName: "six-to-fix-evidence",
    query: $"marketing strategy evidence for {area}",
    tenantId: tenantId,
    ct);
// AzureSearchClient applies: Filter = $"tenantId eq '{tenantId}' and clientId eq '{clientId}' and area eq '{area}'"
// Additional filter: clientId and area must be appended by the caller in the IDictionary fields
```

**Note:** The current `AzureSearchClient` implementation applies only `tenantId` filter. Callers must append additional filters (clientId, area) before calling `SearchAsync`. This is a gap documented in `.squad/decisions/inbox/oracle-phase3-hubspot.md`.

---

## Index 2: `six-to-fix-skill-outputs`

**Purpose:** Index all skill run outputs, council decisions, and calibration notes for audit trail search, dashboarding, and future model calibration analysis.

### Index Schema

```json
{
  "name": "six-to-fix-skill-outputs",
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "key": true,
      "filterable": true,
      "description": "Unique document ID. Format: {tenantId}-{auditRunId}-{skillName}-{skillRunId}"
    },
    {
      "name": "tenantId",
      "type": "Edm.String",
      "filterable": true,
      "description": "Tenant identifier. Mandatory filter on all search queries."
    },
    {
      "name": "auditRunId",
      "type": "Edm.String",
      "filterable": true,
      "description": "The audit run that produced this output."
    },
    {
      "name": "skillRunId",
      "type": "Edm.String",
      "filterable": true,
      "description": "The specific skill_runs record for this output."
    },
    {
      "name": "skillName",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "One of: 6tofix-scorecard-rubric, systems-maturity-scoring, gap-analysis-template, value-driver-rating, derive-tier"
    },
    {
      "name": "evidenceType",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "Classification of the indexed content. One of: skill_output, council_decision, calibration_note, reviewer_action"
    },
    {
      "name": "content",
      "type": "Edm.String",
      "searchable": true,
      "analyzer": "en.microsoft",
      "description": "Summarized or narrative content extracted from the skill output or decision. NOT the raw JSON — a human-readable summary for search."
    },
    {
      "name": "rawJsonPath",
      "type": "Edm.String",
      "description": "Blob Storage path to the full raw JSON output. Format: skill-outputs/{tenantId}/{auditRunId}/{skillRunId}.json"
    },
    {
      "name": "tier",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "Tier classification at time of indexing. One of: tier_1, tier_2, tier_3. Null for non-derive-tier skills."
    },
    {
      "name": "compositeScore",
      "type": "Edm.Int32",
      "filterable": true,
      "sortable": true,
      "description": "Composite score (0–60) from Skill 1. Populated on all records for an audit run for filtering."
    },
    {
      "name": "completedAt",
      "type": "Edm.DateTimeOffset",
      "filterable": true,
      "sortable": true,
      "description": "When the skill run or decision was completed."
    },
    {
      "name": "clientId",
      "type": "Edm.String",
      "filterable": true,
      "description": "Client identifier for cross-audit queries."
    }
  ]
}
```

---

## Index 3: `six-to-fix-calibration`

**Purpose:** Index calibration deltas (reviewer score overrides) for model improvement analysis and the Calibration Dashboard.

### Index Schema

```json
{
  "name": "six-to-fix-calibration",
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "key": true,
      "filterable": true,
      "description": "Calibration delta ID (maps to calibration_deltas.id in DB)."
    },
    {
      "name": "tenantId",
      "type": "Edm.String",
      "filterable": true,
      "description": "Tenant identifier."
    },
    {
      "name": "auditRunId",
      "type": "Edm.String",
      "filterable": true,
      "description": "The audit run where the override occurred."
    },
    {
      "name": "area",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "Marketing area that was overridden. One of the six standard areas."
    },
    {
      "name": "originalScore",
      "type": "Edm.Double",
      "filterable": true,
      "sortable": true,
      "description": "AI-generated score before reviewer override."
    },
    {
      "name": "adjustedScore",
      "type": "Edm.Double",
      "filterable": true,
      "sortable": true,
      "description": "Reviewer-assigned score after override."
    },
    {
      "name": "scoreDelta",
      "type": "Edm.Double",
      "filterable": true,
      "sortable": true,
      "description": "adjustedScore - originalScore. Negative = AI overscored; Positive = AI underscored."
    },
    {
      "name": "overrideReasonCode",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "description": "Structured reason code for the override. Source: reviewer_actions.override_reason_code."
    },
    {
      "name": "notes",
      "type": "Edm.String",
      "searchable": true,
      "analyzer": "en.microsoft",
      "description": "Reviewer notes explaining the override. Searchable for calibration pattern analysis."
    },
    {
      "name": "recordedAt",
      "type": "Edm.DateTimeOffset",
      "filterable": true,
      "sortable": true,
      "description": "When the calibration delta was recorded."
    }
  ]
}
```

---

## Tenant Scoping — Implementation Contract

All three indexes enforce tenant scoping via filter. The `AzureSearchClient.SearchAsync` implementation automatically adds `Filter = $"tenantId eq '{tenantId}'"`. Callers must never bypass this method with raw `SearchClient` calls.

**Filter composition:** If callers need additional filters (e.g., `clientId`, `area`, `evidenceType`), they must be combined with the tenant filter. Current `AzureSearchClient` does not support caller-supplied additional filters — this is a known gap. Resolution: add an optional `additionalFilters` parameter to `ISearchClient.SearchAsync` in a future iteration.

---

## Index Provisioning

Indexes must be provisioned in Azure AI Search before the application starts. Index creation is NOT automatic — it is a one-time setup task per environment (dev, staging, prod). The provisioning script is located at `infra/search-indexes/` (to be created by Tank as part of infrastructure provisioning).

**Access model:** `AzureSearchClient` uses `DefaultAzureCredential` (managed identity in prod, Azure CLI in dev). The managed identity must be assigned the `Search Index Data Contributor` role on the Azure AI Search resource for indexing, and `Search Index Data Reader` for read-only search operations.

---

## SLA and Consistency

- Evidence indexing (pre-audit): documents must be indexed before `AuditOrchestrator.StartAuditRunAsync` is called for a given client. This is a precondition, not enforced by code.
- Skill output indexing: within 30 seconds of skill completion (per ADR-003 context). Currently not implemented — `AzureSearchClient.IndexDocumentAsync` is the mechanism, but `SkillRunner` does not call it. Future work: add post-skill indexing in `SkillRunner.ExecuteSkillAsync` after successful completion.
- Search consistency: Azure AI Search is eventually consistent. Newly indexed documents may not appear in search results immediately. For audit-critical paths (evidence retrieval), documents must be indexed in a prior session, not concurrently.
