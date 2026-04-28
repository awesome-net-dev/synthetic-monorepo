# Monorepo.Tool

A .NET global CLI that stitches many sibling git repos into a synthetic monorepo by rewriting NuGet `PackageReference` entries to `ProjectReference` at MSBuild evaluation time — **without modifying any leaf repo**.

## Install

```powershell
dotnet tool install --global Monorepo.Tool
```

See the [tool README](tools/Monorepo.Tool/README.md) for full usage, commands, and how it works.
