# PiggyOcr 设计文档

Windows 截图取字工具。按 `Win+Shift+O`，框选屏幕，文字进剪贴板，弹出可编辑结果窗。
参考：SnapOcr（极简、exe 与运行时分离）、StarHuanOCR（GPU 自动探测、自启、更新）、PowerToys Color Picker（蒙层放大镜、按键语义、编辑器）。

状态：**待签字**。签字前不写实现代码。

---

## 1. 目标与非目标

### 目标
- 本地离线 OCR，PP-OCRv6 tiny / small / medium 三档可选，模型按档独立成包。
- 自动探测 DirectML，失败回退 CPU；无需安装 CUDA。
- 绿色便携：解压即用，不写注册表（自启除外）、不需要管理员。
- 冷启动快、常驻内存低、取字路径短。
- 现代 Windows UI（WinUI 3），交互对齐 PowerToys Color Picker。

### 非目标（v1 明确不做）
- Linux / macOS（核心层不依赖 Win32，留给以后）。
- 翻译、PDF、批量、表格结构化、公式识别。
- 历史落盘（仅内存 20 条）。
- 应用内下载模型、静默自更新、安装包、代码签名。
- 独占全屏 / DRM 内容截取。
- 深度学习模型的自定义训练。

---

## 2. 已定决策

| 项 | 决策 |
|---|---|
| 平台 | Windows 10 1903+ / Windows 11，x64 |
| 技术栈 | C# + .NET 10 LTS + WinUI 3（Windows App SDK 2.6.5） |
| 部署 | 未打包（unpackaged）+ 自包含（self-contained），用户零安装 |
| 推理 | ONNX Runtime DirectML 1.24.4，CPU 兜底；不引入 WinML（Win10 上与 ORT 等价） |
| 后处理 | 纯 C# 实现 DB 检测后处理 + CTC 解码，不用 OpenCV |
| 分发 | 绿色 zip：应用包（exe + runtime）+ 三个模型包 |
| 模型 | `models\tiny|small|medium`，用户只解压需要的档；程序自动选已安装的最高档 |
| 默认热键 | `Win+Shift+O`，可自定义 |
| 截屏 | 冻结帧（热键瞬间截整个虚拟桌面，蒙层显示位图） |
| 框选 | 左键拖选=识别+复制+编辑器；中键拖选=识别+复制+Toast；右键=取消；双击=全屏识别 |
| 结果窗 | 可编辑；未修改失焦关闭，已修改失焦不关（Esc 放弃、Ctrl+Enter 复制并关） |
| 文本排版 | 保留换行，同行多框合并；提供"合并为一段"开关 |
| 更新 | 每周检查 GitHub Release，仅提示并打开页面；3 秒超时，失败静默 |
| 自启 | HKCU Run 键，默认关 |
| 日志 | `PiggyOcr.log`，1MB × 3 轮转，不含截图与识别文本，默认开 |
| 许可证 | GPL-3.0 |
| 仓库 | 公开：`lukepushsolo/PiggyOcr` |

---

## 3. 产物与目录

用户解压后的目录：

```
PiggyOcr\
  PiggyOcr.exe
  runtime\
    onnxruntime.dll
    DirectML.dll
    vcruntime140.dll
    vcruntime140_1.dll
    msvcp140.dll
  models\
    tiny\    PP-OCRv6_det_tiny.onnx, PP-OCRv6_rec_tiny.onnx
    small\   PP-OCRv6_det_small.onnx, PP-OCRv6_rec_small.onnx
    medium\  PP-OCRv6_det_medium.onnx, PP-OCRv6_rec_medium.onnx
  PiggyOcr.json        （首次运行生成）
  PiggyOcr.log         （运行时生成）
```

发布资产（GitHub Releases）：

| 资产 | 内容 | 体积（zip / 解压） |
|---|---|---|
| `PiggyOcr-vX.Y.Z-app.zip` | exe + runtime（.NET/WinAppSDK 自包含 + ORT + VC++） | 90.3MB / 约 235MB |
| `PiggyOcr-models-tiny.zip` | tiny det+rec | 5.5MB / 6.0MB |
| `PiggyOcr-models-small.zip` | small det+rec | 25.4MB / 29.7MB |
| `PiggyOcr-models-medium.zip` | medium det+rec | 100.5MB / 132.4MB |

说明：自包含包的 exe 与运行时版本绑定，**更新整体替换应用包**，模型包不动。
不使用单文件发布（首次启动要解压，反而慢）。

---

## 4. 架构

```
PiggyOcr.sln
  src/
    PiggyOcr.Core/        纯 .NET，无 WinUI/Win32 依赖
      OcrEngine.cs        会话生命周期、预热、后端选择
      Backends/           OrtSessionFactory（DML / CPU）
      Preprocess/         缩放、归一化、张量布局
      Postprocess/        DB 二值化、连通域、最小外接矩形、unclip、透视裁剪
      Decode/             CTC 解码、字典
      Layout/             框排序、行合并、段落输出
      Models/             档位发现、文件校验、元数据读取
    PiggyOcr.Platform/    Windows 专用
      Capture/            冻结帧截屏（BitBlt）
      Overlay/            Win32 分层窗口、放大镜、框选
      Hotkey/             RegisterHotKey + 隐藏消息窗口
      Tray/               H.NotifyIcon.WinUI
      Startup/            HKCU Run
      Dxgi/               适配器枚举（Vortice.DXGI）
    PiggyOcr.App/         WinUI 3
      App.xaml / MainWindow
      Views/              ResultWindow, SettingsWindow, FirstRunWindow
      ViewModels/
      Services/           配置、日志、更新、剪贴板、单实例
  tests/
    PiggyOcr.Core.Tests/  单元测试 + 金样图回归
  tools/
    fetch-models.ps1      从 ModelScope 拉模型并校验 SHA256
    package.ps1           dotnet publish + 组装 zip
    make-models-zip.ps1
  docs/
```

依赖：`Microsoft.WindowsAppSDK 2.6.5`、`Microsoft.ML.OnnxRuntime.DirectML 1.24.4`、
`H.NotifyIcon.WinUI 2.4.1`、`Vortice.DXGI`、`Clipper2`（多边形外扩）、`CommunityToolkit.Mvvm`。
不引入 OpenCV。

---

## 5. 关键流程

### 5.1 取字
```
全局热键
  → 若模型仍在预热，先显示"正在加载模型"提示，加载完成后继续
  → BitBlt 虚拟桌面 → 冻结帧位图
  → 每显示器一个无边框分层窗口（物理像素坐标，混合 DPI 安全）
  → 放大镜跟随光标（滚轮缩放 8x→32x）
  → 拖拽框选；Esc / 右键取消；双击=全屏
  → 鼠标松开（按对应按键的动作）
     左键：识别 → 复制 → 打开结果窗
     中键：识别 → 复制 → Toast → 关闭
     其他：按设置
  → OCR（单工作线程，串行）
  → 结果窗（原图缩略图 + 可编辑文本 + 复制按钮 + 历史）
```

### 5.2 启动
```
单实例互斥体 → 读配置 → 初始化日志 → 托盘图标 → 注册热键
  → 后台线程：枚举 DXGI → 创建 ORT 会话（DirectML，失败回退 CPU）→ 预热一次空推理
  → 延迟 5 秒检查更新（每周一次，3 秒超时，失败静默）
```

### 5.3 单实例
命名互斥体 `PiggyOcr.SingleInstance`；第二个实例直接退出（v1 不转发取字请求）。

---

## 6. OCR 实现

### 6.1 后端选择
1. 枚举 DXGI 适配器，按专用显存降序；选第一个可用设备，`AppendExecutionProvider_DML(deviceId)`。
2. 创建会话或预热失败 → 回退 `CPUExecutionProvider`，日志记录原因。
3. 设置页显示当前后端与适配器名。

### 6.2 档位选择
- 用户锁定档位 → 用该档；文件缺失则提示。
- `auto` → 使用**已安装的最高档**（medium > small > tiny）。
- 缺模型时打开首次运行引导窗，列出三档体积、下载页链接、"打开模型目录"按钮；不自动下载。
- tiny 不支持日文，引导窗中注明。

### 6.3 预处理
- det：选区等比缩放后**居中贴到固定的 1920×1088 画布**（黑色填充），只产生一种输入形状。
  DirectML 按输入 shape 编译图，变化尺寸会让每次新选区多付约 300ms；固定画布消除这笔开销。
- rec：高固定 48，宽按比例后补齐到 `{320,640,960,1280,1600,2048}` 之一，右侧补零，限制 DML 形状数量。
- 归一化 0.5/0.5（已用金样图验证，与 RapidOCR 一致）。

### 6.4 检测后处理（纯 C#）
sigmoid → 阈值 0.3 → 连通域 → 轮廓 → 最小外接矩形（旋转卡壳）→
按 `面积×unclip_ratio/周长` 用 Clipper2 外扩 → 透视裁剪 → 过滤极小框。

### 6.5 识别解码
逐帧 argmax → 去 blank(0) → 去连续重复 → 查字典 → 置信度为有效帧最大概率均值。

### 6.6 排版
- 框按 y 中心排序；同一行（垂直重叠 > 50%）按 x 排序。
- 行内拼接：中文相邻不加空格，拉丁字母/数字相邻加一个空格。
- 行间 `\n`；"合并为一段"时用空格连接。

### 6.7 模型加载
- `ORT` 会话启动后常驻；切换档位需重建会话（设置中切换时提示 1–3 秒）。
- 预热在后台线程完成；预热期间热键进入等待态。
- 模型文件做存在性与大小校验；损坏时提示重新解压对应模型包。

---

## 7. UI / UX

### 7.1 蒙层（对齐 Color Picker）
- 全屏冻结画面，选区外 40% 黑遮罩，选区内原亮度。
- 光标旁 160×160 放大镜，默认 8x，滚轮 2x 步进，最大 32x；显示十字准星与坐标。
- 选区尺寸实时显示；提示文字："松开识别 · 右键取消 · 双击全屏"。
- 不抢焦点、不进 Alt+Tab、不出现在任务栏。

### 7.2 结果窗
- 顶部：标题、复制按钮（图标 + "复制"）、历史按钮。
- 中部：多行可编辑文本框；原图缩略图可折叠。
- 底部：字数、"合并为一段"开关、重新识别、关闭。
- 历史：内存环形缓冲 20 条，点击载入；关闭程序即清空。
- 快捷键：`Ctrl+Enter` 复制并关闭；`Esc` 关闭（已修改则放弃）；`Ctrl+A/C/V` 原生。
- 关闭策略：未修改 → 点窗外关闭；已修改 → 保持打开，Esc 放弃、`Ctrl+Enter` 保存到剪贴板并关闭。
- 位置：靠近选区，超出屏幕则夹取。

### 7.3 设置页（单页，改完即存，无"确定"）
| 分组 | 项 | 默认 |
|---|---|---|
| 快捷键 | 截图识别 | `Win+Shift+O` |
| 鼠标 | 左键 / 中键 / 右键 / 双击 | 编辑 / 复制并关 / 取消 / 全屏 |
| 识别 | 模型档位、当前后端、模型目录 | 自动 |
| 结果窗 | 自动复制、关闭策略、历史条数 | 开 / 智能 / 20 |
| 通用 | 开机自启、检查更新、写日志、版本、检查更新按钮 | 关 / 开 / 开 |

热键冲突时提示更换；裸键（单字母）警告会全局占用。
已知限制：前台程序以管理员运行时，普通权限的全局热键无效；HDR 屏颜色可能失真。

### 7.4 托盘
左键单击 = 开始框选；右键菜单 = 截图识别 / 设置 / 检查更新 / 打开模型目录 / 打开日志 / 退出。

### 7.5 首次运行
无可用模型 → 引导窗（三档体积、tiny 不含日文、下载页、打开模型目录）；有模型 → 直接进托盘。

---

## 8. 系统集成

- **自启**：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `PiggyOcr`，默认关；卸载/关闭时删除。
- **检查更新**：`GET api.github.com/repos/lukepushsolo/PiggyOcr/releases/latest`，带 User-Agent；
  与程序版本比较；3 秒超时；失败静默；每周最多一次；手动按钮直接打开 Release 页。
- **配置**：exe 同目录 `PiggyOcr.json`；不可写（如 Program Files）时回退 `%LOCALAPPDATA%\PiggyOcr\PiggyOcr.json`。
- **日志**：自研轻量 logger，同步追加，超 1MB 轮转，最多 3 个文件。
- **隐私**：除更新检查外零联网；不收集任何数据。

---

## 9. 构建与发布

- 目标框架：`net10.0-windows10.0.19041.0`。
- 发布参数：
  `-c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None`
- 运行库：`Microsoft.ML.OnnxRuntime.DirectML` 与 `Microsoft.AI.DirectML` 的 x64 DLL 复制到 `runtime\`；
  VC++ 运行库 app-local（`vcruntime140.dll`、`vcruntime140_1.dll`、`msvcp140.dll`）。
- 原生 DLL 通过 `NativeLibrary.SetDllImportResolver` 从 `runtime\` 加载。
- 模型下载脚本从 ModelScope 拉取并按 `default_models.yaml` 校验 SHA256。
- 打包脚本生成四个 zip，文件名含版本号；Release 说明写清"解压应用包 + 任选一档模型包"。

---

## 10. 测试与验收

### 自动化
- CTC 解码、DB 后处理、行合并的单元测试。
- 金样图回归：10–20 张截图，期望文本；CER 阈值验收。
- CPU / DirectML 输出一致性（同一图，文本差异在阈值内）。

### 手工
- 100% / 150% / 200% DPI；双屏、混合 DPI。
- Win10 22H2 与 Win11。
- 无模型、坏模型、无 GPU、热键冲突。
- 断网启动与更新检查。

### 性能目标（实测更新于 M2）
| 指标 | 目标 | 实测 |
|---|---|---|
| 热键 → 蒙层首帧 | < 120ms | 待测 |
| 松手 → GPU 出字（典型区域） | < 200ms | 208ms（tiny） |
| 松手 → CPU 出字（small） | < 2s | 待测 |
| 托盘出现 | < 1s | 约 0.5s |
| 空闲内存 | 目标 < 300MB（不含模型） | 待测 |

---

## 11. 里程碑

| 阶段 | 内容 | 出口条件 |
|---|---|---|
| M0 验证 | 装 .NET 10 SDK；WinUI 3 自包含未打包跑通；ORT DML 跑 PP-OCRv6 tiny；后处理最小闭环 | 同一张图能出正确文字 |
| M1 核心 | Core 完整 det/rec/排版 + 单元测试 + 命令行验证工具 | 金样图 CER 达标 |
| M2 交互 | 冻结帧、蒙层、放大镜、热键、按键动作、托盘 | 手动取字全流程可用 |
| M3 应用 | 结果窗、设置页、自启、更新、日志、配置 | 功能清单打勾 |
| M4 发布 | 打包脚本、四个 zip、仓库与 Release、README | 陌生机器解压即用 |

---

## 12. 风险与待验证

1. **归一化参数**（0.5/0.5 vs ImageNet）——M0 用金样图对照官方 PaddleOCR 输出确认。
2. **DB 后处理正确性**——参考 RapidOcrNet / SimdPaddleOCR（Apache-2.0）实现，金样图回归；必要时做 spike 先跑通再定。
3. **WinUI 3 蒙层**——分层窗口同进程混用可行，但混合 DPI 需实测；异常时退化为每显示器一个窗口。
4. **DirectML 兼容性**——旧驱动可能创建会话失败；回退 CPU 并提示更新显卡驱动。
5. **自包含体积/启动**——约 190–290MB，启动约 0.4s；若不可接受，唯一出路是 C++ 重写（已评估，不做）。
6. **GitHub 可达性**——国内可能超时；已设计为静默失败 + 手动打开页面。
7. **热键被占用 / 管理员窗口**——已知限制，设置页提示。

---

## 13. M0 验证结果（2026-11-12，本机）

| 验证项 | 结果 |
|---|---|
| .NET 10 SDK | 已安装 10.0.301；运行时 10.0.9 |
| WinUI 3 自包含未打包 | 构建成功、启动成功；输出 **233MB / 520 文件** |
| DirectML | `DmlExecutionProvider` 可用；tiny det/rec 均能加载推理 |
| 归一化 | 0.5/0.5 与 ImageNet 均可正确出字；**采用 0.5/0.5**（与 RapidOCR 一致） |
| 端到端 | 1200×400 样本图检出 3 行，文本正确 |
| 预热后耗时 | det 16–18ms；rec 13–63ms（DML，tiny） |
| 字典 | rec 模型内嵌 `character` 元数据（6904 项），无需外部字典 |

待验证：混合 DPI 多屏蒙层、旧驱动回退、small/medium 实际耗时、长时间运行稳定性。

### M2 结果（2026-11-12）

- 冻结帧截屏、Win32 分层蒙层、放大镜、拖选、双击全屏、Esc/右键取消全部可用。
- 全局热键 `Win+Shift+O`、托盘图标与右键菜单、单实例可用。
- 端到端自动化冒烟通过：热键 → 拖选 → DirectML OCR → 剪贴板，文本正确。
- 实测（tiny，1200×400 选区，RTX 4060 Ti）：端到端 **208ms**（det + 3 段 rec）。
- 发现并修复：Win10 自带 `DirectML.dll` 为 2021 年旧版，必须随包携带 NuGet 的 18.5MB x64 版本，
  否则 DML 设备创建失败并静默退回 CPU。

### M3 结果（2026-11-12）

- 设置页：快捷键录制、四个鼠标动作、模型档位、自动复制、开机自启、检查更新、打开模型目录。
- 配置存 `PiggyOcr.json`（exe 同目录，不可写时回退 `%LOCALAPPDATA%\PiggyOcr`），改完即存。
- 自启写 `HKCU\...\Run`；更新检查 3 秒超时、每周一次、气泡提示，失败静默。
- 首次运行无模型时弹出引导窗（三档体积、打开目录、下载页）。
- 结果窗历史保留 20 条（仅内存），托盘气泡代替自绘 Toast。
- 日志 1MB × 3 轮转，不含截图与识别文本。
- 快捷键被占用时气泡提示，可在设置里重录。

### M4 结果（2026-11-12）

- `tools/fetch-models.ps1`：三档模型下载 + SHA256 校验。
- `tools/package.ps1`：产出 app 包与三个模型包，应用包 **90.3MB**（解压 235MB），
  模型包 5.5 / 25.4 / 100.5MB。
- 修复发布版专属 bug：`dotnet publish` 不会复制 WinUI 编译出的 `.xbf` 与 `PiggyOcr.App.pri`，
  导致发布版第二个窗口在 `InitializeComponent` 崩溃。已在 `Directory.Build.targets` 的 Publish 后补拷贝。
- 解压 app + tiny 模型包到同一目录，端到端通过（热键、蒙层、DirectML、剪贴板、结果窗）；
  打包版设置窗口可正常打开。
- 待办：GitHub 仓库创建被权限策略拦截，尚未推送与发布；自定义图标；干净机器验证。

---

## 14. 许可证

- 代码：GPL-3.0。
- 模型：PP-OCRv6 与 RapidOCR 模型为 Apache-2.0，在 `NOTICE` 中说明。
- 参考实现（RapidOcrNet、SimdPaddleOCR 等）为 Apache-2.0，复用时保留版权声明。
