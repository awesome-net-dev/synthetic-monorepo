# Monorepo.Tool

A .NET global CLI that stitches many sibling git repos into a synthetic monorepo by rewriting NuGet `PackageReference` entries to `ProjectReference` at MSBuild evaluation time — **without modifying any leaf repo**. Fully reversible: one sentinel file toggles the whole thing on or off.

## Install

```powershell
dotnet pack -c Release
dotnet tool install -g --add-source ./nupkg Monorepo.Tool
```

## Layout

```
backend/                              ← your existing backend root
├── <leaf-repo>/.git/…                ← many sibling repos, each with their own csprojs
└── synthetic-monorepo/               ← this tool's home (a sibling, NOT committed to any leaf repo)
    ├── monorepo.json                 ← generated config — edit Enabled=false to opt out selectively
    ├── Monorepo.sln                  ← generated VS solution spanning every leaf
    └── overlay/
        ├── Directory.Build.props
        └── Directory.Build.targets   ← generated MSBuild overlay that does the rewriting
```

After `init` also drops:
```
backend/Directory.Build.props         ← shim that imports overlay (sentinel-gated)
backend/Directory.Build.targets       ← shim that imports overlay (sentinel-gated)
backend/.monorepo-active              ← sentinel file — delete this and the overlay goes dark
```

## Commands

```powershell
# First run: discover repos and generate everything
monorepo init --backend path\to\backend --overlay .

# Re-scan after adding or removing a repo (preserves Enabled=false overrides)
monorepo generate --refresh

# Regenerate from existing monorepo.json without re-scanning
monorepo generate

# Opt a single package out of the rewrite
monorepo disable MyComp.MyProj
monorepo generate

# Opt back in
monorepo enable MyComp.MyProj
monorepo generate

# Toggle the whole overlay off/on without touching any config
monorepo off
monorepo on

# Show current state (configured mappings, drift, sentinel status)
monorepo status
```

## Exit codes

| Code | Meaning |
|------|---------|
| 0    | Success |
| 1    | Unhandled / general error (set `MONOREPO_DEBUG=1` for a stack trace) |
| 2    | Invalid input (bad flag, unknown package, existing config without `--force`) |
| 3    | `monorepo.json` not found |
| 4    | `monorepo.json` corrupt |
| 5    | Drift detected by `status` |

## How it works

`Directory.Build.targets` in the overlay uses three static ItemGroups:
1. **Snapshot** — copies the current `@(PackageReference)` into a private item type before anything is removed.
2. **Remove** — strips every mapped `PackageReference` so NuGet restore never sees them (skipped for net7/net6/netstandard, which stay on the original packages).
3. **Inject** — for each mapping, probes the snapshot and conditionally adds a `<ProjectReference>` at evaluation time. Because it happens at evaluation (not in a `Target`), the reference lands in `project.assets.json` and NuGet propagates transitive packages correctly.

The `.monorepo-active` sentinel is the master kill-switch — the shim `Directory.Build.*` files at the backend root only import the overlay when it exists.

## Troubleshooting

- **"monorepo.json not found"** — `cd` into the overlay directory or a subdirectory of it, or pass `--config`.
- **Drift warning from `status`** — a csproj that used to produce a mapped package has been moved or deleted. Run `monorepo generate --refresh`.
- **A project I depend on wasn't rewritten** — check `monorepo status` for `✗` markers; run `monorepo enable <packageId>` if it's disabled.
- **An older-TFM project is complaining** — net7/net6/netstandard projects are intentionally skipped; they keep their original packages.
