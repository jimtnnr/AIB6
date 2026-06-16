# AirLock — Ollama Docker Model Setup

Reference runbook for adding new local LLMs to the AirLock box. Each model gets its own Docker container, its own port, and persists across reboots. Use this doc to brief an AI assistant when you want a new model added — paste it in, name the model, and the pattern below is everything it needs.

---

## The Pattern

One Ollama container per model. Not one container serving many models — separate containers, separate ports, separate volumes. This keeps each model independently startable, stoppable, and swappable in Portainer without touching the others.

Each container:
- Runs the official `ollama/ollama` image
- Maps a unique host port to the container's internal `11434`
- Gets its own named Docker volume (so model files don't collide)
- Uses `--restart unless-stopped` so it survives reboots and stays running
- Is named `airlock-ollama-<model>` for consistency

---

## Current Port Allocation

| Container | Model | Port |
|---|---|---|
| `airlock-ollama` | mistral:7b-instruct-q4_K_M | 11434 |
| `airlock-ollama-phi3` | phi3 | 11435 |
| `airlock-ollama-qwen` | qwen | 11436 |
| `airlock-ollama-llama32` | llama3.2:3b | 11437 |
| `airlock-ollama-gemma3` | gemma3:4b | 11438 |

**Next free port: 11439**

Keep this table updated every time a new container is added — it's the source of truth for what's running and where.

---

## Standard Environment Variables

| Variable | Value | Why |
|---|---|---|
| `OLLAMA_HOST` | `0.0.0.0:11434` | Binds inside the container so the host port mapping works |
| `OLLAMA_NUM_THREADS` | `16` | Matches the Ryzen 7 5700U's 8 cores / 16 threads |
| `OLLAMA_CONTEXT_LENGTH` | `4096` | Caps max context to "letter size" — this is a ceiling, not a speed setting; it limits RAM reserved for the KV cache, doesn't slow down short responses |

---

## Container Template

Fill in `<NAME>`, `<PORT>`, and `<VOLUME>`:

```bash
docker run -d \
  --name airlock-ollama-<NAME> \
  -p <PORT>:11434 \
  -v <VOLUME>:/root/.ollama \
  -e OLLAMA_HOST=0.0.0.0:11434 \
  -e OLLAMA_NUM_THREADS=16 \
  -e OLLAMA_CONTEXT_LENGTH=4096 \
  --restart unless-stopped \
  ollama/ollama

docker exec airlock-ollama-<NAME> ollama pull <MODEL_TAG>
```

Then verify:

```bash
docker ps | grep airlock-ollama-<NAME>
docker exec airlock-ollama-<NAME> ollama list
```

---

## Worked Examples

### Llama 3.2 3B (port 11437)

```bash
docker run -d \
  --name airlock-ollama-llama32 \
  -p 11437:11434 \
  -v ollama-llama32:/root/.ollama \
  -e OLLAMA_HOST=0.0.0.0:11434 \
  -e OLLAMA_NUM_THREADS=16 \
  -e OLLAMA_CONTEXT_LENGTH=4096 \
  --restart unless-stopped \
  ollama/ollama

docker exec airlock-ollama-llama32 ollama pull llama3.2:3b
```

### Gemma 3 4B (port 11438)

```bash
docker run -d \
  --name airlock-ollama-gemma3 \
  -p 11438:11434 \
  -v ollama-gemma3:/root/.ollama \
  -e OLLAMA_HOST=0.0.0.0:11434 \
  -e OLLAMA_NUM_THREADS=16 \
  -e OLLAMA_CONTEXT_LENGTH=4096 \
  --restart unless-stopped \
  ollama/ollama

docker exec airlock-ollama-gemma3 ollama pull gemma3:4b
```

---

## Benchmark Template

Run against any container's port to get tokens/sec on real hardware:

```bash
time curl -s http://localhost:<PORT>/api/generate -d '{
  "model": "<MODEL_TAG>",
  "prompt": "Write a detailed formal business letter",
  "stream": false
}' | grep -o '"eval_count":[0-9]*\|"eval_duration":[0-9]*\|"prompt_eval_count":[0-9]*'
```

Baseline from Mistral 7B on this box: 15–20 tokens/second with performance governor enabled. Smaller models (3B–4B) should clear that comfortably on the same hardware.

---

## Briefing an AI for a New Model

When asking an AI to add another model, give it:

1. The model name and exact Ollama library tag (e.g. `phi4-mini:3.8b`)
2. This document, or just the port table + template above
3. The next free port from the table

That's enough for it to produce a correct, drop-in container command without re-deriving the pattern from scratch each time.
