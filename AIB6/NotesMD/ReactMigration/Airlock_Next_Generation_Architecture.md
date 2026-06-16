# Airlock Next Generation Architecture

## React Migration, Postgres-Based AirPacks, and SaaS-Ready Platform Plan

**Version:** 1.0  
**Purpose:** Capture the architectural shift from an Avalonia-centered desktop application to a React/API/Postgres/AirPack runtime that can support local appliance, hosted demo, and SaaS deployment modes.

---

## 1. Core Insight

Airlock is not fundamentally an Avalonia application.

Airlock is a **metadata-driven AI workflow runtime**.

The durable product assets are:

- AirPacks
- Workflow metadata
- Prompt assembly
- Template definitions
- Input schemas
- Output generation
- Provider abstraction
- Local/private AI execution where required

Avalonia was one possible user interface. It is not the product boundary.

The new architecture should treat the UI as replaceable and make the AirPack runtime the center of the system.

---

## 2. Strategic Reframe

### Old Mental Model

```text
Avalonia Desktop App
   |
Postgres
   |
Ollama
   |
Qwen 3B
```

This made Airlock feel like a desktop application with an embedded AI workflow.

### New Mental Model

```text
React UI
   |
API Layer
   |
Postgres AirPack Runtime
   |
Model Provider Layer
      |-- Ollama / Qwen
      |-- Claude
      |-- GPT
      |-- Grok
```

This makes Airlock a platform that can run in multiple deployment modes.

---

## 3. Product Boundary

The product is not:

- Avalonia
- Docker
- Lightsail
- Ollama
- Qwen
- React
- SaaS shell

The product is:

```text
AirPack + Runtime + Prompt Engine + Output Workflow
```

Everything else is delivery infrastructure.

---

## 4. Why This Matters

This architecture allows Airlock to support three deployment modes from the same core engine:

### 4.1 Local Appliance Mode

```text
Browser
   |
Local Airlock Appliance
   |
API
   |
Postgres
   |
Ollama / Qwen
```

Use case:

- Law firms
- Privacy-sensitive environments
- Air-gapped offices
- Local-only document workflows

The user opens:

```text
http://airlock.local
```

or:

```text
http://192.168.x.x
```

No desktop app required.

---

### 4.2 Hosted Demo Mode

```text
React Site on S3 / CloudFront
   |
Lightsail Public IP API
   |
Postgres
   |
Ollama / Qwen 3B
```

Use case:

- Public demos
- PMF testing
- Sales calls
- Lead generation
- Proof of concept

React can call the Lightsail API directly by IP and port during MVP testing:

```text
http://LIGHTSAIL_PUBLIC_IP:PORT/api/chat
```

No domain required for early demo validation.

---

### 4.3 SaaS Mode

```text
React / Next.js SaaS Shell
   |
Auth / Billing / Teams
   |
API
   |
Postgres
   |
Model Provider Layer
      |-- Claude
      |-- GPT
      |-- Grok
      |-- Ollama if desired
```

Use case:

- Public cloud Airlock
- Subscription product
- Team accounts
- User management
- Stripe billing
- Multi-tenant AirPack marketplace

---

## 5. Architectural Principle

The same AirPack engine should work across all modes.

```text
AirPack Package
   |
Import
   |
Postgres JSONB Runtime
   |
React Dynamic UI
   |
Prompt Assembly
   |
Model Provider
   |
Generated Output
```

The deployment target changes. The AirPack runtime stays stable.

---

## 6. Migration Rationale: Why Move Away from Avalonia

Avalonia provided a useful early implementation path, but the current Airlock direction reduces the need for desktop-native capabilities.

### Avalonia Advantages

- Desktop app behavior
- Local machine deployment
- Native windowing
- Potential file system access
- USB workflow support

### Avalonia Costs

- Cross-platform UI complexity
- More difficult demo distribution
- Harder to expose to web prospects
- More friction for PMF testing
- Requires desktop installation
- Less natural for SaaS path
- More specialized tooling

### React Advantages

- Browser-native demo
- Easy deployment
- Easier AI-assisted development
- Easier hiring later
- Works across desktop, tablet, and appliance browser
- Natural SaaS path
- Easier to integrate with existing SaaS starter kits
- Better for rapid PMF testing

### Decision

Do not immediately delete Avalonia.

Instead:

1. Extract the core AirPack runtime.
2. Build a React front end that replicates the current workflow.
3. Move AirPack runtime data into Postgres.
4. Use React as the primary demo and future UI path.
5. Let Avalonia become optional or legacy.

---

## 7. Browser vs Desktop Boundary

A browser can handle:

- AirPack selection
- Dynamic forms
- Prompt input
- Letter generation
- Output display
- TXT download
- PDF download
- DOCX download
- AirPack upload
- AirPack catalog
- User authentication
- SaaS dashboard

A browser cannot easily handle:

- Automatic USB detection
- Arbitrary local file writes
- Background system tray operation
- Deep desktop integration
- Native OS-level file monitoring

### Key Realization

For legal letter generation, the browser can handle almost everything required for the initial product and demo.

If future local desktop privileges are required, use a small local agent:

```text
Browser UI
   |
localhost API
   |
Airlock Local Agent
   |
USB / File System / Local Services
```

But do not make that the core architecture unless the product truly requires it.

---

## 8. Postgres-Based AirPacks

### 8.1 Why Postgres

Airlock already relies on Postgres. Postgres becomes more important in the React architecture because it naturally stores the runtime state behind the API.

Postgres can store AirPack definitions using `JSONB`.

This allows AirPacks to remain flexible and metadata-driven without creating excessive relational complexity.

---

### 8.2 AirPack as Portable Package

AirPacks should remain portable.

The `.airpack` file remains the distribution format.

```text
legal_letters.airpack
```

But at runtime:

```text
.airpack file
   |
Upload / Import
   |
Validate
   |
Store in Postgres JSONB
   |
Render with React
```

This gives the best of both worlds:

- Portable package format
- Database-backed runtime
- Easy upload
- Easy versioning
- Easy marketplace path
- Easy SaaS storage

---

## 9. Proposed Postgres Schema

### 9.1 Minimal JSONB-First Schema

```sql
CREATE TABLE airpacks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    version TEXT NOT NULL,
    description TEXT,
    category TEXT,
    definition JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

The `definition` column contains the complete AirPack JSON.

Example:

```json
{
  "title": "Demand Letter",
  "category": "Legal",
  "description": "Generate a structured legal demand letter.",
  "templates": [
    {
      "id": "demand-letter-basic",
      "name": "Basic Demand Letter",
      "fields": [
        {
          "name": "clientName",
          "label": "Client Name",
          "type": "text",
          "required": true
        },
        {
          "name": "opposingParty",
          "label": "Opposing Party",
          "type": "text",
          "required": true
        },
        {
          "name": "facts",
          "label": "Facts",
          "type": "textarea",
          "required": true
        }
      ],
      "systemPrompt": "You are drafting a professional legal demand letter.",
      "userPromptTemplate": "Draft a demand letter for {{clientName}} against {{opposingParty}} using these facts: {{facts}}"
    }
  ]
}
```

---

### 9.2 Generated Output Table

```sql
CREATE TABLE generations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    airpack_id UUID NOT NULL REFERENCES airpacks(id),
    template_id TEXT NOT NULL,
    provider TEXT NOT NULL,
    model TEXT NOT NULL,
    input_data JSONB NOT NULL,
    prompt_text TEXT NOT NULL,
    output_text TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

This stores:

- selected AirPack
- selected template
- provider used
- model used
- input values
- assembled prompt
- generated output
- timestamp

This is useful for:

- audit trail
- debugging
- quality review
- replaying examples
- future billing metrics

---

### 9.3 Optional Relational Expansion Later

If needed later, split into relational tables:

```text
airpacks
airpack_versions
templates
template_fields
provider_configs
generation_history
organizations
users
```

But do not over-normalize early.

Start with JSONB.

---

## 10. AirPack Upload Flow

### User Flow

```text
AirPack Manager
   |
Upload AirPack
   |
Choose file: legal_letters.airpack
   |
Validate
   |
Install
   |
AirPack appears in catalog
```

### Technical Flow

```text
React Upload Component
   |
POST /api/airpacks/upload
   |
API receives file
   |
Validate JSON / package structure
   |
Parse manifest
   |
Insert into Postgres JSONB
   |
Return installed AirPack metadata
```

### Example API Endpoint

```http
POST /api/airpacks/upload
Content-Type: multipart/form-data
```

### API Behavior

1. Read uploaded file.
2. If `.airpack` is a ZIP, extract it.
3. Locate manifest JSON.
4. Validate required fields.
5. Store the AirPack definition in Postgres.
6. Return install status.

---

## 11. React UI Design

The React UI should be metadata-driven.

React does not hardcode legal letters.

React asks the API for available AirPacks:

```http
GET /api/airpacks
```

Then asks for a selected AirPack:

```http
GET /api/airpacks/{id}
```

Then renders the form from the AirPack definition.

---

### 11.1 React Workflow

```text
AirPack Catalog
   |
Select AirPack
   |
Select Template
   |
Dynamic Input Form
   |
Generate
   |
Output Viewer
   |
Download TXT / PDF / DOCX
```

---

### 11.2 Dynamic Form Rendering

AirPack field definition:

```json
{
  "name": "facts",
  "label": "Facts",
  "type": "textarea",
  "required": true
}
```

React renders:

```text
Facts
[ textarea input ]
```

Supported field types:

- text
- textarea
- select
- multi-select
- checkbox
- date
- number
- file upload later

---

## 12. Output Handling

Current desktop output is TXT.

In React, output can be displayed directly in the browser.

### Output Options

```text
Generated Letter
   |
Display in text editor
   |
Copy to clipboard
   |
Download .txt
   |
Download .pdf
   |
Download .docx
```

### Minimal MVP

Start with:

- display text
- copy button
- download `.txt`

Later add:

- PDF export
- DOCX export
- formatting templates
- letterhead
- signature blocks

---

## 13. Model Provider Layer

The AirPack engine should not care which model generates the output.

Model providers should be adapters.

```text
AirPack Engine
   |
Provider Interface
   |-- OllamaProvider
   |-- ClaudeProvider
   |-- OpenAIProvider
   |-- GrokProvider
```

---

### 13.1 Provider Interface Concept

```csharp
public interface IModelProvider
{
    Task<string> GenerateAsync(ModelRequest request);
}
```

Example request:

```csharp
public class ModelRequest
{
    public string SystemPrompt { get; set; }
    public string UserPrompt { get; set; }
    public string Model { get; set; }
    public double Temperature { get; set; }
}
```

---

### 13.2 Ollama Provider

```text
API
   |
localhost:11434
   |
Ollama
   |
Qwen 3B
```

For hosted demo or appliance mode.

---

### 13.3 Claude / GPT / Grok Providers

For SaaS mode, the API can call external model providers.

```text
Airlock API
   |
Claude / GPT / Grok API
```

This allows Airlock to test:

- local model quality
- Claude quality
- GPT quality
- Grok quality
- cost differences
- latency differences

The customer buys the workflow, not the model.

---

## 14. Deployment Mode Details

## 14.1 Hosted Demo on Lightsail

### Goal

Validate:

```text
Can a prospect generate a useful legal letter in a browser?
```

### Minimum Setup

```text
React on S3 / CloudFront
Lightsail instance
Docker
Postgres
Ollama
Qwen 3B
API server
```

### Simple Request Flow

```text
Browser
   |
S3 / CloudFront React App
   |
http://LIGHTSAIL_IP:API_PORT/api/generate
   |
API Server
   |
Postgres
   |
Ollama :11434
   |
Qwen 3B
```

### IP-Based API for MVP

Use:

```text
http://LIGHTSAIL_PUBLIC_IP:5000/api/generate
```

or if exposing Ollama directly for very early testing:

```text
http://LIGHTSAIL_PUBLIC_IP:11434/api/generate
```

Better pattern:

```text
React
   |
LIGHTSAIL_IP:5000/api/generate
   |
Backend API
   |
localhost:11434
   |
Ollama
```

Do not expose Ollama directly longer than needed.

---

### Lightsail Instance Recommendation

For 3B Qwen demo:

- Minimum test: 4 GB RAM
- More comfortable: 8 GB RAM

Recommended starting point:

```text
Lightsail 8 GB RAM instance
```

Reason:

- Avoid fighting memory limits
- Allows Postgres + API + Ollama together
- Better demo reliability
- Still inexpensive relative to EC2 complexity

---

## 14.2 Local Appliance Deployment

### Goal

Deliver private/local AI generation without SaaS dependency.

### Architecture

```text
Customer Browser
   |
Local Appliance IP
   |
React App served locally
   |
API
   |
Postgres
   |
Ollama
   |
Qwen 3B
```

### Appliance Stack

```text
Ubuntu
Docker Compose
React static files
API server
Postgres
Ollama
Qwen model
```

### User Experience

Customer plugs in appliance, connects to local network, opens:

```text
http://airlock.local
```

or:

```text
http://192.168.1.xx
```

The UI is browser-based, but the data remains local.

---

## 14.3 SaaS Deployment

### Goal

Hosted subscription Airlock with login, billing, users, and teams.

### Architecture

```text
SaaS Shell
   |
Authentication
   |
Organizations / Teams
   |
Stripe Billing
   |
AirPack Runtime
   |
Provider Layer
      |-- Claude
      |-- GPT
      |-- Grok
      |-- Ollama optional
```

### Use Off-the-Shelf SaaS Starter Kit

Do not build commodity SaaS shell features from scratch.

Commodity features include:

- login
- password reset
- organizations
- teams
- billing
- subscriptions
- admin screens
- settings
- user roles
- Stripe integration

Airlock's value is not account management.

Airlock's value is the AirPack workflow engine.

---

## 15. SaaS Starter Kit Requirements

When choosing a SaaS starter kit, look for:

- React or Next.js
- Authentication included
- Stripe billing included
- Organization/team support
- PostgreSQL support
- API routes or backend integration
- Role-based access control
- Easy deployment path
- Clean codebase
- Good documentation

Useful categories:

```text
Next.js SaaS starter
React SaaS boilerplate
Stripe SaaS starter
Postgres SaaS starter
```

The starter kit should provide the shell.

Airlock adds:

```text
AirPack Runtime
AirPack Manager
Dynamic Form Renderer
Generation Engine
Provider Adapters
Output Viewer
```

---

## 16. Core API Surface

### AirPack APIs

```http
GET /api/airpacks
GET /api/airpacks/{id}
POST /api/airpacks/upload
DELETE /api/airpacks/{id}
```

### Template APIs

```http
GET /api/airpacks/{id}/templates
GET /api/airpacks/{id}/templates/{templateId}
```

### Generation APIs

```http
POST /api/generate
GET /api/generations
GET /api/generations/{id}
```

### Provider APIs

```http
GET /api/providers
POST /api/providers/test
```

---

## 17. Generation Flow

```text
User selects AirPack
   |
User selects template
   |
React renders form from metadata
   |
User fills fields
   |
POST /api/generate
   |
API loads AirPack from Postgres
   |
API validates input
   |
API assembles prompt
   |
API chooses provider
   |
Provider generates text
   |
API stores generation
   |
React displays output
```

---

## 18. AirPack Definition Example

```json
{
  "id": "legal-letters",
  "name": "Legal Letters",
  "version": "1.0.0",
  "description": "Legal letter generation workflows.",
  "templates": [
    {
      "id": "demand-letter",
      "name": "Demand Letter",
      "description": "Generate a professional demand letter.",
      "fields": [
        {
          "name": "clientName",
          "label": "Client Name",
          "type": "text",
          "required": true
        },
        {
          "name": "opposingParty",
          "label": "Opposing Party",
          "type": "text",
          "required": true
        },
        {
          "name": "matterSummary",
          "label": "Matter Summary",
          "type": "textarea",
          "required": true
        },
        {
          "name": "tone",
          "label": "Tone",
          "type": "select",
          "required": true,
          "options": [
            "Firm",
            "Professional",
            "Aggressive",
            "Settlement-Oriented"
          ]
        }
      ],
      "systemPrompt": "You are an assistant drafting a professional legal demand letter. Do not invent facts. Use only provided facts.",
      "userPromptTemplate": "Draft a {{tone}} demand letter for {{clientName}} involving {{opposingParty}}. Matter summary: {{matterSummary}}"
    }
  ]
}
```

---

## 19. Migration Plan

## Phase 1: Freeze Avalonia Scope

Do not keep expanding Avalonia while evaluating React.

Avalonia becomes the reference implementation.

Deliverables:

- Identify current Avalonia screens
- Identify current AirPack JSON structure
- Identify current Postgres usage
- Identify current prompt assembly logic
- Identify current output flow

---

## Phase 2: Define AirPack Runtime Contract

Create a clean AirPack schema.

Deliverables:

- AirPack manifest format
- Template structure
- Field type definitions
- Prompt assembly rules
- Output format rules
- Versioning rules

---

## Phase 3: Store AirPacks in Postgres JSONB

Deliverables:

- `airpacks` table
- upload/import endpoint
- list endpoint
- retrieve endpoint
- basic validation

Success criteria:

```text
Upload .airpack file -> stored in Postgres -> retrievable as JSON
```

---

## Phase 4: Build React AirPack Catalog

Deliverables:

- AirPack list page
- AirPack detail page
- template selection
- dynamic form rendering

Success criteria:

```text
React renders form from Postgres-stored AirPack metadata
```

---

## Phase 5: Connect Generation Flow

Deliverables:

- generate endpoint
- prompt assembly service
- Ollama provider
- generation storage
- output display

Success criteria:

```text
User fills React form -> Qwen generates letter -> React displays output
```

---

## Phase 6: Hosted Demo on Lightsail

Deliverables:

- Lightsail instance
- Docker Compose
- Postgres container
- Ollama container
- API container
- React deployed on S3 / CloudFront or served locally

Success criteria:

```text
External user can generate a letter through a browser
```

---

## Phase 7: Add Provider Abstraction

Deliverables:

- provider interface
- Ollama provider
- Claude provider later
- OpenAI provider later
- Grok provider later

Success criteria:

```text
Same AirPack can generate using different model providers
```

---

## Phase 8: SaaS Shell Evaluation

Deliverables:

- select starter kit
- evaluate auth
- evaluate Stripe
- evaluate organizations/teams
- evaluate Postgres compatibility
- identify integration points

Success criteria:

```text
AirPack runtime can be inserted into SaaS shell without rebuilding commodity SaaS features
```

---

## 20. Docker Compose Concept

For appliance or Lightsail demo:

```yaml
version: "3.9"

services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: airlock
      POSTGRES_USER: airlock
      POSTGRES_PASSWORD: airlock_dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  ollama:
    image: ollama/ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama

  api:
    build: ./api
    ports:
      - "5000:5000"
    environment:
      DATABASE_URL: postgres://airlock:airlock_dev_password@postgres:5432/airlock
      OLLAMA_URL: http://ollama:11434
    depends_on:
      - postgres
      - ollama

volumes:
  postgres_data:
  ollama_data:
```

React can call:

```text
http://LIGHTSAIL_IP:5000/api/generate
```

The API calls Ollama internally:

```text
http://ollama:11434
```

---

## 21. MVP Scope

The first React MVP should not include everything.

### Include

- AirPack list
- Upload AirPack
- Select AirPack
- Select template
- Dynamic form
- Generate using Ollama/Qwen
- Display output
- Download TXT

### Exclude Initially

- Billing
- Authentication
- Teams
- Marketplace
- DOCX formatting
- PDF formatting
- Multi-provider support
- Domain names
- SSL
- Fancy admin screens

The first goal is proving the browser-based AirPack runtime works.

---

## 22. What This Tests

This migration tests a major assumption:

```text
Can Airlock be delivered as a browser-based AirPack runtime instead of a desktop application?
```

If yes, Airlock becomes simpler and more flexible.

It can support:

- web demos
- local appliances
- private deployments
- SaaS
- future marketplace

without rebuilding the core system.

---

## 23. Key Risks

### Risk 1: Overbuilding SaaS Too Early

Do not start with SaaS billing and auth.

Start with AirPack runtime.

### Risk 2: Losing Portability

Do not abandon `.airpack` packages.

Use them as import/export format.

Postgres is runtime storage, not necessarily distribution format.

### Risk 3: Provider Confusion

Do not let Claude/GPT/Grok distract from the core AirPack workflow.

Provider abstraction is useful, but the workflow is the product.

### Risk 4: Security

Do not leave raw Ollama exposed publicly forever.

For MVP testing, IP/port is acceptable.

For serious users, use:

- API layer
- authentication
- rate limiting
- HTTPS
- logging

### Risk 5: Premature Rewrite

Do not rewrite everything at once.

Build a vertical slice:

```text
One AirPack
One template
One form
One provider
One output
```

Then expand.

---

## 24. Recommended Immediate Next Step

Create a vertical slice using one legal AirPack.

### Target Flow

```text
Upload legal_letters.airpack
   |
Store definition in Postgres JSONB
   |
React lists it
   |
User selects Demand Letter
   |
React renders dynamic fields
   |
User fills form
   |
API assembles prompt
   |
Ollama/Qwen generates letter
   |
React displays TXT output
```

This proves the new architecture.

---

## 25. Final Architecture Vision

```text
                 +----------------------+
                 |      React UI        |
                 | Catalog / Forms / UX |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 |      API Layer       |
                 | Validation / Routing |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 |   AirPack Runtime    |
                 | Prompt Assembly      |
                 | Workflow Execution   |
                 +----------+-----------+
                            |
          +-----------------+-----------------+
          |                                   |
          v                                   v
+-------------------+              +----------------------+
|     Postgres      |              |   Provider Layer      |
| AirPacks JSONB    |              | Ollama / Claude / GPT |
| Generations       |              | Grok / Future Models  |
+-------------------+              +----------------------+
```

---

## 26. Conclusion

The major architectural insight is that Airlock should not be bound to Avalonia.

Airlock should be an AirPack runtime.

The most valuable asset is the metadata-driven workflow system.

React can provide the UI.

Postgres can provide runtime storage.

Ollama can provide local model execution.

Claude/GPT/Grok can provide cloud model execution.

A SaaS starter kit can provide commodity account and billing infrastructure.

This creates a clean path from:

```text
Desktop prototype
```

to:

```text
Browser-based AirPack runtime
```

to:

```text
Local appliance + hosted demo + SaaS platform
```

without losing the core product idea.

The central rule:

```text
Preserve AirPacks.
Replace everything else as needed.
```
