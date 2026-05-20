````md
# Airlock Runtime Architecture Notes

## Core Realization

Airlock is not:
- a cloud-native platform
- a developer workstation
- a generic Linux desktop
- a container orchestration problem

Airlock is:
- a controlled appliance
- a single-purpose runtime environment
- a kiosk-style AI workstation
- an application-first system

The user should perceive:
- one machine
- one interface
- one purpose

The infrastructure should remain invisible.

---

# UX Goal

Desired user experience:

```text
Power On
    ↓
Airlock launches automatically
    ↓
Fullscreen controlled environment
    ↓
User never interacts with:
- Linux desktop
- terminal
- Docker
- Portainer
- PostgreSQL
- infrastructure
````

Analogy:

* ATM
* arcade cabinet
* medical workstation
* self-checkout terminal

Infrastructure exists only to support appliance behavior.

---

# Architecture Direction

## Native Host Services

Use native host services where:

* software is mature
* operationally stable
* low dependency churn
* direct filesystem visibility matters
* abstraction adds little value

### Native Components

* PostgreSQL
* Avalonia application
* host OS services

Reasoning:

* lower operational complexity
* easier debugging
* deterministic file locations
* easier backup/restore
* fewer abstraction layers
* lower brittleness

---

# Docker Usage

Docker should NOT become the platform.

Docker should only isolate volatile inference services.

## Dockerized Components

* Ollama
* Mistral endpoints
* future model runtimes

Reasoning:

* inference runtimes change rapidly
* dependency isolation useful
* model experimentation useful
* service swapping useful
* runtime tuning useful

Docker is treated as:

* a runtime wrapper
* not system architecture

---

# PostgreSQL Decision

## Rejected Approach

PostgreSQL inside Docker.

Reasons:

* unnecessary abstraction
* volume indirection
* hidden persistence
* additional debugging surface
* operational complexity without sufficient benefit
* still requires appliance imaging

Pain encountered:

* volume confusion
* hidden storage semantics
* Portainer indirection
* container lifecycle complexity

Conclusion:
Native PostgreSQL is operationally simpler and less brittle for this topology.

---

# Inference Topology

Avalonia app controls inference endpoints.

Inference services are swappable.

Example:

```text
localhost:11435 -> small context
localhost:11436 -> medium context
localhost:11437 -> large context
```

Application configuration controls:

* active endpoint
* model routing
* runtime profile selection

This enables:

* A/B testing
* rollback
* multiple runtime profiles
* experimental backends
* operational flexibility

without infrastructure rebuilds.

---

# Multiple Ollama Runtime Profiles

Example strategy:

## Small/Fast Runtime

```text
Context: 1024
Port: 11435
Purpose:
- fast generation
- low RAM
- lightweight workflows
```

## Balanced Runtime

```text
Context: 2048
Port: 11436
Purpose:
- standard workflows
- balanced latency/quality
```

## Deep Runtime

```text
Context: 4096
Port: 11437
Purpose:
- complex reasoning
- long prompts
- archival workflows
```

---

# Architectural Principle

Abstractions are tradeoffs, not virtues.

Questions to ask:

* What complexity does this abstraction remove?
* What complexity does it introduce?
* Does it reduce operational burden?
* Does it improve recoverability?
* Does it improve debuggability?
* Does it improve deployment simplicity?

If abstraction cost exceeds benefit:
reject the abstraction.

---

# Final Stack Direction

```text
Host OS
├── Airlock Avalonia Application
├── Native PostgreSQL
└── Dockerized Ollama/Mistral Services
```

Minimal moving parts.
Minimal hidden state.
Controlled topology.
Appliance-first deployment.

```
```
