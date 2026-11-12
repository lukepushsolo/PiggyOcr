# PiggyOcr

Windows 截图取字工具。按 `Win+Shift+O`，框选屏幕，文字进剪贴板，弹出可编辑结果窗。

- 本地离线 OCR，PP-OCRv6（tiny / small / medium 三档独立模型包）
- DirectML 自动加速（N 卡 / A 卡 / 核显），无 GPU 时回退 CPU
- 绿色便携，解压即用，无需安装 .NET、CUDA 或 VC++ 运行库
- 交互参考 PowerToys Color Picker：冻结帧蒙层、放大镜、按键可配
- 开机自启、每周检查更新、单实例、深色/浅色跟随系统

设计文档见 [DESIGN.md](DESIGN.md)。

## 下载与安装

1. 下载 **应用包**：`PiggyOcr-vX.Y.Z-app.zip`（约 90MB，解压后约 235MB，已含运行时）。
2. 下载 **一个模型包**（三选一）：
   - `PiggyOcr-models-tiny.zip` 5.5 MB：最快，不支持日文
   - `PiggyOcr-models-small.zip` 25.4 MB：平衡
   - `PiggyOcr-models-medium.zip` 100.5 MB：最准
3. 把两个 zip 解压到**同一个文件夹**，结构应为：

```
PiggyOcr\
  PiggyOcr.exe
  runtime files...
  models\
    tiny\   det.onnx  rec.onnx
```

4. 双击 `PiggyOcr.exe`。托盘出现后按 `Win+Shift+O` 即可取字。

系统要求：Windows 10 22H2 / Windows 11，x64。不需要管理员权限。
想换档位，再解压另一个模型包到同一目录，然后在托盘右键 → 设置里切换。

## 使用

| 操作 | 行为 |
|---|---|
| `Win+Shift+O` | 截图并识别（可在设置里改） |
| 左键拖选 | 识别 → 复制 → 打开结果窗 |
| 中键拖选 | 识别 → 复制 → 气泡提示 |
| 双击 | 全屏识别 |
| 右键 / Esc | 取消 |
| 滚轮 | 缩放放大镜 |
| 结果窗 `Ctrl+Enter` | 复制并关闭 |

## 已知冲突

Quicker、鼠标手势等工具如果把「长按中键」注册成全局手势，会先于本程序截走中键事件，中键拖选就会失效。改用左键拖选，或在第三方工具里换一个触发方式。

## 从源码构建

需要 .NET 10 SDK。

```powershell
dotnet build PiggyOcr.slnx -c Release
dotnet run --project tools/OcrCli -- <image.png> <modelsRoot> gpu tiny
```

打包（应用包 + 三个模型包）：

```powershell
powershell -File tools/fetch-models.ps1
powershell -File tools/package.ps1 -Version 0.1.0
```

产物在 `dist\`。

## 许可证

代码 GPL-3.0；模型为 Apache-2.0，详见 [NOTICE](NOTICE)。
