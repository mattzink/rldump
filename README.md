# rldump

`rldump` is a small .NET 10 Native AOT command-line tool that queries the Rocket
League **"Prime"** API (`prime.rocketplanet.gg`) and prints the result as JSON.
The output is shaped for a **web-page consumer**: IDs that would overflow
JavaScript's safe-integer range are emitted as strings, timestamps are ISO 8601
UTC, and game-mode indices are replaced with human-readable names (when known).

It is a single self-contained `rldump` binary (linux-x64) with no runtime
dependencies beyond the system C library.

## Commands

```
schedule    Get the current tournament schedule for a region
regions     List the valid schedule regions
```

## Install

The toolchain (.NET 10 SDK + the AOT C compiler) is managed by
[mise](https://mise.jdx.dev/). From a fresh clone:

```sh
mise trust && mise install && mise run setup   # restore packages
mise run publish                                # -> dist/rldump
./dist/rldump --help
```

Or run the tests: `mise run test`.

## Versioning

The base version is the `<Version>` in `src/rldump/rldump.csproj` (`1.0.0`); local
builds report it verbatim. In CI the patch number is replaced with the GitHub run
number, so every published build reports a unique version via `rldump --version`
(e.g. `1.0.57`) and the release artifact is named after it
(`rldump-linux-x64-1.0.57.tar.zstd`).

## Configuration

`rldump` reads its API credentials from a JSON config file. The bearer token is
**required** for any command that talks to the API.

```json
{
  "api": {
    "baseUrl": "https://prime.rocketplanet.gg",
    "token": "eyJ..."
  }
}
```

- `api.token` — **required** (non-empty).
- `api.baseUrl` — optional; defaults to `https://prime.rocketplanet.gg` (trailing
  slashes are trimmed).
- Unknown keys are ignored (forward compatible); a wrong-typed key is a config
  error.

### Where to get a token

A token is issued from your [rocketplanet.gg](https://rocketplanet.gg) account
portal. Paste it into `api.token`.

### Search order and merge

1. `--config <PATH>`, if given — exactly that file (must exist and be valid).
2. Else `./rldump.json` (current working directory), then
   `$XDG_CONFIG_HOME/rldump.json` (falling back to
   `~/.config/rldump.json` when `XDG_CONFIG_HOME` is unset).
3. When both files exist they are **merged per field**, with the cwd file winning.
   A file may contain just one field.

`rldump.json` is gitignored — it may contain a secret, so keep it out of the repo.
An example ships as [`rldump.json.example`](rldump.json.example): copy it to
`rldump.json` (or anywhere, and point `--config` at it) and fill in the token.

## Usage

```
rldump schedule --region USW            # tidy JSON to stdout
rldump regions                          # list valid regions
rldump schedule --region USW --raw      # verbatim API envelope
rldump schedule --region USW --compact  # single-line JSON
rldump schedule --region USW -o out.json --timeout 15
rldump --config /path/to/rldump.json regions
```

Global options: `--config <PATH>`, `--raw`, `--compact`, `-o, --out <FILE>`,
`--timeout <SECS>` (default 30), `--help`, `--version`.

### Sample output

`rldump schedule --region USC` (excerpt — one schedule, first tournament):

```json
{
  "schedules": [
    {
      "time": "2026-11-04T21:00:00Z",
      "scheduleId": "59935",
      "updateSkill": false,
      "tournaments": [
        {
          "id": "53008201",
          "scheduleId": "59935",
          "startTime": "2026-11-04T21:00:00Z",
          "public": false,
          "gameMode": "Dropshot",
          "gameModes": [],
          "title": "3v3 Dropshot",
          "creatorName": "Psyonix",
          "region": "USC",
          "platforms": ["Steam", "PS4", "XboxOne", "Switch", "Epic"],
          "teamSize": 3,
          "mapSetName": null,
          "seriesLength": 1,
          "finalSeriesLength": 3,
          "isSchedulingTournament": true
        }
      ]
    }
  ]
}
```

**`gameMode` / `gameModes` are `string | number` by design.** A known game-mode
index is replaced with its name (`"gameMode": "Dropshot"`); an index the tool
doesn't know (e.g. index `4`, or any future index) is kept as the raw number
(`"gameMode": 4`). No names are invented and no data is dropped, so a consumer
should handle both shapes.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | success |
| 1 | API error (`Error` envelope, or a non-2xx HTTP response) |
| 2 | usage error (unknown command/option, missing required option) |
| 3 | configuration error (no config found, missing token, malformed/unreadable file, or unwritable `-o` path) |
| 4 | network/transport error (DNS, connect, timeout) |

JSON always goes to **stdout** (or `-o FILE`); diagnostics always go to
**stderr**. They are never mixed.

## Adding an endpoint

The client is built to grow. The pattern is:

1. Consult the **live docs data endpoints** (`/docs/index.json`,
   `/docs/contract?service=…`) for the request/response contract — the reflected
   types can be incomplete, so the wire is the source of truth.
2. Add the request/response model types and register them in `ApiJsonContext`
   (source-generated; AOT forbids reflection-based serialization).
3. Model **only the fields you transform**; let everything else flow through
   `[JsonExtensionData]` so new wire fields don't require a model change.
4. Add a source-generated entry to `OutputJsonContext` for the tidy output and a
   mapper in `Output/`.
5. Add a command in `Cli/` wired through `RldumpApp`, and cover it with tests
   (use the fake `HttpMessageHandler`).

## License

MIT — see the [LICENSE](LICENSE) file (repo only; the release tarball ships
just the binary and the example config).
