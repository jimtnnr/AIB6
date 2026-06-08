Airlock AI Text-to-Speech (TTS) Specification
Feature Overview

Add a “Read My Draft Out Loud” button to the Draft Preview screen.

Purpose: allow users to listen to their generated drafts for clarity, proofreading, and accessibility.

Must operate fully offline — no cloud services or external dependencies.

Requirements

Offline-only TTS engine

Engines considered:

Piper (lightweight, CPU-friendly, high-quality voices)

Coqui TTS (more flexible, multiple voices and styles)

Should handle long drafts (letters, memos) without choking on length.

Integrate seamlessly with Airlock’s sealed/air-gapped architecture.

Implementation Notes

Button placement: inside the Draft Preview modal.

Behavior:

User clicks Read My Draft Out Loud.

System sends the current draft text to the local TTS engine.

Audio playback begins automatically.

Optional: stop/pause controls for playback.

Configurable voice options may be added later but default voice is sufficient for v1.

Rationale

Proofreading aid: hearing text out loud highlights errors the eye skips.

Accessibility: useful for users with reading fatigue or visual impairment.

Privacy: offline voice ensures no draft text leaves the Airlock unit.

Do you want me to also write a step-by-step install guide for Piper and/or Coqui (Linux-only), so we can drop it into your VoiceSpec folder alongside the other docs?

You said:
what was name of chat?
ChatGPT said:

The chat where we locked this in was called:

“Voice input integration” — but the actual Text-to-Speech (TTS) decision was noted on June 21, 2025, under that conversation thread.

Specifically, that’s when you decided to add the “Read My Draft Out Loud” button and chose Piper/Coqui as offline TTS options.

Want me to build you a .md TTS Install & Setup Guide next, so you’ve got both the spec and the how-to side by side?

You said:
ok, and add all the details
ChatGPT said:

Got it. Here’s a full .md that combines spec + install + setup details so you’ve got everything in one place for Airlock’s TTS feature:

Airlock AI Text-to-Speech (TTS) Specification & Setup Guide
Feature Overview

Feature Name: Read My Draft Out Loud

Location: Draft Preview modal

Function: Plays back the current draft text using offline Text-to-Speech (TTS).

Purpose: Proofreading, accessibility, and private listening of sensitive drafts.

Requirements

Offline operation only (no cloud APIs).

Supported Engines:

Piper – lightweight, CPU-optimized, high-quality voices.

Coqui TTS – flexible, supports multiple voices/styles.

OS: Linux (Airlock golden image baseline).

Playback Controls:

Minimum: Start, Stop.

Optional (future): Pause, Resume, Voice selection.

Draft Size: Must handle multi-page legal letters (10K+ characters).

Implementation Notes

Add a “Read My Draft Out Loud” button inside the Draft Preview modal.

Flow:

User clicks button.

Current draft text is written to a temporary .txt file.

Piper or Coqui generates .wav or .mp3.

Audio plays locally through system speakers.

For consistency, default to Piper voice en_US-amy-low unless user changes.

Installation Guide
Piper Setup (Recommended)

Install Piper

sudo apt update
sudo apt install piper-tts


Get Voices (example: US English Amy)

wget https://github.com/rhasspy/piper/releases/download/v0.0.2/en_US-amy-low.onnx
wget https://github.com/rhasspy/piper/releases/download/v0.0.2/en_US-amy-low.onnx.json


Place in /opt/piper/voices/.

Test

echo "Hello from Airlock AI" | piper --model /opt/piper/voices/en_US-amy-low.onnx --output_file test.wav
aplay test.wav

Coqui TTS Setup (Optional Alternative)

Install Dependencies

sudo apt update
sudo apt install python3-pip ffmpeg espeak


Install Coqui TTS

pip install TTS


Run Example

echo "Hello from Airlock AI" > input.txt
tts --text "$(cat input.txt)" --out_path test.wav
aplay test.wav

Integration Notes

Add a backend helper script (e.g., tts_runner.sh):

#!/bin/bash
TEXT="$1"
OUTPUT="/tmp/airlock_tts.wav"
echo "$TEXT" | piper --model /opt/piper/voices/en_US-amy-low.onnx --output_file "$OUTPUT"
aplay "$OUTPUT"


Call this script from the Airlock UI when the button is pressed.

Future expansion: allow switching engines (Piper vs Coqui) in Settings tab.

Rationale

Proofreading: Hearing text exposes errors the eye skips.

Accessibility: Useful for visually impaired or fatigued users.

Privacy: 100% offline, no draft leaks to cloud.

