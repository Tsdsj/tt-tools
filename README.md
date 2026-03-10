# TtLauncher

> 🇨🇳 [中文文档](./README_zh.md) | 🇬🇧 English

**TtLauncher** 是一款运行于 Windows 的 WPF 个人效率启动器。  
TtLauncher is a WPF launcher for personal productivity on Windows.

Current target release: `beta-0.0.1`

## Features

- App search and launch
- Everything file search
- OCR screen capture
- Port inspection
- Tray menu and startup toggle
- Raycast-like dark floating UI

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for local development
- Tesseract language data for OCR

Required OCR files:

```text
tessdata/eng.traineddata
tessdata/chi_sim.traineddata
```

## Development

Use the local SDK bundled in this repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 run --project .\src\TtLauncher\TtLauncher.csproj
```

Build only:

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 build .\src\TtLauncher\TtLauncher.csproj
```

## Commands

Default app search:

```text
chrome
code
wechat
```

Everything file search:

```text
f readme
f resume
f config.json
```

OCR:

```text
ocr
```

Ports:

```text
port 3000
ports
```

## Everything

The project supports a bundled `es.exe`.

Source location:

```text
src/TtLauncher/Assets/Tools/Everything/es.exe
```

Published location:

```text
tools/everything/es.exe
```

## Publish

Run the publish script:

```powershell
.\publish.ps1
```

Default publish settings:

- Configuration: `Release`
- Runtime: `win-x64`
- Self-contained: `true`
- Output: `publish/win-x64`

Manual publish command:

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 publish .\src\TtLauncher\TtLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\publish\win-x64
```

## Publish Checklist

Check these files before shipping:

```text
publish/win-x64/TtLauncher.exe
publish/win-x64/tools/everything/es.exe
publish/win-x64/tessdata/eng.traineddata
publish/win-x64/tessdata/chi_sim.traineddata
```

Basic verification:

1. Open the launcher.
2. Search apps.
3. Search files with `f ...`.
4. Run `ocr`.
5. Run `port 3000` and `ports`.
6. Check tray menu and startup toggle.

## Git

Suggested commands:

```powershell
git status
git add .
git commit -m "release: prepare beta 0.0.1"
git tag beta-0.0.1
```

Push if needed:

```powershell
git push
git push origin beta-0.0.1
```
