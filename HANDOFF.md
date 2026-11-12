# PiggyOcr 交接说明

更新时间：2026-11-12
当前版本：0.1.0（未发布）

## 一句话状态

功能完成、构建与自动化测试通过、发布包已用最新代码生成。
**唯一阻塞**：创建公开仓库并推送 / 发 Release，需要用户明确授权（见 AGENTS 硬红线）。

## 当前事实

- 20 个提交；打包/文档改动均已提交。
- 构建：`dotnet build PiggyOcr.slnx -c Release` 成功。
- 测试：`dotnet test tests/PiggyOcr.Core.Tests -c Release` → 6/6。
- 发布包（`dist\`，22:42 重新打包）：

| 文件 | 大小 |
|---|---|
| `PiggyOcr-v0.1.0-app.zip` | 76.1MB（完整包，首次安装） |
| `PiggyOcr-v0.1.0-update.zip` | 0.8MB（更新包，仅程序层） |
| `PiggyOcr-models-tiny.zip` | 5.5MB |
| `PiggyOcr-models-small.zip` | 25.4MB |
| `PiggyOcr-models-medium.zip` | 100.5MB |

- 应用包只保留 `zh-CN` / `en-us` 语言资源，并改引 `Microsoft.WindowsAppSDK.WinUI`
  单独组件；剔除未使用的 AI/Search/Widgets/ML、DWriteCore、WebView2、Phone、
  Designer 与调试符号。文件数 542 → 300，解压 235.1 → 196.0MB。
- 更新包不含运行时、模型和 `PiggyOcr.json`，覆盖安装不会动用户配置与模型。
  若某版本升级了 .NET / Windows App SDK / ONNX Runtime，必须发完整包。
- 打包版已实测：左键取字、中键复制、右键取消、结果窗置顶、点窗外关闭；
  完整包启动、更新包覆盖后取字均通过；精简组件后取字、设置页、结果窗、
  首次运行引导与中文渲染均通过。
- 首次运行引导、设置页快捷键录制已用自动化/截图验证。

## 下一步（按顺序）

1. **发布授权**：用户明确回复「授权创建公开仓库 `lukepushsolo/PiggyOcr` 并上传 v0.1.0 Release」。
   - `gh repo create lukepushsolo/PiggyOcr --public --source . --remote origin --push`
   - 创建 `v0.1.0` Release，上传 `dist\` 五个 zip（app / update / 三档模型）。
   - 未授权前不要推送 / tag / 发 Release。
2. **人工 QA**：按 `docs/TESTING.md` 过一遍，重点：
   - 干净 Win10（无 .NET / VC++ / CUDA）解压运行；
   - 多屏 + 混合 DPI；
   - 托盘菜单、开机自启、检查更新。
3. 可选：README 截图、CI release workflow、exe 签名。

## 关键决策（已签字，勿重开）

详见 `DESIGN.md`。摘要：

- C# / .NET 10 + WinUI 3，自包含、未打包、绿色便携。
- ONNX Runtime DirectML 1.24.4，失败回退 CPU；不用 WinML、不用 OpenCV。
- 纯 C# DB / CTC 后处理。
- 三档模型独立 zip：`models/tiny|small|medium`，自动选已安装的最高档。
- 全局热键 `Win+Shift+O`；冻结帧蒙层 + 放大镜；左=编辑、中=复制关闭、右=取消、双击=全屏。
- 结果窗：未修改失焦关闭，已修改保持；历史 20 条仅内存。
- GPL-3.0；配置写 exe 旁 `PiggyOcr.json`。

## 已修复的坑（不要回退）

1. **DirectML.dll**：系统自带版本过旧，必须 app-local 复制 NuGet 版（`Directory.Build.targets`）。
2. **WinUI publish 漏 `.xbf` / `.pri`**：第二个窗口 `InitializeComponent` 崩（`Directory.Build.targets`）。
3. **原版 Win10 无 VC++ 运行时**：app-local 部署 7 个 DLL（`Directory.Build.targets`）。
4. **DML 按输入 shape 编译**：det 固定 1920×1088 画布 + rec 宽度分桶，端到端 399ms → 208ms。
5. **TextBox 规范化换行**：失焦关闭策略改用 `_modified` 标志，不比较文本。
6. **WinUI `Activate()` 被前台锁挡住**：`WindowActivation.ForceForeground`（`SetForegroundWindow` + `AttachThreadInput` 回退）。
7. **空 `models` 目录遮蔽 `PIGGYOCR_MODELS`**：改为 `ModelSet.Discover` 判断。
8. **Quicker 长按鼠标键抢事件**：已知冲突，文档说明，不改代码绕过全局钩子。

## 文件地图

```
src/PiggyOcr.Core/          纯 .NET：OcrEngine、预处理、DB 后处理、CTC、排版
src/PiggyOcr.Platform/      Win32：截屏、分层蒙层、托盘/热键、窗口前台
src/PiggyOcr.App/           WinUI 3：生命周期、设置窗、结果窗、首次引导
  Services/                 AppConfig、HotkeyParser、UpdateService
tests/PiggyOcr.Core.Tests/  xUnit，只测 Core 纯逻辑
tools/OcrCli/               单图调试
tools/e2e-smoke.ps1         左/中/右键端到端
tools/e2e-settings.ps1      快捷键录制（UIA）
tools/e2e-result-window.ps1 结果窗置顶 + 失焦关闭（UIA）
tools/visual-check.ps1      DPI 感知截图（蒙层/设置/结果）
tools/fetch-models.ps1      三档模型下载 + SHA256
tools/package.ps1           完整包 + 更新包 + 三个模型包
docs/TESTING.md             发布前人工清单
```

## 约束提醒

- `App.xaml.cs` 已 570 行，**不要再加功能**；新逻辑放 `Services/` 或新窗口文件。
- Core 禁止 Windows / WinUI / `System.Drawing` 依赖。
- 单次提交只做一件事，Conventional Commits。
- 模型/运行时二进制不入库。
- 发布相关动作必须先获得用户明确授权。
