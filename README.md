# PiggyOcr

Windows 截图取字工具。按 `Win+Shift+O`，框选屏幕，文字进剪贴板，弹出可编辑结果窗。

- 本地离线 OCR，PP-OCRv6（tiny / small / medium 三档独立模型包）
- DirectML 自动加速（N 卡 / A 卡 / 核显），无 GPU 时回退 CPU
- 绿色便携，解压即用，无需安装 .NET、CUDA 或运行库
- 交互参考 PowerToys Color Picker：冻结帧蒙层、放大镜、按键可配

设计文档见 [DESIGN.md](DESIGN.md)。项目处于 M0 验证阶段。

## 许可证

GPL-3.0。模型为 Apache-2.0，详见发布包中的 `NOTICE`。
