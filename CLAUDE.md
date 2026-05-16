# CLAUDE.md — CodexBar for Windows

本文档为 AI 助手（Claude Code 等）提供关于本仓库结构、开发约定和工作流的必要背景，以便在本项目中有效协作。

---

## 项目概述

**CodexBar for Windows**（当前版本：`v0.3.5`）是 macOS 项目 [`lizhelang/codexbar`](https://github.com/lizhelang/codexbar) 的 Windows 原生移植版。

核心定位：一个 Windows 系统托盘工具，让用户在不拆分本地 `~/.codex` 历史池的前提下，管理并快速切换多个 OpenAI 官方账号和第三方兼容 API。

---

## 绝对兼容性约束（不可违反）

这些规则是本项目最重要的行为边界，**任何改动都不能违反**：

1. **不拆分共享 `CODEX_HOME` / `~/.codex` 历史池**
2. **切换账号只更新 `config.toml` 和 `auth.json` 中的激活态**，不复制、不重写、不迁移 `sessions` / `archived_sessions`
3. **账号切换只影响新会话**，历史会话不受影响
4. **OpenAI OAuth 必须保留**：浏览器授权 + localhost 回调捕获 + 手工粘贴 fallback 三条路径

---

## 项目结构

```
CodexBar-win/
├── src/
│   ├── CodexBar.Core/          # 核心数据模型、接口、路径解析、配置存储
│   ├── CodexBar.CodexCompat/   # Codex 文件兼容层（TOML/auth.json 读写、事务、激活）
│   ├── CodexBar.Auth/          # OAuth 客户端、PKCE、loopback 服务器、workspace 发现、使用量
│   ├── CodexBar.Runtime/       # Codex 进程管理、路径探测、更新服务、单实例管理
│   ├── CodexBar.Api/           # 本地 ASP.NET Core HTTP API（仅监听 127.0.0.1:5057）
│   ├── CodexBar.Win/           # WPF 主应用：托盘、主浮窗、小浮窗、设置窗口等
│   ├── CodexBar.Cli/           # 命令行工具（调试/诊断用）
│   └── CodexBar.Updater/       # 独立更新助手进程（主进程退出后执行替换）
├── tests/
│   └── CodexBar.Tests/         # 回归测试（无外部测试框架，自包含断言）
├── frontend-rebuild/           # 前端原型（Vite + TypeScript），非当前主 UI
├── docs/                       # 项目文档：实现进度、工作流、原生窗口说明
├── Directory.Build.props       # 全局版本号（Version 字段）
├── CodexBar.Win.sln            # 解决方案文件
├── build.ps1                   # 构建脚本（使用本地 .dotnet）
├── test.ps1                    # 构建并运行测试
├── run-win.ps1                 # 启动主 WPF 程序
├── run-cli.ps1                 # 运行 CLI
├── run-api.ps1                 # 仅启动本地 API
├── package.ps1                 # 生成便携 zip 包
└── NuGet.Config                # NuGet 源配置
```

### 各模块职责

| 模块 | 职责 |
|------|------|
| `CodexBar.Core` | 所有跨层共用的数据模型（`Models.cs`）、接口（`ISecretStore`、`IOAuthTokenStore`）、路径（`AppPaths`，存于 `%USERPROFILE%\.codexbar`）、应用配置存储 |
| `CodexBar.CodexCompat` | 读写 Codex 的 `config.toml`（轻量 TOML，保留无关内容）和 `auth.json`；执行激活事务（含回滚）；会话归档服务 |
| `CodexBar.Auth` | PKCE OAuth 流程、loopback 回调服务器、手工 fallback 解析、workspace 发现、官方使用量刷新、Windows Credential Manager 密钥存储 |
| `CodexBar.Runtime` | Codex Desktop / CLI 路径探测（MSIX 优先）、进程启动与清理、环境变量注入、单实例 + 命令转发、自动更新检查 |
| `CodexBar.Api` | ASP.NET Core minimal API，端口 5057，供前端重建或调试调用；CORS 只允许受信任的 loopback origin |
| `CodexBar.Win` | WPF 宿主：系统托盘 + `FlyoutWindow`（主浮窗）+ `OverlayWindow`（小浮窗）+ `SettingsWindow` + OAuth/兼容 Provider/编辑账号对话框 |
| `CodexBar.Cli` | PowerShell 脚本可调用的诊断命令（`config show`、`scan-accounts`、`locate-codex` 等）|
| `CodexBar.Updater` | 独立二进制，等主进程退出后备份程序目录、解压新版、替换、重启 |

---

## 技术栈

- **运行时**：.NET 8 (net8.0-windows)
- **UI 框架**：WPF + Windows Forms（托盘使用 WinForms `NotifyIcon`）
- **本地 API**：ASP.NET Core Minimal API
- **语言**：C# 13（LangVersion: latest），全项目启用 Nullable + ImplicitUsings
- **密钥存储**：Windows Credential Manager（`CodexBar.Auth/WindowsCredentialSecretStore.cs`）
- **配置格式**：CodexBar 自身配置为 JSON（`%USERPROFILE%\.codexbar\config.json`）；Codex 配置为 TOML（`~/.codex/config.toml`）+ JSON（`~/.codex/auth.json`）

---

## 关键路径与存储位置

| 路径 | 用途 |
|------|------|
| `%USERPROFILE%\.codexbar\` | CodexBar 专属数据根目录 |
| `%USERPROFILE%\.codexbar\config.json` | CodexBar 应用配置（账号列表、设置、激活状态） |
| `%USERPROFILE%\.codexbar\switch-journal.jsonl` | 切换日志（用于 usage 归因） |
| `%USERPROFILE%\.codexbar\logs\` | 诊断日志 |
| `CODEX_HOME` 或 `~/.codex\` | Codex 共享数据目录（**只读写激活态，不拆分**） |
| `~/.codex\config.toml` | Codex provider/model 配置（切换时原子更新） |
| `~/.codex\auth.json` | Codex OAuth token（切换时原子更新，必须包含顶层 `last_refresh`） |
| `~/.codex\sessions\` | 共享会话历史（只读扫描，绝不写入） |
| `~/.codex\archived_sessions\` | 归档会话（只读扫描，绝不写入） |

---

## 本地 API 端点（127.0.0.1:5057）

仅限受信任 loopback origin 访问（`http://127.0.0.1:5057`、`http://localhost:5057`、以及前端重建开发端口 `5173`/`4173`）：

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | `/api/dashboard` | 账号列表、usage 摘要、激活状态 |
| GET | `/api/settings` | 应用设置 |
| POST | `/api/settings/save` | 保存设置 |
| POST | `/api/accounts/activate` | 切换激活账号 |
| POST | `/api/accounts/launch` | 切换并启动 Codex |
| POST | `/api/accounts/probe` | 探测兼容 Provider 连通性 |
| POST | `/api/accounts/edit` | 编辑账号信息 |
| DELETE | `/api/accounts/{providerId}/{accountId}` | 删除账号 |
| POST | `/api/accounts/reorder` | 更新账号手动排序 |
| POST | `/api/providers/compatible` | 添加兼容 Provider |
| GET | `/api/oauth/state` | OAuth 当前状态 |
| POST | `/api/oauth/open-browser` | 打开浏览器开始 OAuth |
| POST | `/api/oauth/complete` | 完成 OAuth 并保存账号 |
| GET | `/api/history/export` | 导出历史会话 ZIP |
| POST | `/api/history/import` | 导入历史会话 ZIP |

---

## 开发工作流

### 前置条件

- Windows 环境（WPF 只能在 Windows 上编译和运行）
- 项目自带本地 `.dotnet` 运行时，不依赖全局 .NET 安装

### 常用命令（使用本地 .dotnet，通过 PowerShell 脚本）

```powershell
# 构建整个解决方案
.\build.ps1

# 构建并运行所有测试
.\test.ps1

# 启动主 WPF 程序（托盘模式）
.\run-win.ps1

# 直接打开设置窗口
.\run-win.ps1 --settings

# 运行 CLI 诊断命令
.\run-cli.ps1 config show
.\run-cli.ps1 scan-accounts
.\run-cli.ps1 locate-codex
.\run-cli.ps1 resolve-openai
.\run-cli.ps1 refresh-openai-usage

# 生成便携 zip 包（含本地 .dotnet）
.\package.ps1
```

### 直接使用 dotnet（需全局 .NET 8 SDK）

```powershell
# 构建
dotnet build .\CodexBar.Win.sln

# 运行测试
.\.dotnet\dotnet.exe .\tests\CodexBar.Tests\bin\Debug\net8.0-windows\CodexBar.Tests.dll

# 运行主程序
dotnet run --project .\src\CodexBar.Win\CodexBar.Win.csproj
```

---

## 测试约定

- 测试位于 `tests/CodexBar.Tests/`，无外部测试框架（xUnit/NUnit）
- 测试通过自包含断言类（`ApiRegressionAssertions`）执行，失败时抛异常
- 当前通过：**84 个测试**（build 0 error 0 warning）
- 测试覆盖核心回归场景（见 `docs/IMPLEMENTATION_PROGRESS.md` 详细列表）

**测试规则：**
- 改动后必须运行相关测试，未跑须说明原因
- 测试失败不得模糊汇报，必须明确说明
- 新增功能建议同步添加回归测试

---

## 版本管理与文档同步规则

**每次有意义的改动后，必须检查并同步更新以下四个文件：**

| 文件 | 内容 |
|------|------|
| `Directory.Build.props` | 全局版本号（`<Version>` 字段） |
| `README.md` | 面向用户的功能说明，末尾版本更新摘要 |
| `CHANGELOG.md` | 详细变更记录 |
| `docs/IMPLEMENTATION_PROGRESS.md` | 功能状态账本（`[x]` / `[~]` / `[ ]` / `[!]`） |

---

## 数据模型关键类型（`CodexBar.Core/Models.cs`）

- `AppConfig` — 整体应用配置（提供商列表、账号列表、设置、激活选择）
- `AccountRecord` — 单个账号记录（支持 OpenAI OAuth 账号和兼容 Provider 账号）
- `ProviderDefinition` — Provider 定义（Kind: `OpenAiOAuth` / `OpenAiCompatible`）
- `AppSettings` — 应用行为设置（排序模式、激活行为、启动偏好等）
- `OAuthTokens` — OAuth token 结构（写入 auth.json 时必须包含顶层 `last_refresh`）
- `CodexHomeState` — Codex 主目录状态（路径、是否通过环境变量覆盖）
- `UsageDashboard` / `AccountUsageSummary` — 本地 usage 扫描结果

---

## 架构要点与常见陷阱

1. **TOML 写入须保留无关内容**：`CodexBar.CodexCompat` 的 TOML 编辑是轻量字符串操作，不能用完整序列化替换（会丢失 Codex 自身的其他配置）。

2. **auth.json 必须包含顶层 `last_refresh`**：Codex 读取 auth.json 时依赖此字段，缺失会导致会话验证失败。

3. **兼容 Provider 激活时须保留 OAuth 身份快照**：切换到第三方 API 时，应尽量保留现有 OpenAI OAuth identity，以维持 Codex Desktop 历史会话可见性。

4. **兼容 Provider 的 Codex facing provider-id**：默认映射到 `openai`（通过 `openai_base_url`），不能覆盖 `[model_providers.openai]` 保留内置。

5. **启动 Codex Desktop 前须清理 .NET 环境变量**：便携包场景下，不能将 `.dotnet` 路径污染到子进程（`CodexLaunchEnvironmentBuilder` 负责清理）。

6. **单实例命令转发**：第二次启动不创建新实例，而是将 `--open` / `--overlay` / `--settings` 参数转发给已运行的主实例。

7. **本地 API 的 CORS 边界**：只信任 `http://127.0.0.1:5057`、`http://localhost:5057`、以及 `5173`/`4173` 端口（前端重建开发/预览）；任意外部页面均被拒绝。

8. **使用量扫描必须只读**：`UsageScanner` 以共享读模式打开 session 文件，跳过无法访问的活跃文件，绝不写入。

9. **Updater 危险目录拒绝**：`CodexBar.Updater` 应拒绝 Windows 系统目录和 `.codex` 历史目录作为目标，这个检查在 helper 侧独立实现，不依赖主进程。

---

## 线程分工（AI 协作约定）

参照 `docs/THREAD_WORKFLOW.md`：

- **main thread**：管路线、版本、审查和发布决策；不直接写业务代码
- **feature thread**：围绕单个功能实现、测试、验证，完成后输出标准交接信息

**推送规则**：只有经 main thread 明确允许后才推送正式仓库；feature thread 完成开发后默认只提交交接信息。

---

## 发布前检查清单

- [ ] `Directory.Build.props` 版本号正确
- [ ] `README.md` 末尾版本摘要已更新
- [ ] `CHANGELOG.md` 已覆盖本次发布内容
- [ ] `docs/IMPLEMENTATION_PROGRESS.md` 与现状一致
- [ ] 构建 0 error 0 warning
- [ ] 84 个回归测试全部通过（或新测试已添加）
- [ ] 高风险功能已手动验证
- [ ] 无临时实现或调试代码遗留
