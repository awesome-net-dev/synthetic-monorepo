# Monorepo.Tool (Synthetic Monorepo)

> Turn a scattered collection of sibling repos into a unified monorepo — without touching a single file inside them.

---

## The Problem

You have multiple .NET repositories living side by side. Each one is well-structured, independently deployable, and owned by a team. But the moment `repo-b` needs something from `repo-a`, you're stuck choosing between two bad options:

- **NuGet packages** — publish, wait, bump version, repeat. Every cross-repo change becomes a multi-step ceremony. Refactoring across repos is a fantasy. The IDE treats them as strangers.
- **Convert to a real monorepo** — merge everything, rewrite CI pipelines, break every team's workflow, and commit to a migration you can never fully undo.

There is a third option. One that requires no migration, no compromises, and no permanent decisions.

---

## The Solution

**Synthetic Monorepo** creates an MSBuild overlay that quietly rewrites `PackageReference` entries into `ProjectReference` entries — at build evaluation time, before the compiler sees a thing. Your repos remain unchanged. Your git history stays clean. NuGet still works when you need it.

And when you're done? One command brings you back.

```
monorepo on    # you're in a monorepo
monorepo off   # you're not
```

That's the whole model. A light switch.

---

## Why This Is Different

Most monorepo tools require you to *become* a monorepo. This tool lets you *act like* one whenever it's useful — and step back out whenever it isn't.

- **Non-invasive** — no files are created or modified inside any leaf repo. Ever.
- **Zero git noise** — `git status` across all your repos stays clean while the overlay is active.
- **Reversible by design** — turning it off restores the exact state you started in.
- **Works with your existing toolchain** — NuGet, MSBuild, Visual Studio, Rider, `dotnet build`. No plugins, no wrappers.
- **Instant IDE benefits** — with the overlay on, Go to Definition, Find All References, and cross-repo refactoring work as if the code were always in one place.

---

## Requirements

- .NET 9 SDK or later
- `git` available in `PATH`
- Repos arranged as siblings under a common `backend/` root

---

## Installation

```bash
dotnet tool install -g Monorepo.Tool
```

Verify:

```bash
monorepo --version
```

---

## Getting Started

### 1. Initialize

Run this once from inside your overlay directory (e.g. `backend/synthetic-monorepo/`):

```bash
monorepo init --backend ../
```

This scans all sibling repos, discovers cross-repo `PackageReference` relationships, writes `monorepo.json`, and generates the MSBuild overlay. Nothing in the sibling repos is touched.

### 2. Turn it on

```bash
monorepo on
```

Creates a `.monorepo-active` sentinel file in the backend root. MSBuild picks it up on the next build — no restart, no IDE reload. Open any solution and your cross-repo `PackageReference` entries are now live `ProjectReference`s.

### 3. Turn it off

```bash
monorepo off
```

Removes the sentinel. Your repos are independent again. The overlay files remain on disk but are completely inert without the sentinel — they import nothing, change nothing.

### 4. Update when repos drift

When you add packages, create new projects, or bring in new repos:

```bash
monorepo generate --refresh
```

Re-scans all repos and merges new discoveries into `monorepo.json`. Packages you previously disabled (`Enabled=false`) stay disabled — your manual overrides survive a refresh.

Check what the overlay currently knows:

```bash
monorepo status
```

---

## How It Works

```
backend/
├── .monorepo-active              ← sentinel (created by `on`, removed by `off`)
├── Directory.Build.props         ← generated shim (imports overlay when sentinel exists)
├── Directory.Build.targets       ← generated shim (imports overlay when sentinel exists)
│
├── repo-a/                       ← your repos, untouched
├── repo-b/
├── repo-c/
│
└── synthetic-monorepo/           ← this tool lives here
    ├── monorepo.json             ← discovered mappings
    ├── Monorepo.sln              ← generated solution spanning all repos
    └── overlay/
        └── Directory.Build.targets   ← the rewrite logic
```

When the sentinel exists, the shims import the overlay. The overlay uses three static MSBuild `ItemGroup` layers evaluated before restore:

1. Snapshot all `PackageReference` items
2. Remove mapped packages from the restore graph
3. Inject `ProjectReference` items pointing at the producing project

When the sentinel is absent, the `Condition="Exists(...)"` on every import is false. The overlay is never loaded. Your repos behave exactly as if this tool does not exist.

---

## Command Reference

| Command | What it does |
|---|---|
| `monorepo init --backend <path>` | First-time setup: scan, write config, generate overlay |
| `monorepo on` | Enable the overlay (create sentinel) |
| `monorepo off` | Disable the overlay (remove sentinel) |
| `monorepo generate` | Regenerate overlay from current `monorepo.json` |
| `monorepo generate --refresh` | Re-scan repos, merge new findings, regenerate |
| `monorepo status` | Show state, mappings, exempt repos, drift |
| `monorepo enable <packageId>` | Re-enable a previously disabled mapping |
| `monorepo disable <packageId>` | Disable a mapping (keeps `PackageReference` for that package) |
| `monorepo cpm` | Migrate to Central Package Management |
| `monorepo release` | Bump version, write `CHANGELOG.md`, create git tag |
| `monorepo bump patch\|minor\|major` | Create a version tag without writing a changelog |
| `monorepo clean` | Delete all `bin/` and `obj/` directories across all repos |

Every command accepts `--dry-run` to preview changes without writing anything, and `--help` for full option details.

---

## License

MIT