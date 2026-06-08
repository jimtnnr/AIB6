🗣️ Read My Draft Out Loud — Coqui TTS Integration Spec
Overview

This document outlines the full implementation plan for adding offline Text-to-Speech to Airlock AI using Coqui TTS, with a single-button Read My Draft Out Loud feature in the draft preview tab.

Goals

Add a male American English voice to speak the user's draft aloud.

Entirely offline, using Coqui TTS on Linux.

No cloud dependencies, no telemetry, no open microphone.

One-button experience: Read, and optionally Stop.

System Architecture
Components

AirlockApp: .NET + Avalonia frontend

coqui-tts: Offline TTS engine running via Python

tts_wrapper.py: Python bridge script (invoked via CLI or socket)

TextToSpeechService.cs: C# wrapper that sends draft content to Coqui

AudioPlayer.cs: Native .NET audio output using NAudio (Windows) or OpenAL (Linux)

Data Flow
UI Button (Read My Draft Out Loud)
    ↓
Letter string
    ↓
TextToSpeechService.cs (.NET)
    ↓
Call: tts_wrapper.py "letter text here"
    ↓
tts_wrapper.py
    ↓
Coqui TTS
    ↓
.wav output saved to /tmp/airlock_tts.wav
    ↓
AudioPlayer.cs plays the .wav

Installation (Linux)
Install Python dependencies
sudo apt update
sudo apt install python3 python3-pip espeak-ng ffmpeg libsndfile1
pip install TTS

Download a Coqui voice

Recommended voice:

tts --list_models | grep "en"  # see what's available

# Example: male en-US voice
tts --text "Airlock AI is speaking." --model_name "tts_models/en/ljspeech/tacotron2-DDC" --out_path airlock-test.wav

Save your model choice in tts_wrapper.py.
File: tts_wrapper.py
import sys
from TTS.api import TTS

text = sys.argv[1]
tts = TTS(model_name="tts_models/en/ljspeech/tacotron2-DDC")
tts.tts_to_file(text=text, file_path="/tmp/airlock_tts.wav")


Make it executable:

chmod +x tts_wrapper.py

File: TextToSpeechService.cs
using System.Diagnostics;

public class TextToSpeechService
{
    public static void Speak(string text)
    {
        string escapedText = text.Replace("\"", "\\\"");
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"tts_wrapper.py \"{escapedText}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(psi);
        process.WaitForExit();
    }
}

File: AudioPlayer.cs
using System.Media;

public class AudioPlayer
{
    public static void Play()
    {
        using var player = new SoundPlayer("/tmp/airlock_tts.wav");
        player.PlaySync();
    }
}

UI Integration
Location

Add button to Draft Preview tab:

Label: "Read My Draft Out Loud"

OnClick: TextToSpeechService.Speak(draftContent); AudioPlayer.Play();

Optional Enhancements

Add Stop Playback button (tracks and kills SoundPlayer thread).

Add Voice Control in Settings later (not MVP).

Store synthesized voice file in temp only, not persisted.

Testing
Smoke Test
python3 tts_wrapper.py "Testing Airlock voice synthesis."
aplay /tmp/airlock_tts.wav

.NET Test

Call:

TextToSpeechService.Speak("Airlock AI is speaking.");
AudioPlayer.Play();

License & Voice Model Notes

Coqui's default models are Apache 2.0

If needed, replace with a better-sounding or male-specific model later.

Optionally fine-tune a voice on your own data (future project).

Let me know if you want this split into implementation tasks, or bundled into a .zip starter kit.

You said:
what is most stable, why is coqui the best?
ChatGPT said:

Here’s why Coqui TTS is the best option — especially for Airlock’s sealed, offline, high-integrity philosophy — and why it beats alternatives across multiple axes:

✅ Why Coqui TTS Is the Best Fit
1. Offline First

Zero cloud dependencies. Unlike Google TTS, Azure, ElevenLabs, etc., Coqui runs entirely on your own hardware.

This matches Airlock's sealed-box model: no phoning home, no telemetry, no leaks.

2. Open Source & Actively Maintained

Apache 2.0 licensed.

Fork of Mozilla’s original TTS project, but better supported and with more available models.

Stable base of community support on GitHub and Discord.

3. Pretrained Voices Ready-to-Go

Dozens of voices (including American male/female) available via one-line download.

No need to train or synthesize your own — but you can, if you want.

Audio quality ranges from decent (e.g. Tacotron2-DDC) to excellent (e.g. VITS, GlowTTS, FastPitch).

4. Cross-Platform

Runs well on Linux, Windows, macOS, and even Raspberry Pi.

Python-based backend, but modular enough to interface cleanly with .NET apps via subprocess.

5. Fast Enough for Real-Time Use

Generates a 500-word draft in seconds on a modest CPU.

No GPU required — ideal for Airlock’s CPU-optimized box.

Low latency for interactive use (under 1 second for shorter lines).

🛠️ Stability and Alternatives
Engine	Offline	License	Quality	Control	Stability (Linux)	Notes
Coqui TTS	✅	Apache 2.0	High	Full	✅ Very stable	Best balance
Piper	✅	MIT	Medium	Fixed voice per model	✅ Good	Smaller/faster, but less expressive
Festival	✅	GPL	Low	Limited	✅ Rock solid	Robotic, dated
eSpeak NG	✅	GPL	Low	Limited	✅ Bulletproof	Emergency backup
ElevenLabs	❌	Proprietary	Very High	API only	❌ N/A	Not usable in Airlock
Google Cloud	❌	Proprietary	High	Limited	❌ N/A	Leak risk
🔒 Coqui Matches Airlock's Identity

Airlock is:

A sealed, offline, private unit.

Built for serious, real-world use — not toy apps.

Based on transparent, auditable systems (Postgres, Whisper, Local LLMs).

Coqui is the voice equivalent of:

Whisper for transcription.

Mixtral/Mistral for generation.

Postgres for data.

In other words, it fits the stack.

If you want something lighter, we can explore Piper, which is also offline and faster, but its voices are harder to tune and not as expressive. Coqui wins for now — and once it’s integrated, you’re ready for storytelling, therapy, coaching, or even AI-on-AI speech back-and-forth.

Let me know if you want a tts_setup.sh to automate Coqui install on your Linux image.

You said:
we can do heavy we own 2TB on a linux box
ChatGPT said:

Perfect. With 2TB and a beefy Linux box, you can go full Coqui — no compromises, full model downloads, even multi-speaker support if you want. Here's how we’ll frame it moving forward:

🧠 Coqui TTS Integration Setup (Airlock AI Edition)
🔧 Overview

You're installing Coqui TTS as an offline text-to-speech engine inside a sealed Linux AI unit (Airlock). This system will:

Read drafts aloud (Preview Modal, Generator Tab)

Default to a serious male American voice

Require zero internet, zero API keys

Be robust enough to generate 100s of letters/day

📁 Folder Layout Suggestion
/opt/airlock/tts/
├── models/
│   └── en_US/male_American
├── output/
│   └── audio.wav
├── tts_runner.py
├── config.json
└── tts_wrapper.sh

🛠️ Setup Steps
1. Install Coqui TTS
sudo apt update
sudo apt install -y python3 python3-pip
pip install TTS

2. Download a Pretrained Voice
# You can test with vits-en-US-libritts
tts --list_models | grep en

# Example: install a good male American English voice
tts --model_name "tts_models/en/ljspeech/tacotron2-DDC" --text "Hello from Airlock" --out_path ./audio.wav


Recommended voice models:

tts_models/en/ljspeech/tacotron2-DDC

tts_models/en/vctk/vits (multi-speaker)

tts_models/en/jenny/jenny (clean commercial tone)

3. Create a Simple Python Runner (tts_runner.py)
from TTS.api import TTS
import sys

text = sys.argv[1]
output_path = sys.argv[2]

tts = TTS(model_name="tts_models/en/ljspeech/tacotron2-DDC", progress_bar=False, gpu=False)
tts.tts_to_file(text=text, file_path=output_path)

4. Create a Bash Wrapper (tts_wrapper.sh)
#!/bin/bash
TEXT="$1"
OUTFILE="/opt/airlock/tts/output/audio.wav"
python3 /opt/airlock/tts/tts_runner.py "$TEXT" "$OUTFILE"
aplay "$OUTFILE"  # or use ffplay/mpv


Make it executable:

chmod +x /opt/airlock/tts/tts_wrapper.sh

🧩 .NET Integration (Avalonia)

From C#, call tts_wrapper.sh via Process.Start() when the user clicks “Read My Draft Aloud”

Pass the cleaned letter body as an argument

Pipe stderr to log for debug

Optionally show a visual "Speaking..." overlay with cancel button

🧠 Optional Enhancements

Cache recent outputs for replay

Log all speech requests (text + timestamp) to Postgres for future auditing

Add --speaker_idx for multi-speaker models like vctk

Add playback speed / pitch modifiers (Coqui supports it)

Let me know when you're ready and I’ll script the full install as tts_setup.sh, including voice model fetch, wrapper creation, and .NET shell compatibility.

This adds a huge layer of personality to Airlock. It's no longer just a draft — it speaks.