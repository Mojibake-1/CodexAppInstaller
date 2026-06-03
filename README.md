# Codex 安装程序

一个 Windows 11 风格的现代化 Codex 安装器。双击 **`CodexAppInstaller.exe`** 即可运行。

它会从 Microsoft Store 下载最新的 Codex MSIX，校验哈希后解压，并把应用文件复制到你选择的目录——
全程只是包装同目录下的后端脚本 `Install-CodexApp.ps1`。

## 使用方法

1. 选择（或保留默认）安装位置。默认是 `%LOCALAPPDATA%\Programs\Codex`。
2. *（可选）* 点 **检查最新版本**，在不安装的情况下解析并显示当前 Store 版本。
3. 点 **安装** 开始下载、校验、解压并安装。展开 **详细信息** 可看到实时日志；底栏显示
   步骤、百分比和进度条。运行中 **安装** 会原地变为 **取消**。
4. 安装成功后会出现 **打开文件夹**。

> 更新前请先退出正在运行的 Codex。

## 现代化亮点

- **Windows 11 原生观感**——Mica 半透明背景、跟随系统的强调色、圆角、自绘标题栏、Segoe Fluent
  图标。跟随系统 **浅色/深色** 主题并即时切换。
- **真实进度**——把后端的文本进度条解析并重绘为动画进度条、步骤指示（`n/5`）和百分比。
- **内置终端**——语法着色的控制台（默认折叠）展示实时输出，不打扰静态界面。
- 克制而流畅的动效（尊重「减弱动态效果」设置）。

界面**不改动任何安装逻辑**——只负责运行 `Install-CodexApp.ps1`、渲染其输出、以及取消任务。脚本优先
使用磁盘上同目录的副本（便于审计和替换），同时也内嵌在 exe 中作为兜底。

## 兼容性 / 低配机适配

| 环境 | 行为 |
|------|------|
| **Windows 11**（22000+） | 完整效果：Mica 背景、窗口圆角、沉浸式深色标题栏。 |
| **Windows 10** | 自动降级为**纯色画布 + 直角窗口**（Mica / 圆角 API 不可用时静默跳过），其余功能一致。 |
| **低配 / 软件渲染**（无独显、虚拟机、远程桌面 RDP） | 进入**精简模式**：关闭昂贵的模糊投影（改用描边区分卡片）、关闭持续动画（微光 / 脉冲），状态即时切换，避免占用 CPU。 |
| **开启了「减弱动态效果」** | 关闭所有动画，仅保留终态。 |

判定依据：`RenderCapability.Tier`（软件渲染为 0）与 `SystemParameters.ClientAreaAnimation`。可用环境变量
手动验证降级路径：

```powershell
$env:CODEX_FORCE_WIN10 = "1"      # 强制走 Windows 10 路径（纯色 + 直角）
$env:CODEX_INSTALLER_LITE = "1"   # 强制精简模式（无投影、无动画）
```

## 从源码构建

无需 .NET SDK——使用系统自带的 .NET Framework C# 编译器即可。

```powershell
.\build.ps1            # 生成 CodexAppInstaller.exe
.\build.ps1 -SelfTest  # 构建后做一次无窗口的构造自检
.\build.ps1 -Run       # 构建后直接启动
```

本应用是**完全用 C# 代码后置（无 XAML、无 NuGet）编写的 WPF**，由 `csc.exe` 编译
（`/codepage:65001`，引用系统自带的 WPF 程序集）。源码位于 `src\`：

| 文件 | 职责 |
|------|------|
| `Program.cs`      | 入口；`--self-test` 与 `--render <png> [dark\|light] [demo]` 模式 |
| `MainWindow.cs`   | 窗口布局、状态机、后端联动、输出解析→界面、动效 |
| `Backend.cs`      | 运行 `powershell.exe`，逐字符读取 stdout/stderr（捕获 `\r` 进度回刷） |
| `OutputParser.cs` | 解析后端输出行（步骤 / 百分比 / 文件 / 大小 / 成功 / 错误） |
| `Interop.cs`      | DWM Mica、圆角、系统强调色、浅/深色检测与监听、`Perf` 能力检测 |
| `Theme.cs`        | 深色 + 浅色调色板、字体、图标字形、步骤名中文映射 |
| `Ui.cs` / `Controls.cs` | 可复用控件：卡片、按钮、开关、步骤点、控制台 |
| `Motion.cs`       | 动画助手（精简模式下整体禁用） |
| `Folder.cs`       | 现代 `IFileOpenDialog` 文件夹选择器 |

`--render` 会把界面离屏渲染成 PNG，便于在不弹窗的情况下做视觉验收。

## 说明

- 本工具会运行一个未签名的 PowerShell 脚本，并非微软官方安装器。
- 仅按用户安装到个人目录，无需管理员权限。
- 旧的 WinForms 版本保留为 `CodexAppInstaller.winforms.bak.exe`，其源码在 `legacy\` 下。
