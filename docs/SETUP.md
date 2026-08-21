# 都都大冒险 - Unity 6.5 项目创建指南

> 本文档基于 **Unity 6.5** 编写。Unity 6.5 与旧版 Unity（2021/2022 LTS）在渲染管线和物理 API 上有重要差异，网上大量老教程会误导你，请以本文档为准。

## 重要背景：为什么用 URP 而不是 Built-In

Unity 在 **6.5 版本正式启动了 Built-In 渲染管线的废弃流程**，官方明确表示「强烈不建议任何新项目使用」。虽然它至少会保留到 6.7 LTS，但新项目应该直接用 **URP（Universal Render Pipeline）**。

URP 对 2D 的支持很完善（2D 光照、Sprite 遮罩、后处理都比 Built-In 强），并且 Asset Store 素材正在全面转向 URP 兼容。所以我们从一开始就用 URP，避免以后痛苦迁移。

---

## 第一步：确认 Unity 6.5 安装模块

打开 Unity Hub → **Installs**，找到你的 Unity 6.5，点右侧齿轮 → **Add modules**，确认已勾选：

| 模块 | 用途 | 必需性 |
|------|------|--------|
| Universal Windows Platform Build Support | Xbox 导出（Dev Mode 侧载） | Xbox 必需 |
| Android Build Support（含 SDK/NDK/JDK） | 安卓平板导出 | 平板必需 |
| iOS Build Support | iPad 导出（仅 macOS 可用） | iPad 必需 |
| Documentation | 离线文档 | 建议 |

> 你在 macOS 上开发，**导出 Xbox 的 UWP 包必须在 Windows 机器上完成**。Mac 上可以先专注平板和编辑器内开发，Xbox 打包后期再处理。

---

## 第二步：项目布局（Unity 项目在 `DuduAdventure/` 子目录）

**项目已经建好了**，用 Unity Hub 的 **Add → Add project from disk** 指向下面这个目录即可打开：

```
/Users/eleme/code/github/dudu-adventure-game/DuduAdventure
```

实际结构：

```
dudu-adventure-game/              ← Git 仓库根
├── .git/
├── .gitignore
├── README.md
├── docs/                         ← GDD / SETUP / UNITY_MCP
└── DuduAdventure/                ← 这才是 Unity 项目根
    ├── Assets/
    │   ├── Scripts/              ← 10 个骨架脚本在这里
    │   ├── Sprites/ Prefabs/ Animations/ Tilemaps/ ...
    │   ├── Scenes/               ← SampleScene
    │   ├── Settings/             ← URP 渲染管线配置（模板生成）
    │   └── Plugins/NuGet/        ← MCP 插件依赖，已 gitignore
    ├── Packages/
    ├── ProjectSettings/
    ├── UserSettings/             ← 已 gitignore（内含 MCP token）
    └── Library/                  ← 已 gitignore
```

> **为什么不是「仓库根 = Unity 项目」**：本文档早期版本推荐过那种布局，但实际操作中 Unity Hub 建项目时生成的是子目录，两种结构并存过一段时间，结果出现了**两份 `Assets/Scripts`**——改了仓库根那份，Unity 完全不认，白改。现在已统一为子目录布局，仓库根不再有 `Assets/`。

> **注意**：Unity-MCP 插件要求**项目路径不能包含空格**。上面这个路径没有空格，符合要求。

---

## 第三步：确认骨架代码被识别

脚本已经在 `DuduAdventure/Assets/Scripts/` 下了，**不需要再复制任何文件**。

打开 Unity 后它会自动编译。骨架代码已按 Unity 6 的 API 写好：用 `Rigidbody2D.linearVelocity` 而不是旧版的 `.velocity`，用 `Physics2D.OverlapCircle` 的 `List` 重载而不是已废弃的 `NonAlloc` 系列，检查点标识用组件自带的序列化 ID 而不是 Unity 6.3 起已废弃的 `GetInstanceID()`。

想确认编译状态，不用盯 Console，直接看这两处：

```bash
cd DuduAdventure
ls -l Library/ScriptAssemblies/Assembly-CSharp.dll   # 存在 = 我们的脚本编译成功
grep -c "error CS" Logs/Editor.log                   # 应为 0
```

`Assembly-CSharp.dll` **缺失是个强信号**：说明有编译错误，而编译错误会让域重载失败，连带导致 `[MenuItem]`、`[InitializeOnLoad]` 不注册——插件菜单会凭空消失。遇到「菜单不见了」先查这里。

Unity 首次导入会为每个文件生成 `.meta`，这些**需要提交到 Git**（`.gitignore` 已配置为保留）。

---

## 第四步：三个必须先改的设置

这三项不改的话，代码会直接报错或者画面全黑，是 Unity 6 + URP 的经典坑。

### 4.1 允许旧版 Input（否则代码报错）

骨架代码用的是旧版 `Input.GetAxisRaw()` / `Input.GetButtonDown()`。而 Universal 2D 模板默认只启用新版 Input System（`activeInputHandler: 1`），这时旧版调用会在运行时**抛异常**——注意是运行时，编译期看不出问题。

**这一项已经改好了**（`ProjectSettings.asset` 里 `activeInputHandler` 已由 `1` 改为 `2` = Both）。但**必须重启 Unity 编辑器才生效**，这是 Unity 的限制。

要手工确认或改动：**Edit → Project Settings → Player → Other Settings → Active Input Handling** → 选 **Both**。

> 后期做触屏和手柄时会迁移到 New Input System（模板已经带了 `Assets/Settings/InputSystem_Actions.inputactions`，`com.unity.inputsystem 1.20.0` 也已安装），届时再改成 "Input System Package (New)"。现在先用旧版快速验证手感。

### 4.2 给 2D 场景加全局光（否则精灵全黑）

URP 的 2D 渲染器下，Sprite 默认材质是 **Sprite-Lit-Default**（受光照影响）。如果场景里没有任何 2D 光源，所有精灵都会渲染成**黑色**。

解决方式二选一：

- **推荐**：Hierarchy 里右键 → **Light → Light 2D → Global Light 2D**，Intensity 设为 1
- 或者：把 Sprite 材质换成 **Sprite-Unlit-Default**（不受光照，但以后加光效麻烦）

先加 Global Light 2D。后面做火焰山之类的光照氛围时会用到 2D 光源系统。

### 4.3 像素完美渲染

1. 选中场景里的 **Main Camera**
2. **Add Component** → 搜索 **Pixel Perfect Camera**
3. 配置：
   - Assets Pixels Per Unit: **32**（和 GDD 里的 32×32 网格对应）
   - Reference Resolution: X **480**，Y **270**（16:9）
   - 勾选 **Upscale Render Texture**
   - 勾选 **Pixel Snapping**

另外每张导入的像素图都要改（选中图片后在 Inspector 里）：

- Filter Mode: **Point (no filter)** ← 不改会模糊
- Compression: **None**
- Pixels Per Unit: **32**

---

## 第五步：Package 现状

**Window → Package Manager** 里可以看到，Universal 2D 模板已经带齐了大部分东西，不用重复装：

| 包名 | 版本 | 用途 |
|------|------|------|
| `com.unity.render-pipelines.universal` | 17.6.0 | URP |
| `com.unity.inputsystem` | 1.20.0 | 新版输入（触屏+手柄），后期迁移时用 |
| `com.unity.2d.tilemap.extras` | 8.0.3 | Rule Tile 等增强 Tilemap 工具 |
| `com.unity.2d.animation` | 15.1.0 | 骨骼/帧动画 |
| `com.unity.2d.aseprite` | 5.0.3 | 直接导入 `.aseprite` 文件 |
| `com.unity.2d.spriteshape` | 15.0.3 | 曲线地形 |
| `com.ivanmurzak.unity.mcp` | 0.89.0 | AI 助手接入（走 OpenUPM 源，见 UNITY_MCP.md） |

还可能想装的：

| 包名 | 用途 | 何时装 |
|------|------|--------|
| `com.unity.cinemachine` | 高级相机，可替代自写的 CameraFollow | 觉得自写相机不够用时 |

`com.unity.2d.aseprite` 值得留意——它让你把 Aseprite 源文件直接丢进 `Assets/Sprites/`，Unity 自动切图并生成动画剪辑，省掉手工导出雪碧图这一步。

---

## 第六步：搭第一个测试场景

目标：**5 分钟内让一个方块能跑能跳**。不要美术，用纯色方块。

### 6.1 建场景

**File → New Scene** → 选 **Basic 2D (URP)** → 保存为 `Assets/Scenes/Level1_HuaGuoShan.unity`

### 6.2 Tag 与 Layer（已配置好）

**这一步已经做完了**，`Edit → Project Settings → Tags and Layers` 里可以核对。

Layer（8 号往后才是用户层，0–7 被 Unity 占用）：

| 编号 | 名称 | 用途 |
|------|------|------|
| 8 | `Ground` | 地面，PlayerController 的落地检测靠它 |
| 9 | `Player` | 玩家 |
| 10 | `Enemy` | 敌人，PlayerCombat 的攻击判定靠它 |
| 11 | `Platform` | 可穿透平台 |
| 12 | `Hazard` | 尖刺、岩浆等环境伤害 |
| 13 | `Interactable` | 可交互物（宝箱、开关） |

Tag：`Player`、`Enemy`、`Checkpoint`、`LevelEnd`、`Hazard`。

> Tag 和 Layer 是两套独立机制，别混。Layer 给物理系统做碰撞筛选（`LayerMask`），Tag 给代码做身份识别（`CompareTag`）。玩家两样都要设。

### 6.3 建地面

1. 右键 Hierarchy → **2D Object → Sprites → Square**，命名 `Ground`
2. Transform: Position (0, -3, 0)，Scale (20, 1, 1)
3. Inspector 右上角 Layer 设为 **Ground**
4. **Add Component → Box Collider 2D**

### 6.4 建玩家

1. 右键 → **2D Object → Sprites → Square**，命名 `Player`
2. Transform: Position (0, 0, 0)，Scale (1, 1, 1)
3. Layer 设为 **Player**，Tag 设为 **Player**（重要，多个脚本靠 Tag 找玩家）
4. 依次添加组件：
   - **Rigidbody 2D**：Gravity Scale 设 **3**，Collision Detection 设 **Continuous**
   - **Box Collider 2D**
   - **Player Controller**（脚本）
   - **Player State Machine**（脚本）
   - **Player Combat**（脚本）
   - **Health Component**（脚本）
5. 建地面检测点：右键 Player → **Create Empty**，命名 `GroundCheck`，Position (0, -0.5, 0)
6. 回到 Player 的 **Player Controller** 组件：
   - Ground Check Point ← 拖入刚建的 `GroundCheck`
   - Ground Layer ← 勾选 **Ground**
7. **Player Combat** 组件的 Enemy Layer ← 勾选 **Enemy**

### 6.5 配相机

1. 选中 **Main Camera**
2. **Add Component → Camera Follow**（脚本）
3. Target ← 拖入 `Player`
4. Camera 组件的 Projection 确认是 **Orthographic**，Size 设 **5**

### 6.6 测试

点 **Play**：

- A/D 或方向键左右移动
- 空格跳跃（可二段跳）
- J 或鼠标左键攻击
- K 或 Shift 冲刺

排错对照：

| 现象 | 原因 |
|------|------|
| 角色完全不动 | 4.1 的 Active Input Handling 没改成 Both |
| 角色一直往下掉 | Ground 的 Layer 或 PlayerController 的 Ground Layer 没对应 |
| 画面全黑 | 4.2 没加 Global Light 2D |
| 角色抖动/穿墙 | Rigidbody2D 的 Collision Detection 改成 Continuous |

---

## 第七步：版本管理

仓库已经有基线提交和迁移提交，日常改完直接提交即可：

```bash
cd /Users/eleme/code/github/dudu-adventure-game
git add .
git status          # 提交前扫一眼，确认没有意外的大文件
git commit -m "feat: 你做的改动"
```

### `.gitignore` 排除了什么，以及为什么

| 排除项 | 原因 |
|--------|------|
| `Library/`、`Temp/`、`Logs/`、`Build/` | Unity 自动生成，换机重新导入即可 |
| `UserSettings/` | **含 Unity-MCP 的连接 token**，属于本机凭据，绝不能进版本库 |
| `Assets/Plugins/NuGet/` | 18 MB 二进制依赖，插件的 DependencyResolver 会照 `.nuget-installed.json` 重新拉 |
| `*.slnx`、`.vscode/`、`.DS_Store` | IDE / 系统本地产物 |
| `.meta` 文件 | **不排除，必须提交**——Unity 靠它保存资源 GUID 和导入设置，丢了会导致引用全断 |

加上这些排除后，待提交文件从 178 个降到 89 个。

> **一个容易踩的坑**：`.gitignore` 里只要模式中间含斜杠，就会被**锚定到 `.gitignore` 所在目录**。所以写 `Assets/Plugins/NuGet/` 只能匹配仓库根的 `Assets/`，匹配不到 `DuduAdventure/Assets/`，必须写成 `**/Assets/Plugins/NuGet/`。改完用 `git check-ignore -v <路径>` 验证一下，别靠猜。

---

## 接下来两周的建议

**第 1 周：只调手感。** 反复调 PlayerController 的这几个参数，直到跳跃"手感对了"：

| 参数 | 起始值 | 调整方向 |
|------|--------|----------|
| Move Speed | 8 | 觉得拖沓就加大 |
| Jump Force | 14 | 配合 Gravity Scale 一起调 |
| Gravity Scale（在 Rigidbody2D 上） | 3 | 加大 = 下落更快更"脆"，动作游戏一般偏大 |
| Acceleration Time | 0.1 | 减小 = 更灵敏，增大 = 更"重" |
| Coyote Time | 0.12 | 手感宽容度，别超过 0.2 |

手感标杆参考：《蔚蓝》精准轻快，《空洞骑士》厚重有分量。先决定你要哪种。

**第 2 周：做一个完整的小关卡。** 用灰色方块搭出跳跃、平台、一个敌人的完整流程。跑通了再考虑美术。

不要一上来就画孙悟空的像素图——手感没定型的话，动画帧数和碰撞尺寸都得重做。

## 相关文档

- 游戏设计参考 [GDD.md](GDD.md)
- Unity 6.5 官方 2D 手册：https://docs.unity3d.com/6000.5/Documentation/Manual/Unity2D.html
