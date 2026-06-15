# AirLock – Hardware Specification & Cost: Qwen2.5 3B Revision (Proposed)

**Status: Proposed — Pending Validation.** This document proposes a major hardware cost reduction based on switching the default model from Mistral 7B Q4 to Qwen2.5 3B. The core evidence is real (see Evidence Basis below) but limited to one benchmark on dev hardware and one in-app test on a prompt with a known bug. Recommended validation steps are listed at the end — this spec should be confirmed against them before being treated as final, the way `airlock_hardware_spec.md` (Mistral-based) was.

---

## What Changed

The original hardware spec (`airlock_hardware_spec.md`) set the appliance baseline at the **Beelink SER8 (~$599)** because Mistral 7B Q4 needs 15-20 tok/sec to feel acceptable, and that speed required a Radeon 780M iGPU + DDR5 dual-channel — hardware that didn't exist in the development machine.

**Qwen2.5 3B changes the equation entirely.** Measured on the *existing* development machine (Ryzen 7 5700U, CPU-only, DDR4, no iGPU offload — the machine the original spec called "not the shipping hardware spec"):

| Model | Measured tok/sec | Hardware |
|---|---|---|
| Mistral 7B Q4_K_M | ~4.1 | Ryzen 7 5700U, CPU-only |
| **Qwen2.5 3B** | **~16.4** | **Same machine, same conditions** |

Qwen2.5 3B on the *worst* hardware in this entire conversation already exceeds the SER8's target speed for Mistral (15-20 tok/sec). The question is no longer "what hardware gets Mistral to 15-20 tok/sec" — it's **"what's the cheapest hardware that comfortably runs a 3B model,"** which is a fundamentally lower bar.

---

## The Core Hardware Problem (Restated for 3B)

The original spec's core insight still holds: this is a **memory bandwidth problem**, not a clock-speed problem. What changes is the *size* of the problem.

### Why 3B Changes the Memory Math

Mistral 7B Q4_K_M requires ~4.4GB of weights. Qwen2.5 3B at Q4 requires roughly **~1.9-2GB** — less than half. Every token generated moves less than half the data through memory. This is the direct mechanical reason Qwen is ~4x faster than Mistral on identical hardware: less than half the data per token, plus whatever architectural efficiency differences exist between the two models.

**Practical implication:** the iGPU-offload story (Radeon 780M, ROCm) that justified the SER8's premium largely existed to compensate for Mistral 7B's memory footprint. A 3B model's footprint is small enough that ordinary CPU memory bandwidth — even DDR4 dual-channel on a mid-range laptop CPU — is sufficient.

---

## Proposed Hardware Tier — AirLock Standard (Revised)

**Target: ~$299 mini PC class**

| Spec | Detail |
|---|---|
| CPU | Ryzen 5 5500U/5625U class, or Intel N305 class (8-core) |
| iGPU | Not required as a selection criterion (no offload dependency for 3B) |
| RAM | 16GB (vs 32GB for the Mistral-era spec) |
| Storage | 256-512GB NVMe |
| Price | ~$249-299 |
| Inference speed | Expected 12-20 tok/sec (Qwen2.5 3B Q4, CPU-only) — based on dev-machine measurement on comparable/older silicon |
| 500-word letter (est.) | ~12-25 seconds |
| 1000-word letter (est.) | ~25-50 seconds |

### Why This Doesn't Compromise

Compared to the original SER8 spec, what's being relaxed and why it's safe:

- **RAM: 32GB → 16GB.** Qwen2.5 3B's ~2GB footprint, plus OS, plus AirLock's Avalonia/.NET runtime, plus PostgreSQL, comfortably fits in 16GB with room to spare. 32GB was sized for Mistral 7B's larger footprint plus iGPU shared-memory headroom — neither constraint applies at 3B.
- **iGPU requirement: Radeon 780M (specific) → not a selection criterion.** The 780M's ROCm support was the *entire reason* the SER8 was chosen over cheaper alternatives. Removing this requirement opens the field to nearly any modern mini PC, which is where the price drop comes from.
- **CPU tier: Ryzen 9 8845HS → Ryzen 5 5500U/5625U or Intel N305.** The dev machine's Ryzen 7 5700U (older than even the proposed 5500U) already hit 16.4 tok/sec on Qwen. A same-or-newer-generation CPU at this tier should match or exceed that.

### What Is NOT Being Relaxed

- **Storage: still NVMe.** Boot time, model load time, and letter-archive I/O all benefit from NVMe regardless of model size. This was never the expensive part of the BOM and isn't a place to cut.
- **8-core CPU still recommended**, even though inference itself may not need all 8 cores — Docker (Ollama container) + PostgreSQL + Avalonia UI + OS all run concurrently. The original spec's reasoning about *total system* responsiveness, not just raw inference, still applies.
- **CPU governor → performance mode is still required.** This was a software/config requirement, not a hardware cost item, and remains unchanged. Skipping it still cuts inference speed by more than half regardless of model size.
- **The appliance form factor, silent operation, mini-PC packaging** — all unchanged. This spec changes *which* mini PC, not the product category.

---

## Updated Hardware Tier Table

| Tier | Old (Mistral-era) | New (Qwen-era, proposed) |
|---|---|---|
| **Standard** | Beelink SER8, ~$599, Radeon 780M required | ~$299-class mini PC, no iGPU requirement |
| **Pro** | Beelink SER10 MAX, ~$1,799, runs 13B-27B | *Re-evaluate*: at Qwen-tier needs, "Pro" may mean running **Qwen2.5 7B or 14B** on SER8-class hardware (~$599) — i.e., today's old Standard tier becomes the new Pro tier, running a bigger/better model at the same old price point |
| **Flagship** | Beelink GTR9 Pro, ~$1,500-2,000 | Largely unchanged — still relevant only for shared/multi-user inference, now potentially running even larger Qwen variants |

This produces a clean **"the whole stack shifts down one price tier"** story: what used to be Pro-tier hardware running Mistral 7B now becomes Standard-tier hardware running a 3B model just as fast, with old Pro-tier hardware now available to run a meaningfully larger/better model than Mistral 7B at the *old Standard's* price expectations.

---

## Cost Impact Summary

| | Old (Mistral, SER8) | New (Qwen, proposed) | Change |
|---|---|---|---|
| Hardware cost | ~$599 | ~$249-299 | **~50-58% reduction** |
| RAM required | 32GB | 16GB | -50% |
| iGPU dependency | Required (Radeon 780M specifically) | None | Removed constraint |
| Inference speed | 15-20 tok/sec | 12-20 tok/sec (projected) | Comparable or better |
| 500-word letter | ~35-45 sec | ~12-25 sec (projected) | Faster |

If this holds, the appliance's bill-of-materials drops by roughly **$300**, which has direct implications for retail pricing, margin, or both — worth a conversation with whoever owns the business-model docs (`airlock_business_model.md`, `airlock_hardware_sourcing.md`) once validated.

---

## Evidence Basis (What This Spec Rests On)

1. **Qwen2.5 3B: 1093 tokens in 66.61s (~16.4 tok/sec)** — measured via direct Ollama API call (`curl`), dev machine (Ryzen 7 5700U, CPU-only, DDR4), `num_ctx: 1024`, prompt: "Write a detailed and thorough formal business letter of around 1000 words..."
2. **Mistral 7B Q4: 566 tokens in 137.07s (~4.1 tok/sec)** — same conditions, same prompt, for comparison.
3. **One in-app generation** — Legal Strict Facts > Demand > Payment Demand, Intent to Escalate, Brief length, via Qwen (repurposed Mixtral config slot): 12 seconds, structurally correct output. Note: this prompt contained the known `{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}` unresolved-placeholder bug (see AirPack Spec v1.2) — quality assessment from this run is provisional.

**Nothing in this document has been tested on $299-class hardware.** All projections for that tier are extrapolations from dev-machine measurements plus general assumptions about relative CPU generations.

---

## Recommended Validation Before Treating This as Final

1. **Re-run the model comparison after the AirPack v1.2 plumbing fix** — confirms Qwen's quality on a *correct* prompt, not one with unresolved placeholders. This is the single most important open item and affects this spec's core premise (that Qwen quality is acceptable).
2. **Throttled-container test on the dev machine** — `docker update --cpus="N"` to simulate a weaker CPU (e.g., 2-4 cores, roughly N100/N305-class) and re-benchmark Qwen2.5 3B. Cheap, fast, no purchase required, and would directly validate or refute the $299 projection.
3. **If (1) and (2) hold up — purchase one $299-class mini PC** and run the full benchmark suite (this is the only way to *actually* validate the projected 12-20 tok/sec range; everything before this step is extrapolation).
4. **Re-test the "Pro tier" hypothesis** — run Qwen2.5 7B or 14B on the dev machine (or SER8-class hardware if available) to see whether the "old Pro tier hardware now runs a better-than-Mistral model" claim holds.
5. **Cross-check with `airlock_business_model.md` / `airlock_hardware_sourcing.md`** — a ~$300 BOM reduction is a business-model-level change, not just a hardware spec update, and those docs likely have pricing/margin assumptions built around the $599 figure.

---

*Drafted: June 15, 2026. Status: Proposed, pending validation per above. Do not treat the $299 figure as committed until at minimum validation steps 1-2 are complete.*
