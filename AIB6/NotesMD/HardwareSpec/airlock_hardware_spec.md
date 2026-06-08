# AirLock – Hardware Specification & Inference Performance Guide

---

## The Core Hardware Problem

AirLock runs a 7 billion parameter language model locally. Every token the model generates requires loading and processing billions of floating point weights through memory. This is not a compute-bound problem — it is a **memory bandwidth problem**.

The bottleneck is not CPU clock speed. The bottleneck is how fast the hardware can move model weights from RAM into the processor on every single token generation cycle.

This is why:
- A newer CPU with the same clock speed can be dramatically faster
- More RAM channels matter more than more RAM
- Integrated GPU offloading (iGPU) changes the equation significantly
- Discrete GPU with dedicated VRAM is the ultimate solution but not required

---

## The Mistral 7B Numbers Explained

### What "7B" Means

Mistral 7B has 7 billion parameters — numerical weights that define how the model thinks. At full (FP16) precision, storing those weights requires ~14GB of RAM. That's before any context window or inference overhead.

### Quantization — The Speed/Quality Tradeoff

Quantization compresses the weights to use less memory and fewer bits per value. Less memory = faster movement through memory channels = faster tokens per second.

| Quantization | Bits per weight | RAM Required | Quality Loss | Tok/sec (CPU) |
|---|---|---|---|---|
| FP16 (none) | 16 bit | ~14GB | None | ~2 tok/sec |
| Q8_0 | 8 bit | ~7.7GB | Minimal | ~3-4 tok/sec |
| Q5_K_M | 5 bit | ~5.4GB | Very small | ~4-5 tok/sec |
| Q4_K_M | 4 bit | ~4.4GB | Small | ~5-6 tok/sec |

**AirLock uses Q4_K_M** — the best balance of speed and quality for CPU inference. For letter drafting the quality difference from Q8 is imperceptible.

### What "Tokens Per Second" Means for AirLock

A token is roughly 0.75 words. A 500 word letter = ~667 tokens of output.

| Tok/sec | 500 word letter | 1000 word letter |
|---|---|---|
| 5-6 (current machine) | ~1 min 50 sec | ~3 min 40 sec |
| 8-10 (Beelink SER8) | ~1 min 10 sec | ~2 min 20 sec |
| 15-20 (Beelink SER8 w/ iGPU) | ~35-45 sec | ~70-90 sec |
| 25+ (SER10 MAX) | ~25-30 sec | ~50-60 sec |

### Why Input Prompt Size Matters Less Than Output Size

AirLock sends a prompt of ~300-600 tokens (template + user facts). This prompt evaluation phase is fast — it runs in parallel. The slow part is **generation** — Mistral produces one token at a time, sequentially. You cannot parallelize autoregressive generation. Every token waits for the previous token to complete.

This means: **the length dropdown directly controls how long the user waits.** Brief = fast. Full = slow. This is physics, not a bug.

### Why the Current Machine Is Slow

The current AirLock machine is a **Ryzen 7 5700U (Lucienne)**:
- Older iGPU (Radeon Vega 7) — Ollama cannot offload layers to it effectively
- Single channel DDR4 in some configurations — cuts memory bandwidth in half
- Result: ~5-6 tokens/second — pure CPU inference, no GPU assist

---

## Recommended Hardware Tiers

---

### Tier 1 — AirLock Standard
**Beelink SER8**

| Spec | Detail |
|---|---|
| CPU | AMD Ryzen 9 8845HS (8 cores / 16 threads, up to 5.1GHz) |
| iGPU | AMD Radeon 780M (12 compute units) |
| RAM | 32GB DDR5 dual-channel |
| Storage | 1TB PCIe 4.0 NVMe |
| Price | ~$599 |
| Inference speed | 15-20 tok/sec (Mistral 7B Q4_K_M with iGPU offload) |
| 500 word letter | ~35-45 seconds |
| 1000 word letter | ~70-90 seconds |

**Why this is the v1 appliance spec:**
- Radeon 780M is fully supported by Ollama's ROCm layer — iGPU offloading works out of the box
- 32GB DDR5 dual-channel gives the iGPU fast shared memory to work with
- ~$599 keeps the appliance at a manufacturable price point
- Mini PC form factor — silent, small, no discrete GPU required
- Proven in production for Ollama/Mistral workloads

---

### Tier 2 — AirLock Pro
**Beelink SER10 MAX**

| Spec | Detail |
|---|---|
| CPU | AMD Ryzen AI 9 HX470 (8 cores / 16 threads) |
| iGPU | AMD Radeon 890M (16 compute units) |
| NPU | 86 TOPS combined |
| RAM | 32GB DDR5 |
| Storage | 1TB NVMe |
| Price | ~$1,799 |
| Inference speed | 20-25 tok/sec (Mistral 7B), can run 13B-27B models |
| 500 word letter | ~25-30 seconds |

**Why this matters:**
- Can run larger models — Mistral Small, Llama 3 13B, Qwen 27B in Q4
- Better model = better letter quality without changing the UI at all
- Premium appliance price point — justifies higher retail SKU
- Future-proof for richer Airpack workflows that may benefit from larger models

---

### Tier 3 — AirLock Flagship (Future)
**Beelink GTR9 Pro**

| Spec | Detail |
|---|---|
| CPU | AMD Ryzen AI Max+ 395 |
| Unified Memory | 128GB LPDDR5X-8000 |
| Storage | 2TB NVMe |
| Network | Dual 10GbE |
| Price | ~$1,500-2,000 |
| Inference speed | 40-60+ tok/sec on 7B models, runs 70B models |
| 500 word letter | ~10-15 seconds |

**Note:** Overkill for single-user letter drafting. Relevant if AirLock evolves into a shared inference server serving multiple users on a local network.

---

## Why No Discrete GPU

Mini PCs with discrete GPUs have fixed, small VRAM (typically 4-8GB). Mistral 7B Q4 needs ~4.4GB just for weights, leaving almost no room for context. A dedicated GPU only helps if VRAM exceeds model size — which mini PC discrete GPUs typically cannot provide.

The iGPU approach is better for this form factor because:
- iGPU shares the full system RAM as unified VRAM
- 32GB DDR5 gives the iGPU 32GB to work with
- No thermal or power delivery issues
- Simpler deployment — no driver complexity

---

## The Current Machine vs Target Hardware

| | Current (5700U) | SER8 Target |
|---|---|---|
| iGPU | Radeon Vega 7 (no Ollama support) | Radeon 780M (full Ollama support) |
| Memory | DDR4 | DDR5 dual-channel |
| Tok/sec | ~5-6 | ~15-20 |
| 500 word letter | ~1 min 50 sec | ~35-45 sec |
| Appliance cost | — | ~$599 |

The current development machine (5700U) is adequate for development and testing. It is not the shipping hardware spec.

---

## CPU Governor Note

On any Linux deployment, the CPU governor must be set to `performance` mode before running Ollama. The default `powersave` governor cuts clock speed by more than half and reduces inference speed proportionally.

```bash
sudo cpupower frequency-set -g performance
sudo systemctl enable cpupower
```

This is a required step in every AirLock machine setup. See `InstallingMistralOnDocker.md` for full procedure.

---

## Summary

AirLock is CPU/iGPU inference. The target hardware is the **Beelink SER8 at ~$599**. The Radeon 780M iGPU with 32GB DDR5 dual-channel delivers 15-20 tokens per second on Mistral 7B Q4_K_M — fast enough that letter generation feels like a deliberate wait rather than an unusable delay. The user fills in facts, hits Generate, waits 35-45 seconds, and gets a polished professional letter. That is the product.

---

*Last updated: June 2026 — validated against current mini PC market*
