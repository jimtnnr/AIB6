# Airlock — Ollama/Mistral Performance Tuning
**Machine: Airlock Mini PC | AMD Ryzen 7 5700U | 38GB RAM | CPU-only by design**

---

## What We Found

### Problem 1 — CPU Governor on Powersave
The Linux CPU governor was set to `powersave`, running the CPU at ~1700MHz instead of the full 4400MHz.

**Diagnosis:**
```bash
cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor
# returned: powersave

cat /proc/cpuinfo | grep "cpu MHz" | head -4
# returned: ~1700MHz average
```

**Fix:**
```bash
sudo apt install cpufrequtils -y
sudo cpupower frequency-set -g performance
```

**Verify:**
```bash
cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor
# should return: performance

cat /proc/cpuinfo | grep "cpu MHz" | head -4
# should show 4000MHz+ on at least some cores
```

**Make it permanent:**
```bash
sudo systemctl enable cpupower
sudo nano /etc/default/cpupower
# Set: CPUPOWER_START_OPTS="frequency-set -g performance"
```

---

### Problem 2 — Container Built with NVIDIA/GPU Flags
The original `airlock-ollama` container was created with NVIDIA environment variables baked in (`NVIDIA_VISIBLE_DEVICES=all`, `NVIDIA_DRIVER_CAPABILITIES=compute,utility`). This machine has no GPU. Ollama was likely falling back to a degraded CPU path or single-threaded mode.

**Diagnosis:**
```bash
docker inspect airlock-ollama | grep -i env -A 20
# showed NVIDIA vars present
```

**Fix — recreate the container clean:**
```bash
docker stop airlock-ollama
docker rm airlock-ollama

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

Key flags:
- `OLLAMA_NUM_THREADS=16` — explicitly use all 16 threads (8 cores / 2 threads each)
- `OLLAMA_CONTEXT_LENGTH=4096` — set context window for letter-sized prompts (~3000 words)
- No NVIDIA flags — clean CPU-only

---

### Problem 3 — Wrong Model Variant
`mistral:latest` was being used — an unspecified pull that could be any quantization. For CPU-only inference the quantization level matters significantly for speed.

**Diagnosis:**
```bash
docker exec airlock-ollama ollama list
# showed: mistral:latest  4.4GB
```

**Model options pulled (best to slowest on CPU):**
| Model | Size | Speed | Quality |
|---|---|---|---|
| `mistral:7b-instruct-q4_K_M` | ~4.8GB | Fastest | Good |
| `mistral:7b-instruct-q5_K_M` | ~5.4GB | ~20% slower | Better |
| `mistral:7b-instruct-q8_0` | ~7.7GB | ~2x slower | Best |

**Pull commands:**
```bash
docker exec airlock-ollama ollama pull mistral:7b-instruct-q4_K_M
docker exec airlock-ollama ollama pull mistral:7b-instruct-q5_K_M
docker exec airlock-ollama ollama pull mistral:7b-instruct-q8_0
```

**Recommended default:** `mistral:7b-instruct-q5_K_M` — best quality/speed balance for letter drafting.

---

### Problem 4 — appsettings.json Points at Generic Model Name
Airlock's config was pointing at `"mistral"` (resolves to `mistral:latest`) instead of a specific quantized variant.

**Fix — update `appsettings.json`:**
```json
"Mistral": {
  "ModelName": "mistral:7b-instruct-q4_K_M",
  "Endpoint": "http://localhost:11434/api/generate"
}
```

Switch to `q5_K_M` when you want better quality at acceptable speed cost.

---

## Baseline Performance (Before Fixes)
```
prompt_eval_count: 10 tokens
eval_count: 538 tokens
time: 2 minutes 17 seconds
~4 tokens/second  ← should be 15-20 tok/sec on this CPU
```

---

## Diagnostic Commands (Run Anytime)

**Check container is healthy:**
```bash
docker ps | grep ollama
docker stats airlock-ollama --no-stream
```

**Check CPU governor:**
```bash
cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor
cat /proc/cpuinfo | grep "cpu MHz"
```

**Check loaded models:**
```bash
docker exec airlock-ollama ollama list
```

**Benchmark Ollama speed:**
```bash
time curl -s http://localhost:11434/api/generate -d '{
  "model": "mistral:7b-instruct-q4_K_M",
  "prompt": "Write a detailed formal business letter",
  "stream": false
}' | grep -o '"eval_count":[0-9]*\|"eval_duration":[0-9]*\|"prompt_eval_count":[0-9]*'
```

---

## Architecture Notes
- Airlock is a C# / Avalonia desktop app
- Model config lives in `appsettings.json` under `ModelSettings`
- Prompt is built in `PromptTemplateRegistry.cs` via `FillPrompt()` from `.aibcodex` template files
- HTTP call is in `LetterTab.axaml.cs` → `CallLlmAsync()` → streams to `http://localhost:11434/api/generate`
- Mixtral is available on port `11534` as a second model option
- Context is intentionally large — Airlock sends big prompts by design
- CPU-only is intentional — small form factor, no GPU

---

*Last updated: June 2026 — Session with Claude*