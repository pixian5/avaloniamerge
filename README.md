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
- `--method <name>` 目录文件比较方法：`exists`、`size`、`time`、`full`
- `--json` 输出 JSON
- `--out <path>` 输出到文件

CLI 返回码：`0` 表示完全一致，`1` 表示存在差异，`2` 表示运行失败。

## 测试

```bash
dotnet run --project /Users/x/code/avaloniamerge/AvaloniaMerge.Tests -c Release
```

## 说明

- 目录对比会包含子目录与空目录，并以目录树进行对比。
- 二进制文件会逐字节对比并支持早停。
- 发布默认采用框架依赖（运行时不打包进产物），需要安装 .NET 10 运行时。
