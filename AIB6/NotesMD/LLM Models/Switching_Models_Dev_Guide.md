# AirLock — Switching Models (Developer Reference)

*Internal/dev-only. End users never see or choose a model — this is for development, testing, and deployment configuration only.*

---

## Why There's No UI Selector

AirLock follows a "factorial collapse" / appliance philosophy — the runtime should be boring, and infrastructure decisions (which model, which inference backend) are made *once*, by whoever configures the appliance, not exposed as an ongoing choice to the end user. Model selection is the same category of decision as "which CPU governor" or "which Docker image" — it's deployment configuration, not a user-facing feature.

---

## How It Works

`appsettings.json` has a `ModelSettings.Models` dictionary — any number of named model configurations, each pointing at its own Ollama endpoint:

```json
"ModelSettings": {
  "DefaultModel": "mistral",
  "Models": {
    "mistral": {
      "ModelName": "mistral:7b-instruct-q4_K_M",
      "Endpoint": "http://localhost:11434/api/generate"
    },
    "phi3": {
      "ModelName": "phi3:mini",
      "Endpoint": "http://localhost:11435/api/generate"
    },
    "qwen": {
      "ModelName": "qwen2.5:3b",
      "Endpoint": "http://localhost:11436/api/generate"
    }
  }
}
```

`DefaultModel` is the **key** (e.g. `"mistral"`, `"phi3"`, `"qwen"`) — not the model name itself. AirLock looks up that key in `Models` at startup and uses the matching `ModelName`/`Endpoint`.

---

## To Switch the Active Model

1. Make sure the target model's Ollama container is running (see `Models_and_Containers.md` for the current set — Mistral on 11434, Phi-3 on 11435, Qwen on 11436)
2. Edit `appsettings.json`:
   ```json
   "DefaultModel": "phi3"
   ```
3. Restart AirLock (`dotnet run`)

That's it. `LetterTab` reads `DefaultModel`, looks it up in `Models`, and uses that endpoint for all generation for the rest of that session.

---

## To Add a New Model

1. Pull and run a new Ollama container on its own port (see `Models_and_Containers.md` for the pattern)
2. Add a new entry to `Models` in `appsettings.json`:
   ```json
   "newmodel": {
     "ModelName": "llama3.2:3b",
     "Endpoint": "http://localhost:11437/api/generate"
   }
   ```
3. Optionally set `DefaultModel` to `"newmodel"` to make it active, or leave it as-is and just have it available for future switching

No code changes needed — `LetterTab`'s constructor reads whatever's in the dictionary.

---

## Safety Behavior (What Happens If Something's Misconfigured)

- **`DefaultModel` doesn't match any key in `Models`** → AirLock logs a console warning (`[CONFIG WARNING]`) and falls back to the *first* entry in `Models` instead of crashing.
- **`Models` is empty entirely** → AirLock throws on startup with a clear error message pointing at `appsettings.json`. This is a hard stop — there's no sensible default to fall back to with zero models configured.

---

## Important: Models Are Not Drop-In Interchangeable

Switching `DefaultModel` changes *which model generates the letter*, but the `.airpack` prompt templates (`RoleInstruction`, `PromptTemplateText`, tone/length directives in `PromptMappings`) were written and tuned against Mistral 7B's behavior. Different models may:

- Follow length instructions more or less precisely
- Interpret tone directives differently
- Be more or less prone to ignoring the "use only provided facts, don't invent" instruction
- Produce noticeably different output structure even from an identical prompt

**Before shipping a model change**, run the comparison process documented in `Model_Comparison_Test.md` — same prompt/template/settings across candidate models, check output against the quality checklist, and decide based on real output, not just speed.

If a new model is adopted permanently, the `.airpack` files (especially `RoleInstruction` and any `PromptTemplateText` wording) may need re-tuning for that model's specific behavior.

---

*Last updated: June 15, 2026*
