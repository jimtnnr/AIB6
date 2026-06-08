# AirLock – Technical Debt & Tuning Backlog

Items found during the June 2026 performance session. Layer these in — do not sprint on all at once.

---

## 1. PromptSanitizer Strips Newlines — EASY FIX

**File:** `PromptSanitizer.cs`

**Problem:**
```csharp
safe = Regex.Replace(safe, @"[\x00-\x1F]", "");
```
This regex covers the full control character range `0x00–0x1F`, which includes:
- `\n` (0x0A) — newline
- `\r` (0x0D) — carriage return
- `\t` (0x09) — tab

The `\r\n` → `\n` normalization runs *before* this line, so it has no effect. All newlines get stripped. Structured user input like:

```
[Who] was involved
[What] happened
[When] it occurred
```

arrives at Mistral as a single flat line. The model has to work harder to parse intent and structure.

**Fix:**
```csharp
// Preserve \n (0x0A) and \t (0x09), strip everything else in control range
safe = Regex.Replace(safe, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
```

**Impact:** Better structured input → better output quality → potentially faster generation because the model wastes fewer tokens resolving ambiguity.

---

## 2. Context Window Size — NEEDS BENCHMARKING

**Current setting:** `OLLAMA_CONTEXT_LENGTH=4096`

**Reality:** Based on actual prompt analysis, a typical Airlock prompt is **300–600 tokens** (template + tone + length directive + user input). Even a full 1000-word letter output lands well under 2048 tokens total.

**Problem:** Setting `num_ctx` too high on CPU wastes memory and may slow initial prompt evaluation. Ollama allocates KV cache proportional to context length at load time.

**Recommendation:** After benchmarking the new container, test:
- `OLLAMA_CONTEXT_LENGTH=2048` — likely sufficient for all current workflows
- `OLLAMA_CONTEXT_LENGTH=4096` — keep if future codex packs grow significantly

**How to test:**
```bash
# Set context to 2048, benchmark
time curl -s http://localhost:11434/api/generate -d '{
  "model": "mistral:7b-instruct-q4_K_M",
  "prompt": "Write a detailed formal business letter",
  "stream": false
}' | grep -o '"eval_count":[0-9]*\|"eval_duration":[0-9]*\|"prompt_eval_count":[0-9]*'
```

Compare tokens/second at 2048 vs 4096. If speed improves meaningfully, drop to 2048.

---

## 3. Model Name in appsettings.json — PENDING

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

User input is capped at 1000 characters (~250 tokens). This is probably fine for current workflows but worth revisiting if codex packs evolve to accept richer structured input (e.g. pasted documents, incident reports, medical notes).

Not urgent. Note for future pack design.

---

## Priority Order

| # | Item | Effort | Impact |
|---|---|---|---|
| 1 | Update model name in appsettings.json | 2 min | High — do today |
| 2 | Fix PromptSanitizer newline stripping | 5 min | Medium — next session |
| 3 | Benchmark and tune context window | 30 min | Medium — next session |
| 4 | Revisit MaxLength cap | Low urgency | Future pack design |

---

*Logged: June 2026*