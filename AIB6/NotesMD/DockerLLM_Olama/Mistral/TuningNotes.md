# AirLock AI – Dockerized Mistral Runtime: Install Guide

## Objective

Create a clean, deterministic local runtime for AirLock AI using Docker and a single local Mistral inference endpoint.

The goal is to preserve the existing Avalonia application architecture exactly as-is while replacing direct local model dependencies with a Dockerized runtime.

This is infrastructure planning only. The existing AirLock application, UI, and API contract remain canonical.

---

## Existing Application Contract

The current Avalonia application expects a local HTTP inference endpoint:

```json
"ModelSettings": {
  "DefaultModel": "mistral",
  "Mistral": {
    "ModelName": "mistral:7b-instruct-q4_K_M",
    "Endpoint": "http://localhost:11434/api/generate"
  }
}
```

This contract is preserved unchanged. The Docker runtime must satisfy this exact local API structure. Do not use `"mistral"` or `"mistral:latest"` as the model name — always specify an explicit quantized variant.

---

## Scope

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

---

## Step 1 — Set CPU Governor to Performance

**Do this before anything else. On every new machine.**

The Linux CPU governor defaults to `powersave` on most installs. On the Ryzen 7 5700U this limits the CPU to ~1700MHz instead of ~4400MHz — cutting inference speed by more than half.

```bash
sudo apt install cpufrequtils -y
sudo cpupower frequency-set -g performance
```

Verify:
```bash
cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor
# must return: performance

cat /proc/cpuinfo | grep "cpu MHz" | head -4
# must show 4000MHz+ on at least some cores
```

Make it permanent:
```bash
sudo systemctl enable cpupower
sudo nano /etc/default/cpupower
# Set line: CPUPOWER_START_OPTS="frequency-set -g performance"
```

---

## Step 2 — Create the Ollama Container

Use this exact command. No GPU flags. No NVIDIA environment variables. Full CPU threads. Explicit context length.

```bash
docker run -d \
  --name airlock-ollama \
  -p 11434:11434 \
  -v ollama:/root/.ollama \
  -e OLLAMA_HOST=0.0.0.0:11434 \
  -e OLLAMA_NUM_THREADS=16 \
  -e OLLAMA_CONTEXT_LENGTH=4096 \
  --restart unless-stopped \
  ollama/ollama
```

**Critical:** Never add `NVIDIA_VISIBLE_DEVICES`, `NVIDIA_DRIVER_CAPABILITIES`, or any GPU flags. AirLock is CPU-only by design. A GPU-configured container will fall back to a degraded CPU path.

---

## Step 3 — Pull Mistral Models

Always pull specific quantized variants. Never use `ollama pull mistral` — it resolves to `latest` which is unspecified.

```bash
docker exec airlock-ollama ollama pull mistral:7b-instruct-q4_K_M
docker exec airlock-ollama ollama pull mistral:7b-instruct-q5_K_M
docker exec airlock-ollama ollama pull mistral:7b-instruct-q8_0
```

| Model | Size | Speed | Quality | Use |
|---|---|---|---|---|
| `q4_K_M` | ~4.8GB | Fastest | Good | Default / testing |
| `q5_K_M` | ~5.4GB | ~20% slower | Better | Recommended production |
| `q8_0` | ~7.7GB | ~2x slower | Best | Quality-critical |

Each pull takes time on a slow connection. The model volume is persistent — pulls only happen once.

---

## Step 4 — Verify the Endpoint

```bash
curl http://localhost:11434/api/tags
```

Should return a JSON list of available models. If it returns nothing, the container is not ready — wait 10 seconds and retry.

---

## Step 5 — Benchmark

Run this and confirm speed is acceptable before starting AirLock:

```bash
time curl -s http://localhost:11434/api/generate -d '{
  "model": "mistral:7b-instruct-q4_K_M",
  "prompt": "Write a detailed formal business letter",
  "stream": false
}' | grep -o '"eval_count":[0-9]*\|"eval_duration":[0-9]*\|"prompt_eval_count":[0-9]*'
```

**Expected on Ryzen 7 5700U in performance mode:** 15–20 tokens/second

If you see under 8 tokens/second:
1. Check CPU governor — probably still on `powersave`
2. Check container env vars — probably has NVIDIA flags
3. Recreate the container using the command in Step 2

---

## Step 6 — Update appsettings.json

Point AirLock at the specific model variant:

```json
"ModelSettings": {
  "DefaultModel": "mistral",
  "Mistral": {
    "ModelName": "mistral:7b-instruct-q5_K_M",
    "Endpoint": "http://localhost:11434/api/generate"
  }
}
```

Use `q4_K_M` for faster testing, `q5_K_M` for production.

---

## Port Allocation

| Service | Port |
|---|---|
| Mistral (Ollama) | 11434 |
| Mixtral (reserved) | 11534 |

Ports are fixed and deterministic. Do not use dynamic port assignment.

---

## Offline-First Requirement

AirLock requires:
- Local inference only
- No cloud dependency after initial model pull
- No telemetry
- No API billing
- No internet required at runtime

The Dockerized Mistral runtime must function without external connectivity once models are installed locally. The `ollama` volume persists models across container restarts.

---

## Diagnostic Commands

```bash
# Container running?
docker ps | grep ollama

# Resource usage
docker stats airlock-ollama --no-stream

# Models available
docker exec airlock-ollama ollama list

# CPU governor
cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor

# Container environment (check for GPU flags)
docker inspect airlock-ollama | grep -i env -A 20
```

---

## Future Appliance Image Direction

```
Minimal Linux
→ X11
→ Lightweight Desktop
→ Docker Engine
→ Ollama Container (CPU-only, no GPU flags)
→ Mistral Runtime (q5_K_M)
→ AirLock Avalonia UI
```

The operating system should become invisible to the operator. The primary user experience should remain AirLock itself.

---

*Last updated: June 2026 — validated on Ryzen 7 5700U / 38GB RAM*