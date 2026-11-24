# Antigravity Token Orb (Antigravity Token 悬浮球)

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0284c7.svg)](https://github.com/shengzeweinisi-glitch/antigravity-token-orb)
[![Runtime](https://img.shields.io/badge/.NET%20Framework-4.x%20Built--in-6366f1.svg)](https://github.com/shengzeweinisi-glitch/antigravity-token-orb)
[![Release](https://img.shields.io/github/v/release/shengzeweinisi-glitch/antigravity-token-orb?color=38bdf8)](https://github.com/shengzeweinisi-glitch/antigravity-token-orb/releases)
[![License](https://img.shields.io/badge/license-MIT-10b981.svg)](LICENSE)

一款专为 Windows 打造的**零依赖、纯原生轻量化 Antigravity Token 实时监控悬浮球**。直接以只读方式增量监听本地 Antigravity 日志（`~/.gemini/antigravity/brain/*/.system_generated/logs/transcript.jsonl`）与会话标题标注，让你在桌面随时掌握 Token 消耗速度、累计用量、预估费用、缓存命中率与上下文窗口占用。

---

## 核心特性

- **双外观形态（免点开直看官方真实 5h + 周配额百分比）**：
  - **圆形悬浮球**：通过官方 `agy -p /usage` 命令后台异步同步**服务端真实 5 小时与 1 周配额百分比**（支持切换显示「已用 %」或「剩余 %」，无 `agy` CLI 时自动回退为本地 Token 估算）；外圈动态光环同步反映 5h 占比与预警状态。
  - **迷你胶囊**：横向胶囊同步展示 `实时速率 | 5h % + 进度条 | 周 % + 进度条`，适合贴边常驻。
- **官方配额监控卡片**：
  - 详情面板置顶展示 **Gemini 模型组** 与 **Claude / GPT 模型组** 的 5 小时 / 1 周真实占比、精确重置倒计时与一键手动刷新，支持「自动跟随当前模型 / 固定分组」切换。
- **4 套实时配色主题**：
  - `深空霓虹 (Cyber Neon)` · `翡翠矩阵 (Emerald Matrix)` · `落日琥珀 (Sunset Amber)` · `浅色磨砂 (Frost Light)`，一键即时切换。
- **实时消耗速度与趋势**：
  - 1 分钟滚动速率（`tok/min` & `tok/s`）、输出生成速率、5 分钟平均速率、最近一步增量。
  - 内置 **近 20 分钟每分钟消耗趋势图**（柱状 + 折线 + 峰值标记）。
- **多维度消耗与费用估算**：
  - 支持切换 **5小时 / 今日 / 近7天(周) / 当前会话 / 全部历史**，一键切换口径（**新增 `Input + Output`** / **全量含缓存 `Input + Output + Cache Read`**）。
  - 支持在设置中心自定义 **5 小时 / 周进度条参考配额上限**。
  - 实时统计 **Prompt 缓存命中率** 与 **当前活跃会话上下文窗口占用**。
  - 自定义 **输入 / 输出 / 缓存读取** 单价（支持 `Gemini Pro` / `Gemini Flash` / `Claude Sonnet` 一键预设），支持 **人民币 (`¥ CNY`) / 美元 (`$ USD`)** 与自定义汇率。
- **超限预警系统**：
  - 自定义 **上下文窗口阈值**（超限提醒 `/compact`）、**1 分钟速率阈值**、**今日新增总量阈值**。
  - 触发时提供三重提醒：悬浮球红光脉冲徽标 + 面板顶部预警条 + Windows 托盘气泡弹窗（带 3 分钟冷却防刷屏）。
- **轻量、隐私、免安装**：
  - 100% 本地只读解析，**零联网请求、零数据上传**。
  - 使用 Windows 系统内置 `.NET Framework 4.x` 编译，单文件绿色运行，无需安装 Python / Node / Electron。
  - 支持自由拖拽、**靠桌面右边缘 60fps 果冻 Q 弹磁吸自动缩成发光竖条（鼠标靠近弹性拉出）**、重复双击直接唤出置前、记忆位置、透明度调节、窗口置顶、系统托盘常驻与 **Windows 开机自动启动**。

---

## 快速开始

### 方式一：直接下载运行（推荐）
1. 前往本仓库的 **Releases** 页面下载 `AntigravityTokenOrb.exe`。
2. 双击直接运行即可（首次运行如遇 Windows SmartScreen 拦截，点击 **「更多信息」→「仍要运行」**）。

### 方式二：从源码一键编译
无需安装 Visual Studio 或任何第三方 SDK，只要是 Windows 10 / 11，在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

即可自动调用系统内置的 `.NET Framework 4.x` 编译器（`csc.exe`）生成独立的 `AntigravityTokenOrb.exe`。

---

## 交互操作指南

| 操作 | 效果 |
| :--- | :--- |
| **左键单击悬浮球** | 展开 / 收起详情数据面板 |
| **左键拖动悬浮球** | 自由移动位置，靠近屏幕边缘自动吸附并记忆 |
| **面板右上角 `⚙` / 右键菜单** | 打开设置中心（主题、单价、预警阈值、开机自启等） |
| **点击面板中的任意会话** | 切入查看该会话的详细统计与上下文占用 |
| **双击系统托盘图标** | 唤出或置前悬浮球 |

---

## 数据来源与隐私说明

- 程序仅以只读共享模式（`FileShare.ReadWrite`）读取当前用户目录下的：
  - `%USERPROFILE%\.gemini\antigravity\brain\*\.system_generated\logs\transcript.jsonl`（仅提取 `step_index`、`created_at`、`model`、`input_tokens`、`output_tokens`、`cache_read_tokens` 顶层元数据字段，不读取对话正文）
  - `%USERPROFILE%\.gemini\antigravity\annotations\*.pbtxt`（仅提取会话标题 `title`）
- 官方配额百分比通过调用本机已登录的官方 CLI 命令 `agy -p /usage` 获取（不消耗 LLM Token，不接触账号凭据；若本机未安装 `agy` CLI 则自动纯离线运行）。
- 用户个性化配置仅保存在本地 `%LOCALAPPDATA%\AntigravityTokenOrb\settings.json`，程序本身不包含任何第三方遥测或数据上报代码。

---

## 项目结构

```text
antigravity-token-orb/
├── src/
│   ├── Program.cs              # 程序入口、DPI 适配、单实例锁
│   ├── Models.cs               # 数据结构、格式化、费用估算与预警评估
│   ├── JsonLineParser.cs       # 轻量流式顶层 JSONL 解析与 .pbtxt 标题解码
│   ├── QuotaSyncEngine.cs      # 官方 `agy -p /usage` 异步实时配额同步引擎
│   ├── TokenMonitorEngine.cs   # 增量日志监听、滚动速率与 20 分钟趋势聚合
│   ├── ThemeManager.cs         # 4 套明暗配色主题定义
│   ├── SettingsManager.cs      # 配置持久化与 Windows 开机自启注册表管理
│   ├── OrbWindow.cs            # 圆形悬浮球与迷你胶囊 WPF 窗口
│   ├── DashboardWindow.cs      # 可展开详情面板与趋势图画布
│   ├── SettingsWindow.cs       # 统一设置中心窗口
│   └── TrayManager.cs          # 系统托盘图标、菜单与预警气泡提醒
├── orb.ico                     # 自定义霓虹球图标
├── build.ps1                   # Windows 零依赖一键编译脚本
├── start.bat                   # 双击启动脚本
├── .github/workflows/release.yml # GitHub Actions 自动编译与发布工作流
└── LICENSE                     # MIT 开源协议
```

## License

本项目采用 [MIT License](LICENSE) 开源。
