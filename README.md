# ListenBridge

![License](https://img.shields.io/badge/license-MIT-blue.svg)

ListenBridge imports listening history exports into ListenBrainz.

It currently reads YouTube Music / YouTube Takeout `watch-history.html` files and Spotify streaming history JSON exports, normalizes them into a shared listen model, optionally filters or reports the result, and submits the listens to the ListenBrainz API.

## Features

- Import YouTube Music entries from Google Takeout history HTML.
- Import Spotify streaming history JSON in simple and extended export formats.
- Submit normalized listens to ListenBrainz in configurable chunks.
- Run dry imports and write parsed output as JSON or a sortable HTML report.
- Filter imports by Unix timestamp range.
- Collapse duplicate YouTube listens using optional cleanup windows.
- Audit a ListenBrainz export for exact and near duplicates.

## Requirements

- .NET 10 SDK
- ListenBrainz user token from `https://listenbrainz.org/settings/` for submissions
- One supported source export:
  - YouTube Takeout `watch-history.html`
  - Spotify streaming history JSON array

## Quick Start

Restore and build the solution:

```powershell
dotnet restore ListenBridge.sln
dotnet build ListenBridge.sln
```

Import YouTube history:

```powershell
dotnet run --project src/ListenBridge.Cli -- --input "path/to/watch-history.html" --token "LISTENBRAINZ_TOKEN"
```

Import Spotify history:

```powershell
dotnet run --project src/ListenBridge.Cli -- --source spotify --input "path/to/StreamingHistory_music_0.json" --token "LISTENBRAINZ_TOKEN"
```

Preview an import without submitting:

```powershell
dotnet run --project src/ListenBridge.Cli -- --input "path/to/watch-history.html" --dry-run --output-json "reports/listens.json" --output-html "reports/listens.html"
```

Audit a ListenBrainz export instead of importing:

```powershell
dotnet run --project src/ListenBridge.Cli -- --audit-export "path/to/listenbrainz_export.json"
```

## CLI Options

| Option | Description |
| --- | --- |
| `-i, --input <path>` | Source export file to import. Required unless `--audit-export` is used. |
| `--source <name>` | Import source: `youtube` or `spotify`. Defaults to `youtube`. |
| `-t, --token <token>` | ListenBrainz user token. Required for submissions, not required for `--dry-run` or audit mode. |
| `--chunk-size <n>` | ListenBrainz submission chunk size. Defaults to `200`. |
| `--after <seconds>` | Include listens after this Unix timestamp. |
| `--before <seconds>` | Include listens before this Unix timestamp. |
| `--dry-run` | Parse and print listens without submitting them. |
| `--output-json <path>` | With `--dry-run`, write parsed listens to JSON. |
| `--output-html <path>` | With `--dry-run`, write a sortable HTML report. |
| `--skip-threshold-seconds <n>` | For YouTube imports, collapse repeated same-listen entries within the given window. |
| `--dedupe-window-seconds <n>` | For YouTube imports, deduplicate close consecutive same-listen entries within the given window. |
| `--exclude-topic-channels` | For YouTube imports, exclude channels whose names contain `- Topic`. |
| `--audit-export <path>` | Audit a ListenBrainz export JSON file instead of importing source data. |
| `--audit-window-seconds <n>` | Near-duplicate window for audit mode. Defaults to `60`. |
| `-h, --help` | Show CLI help. |

## Source Notes

### YouTube

Use the HTML history file from Google Takeout. The parser keeps entries from the YouTube Music section and topic-channel music entries, extracts track, artist, timestamp, and origin URL, then applies optional YouTube cleanup filters.

### Spotify

Use Spotify streaming history JSON arrays from account data exports. ListenBridge supports both formats:

- Simple export fields: `endTime`, `artistName`, `trackName`
- Extended export fields: `ts`, `master_metadata_album_artist_name`, `master_metadata_track_name`, `spotify_track_uri`

Rows missing artist, track, or timestamp data are skipped and counted as invalid rows in diagnostics.

### Audit Mode

`--audit-export` does not import or submit listens. It reads a ListenBrainz export JSON file and reports total listens, parsed listens, unique identities, exact duplicates, near duplicates, date range, and top identities.

## Architecture

ListenBridge is split into focused .NET projects:

| Project | Responsibility |
| --- | --- |
| `src/ListenBridge.Cli/` | CLI parsing, command execution, and report writing. |
| `src/ListenBridge.Core/` | Shared listen model, parser and destination abstractions, filtering, and import orchestration. |
| `src/ListenBridge.YouTube/` | YouTube Takeout HTML parsing. |
| `src/ListenBridge.Spotify/` | Spotify streaming history JSON parsing. |
| `src/ListenBridge.ListenBrainz/` | ListenBrainz payload mapping, API client, and export audit logic. |
| `tests/ListenBridge.Tests/` | xUnit coverage for parsers, CLI behavior, filtering, payloads, API requests, and audit behavior. |

Import flow:

1. `ListenBridge.Cli` parses options and selects the command.
2. The selected source parser returns normalized listens with diagnostics.
3. `ListenBridge.Core` applies date filtering and coordinates import results.
4. `ListenBridge.ListenBrainz` maps listens to API payloads and submits them.

## Development

Run tests:

```powershell
dotnet test ListenBridge.sln
```

Run the local baseline checks before opening a pull request:

```powershell
dotnet format ListenBridge.sln --verify-no-changes --no-restore
dotnet build ListenBridge.sln --configuration Release --no-restore
dotnet test ListenBridge.sln --configuration Release --no-build --collect:"XPlat Code Coverage"
```

Project conventions:

- Keep source code under `src/` and tests under `tests/`.
- Keep source parsing, core orchestration, and destination submission separate.
- Add or update tests when changing parser behavior, filtering, CLI options, payload mapping, or ListenBrainz requests.
- Follow `.github/copilot/copilot-instructions.md` for repository-specific coding guidance.

## License

This repository is licensed under the MIT License. See `LICENSE` for details.
