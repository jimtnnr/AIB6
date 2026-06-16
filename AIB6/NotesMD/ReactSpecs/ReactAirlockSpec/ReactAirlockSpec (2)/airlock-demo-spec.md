# AirLock AI — Web Demo & Airpack/Postgres Migration Spec

**Status:** Draft v1 — captures all decisions made during scoping, intended to stand on its own without re-litigating prior discussion.
**Author context:** AirLock AI is currently a sealed, offline Avalonia desktop appliance (see main project README). This spec covers a *separate* initiative: a public, browser-based sales demo, plus the underlying Postgres-based airpack model that demo depends on.

---

## 1. Purpose

The primary deliverable is a working, public demo of AirLock's letter-drafting workflow, hosted at `demo.airlock-ai.com`, for prospective buyers of the physical appliance to try before purchasing. It is explicitly **not** a replacement for the appliance and does not retire Avalonia.

The demo is built with one eye on a possible future "connected SaaS" pivot — multi-tenancy is designed into the data model now — but no SaaS commitment exists. The stated business rule: **there will be no SaaS push without the device business having already succeeded.** Until then, this system is intentionally disposable: no durability, no backups, no hardening, no failover. If it breaks, it gets rebuilt from the same Docker Compose definition that created it. That stance applies to everything below unless stated otherwise.

---

## 2. Decisions Already Locked In

| Area | Decision | Rationale |
|---|---|---|
| Desktop wrapper | No Electron | Already tried, rejected by the team |
| Demo hosting | Self-managed on the same Lightsail VM as the LLM, not S3/CloudFront | Demo needs a live backend; S3/CloudFront is static-only |
| Domain | `demo.airlock-ai.com`, separate subdomain/deploy from the main marketing site | Avoids risking the live site; demo has different needs (stateful UI, backend calls) than the hand-built static brochure site |
| Database hosting | Self-hosted Postgres on the Lightsail box (not Neon) | Consolidates everything onto one box now that durability isn't a goal; avoids paying for a managed DB you don't need yet |
| Containerization | **Entire stack — Ollama, Postgres, API service, Caddy — runs as one Docker Compose definition** | Reproducibility: one `docker compose up -d` rebuilds the whole box. Removes the "8 manual setup steps" drift risk that exists in the current native appliance install process |
| Reverse proxy / TLS | Caddy, containerized, automatic Let's Encrypt cert | Far less config than nginx + certbot for a single subdomain |
| Streaming | **No token-by-token streaming in v1** — generate, then reveal | Real backend complexity reduction; acceptable demo UX tradeoff |
| Airpack storage | Postgres, JSONB blob mirroring the existing `.airpack` shape, plus a handful of real columns (`title`, `main_type`, `sub_type`, `label`, `tier`) pulled out for filtering | Trivial import/export round-trip; avoids guessing at a fully normalized schema before the pack format has proven stable |
| Airpack exchange format | `.airpack` JSON remains the canonical import/export format | Postgres becomes the runtime source of truth; JSON stays the portable format between systems |
| Demo content | Existing Legal Letters pack only, no Import UI exposed | No reason for a website visitor to import packs; adding a second vertical later is cheap once the plumbing exists |
| Multi-tenancy | Shared schema with a `tenant_id` column (not schema-per-tenant) | Standard pairing with most off-the-shelf paywall/auth wrappers (Clerk, Supabase Auth, WorkOS, etc.), which issue a tenant/org id you scope rows by |
| Tenant shape | 1 user : 1 tenant (no org/team/seats concept) | Confirmed — simplest possible "open hook," no need for role/seat modeling now |
| Paywall/auth integration | Deliberately **not** built. Left as an explicit open hook (see §8) | No wrapper chosen yet; designing around a specific one now risks designing around the wrong one |
| Lead capture / gating | None in v1 — no email wall, no form | Easier to add a gate later than remove a clunky one now |
| Abuse hardening | None in v1 — relying on obscurity (unlisted/low-traffic subdomain) for the time being | Explicit, time-boxed risk acceptance, not an oversight |
| Voice input | **Will never be part of the browser-based app**, full stop | Explicit product decision, independent of any future architecture choice |
| USB import/export | Out of scope for the demo entirely (no local drive to plug into a cloud VM) | N/A in a cloud context |
| Marketing site copy / redesign | Out of scope for this spec | Site is being reworked separately; this spec only adds the subdomain and its backend |

---

## 3. System Architecture

Single Lightsail VM running four Docker Compose services on one internal network. Only the reverse proxy is exposed to the internet.

```
                         Internet
                            │
                      80/443 (TLS)
                            │
                       ┌─────────┐
                       │  Caddy  │  (reverse proxy + static file server)
                       └────┬────┘
                  ┌─────────┴─────────┐
                  │                   │
         serves React dist/   reverse_proxy /api/*
                  │                   │
                  │             ┌───────────┐
                  │             │    API    │  (Node/Express — see §6)
                  │             └─────┬─────┘
                  │              ┌────┴────┐
                  │              │         │
                  │       ┌──────────┐ ┌────────┐
                  │       │ Postgres │ │ Ollama │
                  │       └──────────┘ └────────┘
                  │
            (no public traffic touches
             Postgres, Ollama, or API directly —
             all reachable only inside the compose network)
```

DNS: `demo.airlock-ai.com` → static IP attached to the Lightsail instance. Lightsail firewall: only 80/443 open. No CloudFront, no S3, no separate compute service involved.

---

## 4. Docker Compose Definition

```yaml
version: "3.9"

services:
  ollama:
    image: ollama/ollama
    volumes:
      - ollama-data:/root/.ollama
    environment:
      OLLAMA_HOST: 0.0.0.0:11434
      OLLAMA_NUM_THREADS: 16
      OLLAMA_CONTEXT_LENGTH: 2048
    restart: unless-stopped

  postgres:
    image: postgres:16
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./db/init:/docker-entrypoint-initdb.d   # schema + seed airpack run on first boot
    environment:
      POSTGRES_USER: airlock
      POSTGRES_PASSWORD: airlock_demo
      POSTGRES_DB: airlock_demo
    restart: unless-stopped

  api:
    build: ./api
    depends_on:
      - ollama
      - postgres
    environment:
      DATABASE_URL: postgres://airlock:airlock_demo@postgres:5432/airlock_demo
      OLLAMA_ENDPOINT: http://ollama:11434/api/generate
      DEMO_TENANT_ID: <fixed UUID seeded in init script>
      ALLOWED_ORIGIN: https://demo.airlock-ai.com
    restart: unless-stopped

  caddy:
    image: caddy:2
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - ./web/dist:/srv/demo
      - caddy-data:/data
    depends_on:
      - api
    restart: unless-stopped

volumes:
  ollama-data:
  postgres-data:
  caddy-data:
```

**Caddyfile:**

```
demo.airlock-ai.com {
    handle /api/* {
        reverse_proxy api:3000
    }
    handle {
        root * /srv/demo
        file_server
        try_files {path} /index.html
    }
}
```

The model itself (`mistral:7b-instruct-q4_K_M`) is pulled once after first boot via `docker compose exec ollama ollama pull mistral:7b-instruct-q4_K_M`, same as the existing appliance process — this could be folded into a compose `entrypoint` override later if fully hands-off boot becomes worth the extra complexity, but isn't required for v1.

**Rebuild procedure (the disaster-recovery plan, since no other one exists):** provision a fresh Lightsail instance, install Docker, clone the repo, run `docker compose up -d`. That's it.

---

## 5. Database Schema

```sql
-- Tenants — future-proofing for multi-tenancy / eventual SaaS.
-- v1 has exactly one row: the fixed "demo" tenant all anonymous traffic is attributed to.
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT,
    email TEXT,
    tier TEXT NOT NULL DEFAULT 'demo',     -- 'demo', 'trial', 'pro', etc. — drives airpack visibility later
    external_id TEXT,                      -- id issued by a future paywall/auth provider; null until that integration exists
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Users — kept as a separate table from tenants even though v1 is strictly 1:1,
-- because most off-the-shelf auth/paywall wrappers assume a user/org split,
-- and collapsing the two now would have to be undone later.
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    email TEXT,
    external_id TEXT,                      -- id issued by a future auth provider
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Airpacks — runtime source of truth. .airpack JSON files remain the import/export format;
-- this table is what the app actually reads from at request time.
CREATE TABLE airpacks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title TEXT NOT NULL,                   -- e.g. "Legal Letters"
    main_type TEXT NOT NULL,                -- e.g. "Demand"
    sub_type TEXT NOT NULL,                 -- e.g. "payment_demand"
    label TEXT NOT NULL,                    -- e.g. "Payment Demand"
    tier TEXT NOT NULL DEFAULT 'demo',      -- gates pack visibility by subscription tier later
    pack_data JSONB NOT NULL,               -- full payload: Structure, Intent, InputScaffold,
                                             -- ToneDirectives, PromptTemplateText, RoleInstruction, _sigil, etc.
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Generation events — lightweight usage tracking. Cheap to add now, expensive to backfill later
-- if usage-based billing ever becomes relevant. Not wired to anything yet.
CREATE TABLE generation_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    airpack_id UUID REFERENCES airpacks(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Assumptions baked into this schema, stated explicitly so they're decisions rather than accidents: airpacks are **global, shared content gated by tier**, not owned per-tenant — there is no current concept of a tenant bringing their own custom pack. If custom/branded packs per buyer organization become a real need later, that requires adding ownership/scoping columns to `airpacks` at that time, not before.

The demo seeds exactly one `tenants` row and one `airpacks` row (the existing Legal Letters pack, ported into `pack_data` as-is) via the Postgres init script that runs on first container boot.

---

## 6. API / Backend Service

A small Node/Express service (default recommendation — pairs naturally with the React/Vite frontend and keeps the stack to one language; not a hard requirement, just the path of least resistance unless there's a reason to prefer something else). Responsibilities:

- Loads the seeded airpack from Postgres and serves its metadata (letter type, sub-types, input scaffold) to the frontend, mirroring what `PromptTemplateRegistry` does today for the desktop app.
- Assembles the final prompt from template + user-submitted facts + tone + length, mirroring the existing `PromptSanitizer` / `PromptMappings` logic. Input sanitization rules carry over from the desktop app, including the existing fix preserving `\n`/`\t` from structured input.
- Calls `http://ollama:11434/api/generate` (the compose service name resolves the same way `localhost:11434` does on the appliance) and waits for the full response — no streaming back to the client in v1.
- Tags every generation with the fixed demo `tenant_id` and writes a row to `generation_events`, exercising the tenant-scoped code path even though there's no real auth yet.
- Enforces CORS against `https://demo.airlock-ai.com` specifically — not a wildcard origin, since this is a publicly reachable endpoint.

No streaming, no persistence of generated letter content, no Import endpoint in v1 — those are explicitly out of scope (§9).

---

## 7. Frontend (Demo App)

React + Vite, its own codebase and deploy pipeline, entirely separate from the existing hand-built marketing site at `airlock-ai.com`. It is **not** bound by the marketing site's constraints (no Tailwind, grid-only, no CDN dependencies) — that philosophy fit a static brochure page; the demo is a meaningfully more stateful application and should use whatever's pragmatic.

Built artifact (`dist/`) is deployed by building locally/in CI and copying the result directly onto the Lightsail box (`scp`/`rsync`) into the directory Caddy serves from — no S3 bucket, no CloudFront invalidation step, consistent with the "rebuild in minutes" posture for this whole system.

v1 functional scope:
- Single letter-type flow driven by the one seeded airpack (no type/pack picker needed yet).
- Structured input form matching the airpack's `InputScaffold` (who, amount, dates, etc.), tone, and length — same inputs the desktop `PromptBuilderDialog` collects today.
- Submit → loading state → full generated letter displayed once the API responds (no streaming).
- No login, no save/archive, no export, no voice input.

---

## 8. Multi-Tenancy & the Paywall Hook (Deferred, By Design)

The schema in §5 exists specifically so that *if* the device business succeeds and a SaaS pivot becomes real, the foundational tenant-scoping is already in place rather than retrofitted under pressure. What is **not** designed yet, intentionally, is the actual paywall/auth wrapper integration — no specific vendor (Clerk, Stripe, Supabase Auth, WorkOS, Outseta, etc.) has been chosen, and designing the hook around a guess risks designing around the wrong product.

The expected shape, when that decision gets made: the chosen wrapper handles signup, login, and billing entirely; on successful signup it issues a tenant/org identifier (via webhook or a token claim); the API's job at that point is just to provision a `tenants` row (and a `users` row) keyed off that external id, defaulting to whatever tier the signup corresponds to. The `external_id` columns on both `tenants` and `users` already exist for this purpose — they're simply unpopulated until that wrapper is chosen and wired in.

This is the "open hook" referred to throughout scoping: a deliberate, named gap, not a forgotten one.

---

## 9. Explicitly Out of Scope for This Phase

Recording these so they don't get silently relitigated later — each was raised and deliberately deferred, not overlooked:

Streaming token-by-token generation. Lead capture, email gates, or CRM integration of any kind. Paywall/billing wrapper integration — the hook exists, the wrapper doesn't. Multiple airpacks or an Import-a-pack UI inside the demo. Backups, point-in-time recovery, high availability, or any failover design for either Postgres or the LLM container. Rate limiting, captcha, or other abuse mitigation beyond an unlisted subdomain and origin-restricted CORS. USB import/export (not applicable to a cloud-hosted demo with no local drives). Voice input, permanently, regardless of future architecture. Marketing site copy, positioning, or redesign beyond the existence of the `demo.airlock-ai.com` subdomain itself. Migrating the physical appliance off Avalonia — see §10, this is a live question but not one this spec resolves.

---

## 10. Relationship to the Physical Appliance (Forward-Looking, Not Committed)

Two genuinely separate questions came up during scoping and it's worth keeping them separate going forward.

**Backend containerization is unambiguous progress for the appliance too, independent of any UI decision.** The current native appliance install process is roughly eight manual steps performed by hand on each physical box (CPU governor tuning, Ollama container creation, model pull, systemd warmup unit, native PostgreSQL install + restore, USB tooling install, `appsettings.json` configuration, build). That manual process is where machine-to-machine drift actually comes from. Collapsing Postgres, Ollama, and (if it's ever needed locally) the API service into one Compose file — the same one built for this demo — means the backend gets proven in production via public demo traffic before it ever touches a customer's box. The existing Avalonia app doesn't need to change at all to benefit from this: it already talks to Ollama and Postgres over `localhost`, and doesn't know or care whether those services are bare-metal or containerized underneath. **Recommendation: adopt Docker Compose for the appliance's backend services regardless of what happens with the UI question below.**

**Whether the appliance's UI itself ever moves from Avalonia to a browser-based React frontend is a separate, still-open question**, explicitly not decided by this spec. Voice input is moot for that decision now (ruled out permanently, independent of architecture). The one substantive blocker identified during scoping is USB drive handling:

- WebUSB is permanently unavailable for this purpose — browser vendors deliberately refuse WebUSB access to anything the OS classifies as Mass Storage, as a security boundary against raw block-level I/O from a web page. This is not a temporary limitation.
- The File System Access API (`showDirectoryPicker()` / `showOpenFilePicker()`, available in Chromium, which is what a kiosk would run anyway) *can* do the actual file copying for import/export, but only in response to a manual user action — there is no way for a web page to passively detect "a drive was just inserted" the way `UsbDriveScanner` does today. That breaks the "app finds the drive, user just plugs it in" behavior that's an explicit design principle in the project's own positioning docs.
- **Documented solution-in-principle, not yet built:** a small native "USB sidecar" — a minimal compiled helper process (e.g. Go or Rust, no runtime dependencies) running on the host, outside both the browser sandbox and Docker's isolation (or as a privileged container with `/run/udev` and the dbus socket bind-mounted in). It watches for drive insertion via udisks2/dbus signals exactly as `UsbDriveScanner` does today, and exposes a minimal local HTTP/WebSocket API the browser-based frontend can poll or subscribe to for drive status, and call to actually trigger import/export. This restores true auto-detection with no UX regression versus the current app.
- Installation cost for this sidecar is incremental, not novel — appliance setup already requires running provisioning scripts by hand (Docker, systemd units, etc.); the sidecar is one more binary and one more systemd unit in that same script, comparable to the existing `airlock-warmup.service`. It's also a natural candidate to be baked into a future golden image, turning "install on a box" into "flash an image" rather than running scripts at all.

Net position: there is no hard technical blocker to eventually replacing Avalonia with a browser-based UI on the appliance, but the decision should be made deliberately when it's actually being considered, not implied by anything in this spec. This section exists so that conversation doesn't have to start from zero next time.

---

## 11. Open Items Carried Forward (Not Blocking, Worth Tracking)

A short list of things flagged during scoping that were deliberately left soft rather than pinned down, so they don't get lost: which specific paywall/auth wrapper to eventually adopt (§8); whether airpacks should ever support tenant-level ownership/customization rather than purely tier-gated shared content (§5); whether the Lightsail box should eventually split Postgres onto its own instance once/if real traffic exists (noted only as a future consideration, not a v1 requirement); and the exact moment, if ever, the appliance UI migration in §10 gets formally taken up.

---

## 12. UI Reference: Current Avalonia Screens

Screenshots of the live Avalonia app, archived alongside this spec as the visual source of truth for the React build. Stored at `ui-reference-screens/` next to this document. Cross-referenced against the actual `.axaml`/`.cs` source reviewed during scoping — call-outs below are things visible only in the screenshots, not derivable from code alone.

### 01 — Create Drafts (main letter-generation screen)
![Create Drafts](ui-reference-screens/01-create-drafts.png)

Matches `LetterTab.axaml` exactly: four dropdowns in a row (Letter Type, Letter Sub-Type, Escalation Level, Letter Length), a "Context Builder" label with a small pencil icon button that opens the structured-input dialog, a multi-line context box showing the airpack's `InputScaffold` as watermark text, the orange Generate Draft / Save Draft buttons, and the read-only Draft Preview box below. The watermark text visible here ("[Who] submitted the claim / [What] was denied / [When] the denial occurred / [Why] the denial should be reconsidered") is the `InputScaffold` for the **Appeal → Insurance Claim Appeal** sub-type specifically — confirms each sub-type carries its own distinct scaffold text, not a shared generic one. The window title bar reads "Legal Letters," confirming `MainWindow.axaml.cs` pulls its title from the first loaded template's `Title` field.

### 02 — Review Drafts (archive grid)
![Review Drafts](ui-reference-screens/02-review-drafts.png)

Matches the imperative grid built in `ArchiveGridView.axaml.cs`: search box top-left, "Show Hidden" toggle top-right, sortable Filename/Timestamp column headers (note the ↓ arrow confirming active sort direction), star/hide checkboxes per row, an orange "View" link per row, and Previous/Page N/Next pagination with no total-page-count shown — consistent with `PageLabel.Text` only ever being set to `"Page {page}"`.

Worth flagging since it's new information not present in any doc or code reviewed so far: several filenames include a `(Strict Facts)` suffix on the sub-type — e.g. "Appeal - Insurance Claim Appeal(Strict Facts) / Reminder / Short" alongside plain "Appeal - Insurance Claim Appeal / Reminder / Medium" rows. That implies there are (or were) two distinct airpack sub-type variants in play — a standard one and a stricter fact-adherence variant — that aren't both accounted for in the single example schema shown in the README. Worth confirming whether that's an intentional second sub-type worth carrying into the React/Postgres version, or a leftover test variant.

### 03 — Import Airpacks
![Import Airpacks](ui-reference-screens/03-import-airpacks.png)

Matches `ImportTab.axaml.cs` exactly: title, instructional text, the orange "Click - Import Airpack (.airpack)" button, and the gray "Debug: Scan USB Drives" button beneath it — confirmed still present and not yet removed, consistent with the tech-debt note that it's pending cleanup. Not in scope for the demo (§9), but useful as exact reference if this screen is ever ported for the appliance.

### 04 — Letter Preview Dialog (opened from Review Drafts → View)
![Letter Preview Dialog](ui-reference-screens/04-letter-preview-dialog.png)

Matches `LetterPreviewDialog.cs`: modal title bar showing the raw filename, scrollable letter body, Export/Close buttons bottom-right. One thing worth flagging directly rather than letting it pass quietly: the dialog title reads `Appeal_InsuranceClaimAppeal(StrictFacts)_Reminder_Medium_...`, but the letter body underneath is a payment-demand letter ("Subject: Urgent Demand for Payment — Outstanding Obligation," referencing a goods-delivery contract) — content that reads like the **Demand → Payment Demand** pack, not an insurance appeal. That's either a mislabeled test artifact from manual testing, or a real mismatch between the selected template and the generated content. Worth a quick sanity check before treating this screenshot's letter content as representative of the Appeal pack's actual output.

### 05 — Context Builder dialog
![Context Builder Dialog](ui-reference-screens/05-context-builder-dialog.png)

Matches `PromptBuilderDialog.axaml` exactly: five labeled fields (Who To, Who From, Where, What Happened/When, What You Want/When) with placeholder example text in each, Insert/Cancel buttons bottom-right. Of the five screens, this is the most directly portable to a React form component with no structural ambiguity — straightforward one-to-one mapping from XAML fields to form inputs.
