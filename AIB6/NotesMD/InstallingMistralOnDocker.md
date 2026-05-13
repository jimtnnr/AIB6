# AirLock AI – Dockerized Mistral Runtime Plan

## Objective

Create a clean, deterministic local runtime for AirLock AI using Docker and a single local Mistral inference endpoint.

The goal is to preserve the existing Avalonia application architecture exactly as-is while replacing direct local model dependencies with a Dockerized runtime.

This is infrastructure planning only. The existing AirLock application, UI, and API contract remain canonical.

---

# Existing Application Contract

The current Avalonia application already expects a local HTTP inference endpoint:

```json
"ModelSettings": {
  "DefaultModel": "mistral",
  "Mistral": {
    "ModelName": "mistral",
    "Endpoint": "http://localhost:11434/api/generate"
  }
}
```

This existing contract is preserved unchanged.

The Docker runtime must satisfy this exact local API structure.

---

# Scope

This plan is intentionally narrow.

Included:

- Docker runtime
- Single Mistral model
- Local-only inference
- Offline-capable operation
- Existing Avalonia compatibility
- Existing localhost API compatibility
- Future appliance-image compatibility

Excluded:

- Mixtral
- Multi-model routing
- Cloud inference
- Kubernetes
- Orchestration layers
- Architecture rewrites
- Avalonia rewrites
- API redesigns

---

# Proposed Runtime Direction

## Runtime Selection

Current preferred runtime candidate:

- Ollama

Reasoning:

- Existing AirLock endpoint structure already closely matches Ollama conventions
- Minimal glue code
- Minimal moving parts
- Minimal infrastructure complexity
- Stable localhost API behavior
- Existing Docker support
- Existing Mistral support

---

# Runtime Architecture

## Host System Responsibilities

The host Linux system should remain minimal and primarily responsible for:

- Booting
- Networking
- Launching Docker
- Running AirLock UI
- Hosting local inference containers

The host OS is not intended to become a general-purpose desktop environment.

---

## Docker Responsibilities

Docker runtime responsibilities:

- Host Mistral inference
- Expose localhost API
- Maintain persistent model storage
- Isolate inference runtime from host dependencies
- Allow reproducible deployments
- Support future appliance imaging

---

# API Contract

The critical requirement is preserving the existing local API contract:

```text
http://localhost:11434/api/generate
```

The Avalonia application should remain unaware of:

- Container internals
- Model loading details
- Runtime implementation details
- Docker specifics
- Future infrastructure changes

The API contract remains the stable integration boundary.

---

# Container Goals

The Mistral container should provide:

- Stable startup behavior
- Stable localhost port exposure
- Persistent model storage
- Offline operation after initial model pull
- Predictable restart behavior
- Minimal runtime dependencies

---

# Port Strategy

Current port allocation:

| Service | Port |
|---|---|
| Mistral | 11434 |

This should remain fixed and deterministic.

Dynamic port assignment should be avoided.

---

# Offline-First Requirement

AirLock philosophy requires:

- Local inference
- No cloud dependency
- No telemetry dependency
- No API billing dependency
- No CDN dependency
- No internet requirement after installation

The Dockerized Mistral runtime must continue functioning without external connectivity once models are installed locally.

---

# Why Docker Fits AirLock

Docker provides several advantages aligned with the AirLock philosophy:

- Reproducible deployment
- Dependency isolation
- Easier appliance imaging
- Cleaner runtime boundaries
- Simplified support model
- Simplified deployment replication
- Easier future upgrades

This supports the broader AirLock design principles:

- Minimal friction
- Deterministic behavior
- Reduced system complexity
- Reduced support entropy

---

# Future Golden Image Direction

Future AirLock appliance imaging may eventually standardize around:

```text
Minimal Linux
→ X11
→ Lightweight Desktop
→ Docker Engine
→ Ollama Container
→ Mistral Runtime
→ Avalonia UI
```

The operating system should become largely invisible to the operator.

The primary user experience should remain AirLock itself.

---

# Immediate Next Step

The next implementation phase should focus only on:

1. Pulling the Ollama Docker image
2. Running the container locally
3. Pulling the Mistral model
4. Verifying localhost endpoint compatibility
5. Verifying Avalonia integration against the existing endpoint contract

No architectural redesign is required.