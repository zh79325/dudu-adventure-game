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

仓库根**不是** Unity 项目根，Unity 项目在 `DuduAdventure/` 子目录里。用 Unity Hub 打开时要选这个子目录。

```
dudu-adventure-game/              # 仓库根
├── DuduAdventure/                # ← Unity 项目根（Hub 里打开这个）
│   ├── Assets/
│   │   ├── Scripts/              # 游戏代码
│   │   │   ├── Core/             # 核心系统（GameManager、泛型状态机）
│   │   │   ├── Player/           # 玩家控制（移动、战斗、状态）
│   │   │   ├── Enemy/            # 敌人 AI
│   │   │   ├── Combat/           # 战斗系统（血量、伤害）
│   │   │   ├── Camera/           # 相机跟随
│   │   │   ├── Level/            # 关卡管理、检查点
│   │   │   ├── UI/               # 用户界面
│   │   │   └── Audio/            # 音频管理
│   │   ├── Sprites/              # 像素素材
│   │   ├── Animations/           # 动画资源
│   │   ├── Tilemaps/             # 地图瓦片
│   │   ├── Prefabs/              # 预制体
│   │   ├── Scenes/               # 场景文件
│   │   ├── ScriptableObjects/    # 数据配置
│   │   ├── Settings/             # URP 渲染管线配置（模板生成）
│   │   └── Plugins/              # 第三方依赖（NuGet 目录不进版本库）
│   ├── Packages/                 # 包清单 manifest.json
│   └── ProjectSettings/          # Tag/Layer、输入模式等
└── docs/
    ├── GDD.md                    # 游戏设计文档
    ├── SETUP.md                  # 项目创建与配置指南
    └── UNITY_MCP.md              # AI 助手接入 Unity 编辑器
```

## 快速开始

1. 安装 Unity Hub → Unity 6.5（模块需勾选目标平台的 Build Support）
2. Unity Hub → **Add** → **Add project from disk** → 选 `DuduAdventure/` 目录
3. 详细步骤与已完成的配置见 [docs/SETUP.md](docs/SETUP.md)
4. 接入 AI 辅助开发见 [docs/UNITY_MCP.md](docs/UNITY_MCP.md)
5. 游戏设计参考 [docs/GDD.md](docs/GDD.md)
