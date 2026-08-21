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

## 第二步：创建项目（仓库根 = Unity 项目）

本项目采用**仓库根目录直接作为 Unity 项目**的布局。仓库里已有的 `Assets/` 和 `.gitignore` 就是按这个结构准备的，所以不要再建子目录，否则代码要搬来搬去、`.gitignore` 也得改。

目标结构：

```
dudu-adventure-game/          ← 这就是 Unity 项目根目录
├── .git/
├── .gitignore                ← 已配置好（忽略 Library/ Temp/ Logs/ 等）
├── README.md
├── docs/
├── Assets/                   ← 骨架代码已在这里
│   └── Scripts/
├── Packages/                 ← Unity 创建
├── ProjectSettings/          ← Unity 创建
└── Library/                  ← Unity 创建，已被 gitignore
```

> **注意**：Unity-MCP 插件要求**项目路径不能包含空格**。`/Users/eleme/code/github/dudu-adventure-game` 没有空格，符合要求。

### 方式 A：直接指向现有目录（先试这个）

1. Unity Hub → **Projects** → **New project**
2. 编辑器版本选 **6.5.x**
3. 模板选 **Universal 2D**
   - 不要选 "2D (Built-In Render Pipeline)"（已废弃）
   - 列表里看不到就在搜索框输入 "2D"
4. **Project name**: `dudu-adventure-game`
5. **Location**: `/Users/eleme/code/github`（注意是**父目录**，不是仓库本身）
6. 点击 **Create project**

Unity Hub 会拼成 `/Users/eleme/code/github/dudu-adventure-game`，正好是现有仓库。如果它提示目录非空但允许继续，就继续——Unity 只会补上 `Packages/`、`ProjectSettings/`、`Library/`，不会动你的 `Assets/Scripts` 和 `docs/`。

### 方式 B：Unity Hub 拒绝时的备选

如果 Unity Hub 因为目录已存在而不让创建：

1. 先在别处正常创建，比如 Location 选 `~/Desktop`，Project name 填 `DuduTemp`
2. 创建完成后**关闭 Unity 编辑器**
3. 把 Unity 生成的目录搬到仓库根：

```bash
cd ~/Desktop/DuduTemp
mv Packages ProjectSettings /Users/eleme/code/github/dudu-adventure-game/
# Assets 里 Unity 生成的默认内容（Settings 文件夹等）也要一起搬
cp -R Assets/. /Users/eleme/code/github/dudu-adventure-game/Assets/
```

4. Unity Hub → **Add** → **Add project from disk** → 选 `/Users/eleme/code/github/dudu-adventure-game`
5. 确认能正常打开后，`~/Desktop/DuduTemp` 就可以不管了

> 方式 B 里 `Assets/` 是**合并**而不是覆盖，所以要用 `cp -R Assets/.` 这种写法。URP 模板会在 `Assets/Settings/` 下生成渲染管线配置文件，漏搬会导致画面异常。

---

## 第三步：确认骨架代码被识别

因为仓库根就是 Unity 项目，`Assets/Scripts` 已经在正确位置了，**不需要复制任何文件**。

打开 Unity 后它会自动编译。检查 **Console 窗口没有红色报错**即可——骨架代码已按 Unity 6 的 API 写好：用 `Rigidbody2D.linearVelocity` 而不是旧版的 `.velocity`，用 `Physics2D.OverlapCircle` 的 List 重载而不是已废弃的 `NonAlloc` 系列。

Unity 首次导入会为每个文件生成 `.meta` 文件，这些**需要提交到 Git**（`.gitignore` 已配置为保留它们）。

---

## 第四步：三个必须先改的设置

这三项不改的话，代码会直接报错或者画面全黑，是 Unity 6 + URP 的经典坑。

### 4.1 允许旧版 Input（否则代码报错）

骨架代码用的是旧版 `Input.GetAxisRaw()` / `Input.GetButtonDown()`。Unity 6 默认可能只启用了新版 Input System，这时旧版调用会**抛异常**。

**Edit → Project Settings → Player → Other Settings → Active Input Handling** → 改成 **Both**

改完 Unity 会要求重启编辑器，重启即可。

> 后期做触屏和手柄时会迁移到 New Input System，届时再改成 "Input System Package (New)"。现在先用旧版快速验证手感。

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

## 第五步：安装需要的 Package

**Window → Package Manager** → 左上角 **+** → **Install package by name**：

| 包名 | 用途 | 何时装 |
|------|------|--------|
| `com.unity.inputsystem` | 新版输入（触屏+手柄） | 做操作适配时 |
| `com.unity.2d.tilemap.extras` | Rule Tile 等增强 Tilemap 工具 | 做关卡时 |
| `com.unity.cinemachine` | 高级相机（可替代自写的 CameraFollow） | 可选 |

Universal 2D 模板已经自带 2D Sprite、2D Tilemap、2D Animation、URP，不用重复装。

---

## 第六步：搭第一个测试场景

目标：**5 分钟内让一个方块能跑能跳**。不要美术，用纯色方块。

### 6.1 建场景

**File → New Scene** → 选 **Basic 2D (URP)** → 保存为 `Assets/Scenes/Level1_HuaGuoShan.unity`

### 6.2 配置图层

**Edit → Project Settings → Tags and Layers**，在 Layers 里添加：

- Layer 6: `Ground`
- Layer 7: `Player`
- Layer 8: `Enemy`

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

## 第七步：提交代码

```bash
cd /Users/eleme/code/github/dudu-adventure-game
git add .
git commit -m "feat: Unity 6.5 项目初始化 + 核心架构骨架"
git push origin main
```

`.gitignore` 已配置好会排除 `Library/`、`Temp/`、`Logs/` 等自动生成目录。

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
