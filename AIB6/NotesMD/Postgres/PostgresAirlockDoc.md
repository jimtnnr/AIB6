# AirLock – PostgreSQL Architecture & Operations

---

## Why Native PostgreSQL (Not Docker)

PostgreSQL runs natively on the host OS — not in a container. This is intentional.

Reasons:
- Stateful data should not be hidden behind Docker volume abstraction
- Easier backup and restore — files are where you expect them
- Easier debugging — no container lifecycle to manage
- Lower operational complexity for an appliance
- PostgreSQL is mature and stable — no benefit to isolating it

Docker is reserved for volatile inference services (Ollama/Mistral) that change frequently and benefit from isolation. PostgreSQL does not need that treatment.

---

## Connection

```
Host:     localhost
Port:     5432
Username: airlock
Password: airlock
Database: airlock
```

Connection string in `appsettings.json`:
```json
"Postgres": "Host=localhost;Port=5432;Username=airlock;Password=airlock;Database=airlock"
```

All database access goes through `PostgresHelper.cs` using `Npgsql`.

---

## What PostgreSQL Stores

PostgreSQL stores **metadata only**. It does not store letter content.

| What | Where |
|---|---|
| Letter metadata | PostgreSQL (`letters` table) |
| Letter content | Local filesystem (`~/Documents/AIBDOCS/`) |
| Templates | Local filesystem (`~/Documents/AIBDOCS/config/`) |
| Exports | Local filesystem (`~/Documents/AIBDOCS/`) |

This is the correct separation. The database is an index. The filesystem is the archive.

---

## Schema

### `letters` table

The primary operational table.

```sql
CREATE TABLE public.letters (
    id        integer   NOT NULL,  -- auto-increment primary key
    filename  text      NOT NULL,  -- filename of the saved letter
    letter_type text    NOT NULL,  -- e.g. "Legal Letters > Appeal"
    timestamp timestamp NOT NULL,  -- generation timestamp
    favorite  boolean   NOT NULL DEFAULT false,
    hidden    boolean   NOT NULL DEFAULT false
);
```

### `draft_archive` table

Present in schema but currently unused. Reserved for future archive workflow. No data in it yet.

---

## Stored Procedures & Functions

### `insert_letter` (PROCEDURE)
Called on every save. Inserts a new row into `letters`.

```sql
CALL insert_letter(
    p_filename    text,
    p_letter_type text,
    p_timestamp   timestamp,
    p_favorite    boolean,
    p_hidden      boolean
)
```

Called from `PostgresHelper.cs`:
```csharp
await using var cmd = new NpgsqlCommand("CALL insert_letter(@filename, @letter_type, @timestamp, @favorite, @hidden)", conn);
```

### `get_letters` (FUNCTION)
Returns all letters ordered by timestamp descending.

```sql
SELECT * FROM get_letters();
```

### `get_draft_archive_page` (FUNCTION)
Paginated, filterable, sortable query for the archive grid UI.

Parameters:
- `page` — page number (1-based)
- `size` — rows per page
- `sort_column` — column to sort by
- `sort_direction` — `ASC` or `DESC`
- `filter` — text search against filename and letter_type
- `show_hidden` — whether to include hidden letters

Used by `ArchiveGridView` to power the Review Drafts tab.

---

## Filename Convention

Filenames encode all metadata, which means the file itself is self-describing even without the database:

```
{Type}_{SubType}_{Intent}_{Length}_{Timestamp}.txt
```

Example:
```
Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_141533.txt
```

This is intentional — if the database is lost, the archive folder alone is recoverable.

---

## Current Data State (as of June 2026)

10 letters in the `letters` table. All are `Legal Letters > Appeal` type. Mix of:
- 2 favorites (`favorite = true`)
- 3 hidden (`hidden = true`)
- 5 normal

`draft_archive` table is empty — not yet in use.

---

## Backup

Since PostgreSQL is native, backup is straightforward:

```bash
pg_dump -U airlock airlock > airlock_backup_$(date +%Y%m%d).sql
```

Restore:
```bash
psql -U airlock airlock < airlock_backup_20260601.sql
```

The `full_dump.sql` in the repo is the current schema + data export. Keep it updated after schema changes.

---

## Appliance Imaging Notes

When building a new Airlock machine:

1. Install PostgreSQL natively
2. Create user and database:
```bash
sudo -u postgres psql
CREATE USER airlock WITH PASSWORD 'airlock';
CREATE DATABASE airlock OWNER airlock;
```
3. Restore schema:
```bash
psql -U airlock airlock < full_dump.sql
```
4. Verify connection matches `appsettings.json`

---

## Architecture Rule

PostgreSQL is metadata infrastructure. It serves AirLock. AirLock does not bend around it.

If the database is lost, the filesystem archive survives. If the filesystem is lost, the database is just an index with no content. Both should be backed up together.

---

*Last updated: June 2026*