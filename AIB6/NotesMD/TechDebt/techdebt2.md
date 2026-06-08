# AirLock – Technical Debt & Tuning Backlog

Items found during the June 2026 performance session. Layer these in — do not sprint on all at once.

---

## 1. PromptSanitizer Strips Newlines — EASY FIX

**File:** `PromptSanitizer.cs`

**Problem:**
```csharp
safe = Regex.Replace(safe, @"[\x00-\x1F]", "");
```
This regex covers the full control character range `0x00–0x1F`, which includes `\n` (0x0A). The `\r\n` → `\n` normalization runs before this line so it has no effect. All newlines get stripped. Structured user input like:

```
[Who] was involved
[What] happened
[When] it occurred
```

arrives at Mistral as a single flat line. The model works harder to parse intent and structure.

**Fix:**
```csharp
// Preserve \n (0x0A) and \t (0x09), strip everything else in control range
safe = Regex.Replace(safe, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
```

**Impact:** Better structured input → better output quality → potentially faster generation.

---

## 2. Context Window Size — NEEDS BENCHMARKING

**Current setting:** `OLLAMA_CONTEXT_LENGTH=4096`

**Reality:** Typical Airlock prompt is 300–600 tokens. Even a full 1000-word letter output lands well under 2048 tokens total. Setting `num_ctx` too high on CPU wastes memory and may slow prompt evaluation — Ollama allocates KV cache proportional to context length at load time.

**Test after new container is benchmarked:**
```bash
time curl -s http://localhost:11434/api/generate -d '{
  "model": "mistral:7b-instruct-q4_K_M",
  "prompt": "Write a detailed formal business letter",
  "stream": false
}' | grep -o '"eval_count":[0-9]*\|"eval_duration":[0-9]*\|"prompt_eval_count":[0-9]*'
```

Compare tokens/second at 2048 vs 4096. If speed improves, drop to 2048.

---

## 3. Model Name in appsettings.json — PENDING PULL

**File:** `appsettings.json`

**Current:**
```json
"ModelName": "mistral"
```

**Fix after pull completes:**
```json
"ModelName": "mistral:7b-instruct-q4_K_M"
```

Or for production quality:
```json
"ModelName": "mistral:7b-instruct-q5_K_M"
```

Never use `mistral` or `mistral:latest` — unspecified variant, unpredictable behavior.

---

## 4. MaxLength Cap on Sanitizer May Be Too Aggressive

**File:** `PromptSanitizer.cs`

```csharp
private const int MaxLength = 1000;
```

User input is capped at 1000 characters (~250 tokens). Fine for current workflows but worth revisiting if Airpacks evolve to accept richer structured input (pasted documents, incident reports, medical notes).

Not urgent. Note for future pack design.

---

## 5. Rename .aibcodex → .airpac — BRANDING

**Why:** `.aibcodex` is an internal development name. The product term is **Airpack**. The file extension should match the brand.

**Files to change:**

`PromptTemplateRegistry.cs`:
```csharp
// From:
var files = Directory.GetFiles(folderPath, "*.aibcodex", SearchOption.TopDirectoryOnly);
// To:
var files = Directory.GetFiles(folderPath, "*.airpac", SearchOption.TopDirectoryOnly);
```

`ImportTab.axaml.cs`:
```csharp
// From:
var importFiles = Directory.GetFiles(importPath, "*.aibcodex");
// To:
var importFiles = Directory.GetFiles(importPath, "*.airpac");
```

Button label in `ImportTab`:
```csharp
// From:
Content = "Click - Import Codex (.aibcodex)"
// To:
Content = "Click - Import Airpack (.airpac)"
```

Also rename all existing `.aibcodex` files in `~/Documents/AIBDOCS/config/` to `.airpac`.

The `_sigil` field (`"owl_440Hz_approved"`) stays unchanged — that is a validation mechanism, not a display name.

**Impact:** Branding only. No logic changes.

---

## Priority Order

| # | Item | Effort | Impact |
|---|---|---|---|
| 1 | Update model name in appsettings.json | 2 min | High — do today after pull |
| 2 | Rename .aibcodex → .airpac | 15 min | Medium — branding |
| 3 | Fix PromptSanitizer newline stripping | 5 min | Medium — quality |
| 4 | Benchmark and tune context window | 30 min | Medium — performance |
| 5 | Revisit MaxLength cap | Low urgency | Future pack design |

---

*Logged: June 2026*