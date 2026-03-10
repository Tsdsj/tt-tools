# TtLauncher

> 🇨🇳 中文文档 | 🇬🇧 [English](./README.md)

TtLauncher 是一款运行于 Windows 的 WPF 个人效率启动器。

当前目标版本：`beta-0.0.1`

## 功能特性

- 应用搜索与启动
- Everything 文件搜索
- OCR 屏幕截图识别
- 端口占用查看
- 系统托盘菜单与开机自启切换
- 类 Raycast 风格的深色悬浮界面

## 环境要求

- Windows 10 或 Windows 11
- .NET 8 SDK（本地开发用）
- Tesseract OCR 语言数据文件

所需 OCR 数据文件：

```text
tessdata/eng.traineddata
tessdata/chi_sim.traineddata
```

## 开发

使用仓库内置的本地 SDK 运行项目：

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 run --project .\src\TtLauncher\TtLauncher.csproj
```

仅构建：

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 build .\src\TtLauncher\TtLauncher.csproj
```

## 命令说明

默认应用搜索：

```text
chrome
code
wechat
```

Everything 文件搜索：

```text
f readme
f resume
f config.json
```

OCR 识别：

```text
ocr
```

端口查看：

```text
port 3000
ports
```

## Everything

本项目支持内置的 `es.exe`。

源文件位置：

```text
src/TtLauncher/Assets/Tools/Everything/es.exe
```

发布后位置：

```text
tools/everything/es.exe
```

## 发布

运行发布脚本：

```powershell
.\publish.ps1
```

默认发布配置：

- 构建模式：`Release`
- 目标运行时：`win-x64`
- 自包含：`true`
- 输出目录：`publish/win-x64`

手动发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\dotnet-local.ps1 publish .\src\TtLauncher\TtLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\publish\win-x64
```

## 发布检查清单

发布前请确认以下文件存在：

```text
publish/win-x64/TtLauncher.exe
publish/win-x64/tools/everything/es.exe
publish/win-x64/tessdata/eng.traineddata
publish/win-x64/tessdata/chi_sim.traineddata
```

基本验证步骤：

1. 打开启动器。
2. 搜索应用程序。
3. 使用 `f ...` 搜索文件。
4. 运行 `ocr`。
5. 运行 `port 3000` 和 `ports`。
6. 检查托盘菜单与开机自启功能。

## Git

推荐命令：

```powershell
git status
git add .
git commit -m "release: prepare beta 0.0.1"
git tag beta-0.0.1
```

推送（如需）：

```powershell
git push
git push origin beta-0.0.1
```
