# AirPack Specification v1.2

*Supersedes v1.1. Resolves the `{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}` placeholder gap identified June 2026, and clarifies the philosophy behind placeholder handling generally.*

---

## Philosophy

AirLock generates **drafts for manual editing**, not finished letters. The workflow is and always has been:

```
Generate → Review → Export → Finalize externally
```

This single fact governs every decision in this spec. It means:

- **The output does not need to be complete.** It needs to be a clean, structurally-correct starting point that a human can finish in Word, LibreOffice, or any text editor.
- **Placeholders are a feature, not a failure.** A letter with `[Sender Name]` and `[Date]` sitting in the header is *exactly* the intended output — the user fills those in during the 30 seconds they're already going to spend reviewing and editing the letter anyway.
- **The model's job is structure and language, not data entry.** AirLock should never ask the model to invent information it wasn't given — including information like "your law firm's address" that AirLock itself was never told.
- **Precision in placeholder formatting doesn't matter; clarity does.** Whether a placeholder renders as `[Sender Name]` or `[Sender Address Here]`, the user will recognize it instantly and replace it. What matters is that the model never has to *guess* what to do with something it doesn't recognize — because a guess might not look like a placeholder at all, and might instead look like a hallucinated fact.

### The core rule

> **Every placeholder that reaches the model must be in a form the model has explicit instructions for. Nothing undefined should ever reach the model.**

AirLock's existing fact-preservation instruction already tells the model how to handle bracketed placeholders:

> *"If any information is missing, leave a clear placeholder in brackets (e.g., [Insert Date Here]). Do not invent or assume."*

The model has **never** been told what to do with `{CurlyBraces}` in a prompt. If `{CurlyBraces}` reach the model unresolved — as happened with `{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}` in the v1.1-authored pack — the model has no defined behavior and has to improvise. Improvisation is where hallucination, markdown leakage, and awkward field-echoing come from.

**Therefore: the runtime's job is not to "resolve placeholders correctly." The runtime's job is to ensure every placeholder — regardless of which convention an AirPack author used — arrives at the model in the one format (`[Bracket Placeholder]`) the model already knows how to handle.**

---

## Two Placeholder Conventions

AirPacks and AirLock use two distinct placeholder syntaxes, with distinct purposes:

### `{CurlyBrace}` — runtime substitution tokens

Resolved by **AirLock's code**, before the prompt is sent to the model. These represent values AirLock *itself* knows or can compute: the user's typed facts, the selected tone/length, the letter's own metadata (MainType, SubType, Structure, Intent, Role).

The model **never sees** a correctly-functioning `{CurlyBrace}` token — by the time the prompt reaches the model, all of these have been replaced with real content.

### `[Bracket Placeholder]` — draft-completion markers

Left **in the output** intentionally, for the **human** to fill in during manual editing. These represent information nobody — neither the user nor AirLock — provided. The model is instructed to use these whenever information is missing, rather than inventing a value.

### The rule, restated

If AirLock's code does not have a real value to substitute for a `{CurlyBrace}` token, **that token must be converted to a `[Bracket Placeholder]` before the prompt is sent** — never left as `{CurlyBrace}`, and never silently dropped.

---

## Dropdown Generation (unchanged from v1.1)

AirLock generates the first two dropdowns from AirPack metadata.

### Dropdown 1 — Letter Type

Generated from:

```json
{
  "Title": "Legal Strict Facts",
  "MainType": "Appeal"
}
```

Displayed as:

```text
Legal Strict Facts > Appeal
```

Rules:

- Unique Title/MainType combinations produce unique entries.
- Multiple SubTypes may exist under the same Title/MainType.
- Title represents the workflow family.
- MainType represents the workflow category.

### Dropdown 2 — Letter Sub-Type

Generated from:

```json
{
  "Label": "Insurance Claim Appeal (Strict Facts)"
}
```

Rules:

- User-facing, human readable.
- Does not need to be unique globally.
- Should clearly describe the workflow.

### Dropdown 3 — Escalation Level (runtime-controlled)

Not controlled by AirPack. Current values: Initial Inquiry, Reminder, Demand, Final Notice, Intent to Escalate. Mapped through `PromptMappings`. Injected via `{Tone}`.

### Dropdown 4 — Letter Length (runtime-controlled)

Not controlled by AirPack. Current values: Brief, Short, Medium, Extended, Full. Mapped through `PromptMappings`. Injected via `{Length}`.

---

## Core Substitution Tokens (unchanged, already implemented)

These `{CurlyBrace}` tokens are resolved by `FillPrompt()` today and always have real values:

| Token | Source |
|---|---|
| `{UserInput}` | User's typed facts (sanitized) |
| `{Tone}` | Selected escalation level, mapped via `PromptMappings.ToneMap` |
| `{Length}` | Selected length, mapped via `PromptMappings.LengthMap` |
| `{Structure}` | AirPack's `Structure` field |
| `{Intent}` | AirPack's `Intent` field |
| `{MainType}` | Selected MainType |
| `{SubType}` | Selected SubType |
| `{Role}` | AirPack's `RoleInstruction`, or a default if empty |

No changes to this table in v1.2.

---

## Formatting Templates (v1.2 — revised)

AirPacks may optionally define:

```json
{
  "HeaderTemplate": "...",
  "RecipientTemplate": "...",
  "FooterTemplate": "..."
}
```

### Purpose

Move document *structure* (the shape of a letterhead, a recipient block, a signature block) out of the model's discretion and into the workflow definition — so the model isn't improvising letter formatting from scratch, but it also isn't expected to fill in information AirLock doesn't have.

### What v1.1 got wrong

v1.1 specified that these templates contain their own `{CurlyBrace}` placeholders (`{SenderName}`, `{SenderAddress}`, `{Date}`, `{RecipientName}`, etc.) and that "runtime resolves placeholders" before the prompt is sent. **This was never implemented**, and — more importantly — **most of these values have no data source**. AirLock has no UI field for "the sender's mailing address," and per the Philosophy section above, it doesn't need one. The v1.1 spec was solving a precision problem that doesn't exist for a draft-then-hand-edit product.

### v1.2 behavior

1. **`{HeaderTemplate}`, `{RecipientTemplate}`, `{FooterTemplate}` are substitution tokens** (Core Substitution Tokens table, extended — see below). If present in `PromptTemplateText`, they are replaced with the corresponding top-level field's content.

2. **Within those template strings, any `{CurlyBrace}` token is then resolved according to this priority:**
   - **`{Date}`** → resolved to today's actual date (`DateTime.Now`, formatted as a normal date string). This is the one token AirLock genuinely knows without asking anyone.
   - **Everything else** (`{SenderName}`, `{SenderAddress}`, `{SenderCityStateZip}`, `{RecipientName}`, `{RecipientAddress}`, `{RecipientCityStateZip}`, and any future token an AirPack author invents) → converted to a `[Bracket Placeholder]`, using a humanized version of the token name (e.g. `{SenderCityStateZip}` → `[Sender City, State, Zip]`).

3. **If an AirPack does not define `HeaderTemplate`/`RecipientTemplate`/`FooterTemplate` at all**, and `PromptTemplateText` doesn't reference them, nothing changes — this is fully backward compatible with packs that don't use this feature (e.g. the original Legal Letters pack from README v1).

4. **If `PromptTemplateText` references `{HeaderTemplate}` etc. but the AirPack doesn't define the corresponding field**, the token resolves to an empty string (not an error) — the section is simply omitted from the prompt.

### Example

Given:

```json
"HeaderTemplate": "{SenderName}\n{SenderAddress}\n{SenderCityStateZip}\n{Date}"
```

After v1.2 substitution, the model receives:

```
[Sender Name]
[Sender Address]
[Sender City, State, Zip]
June 15, 2026
```

The model is instructed (via the existing fact-preservation rule) to preserve `[Bracket Placeholders]` in its output — so the final draft contains a recognizable, ready-to-edit header block, and the model never had to guess what `{SenderName}` meant.

---

## The Generic Catch-All

After all named substitutions above (Core Substitution Tokens + Header/Recipient/Footer + their inner tokens), **any remaining `{CurlyBrace}` pattern anywhere in the assembled prompt is converted to a `[Bracket Placeholder]`** using the same humanization rule (e.g. `{SomeNewThing}` → `[Some New Thing]`).

This is a safety net, not the primary mechanism — well-formed AirPacks shouldn't rely on it. But it guarantees the **core rule** holds even for AirPacks that:

- reference a `{CurlyBrace}` token this spec doesn't anticipate
- contain a typo in a token name
- were written against a future spec version this runtime doesn't yet implement

**This is a silent conversion, not a validation error.** A pack with an unrecognized `{Token}` still loads and produces a usable draft — the unrecognized token simply becomes a bracket placeholder the user fills in, same as any other gap. Given the draft-then-hand-edit philosophy, rejecting an otherwise-good pack over one unresolved token would be disproportionate. (Pack-level validation continues to be handled separately by the `_sigil` field, which is unchanged by this spec.)

---

## Humanization Rule (for the catch-all and inner template tokens)

`{PascalCaseToken}` → `[Pascal Case Token]`

Algorithm: insert a space before each internal capital letter, then wrap in brackets. Examples:

| Curly token | Bracket placeholder |
|---|---|
| `{SenderName}` | `[Sender Name]` |
| `{SenderCityStateZip}` | `[Sender City State Zip]` |
| `{RecipientAddress}` | `[Recipient Address]` |
| `{ClaimNumber}` | `[Claim Number]` |

(Minor cosmetic improvements — e.g. `[Sender City, State, Zip]` with commas — can be special-cased later if desired, but the mechanical rule above is sufficient and guarantees no token is ever left unconverted.)

---

## Fact Preservation Mode (unchanged from v1.1)

AirPacks may define stricter drafting behavior under workflow families such as:

```text
Legal Letters
Legal Strict Facts
Insurance Strict Facts
Compliance Strict Facts
```

Characteristics:

- Placeholder-first
- No inferred facts, dates, or status updates
- Names treated as immutable data

```
Missing Fact → Placeholder   (correct)
Missing Fact → Assumption    (avoid)
```

v1.2 makes this principle apply uniformly to **all** placeholder sources — not just the user's facts, but the formatting templates too. Previously, `{HeaderTemplate}` was an exception to "Missing Fact → Placeholder" (it became "Missing Fact → Undefined Token → Model Improvises"). v1.2 closes that exception.

---

## AirPack Design Principle (unchanged from v1.1)

AirPack authors control:

- Workflow family (Title)
- Workflow category (MainType)
- Workflow selection (Label)
- Prompt strategy
- Formatting strategy (HeaderTemplate / RecipientTemplate / FooterTemplate)
- Fact preservation strategy

AirLock runtime controls:

- Escalation levels
- Length options
- Today's date
- The conversion of any `{CurlyBrace}` token without a real value into a `[Bracket Placeholder]`
- Model invocation, storage, import/export, database integration

**Runtime stays fixed. Workflow behavior lives in the AirPack. The runtime's one promise to every AirPack author: whatever `{CurlyBrace}` tokens you write, the model will never see them as `{CurlyBrace}` — only as real values or as `[Bracket Placeholders]` it already knows how to handle.**

---

## Implementation Checklist (for the corresponding code change)

- [ ] `PromptTemplate` class gains `HeaderTemplate`, `RecipientTemplate`, `FooterTemplate` string properties (default empty)
- [ ] `FillPrompt()` substitutes `{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}` in `PromptTemplateText` with the corresponding field's value (empty string if field not defined)
- [ ] Within the substituted header/recipient/footer text, `{Date}` → `DateTime.Now` formatted
- [ ] A generic regex pass converts any remaining `{PascalCaseToken}` → `[Pascal Case Token]`, applied to the **entire assembled prompt** (catches inner template tokens and any future/unanticipated tokens in one pass)
- [ ] Verify backward compatibility: existing packs without `HeaderTemplate`/etc. (e.g. original Legal Letters pack) produce byte-identical prompts to before, except for the new catch-all pass (which should be a no-op for packs with no stray `{Tokens}`)
- [ ] Test against `legal_strict_facts_header_footer.airpack` — confirm header/recipient/footer blocks appear in the prompt as clean `[Bracket Placeholder]` blocks, `{Date}` resolves to a real date, and no `{CurlyBrace}` survives into the final prompt

---

*Last updated: June 15, 2026. Written in response to the header/footer plumbing gap discovered during model comparison testing — see `CHANGELOG_2026-06-15.md`.*
