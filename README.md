# SkillScanner

SkillScanner is a .NET command-line tool for scanning Markdown skill documents with YAML front matter and reporting potential security concerns.

## What it checks

The current `Overprivileged` rule reports high-severity findings when a skill:

- declares that it requires network access in its `compatibility` metadata; or
- refers to `memory.md` or `soul.md` in its Markdown content.

## Requirements

- .NET SDK 9.0 or later

## Run a scan

From the repository root, run:

```bash
dotnet run -- scan <path-to-skill-file>
```

For example, the included sample skill can be scanned with:

```bash
dotnet run -- scan azure-sre-agent/Skill.md
```

The scanner writes a report to standard output, grouped by rule type and including each finding's message and severity.

## Skill document format

Skill files must have YAML front matter delimited by `---`, followed by Markdown content:

```md
---
name: example-skill
description: Example skill
compatibility: Requires network access.
---
# Example Skill
```

## Build

```bash
dotnet build
