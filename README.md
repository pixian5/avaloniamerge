# AvaloniaMerge

AvaloniaMerge 是一套基于 Avalonia 的跨平台差异对比工具，提供 GUI 与 CLI 两种入口，支持 Windows、macOS 与 Linux。

## 构建

```bash
dotnet build /Users/x/code/avaloniamerge/AvaloniaMerge.sln
```

## 运行 GUI

```bash
dotnet run --project /Users/x/code/avaloniamerge/AvaloniaMerge.Gui
```

## 运行 CLI

```bash
dotnet run --project /Users/x/code/avaloniamerge/AvaloniaMerge.Cli -- <left> <right>
```

常用选项：

- `--file` 强制文件对比
- `--dir` 强制目录对比
- `--ignore-case` 忽略大小写
- `--json` 输出 JSON
- `--out <path>` 输出到文件

## 说明

- 目录对比会包含子目录与空目录，并以目录树进行对比。
- 二进制文件会以哈希进行差异判断。
