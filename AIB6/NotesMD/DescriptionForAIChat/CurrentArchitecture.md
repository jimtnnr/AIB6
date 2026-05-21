Current Airlock architecture:

Avalonia UI
↓
Local PostgreSQL
- metadata
- workflow configs
- indexing
- archive state
- favorites/hidden
- app persistence

Local Filesystem
- generated letters
- templates
- exports
- assets

Dockerized Ollama
↓
Local HTTP API
http://localhost:11434/api/generate

Which means:

Docker responsibility:
ONLY inference runtime

Native OS responsibility:
everything stateful