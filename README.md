# Magelaxiom

**Magelaxiom** is a lightweight, always-on-top Windows dictionary for English
words and phrases.

It is a Magellanique open source project.

The app opens as a fixed 640x640 desktop window with one search bar. It looks
up English meanings, synonyms, and antonyms as you type, and lets you save
searched terms for later.

## Download

Portable Windows builds are published on the GitHub Releases page:

```text
https://github.com/magellanique/magelaxiom/releases
```

The release executable is self-contained for lookup data. It does not require
Python, Electron, Node, a browser runtime, or runtime network access.

## Run From Source

After building, run:

```powershell
.\dist\Magelaxiom.exe
```

## Build

```powershell
.\build.ps1
```

To rebuild the embedded dictionary index from the local raw open data:

```powershell
.\build.ps1 -RefreshData
```

If `data\raw` is missing in a fresh clone, fetch the open dataset first:

```powershell
.\tools\FetchOpenData.ps1
```

The build script uses the Windows .NET Framework C# compiler at
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, generates an
inspectable TSV plus compact binary index under `data\generated`, and embeds
the binary index into `dist\Magelaxiom.exe`.

## Dictionary Data

Runtime searches use embedded local data:

- Open English WordNet 2025 WNDB files for English definitions, synonym sets,
  and antonym relations.
- A small manual phrase fallback table for phrases such as `ceteris paribus`,
  `ex ante`, `ex post`, and `the buck stops here`.

Runtime lookup uses a sorted binary index, so searches do not scan or parse the
full dictionary on every query.

## Matching Behavior

Search is forgiving about common phrase formatting:

- `ex-ante`, `ex ante`, and `exante` can resolve to the same entry.
- Spaces, underscores, apostrophes, periods, slashes, and common dash variants
  are normalized for matching.
- Offensive, taboo, or sensitive words are not filtered out by the app. Coverage
  depends on the embedded open dataset.

No dictionary can honestly guarantee every English word in existence, but Open
English WordNet gives broad open-licensed English coverage, and the builder can
be extended with more compatible open datasets later.

## Saved Terms

Saved terms are stored at:

```text
%APPDATA%\Magelaxiom\saved_words.txt
```

## Website

The project website source lives in `docs/` and is designed for GitHub Pages.

## License

Application code is MIT licensed. Dictionary data is from Open English WordNet
and is licensed under Creative Commons Attribution 4.0 International with
Princeton WordNet attribution requirements. See `THIRD_PARTY_NOTICES.md`.
