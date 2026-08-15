# XiangQiLink 象棋连线助手

基于 [VinXiangQi](https://github.com/Vincentzyx/VinXiangQi)（GPL-3.0）重构的中国象棋连线工具，使用 YOLOv5 深度学习识别棋盘与棋子，支持深色/浅色棋盘、自动走棋与观战分析。

## 功能特性

- 🎯 **自动全屏识别**：不需要匹配窗口/选择方案，程序直接盯整个屏幕，棋盘一出现就自动识别
- 🌗 **深色棋盘支持**：内置针对 JJ象棋 等深色棋盘优化的模型与低置信度识别（误检由棋盘校验拦截）
- 🤖 **自动走棋**：引擎（皮卡鱼）算最优步，自动点击走子，无限循环对局
- 👁 **观战模式**：只识别双方走子，在棋盘上画出引擎最优线路（箭头），不干扰鼠标
- 🔁 **自动续盘**：对局结束后自动点击"下一轮/再来一局"，新棋盘出现自动接管
- 📚 **开局库**：支持 .obk（兵河）格式开局库，自动加载，多个库可并存
- 🪟 **窗口模式**：也支持匹配指定窗口（雷电模拟器/天天象棋/JJ象棋 等内置方案）
- 📝 **过程日志**：识别/引擎/点击全过程写入日志，识别失败自动保存现场截图

## 快速开始

1. 从 [Releases](../../releases) 下载 Windows 版压缩包，解压后运行 `XiangQiLink.exe`
2. 方案选择"**自动全屏**"（默认）
3. 选择模式：
   - **自动走棋**：直接点「一键连线」，程序自动识别、计算、走子
   - **观战模式**：勾选「观战模式」（自动勾选前台鼠标、取消自动走棋）后点「一键连线」，程序显示最优线路但不走子
4. 下完一局自动续盘，无限循环

详细说明见 [使用说明.md](使用说明.md)。

## 从源码构建

环境：Windows + Visual Studio 2019+（.NET Framework 4.8），或 Linux + Mono（xbuild）

```bash
# Linux / Mono
xbuild VinXiangQi/VinXiangQi.csproj /p:Configuration=Release
```

注意：发布目录需要以下资源（见 RESOURCES.md）：
- `Models/` 三个 ONNX 模型（万能带旋转/中模型/小模型）
- `Engines/` 皮卡鱼引擎（pikafish-avx2.exe + pikafish.nnue）
- `OpenBooks/` 开局库（.obk）

## 目录结构

```
XiangQiLink/
├── src/VinXiangQi/     # C# 源码（WinForms）
├── LICENSE             # GPL-3.0
├── 使用说明.md          # 使用文档
└── RESOURCES.md        # 模型/引擎/开局库资源说明
```

## 致谢

- [VinXiangQi](https://github.com/Vincentzyx/VinXiangQi)：原版象棋连线工具（YOLOv5 识别 + 界面框架）
- [Pikafish](https://github.com/official-pikafish/Pikafish)：皮卡鱼象棋引擎
- 模型训练数据包含 JJ象棋/天天象棋/QQ象棋 等平台棋盘

## 协议

本项目基于 VinXiangQi 修改，遵循 **GPL-3.0** 协议开源，详见 [LICENSE](LICENSE)。
