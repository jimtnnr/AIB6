````md
# AirLock AI – Ollama Docker Philosophy

## Core Idea

AirLock should not try to become an AI infrastructure project.

AirLock should use a stable local model runtime and focus its value on productization, workflow, privacy, packaging, and operator experience.

Ollama inside Docker gives AirLock a clean runtime boundary:

```text
AirLock Avalonia App
→ localhost API
→ Ollama Docker Container
→ Mistral Model
````

This preserves the existing application contract while avoiding unnecessary model-serving complexity.

---

# Why Ollama

Ollama provides the local LLM runtime layer.

It handles:

* model loading
* model storage
* inference serving
* local HTTP API
* model pulls
* runtime management

This means AirLock does not need to invent:

* model serving
* tokenizer plumbing
* GPU orchestration
* inference APIs
* model download logic
* runtime dependency management

That is not the product.

The product is AirLock.

---

# Why Docker

Docker gives AirLock a clean appliance boundary.

The host system should not become tangled with model runtime dependencies.

Docker allows the model runtime to be:

* isolated
* repeatable
* replaceable
* portable
* easier to support
* easier to image
* easier to reset

This matters because AirLock is intended to behave like an appliance, not a hobby Linux workstation.

---

# Why Mistral Only

The product should start with one stable model path.

Mistral is the default.

Mixtral is removed from the active plan.

This reduces:

* configuration choices
* support states
* model confusion
* memory pressure
* UI complexity
* operator decision load

The first version should prove the cleanest path:

```text
One app
One local endpoint
One runtime
One model
One workflow
```

That is the appliance mindset.

---

# Existing Contract Is Canonical

The Avalonia app already expects:

```json
"ModelSettings": {
  "DefaultModel": "mistral",
  "Mistral": {
    "ModelName": "mistral",
    "Endpoint": "http://localhost:11434/api/generate"
  }
}
```

This is already the right separation.

The app does not need to know how Mistral is hosted.

The app only needs a stable local API endpoint.

That endpoint is the contract.

Do not redesign the app to fit Docker.

Make Docker satisfy the existing app.

---

# Product Value Is Not “Running a Model”

Anyone can technically run a local model now.

That is not the moat.

The value is that most people will not:

* install Linux
* install Docker
* configure an LLM runtime
* pull models
* wire localhost APIs
* troubleshoot inference servers
* build usable workflows
* design a clean interface
* package the result into something coherent

AirLock bridges that gap.

The value is not raw model access.

The value is turning local AI into a working product.

---

# The UI Gap

Most local AI tools have weak product surfaces.

Common problems:

* rickety UI
* cluttered controls
* model dropdown sprawl
* parameter overload
* developer-first screens
* unstable workflows
* poor visual hierarchy
* unclear operator path
* “shareware tool” energy

AirLock should go the other way.

AirLock should feel like a finished appliance:

* clean
* focused
* quiet
* controlled
* intentional
* offline-first
* low-friction
* domain-specific

The operator should not feel like they are managing infrastructure.

The operator should feel like they are using a product.

---

# Factorial Collapse Principle

Every visible option multiplies support complexity.

Every model choice multiplies:

* test states
* support states
* UI states
* user confusion
* documentation burden
* failure modes

Removing choices is not subtraction.

Removing choices collapses complexity.

For AirLock, fewer runtime choices means more product coherence.

The correct first runtime is:

```text
Ollama + Mistral + Docker + localhost API
```

Nothing more.

---

# Appliance Direction

AirLock should eventually move toward a stripped appliance image:

```text
Minimal Linux
→ X11
→ Lightweight desktop
→ Docker
→ Ollama
→ Mistral
→ AirLock Avalonia App
```

The operating system should disappear.

The model runtime should disappear.

The user should see AirLock.

That is the product boundary.

---

# Architecture Rule

Preserve the existing app.

Do not let infrastructure reshape the product.

The correct relationship is:

```text
Infrastructure serves AirLock.
AirLock does not bend around infrastructure.
```

Ollama Docker is valuable because it can satisfy the existing AirLock contract without forcing a rewrite.

That is why it fits.

---

# Strategic Position

AirLock does not compete by being the most configurable local AI stack.

AirLock competes by being:

* simpler
* cleaner
* more private
* more focused
* more controlled
* more appliance-like
* easier to operate
* easier to trust

The runtime should be boring.

The product should be sharp.

Ollama Docker makes the runtime boring enough that AirLock can focus on being the product.

```
```
