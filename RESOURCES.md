# 资源说明

本项目的识别模型、引擎与开局库为外部资源，**不包含在源码仓库中**（GitHub 单文件 100MB 限制与版权原因），请按以下方式获取：

## 模型（Models/）

| 文件 | 说明 |
|---|---|
| 中模型.onnx（28MB） | 推荐默认，深色棋盘精度高 |
| 万能带旋转.onnx（28MB） | 支持旋转棋盘 |
| 小模型.onnx（7.5MB） | 速度快，深色棋盘易误检 |

来源：VinXiangQi 发布包 `Models/` 目录（[VinXiangQi Releases](https://github.com/Vincentzyx/VinXiangQi/releases)）

## 引擎（Engines/）

- `pikafish-avx2.exe`（Windows）
- `pikafish.nnue`（神经网络权重）

来源：皮卡鱼官方发布 [Pikafish Releases](https://github.com/official-pikafish/Pikafish/releases)（Windows 目录，注意使用与 CPU 匹配的版本：avx2/bmi2/sse41-popcnt 等）

## 开局库（OpenBooks/）

- 支持 `.obk`（兵河）格式，放入 `OpenBooks/` 目录自动加载，可多个并存
- 常用库：象眼云库、徐风依旧（作者在相关象棋交流群发布）
- 程序设置里可开启云库（UseChessDB，在线开局库）

## 版权

- 模型版权归 VinXiangQi 项目
- 引擎版权归皮卡鱼开发团队（GPL-3.0）
- 开局库版权归各作者，请尊重原作者许可
