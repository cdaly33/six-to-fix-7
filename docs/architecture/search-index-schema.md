# Azure AI Search — Index Schema Specification

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  

---

## Overview

Six-to-Fix uses Azure AI Search for one active workload:

1. **Evidence Retrieval (pre-audit):** Retrieve evidence chunks from client documents to populate Skill 1's `evidence.*` input arrays. This is a semantic retrieval step performed by `AuditOrchestrator` before the skill chain starts.

Skill output audit trail data and calibration delta data live in PostgreSQL (`skill_runs` and `calibration_deltas` tables respectively) — no AI Search copy is needed.

All search calls are tenant-scoped. All search calls include `tenantId` as a mandatory filter. This is enforced by `AzureSearchClient.SearchAsync`, which always applies `Filter = $"tenantId eq '{tenantId}'"`.

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

## Tenant Scoping — Implementation Contract

All indexes enforce tenant scoping via filter. The `AzureSearchClient.SearchAsync` implementation automatically adds `Filter = $"tenantId eq '{tenantId}'"`. Callers must never bypass this method with raw `SearchClient` calls.

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
