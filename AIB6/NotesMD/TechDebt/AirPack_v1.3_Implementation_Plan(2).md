# AirPack v1.3 — Context Builder Integration: Implementation Plan

*Builds on `AirPack_Spec_v1.2.md` (placeholder philosophy, unchanged) and `airlock_advanced_architecture.md` (file/class reference, current as of June 14). This document is the "how and in what order" — it does not redefine the spec, it sequences the actual code changes needed to implement it plus the Context Builder threading decided today.*

---

## Theory of Correspondence — Why These Fields Exist

Correspondence is a structured argument between two parties aimed at producing a change — a belief, an action, a record. Classical rhetoric formalized this two thousand years ago; the structure survives in modern business and legal letter conventions because the underlying problem hasn't changed: a stranger has limited attention and no obligation to read carefully, so the structure has to do work the prose alone won't. Every functional piece of real correspondence does some subset of these jobs, in roughly this order:

| Function | What it does | Where it lives in AirLock |
|---|---|---|
| Frame | Who's writing, to whom, in what capacity, when | `HeaderTemplate` / `RecipientTemplate` |
| Reference | What existing matter this concerns (claim/invoice/case number) — lets a recipient route the letter without reading the body | `{Reference}` (new, this revision) |
| Purpose | A plain early sentence stating why you're writing | Template-authoring quality (Phase 5), not a field |
| Facts | What happened, specific and chronological | `{Facts}` |
| Argument | The bridge from facts to obligation | Deliberately the human's review work, not a field — see v1.2 philosophy |
| The ask | What you want, as an action with a deadline | `{DesiredOutcome}` |
| Consequence | What happens if the ask isn't met | `{ConsequenceClause}` (new, this revision) |
| Close | Signature, contact info, a way to respond | `FooterTemplate` |

Six of eight were already correctly placed before this revision; Purpose and Argument are deliberately left to human review rather than fielded. Reference and Consequence were the two genuine gaps, closed below. Reference is a low-risk addition — same bracket-fallback pattern as everything else. Consequence gets different handling: a bracket placeholder dropped mid-sentence ("failure to comply will result in `[Consequence]`") reads worse than omitting the clause, and an *invented* consequence in a legal letter is a materially riskier failure mode than ordinary genericness — debt-collection correspondence specifically has real restrictions on what can be threatened (not legal advice, just a reason to never let the model improvise here). So Consequence resolves to a full clause-or-nothing, never a bracket.

---

## 0. What We're Building (Recap)

Today's session closed the gap between the structured Context Builder dialog (already built, see `PromptBuilderDialog.axaml.cs`) and the placeholder/token system defined in v1.2 (specified, **not yet implemented in code** — `FillPrompt()` currently only resolves the 8 Core Substitution Tokens; `HeaderTemplate`/`RecipientTemplate`/`FooterTemplate` don't exist on `PromptTemplate` yet). The decisions:

| Source | Field | Feeds token | Fallback |
|---|---|---|---|
| Settings (Postgres), prefills per-letter dialog | Who From → Name, Role | `{SenderName}`, `{SenderRole}` | `[Bracket Placeholder]` |
| Per-letter dialog (editable, overrides prefill) | Who From → Name, Role | `{SenderName}`, `{SenderRole}` | — |
| Settings only, no per-letter override | Sender Company | `{SenderCompany}` | `[Bracket Placeholder]` |
| Settings only, no per-letter override | Sender Address | `{SenderAddress}` | `[Bracket Placeholder]` |
| Settings only, no per-letter override | Sender City/State/Zip | `{SenderCityStateZip}` | `[Bracket Placeholder]` |
| Settings only, no per-letter override | Sender Phone | `{SenderPhone}` | `[Bracket Placeholder]` |
| Settings only, no per-letter override | Sender Email | `{SenderEmail}` | `[Bracket Placeholder]` |
| Per-letter dialog, near top (frame-adjacent) | Reference | `{Reference}` | `[Bracket Placeholder]` — standard fallback, it's a short value |
| Per-letter dialog | Who To → Name, Role | `{RecipientName}`, `{RecipientRole}` | `[Bracket Placeholder]` — never prefilled |
| Per-letter dialog | Where | Folded into `{Facts}` narrative | n/a |
| Per-letter dialog | What Happened | `{Facts}` | n/a |
| Per-letter dialog | What Do You Want | `{DesiredOutcome}` | n/a |
| Per-letter dialog, near bottom (ask-adjacent) | Consequence | `{ConsequenceClause}` | **Clause omitted entirely if empty** — not a bracket, see Theory section above |
| Per-letter dialog, optional, bottom of modal | Additional Notes | Appended to `{Facts}` as a closing paragraph | n/a — overflow field, not required |
| Runtime | — | `{Date}` | n/a — always resolved |

This is additive on top of v1.2, not a rewrite of it. The bracket/curly mechanism stays exactly as specified; we're just giving it real data sources it didn't have before.

**One UI decision settled today, with implications below:** the main-screen Context Builder box stops being an input field entirely. It becomes read-only — grayed/placeholder-styled while empty, normal text once populated — rendering a preview of whatever was entered in the dialog. The modal is the sole source of truth; the box is a window onto it, not a parallel entry point.

---

## 1. Build Order & Why It's Sequenced This Way

Schema before runtime, runtime before UI, UI before content. Each phase below is independently testable and leaves the app in a working state — nothing here requires a big-bang cutover.

```
Phase 0  Schema & data model        (no behavior change — additive fields only)
Phase 1  FillPrompt rewrite          (the runtime promise: tokens → real value or bracket)
Phase 2  PromptBuilderDialog rewrite (data capture only, no assembly logic)
Phase 3  LetterTab wiring            (connects dialog output → FillPrompt)
Phase 4  Settings tab (new)          (the Postgres-backed profile UI)
Phase 5  .airpack content            (Legal Letters pack rewritten to use new tokens)
Phase 6  Verification                (the checklist that proves it all works)
```

---

## 2. Phase 0 — Schema & Data Model Foundations

**Files touched:**
- `PromptTemplateRegistry.cs` (modify — `PromptTemplate` class)
- `SenderProfile.cs` (new)
- Postgres: new `sender_profile` table

**Tasks:**

- [ ] `PromptTemplate` gains three new string properties, default empty: `HeaderTemplate`, `RecipientTemplate`, `FooterTemplate` (this is v1.2's checklist item 1, carried forward — never implemented).
- [ ] New `SenderProfile.cs` POCO: `SenderName`, `SenderRole`, `SenderCompany`, `SenderAddress`, `SenderCityStateZip`, `SenderPhone`, `SenderEmail` — all `string?`. Put it in the global/`AIB6` namespace alongside `LetterMetadata.cs` for consistency (note: `LetterMetadata.cs` itself currently lacks a namespace declaration — worth fixing both at once rather than copying the inconsistency forward).
- [ ] New table, single fixed-id row, upsert pattern:
  ```sql
  CREATE TABLE sender_profile (
    id INTEGER PRIMARY KEY DEFAULT 1,
    sender_name TEXT,
    sender_role TEXT,
    sender_company TEXT,
    sender_address TEXT,
    sender_city_state_zip TEXT,
    sender_phone TEXT,
    sender_email TEXT,
    updated_at TIMESTAMPTZ DEFAULT now()
  );
  ```
  One row, always `id = 1`. No multi-profile support — that's deliberately out of scope (see §8).

---

## 3. Phase 1 — `FillPrompt()` Rewrite (the Runtime Engine)

**Files touched:**
- `PromptTemplateRegistry.cs` (modify — `FillPrompt()` method)
- `PromptFillContext.cs` (new)

This is the core of v1.2's "one promise to every AirPack author" and where most of the actual logic change lives.

**`PromptFillContext.cs` (new):** a small POCO bundling everything the old positional parameter list doesn't cover — `SenderName`, `SenderRole`, `SenderCompany`, `SenderAddress`, `SenderCityStateZip`, `SenderPhone`, `SenderEmail`, `RecipientName`, `RecipientRole`, `Reference`, `Facts`, `DesiredOutcome`, `Consequence` (all `string?`, default null). This is added as a new **optional** parameter to `FillPrompt()` rather than restructuring the existing five positional arguments — keeps every current call site compiling unchanged.

**Tasks:**

- [ ] `FillPrompt(userInput, toneLabel, length, mainType, subType, context: PromptFillContext? = null)` — new optional parameter, existing five untouched.
- [ ] Existing 8-token substitution (`{UserInput}`, `{Tone}`, `{Length}`, `{Structure}`, `{Intent}`, `{MainType}`, `{SubType}`, `{Role}`) unchanged.
- [ ] New: if `PromptTemplateText` contains `{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}`, substitute with the corresponding `PromptTemplate` field's content (empty string if the field is blank — v1.2 checklist item 4).
- [ ] New: within the now-expanded text, resolve `{Date}` → `DateTime.Now` formatted as a normal date string (v1.2 checklist item 3).
- [ ] New: resolve `{SenderName}` / `{SenderRole}` / `{SenderCompany}` / `{SenderAddress}` / `{SenderCityStateZip}` / `{SenderPhone}` / `{SenderEmail}` / `{RecipientName}` / `{RecipientRole}` / `{Reference}` from `context` if present and non-empty (sanitized via `PromptSanitizer.Clean()`, same as every other interpolated value); otherwise convert to `[Bracket Placeholder]` via the humanization rule. `{Reference}` is a short value, same bracket-fallback treatment as the rest — no special handling needed.
- [ ] **New mechanism: `{ConsequenceClause}`.** This is the first token that resolves to *value-or-nothing* rather than *value-or-bracket*. If `context.Consequence` is present, `{ConsequenceClause}` resolves to a complete, runtime-composed sentence (e.g. `"Failure to remedy this within the stated deadline may result in {Consequence}."`, with the raw value sanitized and inserted). If `context.Consequence` is empty, `{ConsequenceClause}` resolves to an empty string — the entire clause disappears rather than leaving a half-formed sentence or a bracket placeholder mid-paragraph. The composition (the fixed wrapper sentence) lives in code, not in the `.airpack` — authors just drop `{ConsequenceClause}` on its own line in `PromptTemplateText` and the runtime decides whether anything renders there.
- [ ] New: `{Facts}` resolves from `context.Facts` if present; falls back to the `userInput` parameter if `context` is null or `Facts` is empty. With `UserInput` now read-only (Phase 3), this fallback is mostly defensive rather than a real user-facing path — the actual overflow mechanism going forward is the dialog's `AdditionalNotes` field, folded into `context.Facts` before it ever reaches `FillPrompt`. Worth keeping the fallback anyway: it's harmless, and it's what makes `{Facts}` degrade gracefully rather than crash if a future caller ever invokes `FillPrompt` without a context at all.
- [ ] New: `{DesiredOutcome}` resolves from `context.DesiredOutcome` — no fallback (this token genuinely has no legacy equivalent; if absent, it converts to a bracket placeholder like anything else unresolved).
- [ ] New: generic catch-all regex pass — any remaining `{PascalCaseToken}` anywhere in the fully-assembled prompt → `[Pascal Case Token]` (v1.2 checklist item 4, the safety net for typos/future tokens).
- [ ] Fixed anti-hallucination closing instruction — add one sentence reinforcing structural compliance: *"Follow the exact structure and order of the letter skeleton provided. Do not add, remove, or reorder sections."* This matters more on a smaller/quantized model where instruction-following on subtle formatting cues is weaker than on a frontier model — better to say it explicitly than assume it's inferred. This is a one-sentence code change (the instruction is appended in code, not per-pack), not a content edit — small enough to bundle into this phase rather than Phase 5.

---

## 4. Phase 2 — `PromptBuilderDialog.axaml.cs` Rewrite

**Files touched:**
- `PromptBuilderDialog.axaml.cs` (modify)
- `ContextBuilderResult.cs` (new, or nest inside the dialog file)

The dialog's job shrinks to **collect and sanitize**, not **assemble**. Today it builds one `AdditionalInfo` string; after this change it returns a small structured result and lets `LetterTab` decide how fields map to tokens — matching the separation of concerns already documented elsewhere in the codebase (dialog sanitizes, `FillPrompt` assembles). The dialog is also now the **only** way data enters the prompt — there's no longer a parallel freeform path on the main screen, so its output has to cover both the structured facts and the overflow case the old box used to handle.

**Tasks:**

- [ ] New `ContextBuilderResult.cs`: `Reference`, `WhoToRole`, `WhoToName`, `WhoFromRole`, `WhoFromName`, `Where`, `WhatHappened`, `WhatWanted`, `Consequence`, plus `AdditionalNotes` — all `string?`, each already passed through `PromptSanitizer.Clean()` (same pattern as today's `AdditionalInfo` assembly).
- [ ] Dialog field order, top to bottom: **Reference** (new, frame-adjacent — sits naturally near Who To/Who From since it's part of identifying the matter, not the narrative), Who To, Who From, Where, What Happened, What Do You Want, **Consequence** (new, ask-adjacent — pairs with "what I want" since a consequence only makes sense attached to a request), then Additional Notes at the very bottom as the overflow field.
- [ ] Dialog gains a new optional multi-line field at the bottom, below the structured fields — bound to `AdditionalNotes`. This is the escape hatch that replaces the old main-screen freeform box's role: anything that doesn't fit the structured fields goes here instead of nowhere.
- [ ] `OnInsert` no longer builds a labeled multi-line `AdditionalInfo` string for the prompt. Instead it populates a `ContextBuilderResult` and exposes it as a public property (e.g. `public ContextBuilderResult Result { get; private set; }`), then `Close(true)` as before.
- [ ] New: `ContextBuilderResult.ToPreviewText()` — renders a human-readable labeled summary (`Re:`, `From:`, `To:`, `Where:`, `What happened:`, `What I want:`, `Consequence:`, `Notes:` — same shape as today's `AdditionalInfo` string, just repurposed as a *display* artifact instead of the prompt payload). This is what the main-screen box shows after Insert.
- [ ] **Carry-forward on reopen.** The dialog constructor takes an optional existing `ContextBuilderResult`. If present (the pencil icon was clicked to *edit* an entry, not create a fresh one), every field — including `AdditionalNotes` — is prefilled from it. Without this, reopening to fix a typo in "What Happened" would silently blank out Who To, Where, and everything else, which would be a worse experience than the current single-freeform-box version.
- [ ] If no existing `ContextBuilderResult` (first open this session), prefill only `FromField`'s role/name from the cached `SenderProfile` — everything else starts blank, as today.
- [ ] `OnCancel` — unchanged.
- [ ] The existing five field names (`ToField`, `FromField`, `WhereField`, `WhereWhenField`, `WantField`) can stay as-is; renaming `WhereWhenField` → something clearer (it backs "What Happened? When," which is confusing next to `WhereField`) is optional cosmetic cleanup, not required for this work.

**Explicitly not in this build:** a dynamic, per-`.airpack`-defined field set (reading a `Fields[]` array from the pack JSON and generating controls programmatically). Discussed and deliberately deferred — the fixed five (plus Additional Notes) cover every sub-type in the one pack that exists today, and going dynamic now would multiply the cost of everything else in this phase (preview rendering, carry-forward, Settings-prefill mapping) for a flexibility nothing currently needs. Revisit only when a second `.airpack` genuinely can't fit the fixed shape — not before.

---

## 5. Phase 3 — `LetterTab.axaml.cs` Wiring

**Files touched:**
- `LetterTab.axaml.cs` (modify — `OnPromptBuilderClick`, `OnGenerateClick`, constructor)
- `LetterTab.axaml` (modify — `UserInput` becomes read-only, plus the empty/filled visual states)

**Tasks:**

- [ ] Constructor: fetch `SenderProfile` once via `PostgresHelper.GetSenderProfileAsync()` (null if no row saved yet), cache it on the tab instance for both dialog prefill and prompt assembly.
- [ ] `UserInput` textbox: set read-only in `.axaml` (`IsReadOnly="True"`). Two visual states — grayed/disabled-looking background while empty (still showing the `InputScaffold` watermark, retargeted from the old freeform-box hint to this preview box), normal text color once it's rendering real content. A plain read-only flag with no visual change would leave it looking exactly as editable as it does today — the gray-out is what actually tells the user not to click into it.
- [ ] `OnPromptBuilderClick` — open the dialog passing the existing `_contextResult` if one exists (carry-forward, per Phase 2). On `true`/Insert, store the returned `ContextBuilderResult` as `_contextResult` and set `UserInput.Text = _contextResult.ToPreviewText()`. The box no longer feeds the prompt directly — it only displays what's about to be sent.
- [ ] `OnGenerateClick` — build a `PromptFillContext` before calling `FillPrompt`:
  - `SenderName`/`SenderRole` = `_contextResult?.WhoFromName`/`WhoFromRole` if the dialog was used, else the cached `SenderProfile`'s values directly (covers the case where someone never opens the dialog but has a saved profile).
  - `SenderCompany`/`SenderAddress`/`SenderCityStateZip`/`SenderPhone`/`SenderEmail` = always straight from the cached `SenderProfile` — no per-letter override path exists for these.
  - `RecipientName`/`RecipientRole` = `_contextResult?.WhoToName`/`WhoToRole`.
  - `Reference` = `_contextResult?.Reference`.
  - `Facts` = `_contextResult.Where` (if non-empty, formatted as a leading "Location: {Where}" line), followed by `_contextResult.WhatHappened`, followed by `_contextResult.AdditionalNotes` (if non-empty, as a closing paragraph — no special label needed, it reads as a natural continuation of the facts).
  - `DesiredOutcome` = `_contextResult?.WhatWanted`.
  - `Consequence` = `_contextResult?.Consequence` — passed through raw; `FillPrompt` is what decides whether `{ConsequenceClause}` renders a sentence or disappears.
  - Call `template.FillPrompt(userInput, tone, length, mainType, subTypeId, context: promptFillContext)`.
- [ ] Worth a deliberate (not default) call: with the box now a reliable signal — still showing the watermark means the dialog was never used — should `OnGenerateClick` surface a quick confirm in that case, or keep today's behavior (silently fall back to the default `[Who]/[What]/[When]` scaffold)? See §8.

---

## 6. Phase 4 — Settings Tab (New)

**Files touched:**
- `SettingsTab.axaml.cs` (new — code-only, following `ImportTab`'s pattern rather than `LetterTab`'s `.axaml`-paired pattern, since this is a simple label/textbox form with no need for visual designer support)
- `PostgresHelper.cs` (modify — two new methods)
- `MainWindow.axaml` (modify — new `TabItem`)
- `MainWindow.axaml.cs` (modify — `OnTabChanged`, optional)

**Tasks:**

- [ ] `PostgresHelper.GetSenderProfileAsync()` — `SELECT` the single row (`id = 1`), return `null` if no row exists yet.
- [ ] `PostgresHelper.UpsertSenderProfileAsync(SenderProfile profile)` — `INSERT ... ON CONFLICT (id) DO UPDATE`, always targeting `id = 1`. (Plain parameterized SQL here rather than a new stored procedure — simpler for a single-row table; easy to switch to a `CALL`-based stored proc later if you want to mirror `insert_letter`'s convention exactly.)
- [ ] New `SettingsTab` — seven labeled text fields matching `SenderProfile`, a Save button calling `UpsertSenderProfileAsync`, loads existing values on construction via `GetSenderProfileAsync`.
- [ ] `MainWindow.axaml` — add a fourth `TabItem` (Header="Settings") alongside Create Drafts / Review Drafts / Import Airpacks.
- [ ] Optional but recommended: extend `OnTabChanged` so that switching *back* to "Create Drafts" after visiting "Settings" refreshes `LetterTab`'s cached `SenderProfile` — same pattern already used for refreshing the archive grid on "Review Drafts." Without this, a Settings edit won't show up in the dialog prefill until the app restarts.

---

## 7. Phase 5 — `.airpack` Content Updates

**Files touched:**
- The Legal Letters `.airpack` file(s) on disk (no code)

**Tasks:**

- [ ] Add `HeaderTemplate`/`RecipientTemplate`/`FooterTemplate` to each sub-type using the new named tokens, e.g.:
  ```json
  "HeaderTemplate": "{SenderName}\n{SenderRole}, {SenderCompany}\n{SenderAddress}\n{SenderCityStateZip}\n{Date}",
  "RecipientTemplate": "{RecipientName}\n{RecipientRole}"
  ```
- [ ] Rewrite `PromptTemplateText` per each sub-type to reference `{Facts}` and `{DesiredOutcome}` as two distinct paragraphs rather than one `{UserInput}` blob — this is also the moment to apply the "Word template" lever from yesterday's discussion (real letter prose with narrow placeholders, not a loose instruction), since you're touching this text anyway. Two birds, same edit.
- [ ] **The structural answer to "are we conveying correspondence theory to the LLM":** not through meta-instructions describing order (unreliable on a small/quantized model — telling it "put the recipient block first" is a request it can ignore), but by literally pre-assembling the skeleton in the correct sequence before the model ever sees it. By the time `FillPrompt` is done, most of the letter is already fixed text; the model's only real freedom is the prose *within* a handful of slots, not the order of the slots themselves. Every sub-type's `PromptTemplateText` should follow this skeleton, matching the Theory table at the top of this document:
  ```
  {HeaderTemplate}

  {RecipientTemplate}

  Re: {Reference}

  Dear {RecipientName},

  [fixed purpose sentence — e.g. "I am writing regarding the above-referenced
  claim, which was denied on {Date}, and to request that this denial be
  reconsidered."]

  {Facts}

  {DesiredOutcome}

  {ConsequenceClause}

  Sincerely,

  {FooterTemplate}
  ```
  Frame → Reference → Purpose → Facts → Ask → Consequence → Close, in that literal order, every time. `{ConsequenceClause}` sits on its own line/paragraph specifically so it can vanish cleanly when empty without leaving a gap or orphaned fixed text around it.
- [ ] `InputScaffold` watermark text can stay as-is or be retired — it was guidance for the old freeform box; with the structured dialog as primary input, it may no longer be the first thing users see. Worth a quick look once Phase 3 is live, not blocking.

---

## 8. Open Decisions Needing Your Call

A few defaults assumed above that are easy to change but worth confirming before or during the build:

**Generate with no Context Builder input.** Today's behavior (silent fallback to the default `[Who]/[What]/[When]` scaffold) is preserved by default above. Since the box is now a reliable "still showing placeholder" signal, a quick confirm dialog before generating is cheap to add if you'd rather not silently spend an inference cycle on empty input. Not built either way until you call it.

**Facts formatting.** Proposed: `Where` as a leading "Location: {value}" line, then `WhatHappened`, then `AdditionalNotes` as a closing paragraph if present. Simple to change if you want different formatting or separators.

**Profile cache refresh.** Proposed: cache `SenderProfile` at `LetterTab` construction, refresh on tab-switch back from Settings. Alternative: fetch fresh from Postgres every time the dialog opens (slightly simpler code, trivial Postgres cost, no caching/staleness logic at all) — also fine, your call.

**Settings tab UI style.** Proposed: code-only like `ImportTab`. Alternative: `.axaml`-paired like `LetterTab` if you'd rather lay it out visually. Either works.

*Settled, not open:* dynamic per-pack fields are deliberately out of this build (see Phase 2) — not a default awaiting confirmation, a decision already made.

---

## 9. Master File Touch List

| File | New / Modified | Phase |
|---|---|---|
| `PromptTemplateRegistry.cs` | Modified | 0, 1 |
| `SenderProfile.cs` | New | 0 |
| `PromptFillContext.cs` | New | 1 |
| `PromptBuilderDialog.axaml.cs` | Modified | 2 |
| `ContextBuilderResult.cs` | New | 2 |
| `LetterTab.axaml.cs` | Modified | 3 |
| `LetterTab.axaml` | Modified | 3 |
| `SettingsTab.axaml.cs` | New | 4 |
| `PostgresHelper.cs` | Modified | 4 |
| `MainWindow.axaml` | Modified | 4 |
| `MainWindow.axaml.cs` | Modified (optional) | 4 |
| Legal Letters `.airpack` file(s) | Modified (content only) | 5 |
| Postgres: `sender_profile` table | New | 0 |

---

## 10. Verification Checklist

- [ ] Empty profile, dialog never opened: generate a letter — all sender/recipient tokens degrade to `[Bracket Placeholder]`, `{Date}` still resolves, no `{CurlyBrace}` survives into the prompt.
- [ ] Profile saved, dialog never opened: `{SenderName}` etc. resolve from Settings; `{RecipientName}` still a placeholder (never prefilled, by design).
- [ ] Profile saved, dialog used with edited Who From: per-letter edit wins over Settings prefill for Name/Role; Company/Address/etc. still come from Settings (no per-letter field exists for them).
- [ ] `Facts` block shows Location line + What Happened text as one clean paragraph; `DesiredOutcome` appears as its own paragraph.
- [ ] Intentionally misspell a token in a test `.airpack` (e.g. `{SenderNmae}`) — confirm it becomes `[Sender Nmae]` rather than crashing or leaking raw into the model.
- [ ] Settings tab: save, switch to Create Drafts, open the Context Builder dialog — confirm prefill reflects the save without an app restart.
- [ ] Fill out the dialog, Insert — confirm `UserInput` switches from grayed/watermark to normal text showing the rendered preview, and that preview text reads cleanly (not a raw dump of the structured fields).
- [ ] Reopen the dialog via the pencil icon after an entry exists — confirm every field (including Additional Notes) repopulates from the prior entry rather than starting blank.
- [ ] Confirm clicking into the read-only `UserInput` box does nothing — no cursor, no edit — and that this is visually obvious before you even try clicking (the gray-out should make it clear without trial and error).
- [ ] Reference filled in: confirm `Re:` line renders the value. Reference left blank: confirm it degrades to `Re: [Reference]`, same as any other bracket-fallback token.
- [ ] Consequence filled in: confirm `{ConsequenceClause}` renders as a complete sentence, properly punctuated, reading naturally after the Ask paragraph. Consequence left blank: confirm the clause disappears entirely — no orphaned line, no bracket, no awkward gap in the letter.
- [ ] Generate a full letter end to end and confirm the output actually follows Frame → Reference → Purpose → Facts → Ask → Consequence → Close in that order — this is the real test of whether the skeleton approach is working, not just whether each token resolves correctly in isolation.

---

*Drafted June 16, 2026, following the Context Builder integration discussion. Revised same day to lock in the read-only preview box, carry-forward-on-reopen, and the Additional Notes overflow field, with dynamic per-pack fields explicitly deferred. Revised again same day to add Reference and Consequence (the two gaps surfaced by the correspondence-theory review) and the standard letter skeleton enforcing Frame → Reference → Purpose → Facts → Ask → Consequence → Close ordering. Supersedes nothing in `AirPack_Spec_v1.2.md` — this is the implementation layer for v1.2 plus the new token sources decided today.*
