# Airlock – Alternate Business Model: Low-Friction Pricing
### Updated June 2026 — $299 Hardware / $99 Onboarding / $199 Monthly

---

## Core Thesis

**Airgapped AI appliance subscription for legal correspondence generation — now with near-zero adoption friction.**

Not: sell expensive AI boxes.

Instead: deploy managed secure appliances with recurring revenue, retained hardware ownership, and Airpack expansion layered on top — at a price point so low it removes price as an objection entirely.

---

## The Pricing Change That Matters

This alternate model lowers both the hardware cost and the onboarding fee, while keeping the monthly subscription unchanged.

| | Original Model (SER8 $599) | Alternate Model |
|---|---|---|
| Hardware cost | $599 | **$299** |
| Onboarding fee | $449 | **$99** |
| First month collected at onboarding | Yes — $199 | Yes — $199 |
| Day one collection per box | $648 | **$298** |
| Net hardware exposure | -$49 per box | **+$301 per box owed** (HW exceeds collection by $301) |
| Month 1 cashflow | +$823 | **+$693** |
| Break-even | Month 1 | **Month 1** |

Even though hardware now costs more than day-one collection per box ($299 vs $298 collected, net), the monthly cadence of 4 new boxes means cumulative cash is positive from **month 1**, and the model breaks even on a per-box basis by the start of month 2.

**Why this pricing works:**
- $99 + $199/month reads as "basically free to try, then a normal SaaS price" — no internal approval process, no "let me think about it"
- Removes the $449 upfront fee that turns a quick yes into a considered purchase
- The product still sells itself on merits: sealed, airgapped, local AI — no data leaves the office
- Cheaper hardware ($299 mini PC tier) keeps the unit economics intact even at lower onboarding

---

## Business Model

### Customer
- Solo attorneys
- Small law firms
- Legal vertical only initially

### Product
- Airgapped Linux appliance
- Local AI inference — no cloud, no data leaves the machine
- Legal correspondence generation via Airpacks
- Appliance retained by Airlock (subscription model)
- Direct sales only initially

### Pricing

| Item | Amount | Timing |
|---|---|---|
| Onboarding fee | $99 | Collected at deployment |
| First month subscription | $199 | Collected at deployment |
| **Day one total** | **$298** | **Collected before box ships** |
| Monthly subscription (month 2+) | $199/month | Recurring |

### Hardware Model
- Hardware cost: $299
- Ownership: retained by Airlock
- Return required on cancellation
- OS: Linux

### Revenue Streams
- Primary: appliance subscriptions ($199/month per box)
- Secondary: Airpacks (~15% uplift estimated)
- Future: custom Airpack development, partner/reseller packs

---

## Model Assumptions

| Variable | Value | Notes |
|---|---|---|
| New boxes per month | 4 | Conservative lifestyle target |
| Hardware cost | $299 | Lower-tier mini PC |
| Onboarding fee | $99 | One-time on deployment — low friction |
| First month fee | $199 | Collected at deployment with onboarding |
| Monthly fee (month 2+) | $199 | Per active appliance — unchanged |
| Monthly churn | 2% | ~1 in 50 customers/month |
| Collection leakage | 5% | Applied to all collected revenue |

### Monthly Calculation Logic
1. Existing customers churn (`ActiveBoxes × (1 - 0.02)`)
2. New appliances added
3. Recurring revenue calculated on full active base (`ActiveBoxes × $199 × 0.95`)
4. Onboarding + first month collected on new boxes (`NewBoxes × $298 × 0.95`)
5. Hardware costs subtracted (`NewBoxes × $299`)
6. Monthly cashflow calculated
7. Cumulative cash updated

---

## 48-Month Projection

| Month | Active Boxes | Recurring Rev | Onboarding+M1 | Total Rev | HW Spend | Monthly CF | Cumulative CF |
|---|---|---|---|---|---|---|---|
| 1 | 4.0 | $756 | $1,132 | $1,889 | $1,196 | **+$693** | **+$693** |
| 2 | 7.9 | $1,497 | $1,132 | $2,630 | $1,196 | +$1,434 | +$2,126 |
| 3 | 11.8 | $2,224 | $1,132 | $3,356 | $1,196 | +$2,160 | +$4,286 |
| 4 | 15.5 | $2,935 | $1,132 | $4,068 | $1,196 | +$2,872 | +$7,158 |
| 5 | 19.2 | $3,633 | $1,132 | $4,765 | $1,196 | +$3,569 | +$10,727 |
| 6 | 22.8 | $4,316 | $1,132 | $5,449 | $1,196 | +$4,253 | +$14,980 |
| 12 | 43.1 | $8,140 | $1,132 | $9,272 | $1,196 | +$8,076 | +$54,104 |
| 24 | 76.8 | $14,527 | $1,132 | $15,660 | $1,196 | +$14,464 | +$194,074 |
| 36 | 103.4 | $19,540 | $1,132 | $20,672 | $1,196 | +$19,476 | +$401,425 |
| 48 | 124.2 | $23,473 | $1,132 | $24,605 | $1,196 | +$23,409 | **+$661,650** |

### Key Milestones

| Milestone | Value |
|---|---|
| Cash positive | **Month 1** |
| Day-one collection vs hardware cost | $298 collected vs $299 cost — near break-even |
| $50K cumulative | ~Month 12 |
| $10K/month cashflow | ~Month 13 |
| $200K cumulative | ~Month 25 |
| Month 24 MRR | ~$14,500 |
| Month 48 MRR | ~$23,500 |
| Month 48 monthly cashflow | ~$23,400 |
| Month 48 cumulative cash | **$661,650** |
| Month 48 active boxes | 124 |

---

## Compared to Original ($599 HW, $449 Onboarding) Model

| Metric | Original ($599 HW, $648 day one) | Alternate ($299 HW, $298 day one) | Difference |
|---|---|---|---|
| Month 1 cashflow | +$823 | +$693 | -$130 |
| Break-even | Month 1 | Month 1 | Unchanged |
| Month 12 cumulative | $55,664 | $54,104 | -$1,560 |
| Month 24 cumulative | $197,194 | $194,074 | -$3,120 |
| Month 48 cumulative | $667,890 | $661,650 | -$6,240 |
| Day-one onboarding ask to customer | $648 | **$298** | -$350 (54% lower) |

The cumulative cash difference over 4 years is **only ~$6,240** — less than 1% of the total. Recurring revenue (the dominant driver of the 48-month curve) is completely unchanged, since the monthly fee stays at $199.

---

## The Friction-Reduction Case

The entire cost of this pricing change is **$6,240 over 48 months** — in exchange for cutting the day-one ask from $648 to $298, a 54% reduction.

$648 upfront reads as a *purchase decision*: something to compare, discuss with a partner, or delay. $298 reads as a *low-risk trial*: roughly the cost of a single billable hour, for a sealed AI appliance that handles correspondence with zero data leaving the office.

**If lower friction increases sales velocity even modestly, this pricing wins outright.** For illustration, doubling the new-boxes-per-month rate (4 → 8) under this pricing:

| Month | Active Boxes (4/mo) | Active Boxes (8/mo) | Month 48 MRR (8/mo) | Month 48 Cumulative (8/mo, approx.) |
|---|---|---|---|---|
| 48 | 124 | ~248 | ~$46,800 | ~$1.2M–$1.4M |

A doubling of sales pace roughly doubles the entire cashflow trajectory — dwarfing the $6,240 cost of the lower onboarding fee. At these unit economics, **sales velocity is by far the dominant lever**, far more than maximizing day-one collection per box.

---

## Airpack Revenue Layer (Not Included Above)

The 48-month projection above is appliance subscription only. Airpack revenue sits on top.

Estimated Airpack uplift: ~15% of recurring revenue.

At Month 48 recurring revenue of ~$23,500/month, 15% Airpack uplift adds ~$3,500/month — pushing total monthly revenue toward **$27,000**.

Full Airpack revenue variables (see `AirpacSliderSpec.md`):
- Standard pack sales (attach rate × packs per customer × price)
- Custom pack development ($1,500 average project)
- Recurring pack subscriptions ($49/month)
- Partner/reseller revenue share (30%)

---

## Operations

- Founder: 1
- Contractors: 2–3
- Employees: none initially
- Strategy: legal vertical only, direct sales only, retained hardware, low-friction pricing

---

## The Lifestyle Math

At Month 48 with 124 active boxes:
- Monthly cashflow: ~$23,400
- Annual cashflow: ~$280,800
- Plus ~$42,000/year Airpack revenue on top
- **Total: ~$323,000/year** from 3-4 new customers per month — virtually identical to the original model's long-run outcome, but with a far easier sales conversation

---

*Last updated: June 2026 — alternate low-friction pricing ($299 hardware, $99 onboarding, $199/month unchanged)*
