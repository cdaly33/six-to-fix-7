# ADR: ISkillLoader Design — YAML Loading for SkillRunner

**Owner:** Oracle (AI & Integration Dev)  
**Date:** 2026-05-10  
**Status:** Decided  
**Branch context:** dev/phase-6-yaml-loading  
**References:** morpheus-yaml-loading.md (Phase 3 deferral)

---

## Context

Morpheus deferred YAML loading in Phase 3 (`morpheus-yaml-loading.md`). The deferral
recommended an `ISkillDefinitionRepository` dual-implementation pattern (inline + YAML).
Phase 6 implements YAML loading now that the project is stable.

---

## Decision

**Implemented `ISkillLoader` interface (not `ISkillDefinitionRepository`)** with a single
`SkillLoader` implementation. Inline fallback retained inside `SkillRunner`.

### Why ISkillLoader over ISkillDefinitionRepository

- The Morpheus deferral suggested two full repository implementations with feature-flag
  switching. That adds DI complexity and a new failure mode (wrong impl selected).
- `ISkillLoader` is a simpler contract: one method, one implementation, one concern.
- The inline fallback stays inside `SkillRunner` — not abstracted behind another interface.
  This is more legible: SkillRunner explicitly owns the "try YAML, fall back to inline" policy.

---

## Interface Contract

```csharp
// src/SixToFix.Application/Services/ISkillLoader.cs
public interface ISkillLoader
{
    Task<SkillDefinition> LoadAsync(string skillName, int skillIndex, CancellationToken ct = default);
}
```

**Why `skillIndex` is a parameter:** YAML files do not declare a skill index. The index
is a chain-execution concern owned by `SkillRunner`, which derives it from
`Array.IndexOf(SkillChain, skillName)`. Passing it into `LoadAsync` keeps the loader
simple and the chain order authoritative in one place.

---

## SkillLoader Implementation

- **Location:** `src/SixToFix.Infrastructure/Services/SkillLoader.cs`
- **Lifetime:** Singleton — stateless file reader, safe for concurrent use.
  Scoped `SkillRunner` → Singleton `ISkillLoader` is the safe one-way direction (ADR-001).
- **Path resolution:** Walks up from `IHostEnvironment.ContentRootPath`, falls back to
  `AppContext.BaseDirectory` walk-up. Works for dev (content root = project dir), xunit
  test runs, and Azure App Service (docs/ co-located with publish output).
- **YAML parsing:** `DeserializerBuilder().Build()` → `Dictionary<object, object>`.
  `output_schema` recursively converted to JSON via `NormalizeYamlValue` +
  `JsonSerializer.Serialize`. YAML booleans, integers, and strings all survive correctly.
- **Validation:** Required fields `name`, `system_prompt`, `output_schema` checked before
  returning. `InvalidOperationException` thrown with descriptive message if any are missing.
- **No caching:** Reads disk on every call. Skills don't change at runtime; hot-reload is
  not a Phase 6 requirement. Caching can be layered on top without interface change.

---

## Fallback Behavior in SkillRunner

```csharp
// SkillRunner.GetSkillDefinitionAsync — try YAML, fall back to inline
try
{
    return await _skillLoader.LoadAsync(skillName, chainIndex, ct);
}
catch (Exception ex)
{
    if (SkillDefinitions.TryGetValue(skillName, out var fallback))
    {
        _logger.LogWarning(ex, "YAML loading failed for skill {SkillName}; falling back to inline definition", skillName);
        return fallback;
    }
    throw new SkillNotFoundException(skillName);
}
```

- **All exceptions** from `LoadAsync` are caught (file not found, parse failure, I/O error).
- Warning is logged with the exception — surfaced in Application Insights, actionable.
- Inline definitions not deleted — they remain the safety net.
- If YAML fails AND no inline fallback exists → `SkillNotFoundException` (chain aborts, HTTP 502).

---

## Registration

```csharp
// AiServiceExtensions.cs
services.AddSingleton<ISkillLoader, SkillLoader>();
services.AddScoped<ISkillRunner, SkillRunner>();
```

---

## YamlDotNet Version

- Added `YamlDotNet 17.1.0` to `SixToFix.Infrastructure`.
- Test project had `YamlDotNet 16.*` — upgraded to `17.*` to prevent NU1605 downgrade
  warning-as-error. Both projects now resolve to 17.1.0.

---

## Testing

Added 5 parameterized tests in `SkillYamlValidationTests.SkillLoader_LoadAsync_ReturnsValidSkillDefinition`:
- One per skill (one theory × 5 skill names)
- Verifies: `Name` matches skill directory, `SkillIndex` correct, `SystemPrompt` non-empty,
  `OutputSchemaJson` non-empty and valid JSON parseable.
- Uses `NSubstitute` to mock `IHostEnvironment.ContentRootPath` → repo root.

---

## Constraints Preserved from Morpheus Deferral

- Inline definitions NOT deleted from `SkillRunner` (resilience backstop).
- Phase 3 comment updated: removed "Future: replace with YAML file loading" note.
- No hot-reload in this phase (out of scope).
