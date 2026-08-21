# 都都大冒险 (Dudu Adventure)

2D 像素风横版动作游戏，基于西游记故事改编。扮演齐天大圣孙悟空，挥舞金箍棒，重走取经之路。

## 平台

- 平板电脑（iPad / Android）
- Xbox（通过 UWP 导出）

## 技术栈

- Unity 6.5（Universal 2D / URP）
- C#
- Aseprite（像素美术）

> Unity 自 6.5 起已废弃 Built-In 渲染管线，本项目使用 URP。

## 项目结构

```
Assets/
├── Scripts/          # 游戏代码
│   ├── Core/         # 核心系统（GameManager、状态机）
│   ├── Player/       # 玩家控制（移动、战斗、状态）
│   ├── Enemy/        # 敌人 AI
│   ├── Combat/       # 战斗系统（血量、伤害）
│   ├── Camera/       # 相机跟随
│   ├── Level/        # 关卡管理
│   ├── UI/           # 用户界面
│   └── Audio/        # 音频管理
├── Sprites/          # 像素素材
├── Animations/       # 动画资源
├── Tilemaps/         # 地图瓦片
├── Prefabs/          # 预制体
├── Scenes/           # 场景文件
└── ScriptableObjects/# 数据配置
docs/
├── GDD.md            # 游戏设计文档
├── SETUP.md          # 项目创建指南
└── UNITY_MCP.md      # AI 助手接入 Unity 编辑器
```

## 快速开始

1. 安装 Unity Hub → Unity 6.5
2. 用 **Universal 2D** 模板创建项目
3. 详细步骤见 [docs/SETUP.md](docs/SETUP.md)
4. 接入 AI 辅助开发见 [docs/UNITY_MCP.md](docs/UNITY_MCP.md)
5. 游戏设计参考 [docs/GDD.md](docs/GDD.md)
