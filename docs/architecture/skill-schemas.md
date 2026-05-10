# AI Skill Chain — JSON Schema Contracts

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Locked — Architectural Commitment  

---

## Overview

This document defines the full JSON Schema contracts for each of the five sequential AI skills in the Six-to-Fix skill chain. All schemas are JSON Schema draft-07 compatible and are used for strict output validation via Azure OpenAI's structured output feature. Any AI response that fails schema validation results in:

- `HTTP 502` returned to the caller
- `SkillRun.status = 'failed'`
- `SkillRun.failure_reason = 'SCHEMA_VALIDATION_FAILURE'`
- Skill chain aborted — no subsequent skills execute
- **No retry** (schema failures are deterministic — retrying the same prompt with the same schema will fail again)

---

## Skill Chain Data Flow

**Context accumulation strategy:** The skill chain passes an **accumulated context object** forward. Each skill receives the full output of all prior skills in addition to its own new inputs. This means:

- Skill 1 receives: raw client input only
- Skill 2 receives: Skill 1 output + systems maturity questionnaire responses
- Skill 3 receives: Skill 1 + Skill 2 outputs + benchmark data
- Skill 4 receives: Skill 1 + Skill 2 + Skill 3 outputs
- Skill 5 receives: Skill 1 + Skill 2 + Skill 3 + Skill 4 outputs

The `run_input_snapshot` in `audit_runs` is an immutable snapshot of all raw inputs captured at run start. Each skill's resolved prompt is stored in `skill_runs.prompt_used`.

The `output_schema_pointer` YAML frontmatter field contains a JSON Pointer (RFC 6901) path pointing to the JSON Schema within the skill markdown file's YAML frontmatter. Example: `#/output_schema` resolves to the `output_schema` key in the frontmatter block.

---

## Skill 1 — `6tofix-scorecard-rubric`

**Purpose:** Score all six marketing areas (0–10 each) based on client profile and evidence. Produces the foundational Activity Scores for the entire audit.

**Depends on:** Raw client data (no prior skill output)

**YAML frontmatter fields:**
- `name: 6tofix-scorecard-rubric`
- `version: 1.0`
- `model: gpt-4o`
- `output_schema_pointer: #/output_schema`
- `depends_on: []`

### Input JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ScorecardRubricInput",
  "type": "object",
  "required": [
    "company_name",
    "industry",
    "stated_strategy",
    "evidence"
  ],
  "properties": {
    "company_name": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200,
      "description": "Client company display name. Source: clients.name"
    },
    "industry": {
      "type": "string",
      "minLength": 1,
      "maxLength": 100,
      "description": "Industry classification. Source: clients.industry"
    },
    "stated_strategy": {
      "type": "string",
      "minLength": 1,
      "maxLength": 2000,
      "description": "Client's self-described marketing strategy. Source: audit intake form / run_input_snapshot"
    },
    "evidence": {
      "type": "object",
      "required": ["brand", "customer", "offering", "communications", "sales", "management"],
      "properties": {
        "brand":          { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Brand area. Source: Azure AI Search retrieval against client documents" },
        "customer":       { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Customer area" },
        "offering":       { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Offering area" },
        "communications": { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Communications area" },
        "sales":          { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Sales area" },
        "management":     { "type": "array", "items": { "type": "string" }, "description": "Evidence items for Management area" }
      },
      "additionalProperties": false
    },
    "benchmark_set": {
      "type": "string",
      "description": "Industry benchmark group identifier. Source: audits.industry_benchmark_set. Optional — if provided, model may reference it for calibration."
    }
  },
  "additionalProperties": false
}
```

**Field sources:**
| Field | Source |
|---|---|
| `company_name` | `clients.name` |
| `industry` | `clients.industry` |
| `stated_strategy` | `audit_runs.run_input_snapshot.stated_strategy` |
| `evidence.*` | Azure AI Search retrieval, chunked from `documents` blobs |
| `benchmark_set` | `audits.industry_benchmark_set` |

### Output JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ScorecardRubricOutput",
  "type": "object",
  "required": ["area_scores", "confidence_scores", "evidence_used", "composite_score"],
  "properties": {
    "area_scores": {
      "type": "object",
      "required": ["brand", "customer", "offering", "communications", "sales", "management"],
      "properties": {
        "brand":          { "type": "integer", "minimum": 0, "maximum": 10 },
        "customer":       { "type": "integer", "minimum": 0, "maximum": 10 },
        "offering":       { "type": "integer", "minimum": 0, "maximum": 10 },
        "communications": { "type": "integer", "minimum": 0, "maximum": 10 },
        "sales":          { "type": "integer", "minimum": 0, "maximum": 10 },
        "management":     { "type": "integer", "minimum": 0, "maximum": 10 }
      },
      "additionalProperties": false
    },
    "confidence_scores": {
      "type": "object",
      "required": ["brand", "customer", "offering", "communications", "sales", "management"],
      "properties": {
        "brand":          { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "customer":       { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "offering":       { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "communications": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "sales":          { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "management":     { "type": "number", "minimum": 0.0, "maximum": 1.0 }
      },
      "additionalProperties": false
    },
    "evidence_used": {
      "type": "object",
      "required": ["brand", "customer", "offering", "communications", "sales", "management"],
      "properties": {
        "brand":          { "type": "array", "items": { "type": "string" }, "minItems": 0 },
        "customer":       { "type": "array", "items": { "type": "string" }, "minItems": 0 },
        "offering":       { "type": "array", "items": { "type": "string" }, "minItems": 0 },
        "communications": { "type": "array", "items": { "type": "string" }, "minItems": 0 },
        "sales":          { "type": "array", "items": { "type": "string" }, "minItems": 0 },
        "management":     { "type": "array", "items": { "type": "string" }, "minItems": 0 }
      },
      "additionalProperties": false
    },
    "composite_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 60,
      "description": "Sum of all 6 area_scores. Must equal brand + customer + offering + communications + sales + management."
    },
    "documented_strategy": {
      "type": "object",
      "required": ["brand", "customer", "offering", "communications", "sales", "management"],
      "properties": {
        "brand":          { "type": "string", "enum": ["none", "partial", "full"] },
        "customer":       { "type": "string", "enum": ["none", "partial", "full"] },
        "offering":       { "type": "string", "enum": ["none", "partial", "full"] },
        "communications": { "type": "string", "enum": ["none", "partial", "full"] },
        "sales":          { "type": "string", "enum": ["none", "partial", "full"] },
        "management":     { "type": "string", "enum": ["none", "partial", "full"] }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

**`output_schema_pointer`:** `#/output_schema`

### Example Input (abbreviated)

```json
{
  "company_name": "Rolling Rock Stone",
  "industry": "Home Services / Landscaping",
  "stated_strategy": "We grow via referrals and local SEO. No documented brand guide.",
  "evidence": {
    "brand": ["Website shows three different logo variants.", "No brand guide on file."],
    "customer": ["ICP described verbally as homeowners 35-55.", "No CRM data provided."],
    "offering": ["Service list on website covers 8 categories.", "No pricing documentation found."],
    "communications": ["Posts to Instagram 2x/week.", "No email list provided."],
    "sales": ["Owner handles all sales personally.", "No pipeline tool mentioned."],
    "management": ["Owner is sole decision-maker.", "No marketing budget line item."]
  },
  "benchmark_set": "home-services-smb"
}
```

### Example Output (abbreviated)

```json
{
  "area_scores": { "brand": 4, "customer": 3, "offering": 5, "communications": 4, "sales": 3, "management": 2 },
  "confidence_scores": { "brand": 0.72, "customer": 0.61, "offering": 0.80, "communications": 0.75, "sales": 0.65, "management": 0.58 },
  "evidence_used": {
    "brand": ["Website shows three different logo variants.", "No brand guide on file."],
    "customer": ["ICP described verbally as homeowners 35-55."],
    "offering": ["Service list on website covers 8 categories."],
    "communications": ["Posts to Instagram 2x/week."],
    "sales": ["Owner handles all sales personally."],
    "management": ["Owner is sole decision-maker."]
  },
  "composite_score": 21,
  "documented_strategy": { "brand": "none", "customer": "none", "offering": "partial", "communications": "none", "sales": "none", "management": "none" }
}
```

### Schema Validation Failure Triggers

- Any `area_scores` value is missing, not an integer, or outside [0, 10]
- Any `confidence_scores` value is missing or outside [0.0, 1.0]
- `composite_score` is missing, not an integer, or outside [0, 60]
- Any required top-level field is absent
- Any undeclared additional property is present (strict `additionalProperties: false`)
- `documented_strategy` values are not one of `"none"`, `"partial"`, `"full"`

---

## Skill 2 — `systems-maturity-scoring`

**Purpose:** Assess operational and systems maturity across four dimensions (documentation, repeatability, measurability, owner-independence) to produce a 0–20 Systems Maturity Score.

**Depends on:** Skill 1 output (`area_scores`, `composite_score`) + systems maturity questionnaire responses

**YAML frontmatter fields:**
- `name: systems-maturity-scoring`
- `version: 1.0`
- `model: gpt-4o`
- `output_schema_pointer: #/output_schema`
- `depends_on: [6tofix-scorecard-rubric]`

### Input JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SystemsMaturityInput",
  "type": "object",
  "required": ["scorecard_output", "maturity_questionnaire"],
  "properties": {
    "scorecard_output": {
      "type": "object",
      "description": "Full output from Skill 1 (6tofix-scorecard-rubric). Passed through in its entirety.",
      "required": ["area_scores", "composite_score"],
      "properties": {
        "area_scores":     { "type": "object" },
        "composite_score": { "type": "integer", "minimum": 0, "maximum": 60 },
        "confidence_scores": { "type": "object" },
        "evidence_used":  { "type": "object" },
        "documented_strategy": { "type": "object" }
      }
    },
    "maturity_questionnaire": {
      "type": "object",
      "required": ["documentation", "repeatability", "measurability", "owner_independence"],
      "properties": {
        "documentation": {
          "type": "object",
          "required": ["responses"],
          "properties": {
            "responses": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["question_id", "answer"],
                "properties": {
                  "question_id": { "type": "string" },
                  "answer":      { "type": "string" }
                }
              },
              "minItems": 1
            }
          }
        },
        "repeatability": {
          "type": "object",
          "required": ["responses"],
          "properties": {
            "responses": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["question_id", "answer"],
                "properties": {
                  "question_id": { "type": "string" },
                  "answer":      { "type": "string" }
                }
              },
              "minItems": 1
            }
          }
        },
        "measurability": {
          "type": "object",
          "required": ["responses"],
          "properties": {
            "responses": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["question_id", "answer"],
                "properties": {
                  "question_id": { "type": "string" },
                  "answer":      { "type": "string" }
                }
              },
              "minItems": 1
            }
          }
        },
        "owner_independence": {
          "type": "object",
          "required": ["responses"],
          "properties": {
            "responses": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["question_id", "answer"],
                "properties": {
                  "question_id": { "type": "string" },
                  "answer":      { "type": "string" }
                }
              },
              "minItems": 1
            }
          }
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

**Field sources:**
| Field | Source |
|---|---|
| `scorecard_output` | Skill 1 validated output |
| `maturity_questionnaire.*` | `audit_runs.run_input_snapshot.maturity_questionnaire` (captured at run start) |

### Output JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SystemsMaturityOutput",
  "type": "object",
  "required": ["systems_maturity_score", "maturity_dimensions", "confidence"],
  "properties": {
    "systems_maturity_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 20,
      "description": "Aggregate systems maturity score. Sum of all 4 dimension scores, each 0–5."
    },
    "maturity_dimensions": {
      "type": "object",
      "required": ["documentation", "repeatability", "measurability", "owner_independence"],
      "properties": {
        "documentation":      { "type": "integer", "minimum": 0, "maximum": 5 },
        "repeatability":      { "type": "integer", "minimum": 0, "maximum": 5 },
        "measurability":      { "type": "integer", "minimum": 0, "maximum": 5 },
        "owner_independence": { "type": "integer", "minimum": 0, "maximum": 5 }
      },
      "additionalProperties": false
    },
    "dimension_rationale": {
      "type": "object",
      "required": ["documentation", "repeatability", "measurability", "owner_independence"],
      "properties": {
        "documentation":      { "type": "string", "minLength": 1, "maxLength": 500 },
        "repeatability":      { "type": "string", "minLength": 1, "maxLength": 500 },
        "measurability":      { "type": "string", "minLength": 1, "maxLength": 500 },
        "owner_independence": { "type": "string", "minLength": 1, "maxLength": 500 }
      },
      "additionalProperties": false
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0
    }
  },
  "additionalProperties": false
}
```

**`output_schema_pointer`:** `#/output_schema`

### Example Input (abbreviated)

```json
{
  "scorecard_output": {
    "area_scores": { "brand": 4, "customer": 3, "offering": 5, "communications": 4, "sales": 3, "management": 2 },
    "composite_score": 21,
    "confidence_scores": { "brand": 0.72, "customer": 0.61, "offering": 0.80, "communications": 0.75, "sales": 0.65, "management": 0.58 },
    "documented_strategy": { "brand": "none", "customer": "none", "offering": "partial", "communications": "none", "sales": "none", "management": "none" }
  },
  "maturity_questionnaire": {
    "documentation": { "responses": [{ "question_id": "doc_01", "answer": "We have no written processes." }] },
    "repeatability":  { "responses": [{ "question_id": "rep_01", "answer": "Campaigns are created ad hoc each time." }] },
    "measurability":  { "responses": [{ "question_id": "mea_01", "answer": "We track Instagram likes only." }] },
    "owner_independence": { "responses": [{ "question_id": "oi_01", "answer": "Everything depends on the founder." }] }
  }
}
```

### Example Output (abbreviated)

```json
{
  "systems_maturity_score": 4,
  "maturity_dimensions": {
    "documentation": 1,
    "repeatability": 1,
    "measurability": 1,
    "owner_independence": 1
  },
  "dimension_rationale": {
    "documentation": "No written processes exist across any function.",
    "repeatability": "Campaigns are entirely ad hoc with no documented playbook.",
    "measurability": "Only vanity metrics tracked; no business-outcome measurement.",
    "owner_independence": "All marketing decisions require founder approval."
  },
  "confidence": 0.84
}
```

### Schema Validation Failure Triggers

- `systems_maturity_score` missing, non-integer, or outside [0, 20]
- Any dimension score missing, non-integer, or outside [0, 5]
- `confidence` missing or outside [0.0, 1.0]
- Any required field absent
- Additional properties present at top level or within `maturity_dimensions`

---

## Skill 3 — `gap-analysis-template`

**Purpose:** Identify specific gaps in each marketing area, rate severity, and produce prioritized recommendations based on the scored areas and systems maturity.

**Depends on:** Skill 1 output + Skill 2 output + benchmark data

**YAML frontmatter fields:**
- `name: gap-analysis-template`
- `version: 1.0`
- `model: gpt-4o`
- `output_schema_pointer: #/output_schema`
- `depends_on: [6tofix-scorecard-rubric, systems-maturity-scoring]`

### Input JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "GapAnalysisInput",
  "type": "object",
  "required": ["scorecard_output", "systems_maturity_output"],
  "properties": {
    "scorecard_output": {
      "type": "object",
      "required": ["area_scores", "composite_score", "evidence_used", "documented_strategy"],
      "properties": {
        "area_scores":     { "type": "object" },
        "composite_score": { "type": "integer", "minimum": 0, "maximum": 60 },
        "evidence_used":  { "type": "object" },
        "documented_strategy": { "type": "object" }
      }
    },
    "systems_maturity_output": {
      "type": "object",
      "required": ["systems_maturity_score", "maturity_dimensions"],
      "properties": {
        "systems_maturity_score": { "type": "integer", "minimum": 0, "maximum": 20 },
        "maturity_dimensions":    { "type": "object" }
      }
    },
    "benchmark_data": {
      "type": "object",
      "description": "Industry benchmark medians per area. Source: benchmark reference table / run_input_snapshot. Optional.",
      "properties": {
        "brand":          { "type": "number", "minimum": 0, "maximum": 10 },
        "customer":       { "type": "number", "minimum": 0, "maximum": 10 },
        "offering":       { "type": "number", "minimum": 0, "maximum": 10 },
        "communications": { "type": "number", "minimum": 0, "maximum": 10 },
        "sales":          { "type": "number", "minimum": 0, "maximum": 10 },
        "management":     { "type": "number", "minimum": 0, "maximum": 10 }
      }
    }
  },
  "additionalProperties": false
}
```

**Field sources:**
| Field | Source |
|---|---|
| `scorecard_output` | Skill 1 validated output |
| `systems_maturity_output` | Skill 2 validated output |
| `benchmark_data` | `audit_runs.run_input_snapshot.benchmark_data` (pre-loaded industry medians) |

### Output JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "GapAnalysisOutput",
  "type": "object",
  "required": ["gaps", "priority_areas"],
  "properties": {
    "gaps": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["area", "severity", "description", "recommendations"],
        "properties": {
          "area": {
            "type": "string",
            "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
          },
          "severity": {
            "type": "string",
            "enum": ["critical", "moderate", "minor"]
          },
          "description": {
            "type": "string",
            "minLength": 10,
            "maxLength": 1000
          },
          "recommendations": {
            "type": "array",
            "items": { "type": "string", "minLength": 5, "maxLength": 500 },
            "minItems": 1,
            "maxItems": 5
          }
        },
        "additionalProperties": false
      }
    },
    "priority_areas": {
      "type": "array",
      "items": {
        "type": "string",
        "enum": ["brand", "customer", "offering", "communications", "sales", "management"]
      },
      "minItems": 1,
      "maxItems": 6,
      "uniqueItems": true,
      "description": "Ordered list of areas to address first, most critical first."
    }
  },
  "additionalProperties": false
}
```

**`output_schema_pointer`:** `#/output_schema`

### Example Input (abbreviated)

```json
{
  "scorecard_output": {
    "area_scores": { "brand": 4, "customer": 3, "offering": 5, "communications": 4, "sales": 3, "management": 2 },
    "composite_score": 21,
    "documented_strategy": { "brand": "none", "customer": "none", "offering": "partial", "communications": "none", "sales": "none", "management": "none" }
  },
  "systems_maturity_output": {
    "systems_maturity_score": 4,
    "maturity_dimensions": { "documentation": 1, "repeatability": 1, "measurability": 1, "owner_independence": 1 }
  },
  "benchmark_data": { "brand": 5.5, "customer": 5.2, "offering": 6.0, "communications": 5.8, "sales": 5.3, "management": 5.0 }
}
```

### Example Output (abbreviated)

```json
{
  "gaps": [
    {
      "area": "management",
      "severity": "critical",
      "description": "No marketing budget, no reporting cadence, and all decisions require founder involvement. Marketing function is entirely informal.",
      "recommendations": [
        "Establish a monthly marketing budget line item with owner accountability.",
        "Implement a bi-weekly marketing review cadence with defined KPIs.",
        "Document decision rights for marketing spend."
      ]
    },
    {
      "area": "customer",
      "severity": "critical",
      "description": "ICP exists only verbally. No CRM, no segmentation data, no persona documentation.",
      "recommendations": [
        "Build one primary ICP document based on top 10 best clients.",
        "Stand up a CRM with basic contact and opportunity tracking."
      ]
    }
  ],
  "priority_areas": ["management", "customer", "sales", "brand", "communications", "offering"]
}
```

### Schema Validation Failure Triggers

- `gaps` array is empty or missing
- Any gap item missing `area`, `severity`, `description`, or `recommendations`
- `area` value not in the six-area enum
- `severity` value not in `["critical", "moderate", "minor"]`
- `recommendations` array empty or items exceeding `maxLength`
- `priority_areas` contains values not in the six-area enum or has duplicates
- Additional properties present on any gap object

---

## Skill 4 — `value-driver-rating`

**Purpose:** Rate each value driver based on current state and potential improvement, given the gap analysis findings. Value drivers represent the levers that, if improved, produce the most client value.

**Depends on:** Skill 1 + Skill 2 + Skill 3 outputs

**YAML frontmatter fields:**
- `name: value-driver-rating`
- `version: 1.0`
- `model: gpt-4o`
- `output_schema_pointer: #/output_schema`
- `depends_on: [6tofix-scorecard-rubric, systems-maturity-scoring, gap-analysis-template]`

### Input JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ValueDriverRatingInput",
  "type": "object",
  "required": ["gap_analysis_output", "scorecard_output"],
  "properties": {
    "gap_analysis_output": {
      "type": "object",
      "required": ["gaps", "priority_areas"],
      "properties": {
        "gaps": {
          "type": "array",
          "items": { "type": "object" }
        },
        "priority_areas": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "scorecard_output": {
      "type": "object",
      "required": ["area_scores", "composite_score"],
      "properties": {
        "area_scores":     { "type": "object" },
        "composite_score": { "type": "integer" }
      }
    },
    "systems_maturity_output": {
      "type": "object",
      "required": ["systems_maturity_score"],
      "properties": {
        "systems_maturity_score": { "type": "integer", "minimum": 0, "maximum": 20 }
      }
    }
  },
  "additionalProperties": false
}
```

**Field sources:**
| Field | Source |
|---|---|
| `gap_analysis_output` | Skill 3 validated output |
| `scorecard_output` | Skill 1 validated output |
| `systems_maturity_output` | Skill 2 validated output |

### Output JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ValueDriverRatingOutput",
  "type": "object",
  "required": ["value_drivers"],
  "properties": {
    "value_drivers": {
      "type": "array",
      "minItems": 1,
      "maxItems": 12,
      "items": {
        "type": "object",
        "required": ["driver_name", "current_rating", "potential_rating", "impact"],
        "properties": {
          "driver_name": {
            "type": "string",
            "minLength": 2,
            "maxLength": 100,
            "description": "Human-readable name for the value driver (e.g., 'Customer Retention', 'Brand Clarity')"
          },
          "current_rating": {
            "type": "integer",
            "minimum": 0,
            "maximum": 10,
            "description": "Current performance rating for this value driver (0–10)"
          },
          "potential_rating": {
            "type": "integer",
            "minimum": 0,
            "maximum": 10,
            "description": "Potential rating achievable within 12 months with recommended actions (0–10)"
          },
          "impact": {
            "type": "string",
            "enum": ["high", "medium", "low"],
            "description": "Estimated business impact if this driver is improved"
          },
          "linked_area": {
            "type": "string",
            "enum": ["brand", "customer", "offering", "communications", "sales", "management"],
            "description": "Primary marketing area this driver relates to"
          },
          "rationale": {
            "type": "string",
            "minLength": 10,
            "maxLength": 500
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

**`output_schema_pointer`:** `#/output_schema`

### Example Input (abbreviated)

```json
{
  "gap_analysis_output": {
    "gaps": [
      { "area": "management", "severity": "critical", "description": "No marketing budget or reporting.", "recommendations": ["Establish budget line item."] },
      { "area": "customer", "severity": "critical", "description": "No CRM or ICP documentation.", "recommendations": ["Build ICP document."] }
    ],
    "priority_areas": ["management", "customer", "sales"]
  },
  "scorecard_output": {
    "area_scores": { "brand": 4, "customer": 3, "offering": 5, "communications": 4, "sales": 3, "management": 2 },
    "composite_score": 21
  },
  "systems_maturity_output": { "systems_maturity_score": 4 }
}
```

### Example Output (abbreviated)

```json
{
  "value_drivers": [
    {
      "driver_name": "Marketing Accountability & Budget Ownership",
      "current_rating": 2,
      "potential_rating": 7,
      "impact": "high",
      "linked_area": "management",
      "rationale": "Establishing a budget process and reporting cadence unlocks all downstream marketing investment decisions."
    },
    {
      "driver_name": "Customer Intelligence & ICP Clarity",
      "current_rating": 3,
      "potential_rating": 8,
      "impact": "high",
      "linked_area": "customer",
      "rationale": "Without documented ICP, all marketing targeting is guesswork. CRM implementation is a force multiplier."
    }
  ]
}
```

### Schema Validation Failure Triggers

- `value_drivers` array is empty or missing
- Any driver missing `driver_name`, `current_rating`, `potential_rating`, or `impact`
- `current_rating` or `potential_rating` outside [0, 10] or non-integer
- `impact` value not in `["high", "medium", "low"]`
- `linked_area` present but not in the six-area enum
- Additional properties on driver objects

---

## Skill 5 — `derive-tier`

**Purpose:** Synthesize the composite score, systems maturity, and value driver ratings into a final tier recommendation (tier_1/tier_2/tier_3) and AI readiness percentage.

**Depends on:** All prior skill outputs (Skills 1–4)

**YAML frontmatter fields:**
- `name: derive-tier`
- `version: 1.0`
- `model: gpt-4o`
- `output_schema_pointer: #/output_schema`
- `depends_on: [6tofix-scorecard-rubric, systems-maturity-scoring, gap-analysis-template, value-driver-rating]`

### Input JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DeriveTierInput",
  "type": "object",
  "required": ["composite_score", "systems_maturity_score", "value_drivers"],
  "properties": {
    "composite_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 60,
      "description": "Sum of all 6 area activity scores. Source: Skill 1 output."
    },
    "systems_maturity_score": {
      "type": "integer",
      "minimum": 0,
      "maximum": 20,
      "description": "Systems maturity aggregate score. Source: Skill 2 output."
    },
    "value_drivers": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["driver_name", "current_rating", "potential_rating", "impact"],
        "properties": {
          "driver_name":      { "type": "string" },
          "current_rating":   { "type": "integer", "minimum": 0, "maximum": 10 },
          "potential_rating": { "type": "integer", "minimum": 0, "maximum": 10 },
          "impact":           { "type": "string", "enum": ["high", "medium", "low"] }
        }
      },
      "minItems": 1,
      "description": "Source: Skill 4 output value_drivers array."
    },
    "area_scores": {
      "type": "object",
      "description": "Full area scores from Skill 1 for contextual reference.",
      "properties": {
        "brand":          { "type": "integer", "minimum": 0, "maximum": 10 },
        "customer":       { "type": "integer", "minimum": 0, "maximum": 10 },
        "offering":       { "type": "integer", "minimum": 0, "maximum": 10 },
        "communications": { "type": "integer", "minimum": 0, "maximum": 10 },
        "sales":          { "type": "integer", "minimum": 0, "maximum": 10 },
        "management":     { "type": "integer", "minimum": 0, "maximum": 10 }
      }
    },
    "maturity_dimensions": {
      "type": "object",
      "description": "Per-dimension scores from Skill 2 for contextual reference."
    }
  },
  "additionalProperties": false
}
```

**Field sources:**
| Field | Source |
|---|---|
| `composite_score` | Skill 1 `composite_score` |
| `systems_maturity_score` | Skill 2 `systems_maturity_score` |
| `value_drivers` | Skill 4 `value_drivers` |
| `area_scores` | Skill 1 `area_scores` |
| `maturity_dimensions` | Skill 2 `maturity_dimensions` |

### Output JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DeriveTierOutput",
  "type": "object",
  "required": ["tier", "ai_readiness", "tier_rationale", "next_steps"],
  "properties": {
    "tier": {
      "type": "string",
      "enum": ["tier_1", "tier_2", "tier_3"],
      "description": "Tier recommendation. tier_1=high maturity, tier_2=developing, tier_3=early stage."
    },
    "ai_readiness": {
      "type": "integer",
      "minimum": 0,
      "maximum": 100,
      "description": "AI readiness percentage (0–100). Represents readiness for AI augmentation of marketing operations."
    },
    "tier_rationale": {
      "type": "string",
      "minLength": 20,
      "maxLength": 2000,
      "description": "Narrative explanation for the tier assignment, referencing key evidence from prior skills."
    },
    "next_steps": {
      "type": "array",
      "items": { "type": "string", "minLength": 5, "maxLength": 500 },
      "minItems": 1,
      "maxItems": 6,
      "description": "Prioritized recommended next steps for the client."
    },
    "tier_score_ranges": {
      "type": "object",
      "description": "Informational: the composite score ranges that anchor the tier assignment.",
      "properties": {
        "tier_1_min": { "type": "integer" },
        "tier_2_min": { "type": "integer" },
        "tier_3_min": { "type": "integer" }
      }
    }
  },
  "additionalProperties": false
}
```

**`output_schema_pointer`:** `#/output_schema`

### Example Input (abbreviated)

```json
{
  "composite_score": 21,
  "systems_maturity_score": 4,
  "value_drivers": [
    { "driver_name": "Marketing Accountability & Budget Ownership", "current_rating": 2, "potential_rating": 7, "impact": "high" },
    { "driver_name": "Customer Intelligence & ICP Clarity", "current_rating": 3, "potential_rating": 8, "impact": "high" }
  ],
  "area_scores": { "brand": 4, "customer": 3, "offering": 5, "communications": 4, "sales": 3, "management": 2 }
}
```

### Example Output (abbreviated)

```json
{
  "tier": "tier_3",
  "ai_readiness": 18,
  "tier_rationale": "A composite score of 21/60 combined with a systems maturity score of 4/20 places this client firmly in Tier 3. Marketing operations are informal, founder-dependent, and lack measurable systems. AI readiness is low because there is insufficient data infrastructure to train or leverage AI tools effectively.",
  "next_steps": [
    "Implement a CRM system to begin capturing structured customer data.",
    "Document the ICP and validate against top 10 existing customers.",
    "Establish a monthly marketing budget with a named owner.",
    "Create a brand guide that standardizes logo, color, and messaging.",
    "Define 3–5 KPIs and begin weekly reporting."
  ]
}
```

### Schema Validation Failure Triggers

- `tier` missing or not one of `["tier_1", "tier_2", "tier_3"]`
- `ai_readiness` missing, non-integer, or outside [0, 100]
- `tier_rationale` missing, empty, or below `minLength: 20`
- `next_steps` array empty or missing
- Additional properties present at top level

---

## Cross-Cutting Schema Rules

1. **`additionalProperties: false`** is enforced on all skill output schemas. Any field not declared in the schema causes immediate validation failure.
2. **Integer vs Number:** Activity scores (`area_scores`, dimension scores) are `integer` type. Confidence scores and AI readiness are `number` (float) or `integer` respectively — see individual schemas.
3. **Enum enforcement:** All string fields with enumerated values (`tier`, `severity`, `impact`, `documented_strategy`, `area`) must exactly match one of the defined values. Casing matters.
4. **Chain abort:** A single `SCHEMA_VALIDATION_FAILURE` on any skill stops all subsequent skill execution. The `AuditRun.status` transitions to `failed` and `AuditRun.failed_skill_id` is set.
5. **Schema pointer resolution:** At startup, `SkillRunner` loads each skill markdown file, parses YAML frontmatter, and resolves the `output_schema_pointer` JSON Pointer to extract the schema object. If the pointer is invalid or the schema is missing, the skill file is rejected at startup — not at runtime.
