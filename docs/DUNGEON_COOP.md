## 都都大冒险 — 多人副本刷宝玩法设计

> 本文档记录玩法转向后的核心决策与分阶实施计划。**决策一旦记在这里就不再反复推翻**，需要改动请直接修订本文并注明原因。
>
> 定位一句话：**2D 横版、本地四人同屏、进副本刷怪打 Boss 爆装备**。参考坐标是《地下城与勇士》的横版副本结构 + 《我的世界地下城》的装备驱动成长。

---

### 已确认的决策

| 议题 | 结论 | 关键理由 |
|------|------|----------|
| 视角 | **2D 横版** | 已写的跳跃/碰撞/手感代码全部保留；像素素材只需侧面一个朝向，美术量最小 |
| 联机形态 | **本地同屏**（一台设备多手柄） | Xbox 体验最佳，无需服务器与状态同步，工作量与调试成本远低于网络联机 |
| 人数上限 | **4 人** | 屏幕不拥挤、战斗特效可控、副本难度好平衡 |
| 成长驱动 | **装备为主 + 每角色一个大招** | 刷装备是核心乐趣且内容扩展便宜（出一件装备就是新玩法）；大招保留西游角色辨识度 |
| 角色重复 | **不允许**，四人各选其一 | 4 人上限与初始四角色（悟空/八戒/沙僧/唐僧）刚好对齐 |
| 镜头 | **固定缩放，跟随队长** | 比"框住所有人的动态缩放"简单得多，且不与 Pixel Perfect Camera 冲突（见下） |
| 掉队处理 | **超时自动传送到队长身边** | 不生硬，但需处理落点安全，避免传送进陷阱 |
| 副本产出 | **手工房间模块 + 随机拼接** | 每段手感可控，又有重玩新鲜感，是刷副本游戏的主流做法 |

#### 一个已避开的技术冲突

最初考虑过"镜头动态缩放框住所有玩家"。这个方案与 **Pixel Perfect Camera 不兼容**——它每帧会按参考分辨率强行改写 `orthographicSize`，外部设置的缩放会被直接吃掉。改为跟随队长的固定缩放后，像素完美渲染可以保留。

如果以后确实想要动态缩放，只能二选一：放弃 Pixel Perfect 的整数上采样（保留 Point 过滤，接受非整数缩放），或者把缩放限制为整数倍档位。

---

### 现有代码里的单人硬假设

改造前已逐一定位，共 6 处，全部需要处理：

| 位置 | 问题 | 处理方式 |
|------|------|----------|
| `PlayerController` 187/274/292 | 直接读全局 `Input.GetAxisRaw` / `GetButtonDown` | 抽象为 `IPlayerInputSource`，每个玩家一份 |
| `PlayerCombat` 175/181 | 同上（攻击、冲刺） | 同上 |
| `EnemyBase` 149 | `Start` 里按 Tag 缓存**唯一**玩家 | 改为每次索敌取最近的存活玩家 |
| `CameraFollow` 102 | 按 Tag 自动找唯一玩家 | 改为从注册表取队长 |
| `LevelManager` 121/188/282 | 单个 `_playerTransform`，复活只传送一个人 | 改为遍历注册表，逐人复活 |
| `GameManager` 生命值 | 全局共享生命 | 需明确：本作按队伍共享生命池 |

> `GameManager` 338 行的 `Input.GetKeyDown(KeyCode.Escape)` 是系统级暂停，属于全局单一动作，**保留全局输入是正确的**，不需要改。

另有一个已修复的坑记录在此：`PlayerStateMachine` 带 `[RequireComponent(typeof(PlayerController))]`，用脚本挂组件时它会自动补一个 `PlayerController`，此时再显式挂一次就会出现**两个实例**。MonoBehaviour 默认允许重复挂载，Unity 运行时取第一个，而配置写在第二个上——表现为"参数明明设了却不生效"。搭场景务必用独立读取验证，别只看写入日志。

---

### 分阶实施计划

每个阶段都以"能玩到"为验收标准，不做纯架构的空转。

#### 阶段 1：多人基础（已完成，待真机试玩）

目标：**两个手柄能同时在灰盒场景里跑跳，镜头跟队长，掉队会被拉回来。**

实际落地的脚本：

| 文件 | 职责 |
|------|------|
| `Player/Input/IPlayerInputSource.cs` | 输入抽象接口 + `PlayerInputSourceResolver` 兜底解析 |
| `Player/Input/LegacyInputSource.cs` | 老 Input 系统键盘源，手工摆放的调试角色兜底用 |
| `Player/Input/DeviceInputSource.cs` | 新 Input System，绑定到单个手柄或键盘 |
| `Player/PlayerIdentity.cs` | 玩家身份（编号 + 角色 ID）与静态 `PlayerRegistry` |
| `Player/PlayerJoinManager.cs` | 按键加入 / 退出、角色去重、上限 4 人 |
| `Player/OffscreenRecovery.cs` | 非队长离屏超时后传送回队长身边 |

改动的既有脚本：`PlayerController`、`PlayerCombat` 不再读全局 `Input`；`CameraFollow` 每帧解析队长；`EnemyBase` 新增 `AcquireTarget()` 周期性改锁最近的活人。

新增资产：`Prefabs/Player_Wukong|Bajie|Shaseng|Tangseng.prefab`（灰盒阶段仅颜色不同），场景 `Level1_HuaGuoShan` 里的手工 Player 已删除，改为 `SpawnPoint` + `Systems/PlayerJoinManager` 运行时生成。

**与原计划的偏差：没有用官方 `PlayerInputManager`。**

原本打算包装官方组件，实际改成在 `PlayerJoinManager` 里自己轮询 `Gamepad.all` 与 `Keyboard.current`。原因是官方那套要额外维护 `.inputactions` 资产，设备与角色的绑定发生在框架内部，出问题时很难在自己的代码里看清"这个手柄到底绑给谁了"。而本地同屏的规则只有三条——一个设备对应一个角色、角色不能重复、最多 4 人——自己轮询后全部逻辑集中在一个文件里，独立开发更好维护。代价是热插拔、设备重连这些边缘情况得自己处理，目前的做法是断开即移除角色。

**本机验证步骤：**

1. 打开 `Assets/Scenes/Level1_HuaGuoShan.unity`，点 Play
2. 键盘会自动作为 1P 加入（悟空，金色方块），A/D 移动、空格跳、J 攻击、K 冲刺
3. 插手柄按 **Start** 加入 2P（八戒，粉色方块），摇杆/十字键移动、A 跳、X 攻击、B 或 RB 冲刺
4. 让 1P 一直往右跑，2P 停在原地 → 约 0.6 秒后 2P 应被传送到 1P 身边
5. 手柄按 **Select/Back** 退出；若退出的是队长，镜头应立刻改跟剩下的人

预期 Console 输出：`[PlayerRegistry] ... 加入`、`[PlayerRegistry] 队长变更为 ...`、掉队时 `[OffscreenRecovery] ... 已传送回队长身边`。

**已知遗留：** 队长目前固定是第一个加入的人，还没有手动移交队长的操作；玩家死亡后的重生仍走 `LevelManager.RespawnPlayer()` 的单人逻辑，需在阶段 2 一并改成按人重生。

#### 阶段 2：装备与属性 + 技能系统（已完成）

目标：**打死一只怪掉出一把橙武，捡起来伤害数字变大。按 U/I/O/P 释放技能消耗蓝量或怒气。**

实际落地的脚本：

| 文件 | 职责 |
|------|------|
| `Stats/CharacterStats.cs` | 属性聚合系统：(base+ΣFlat)*(1+ΣPercent)，dirty-flag 缓存 |
| `Stats/StatModifier.cs` | 属性修改器（flat + percent）|
| `Stats/LevelSystem.cs` | 经验曲线 base*level^1.5，每级自动加属性 + 特定等级解锁技能 |
| `Stats/ResourceComponent.cs` | 通用资源组件（Mana 自然回复 / Rage 靠战斗积攒） |
| `Stats/RageAccumulator.cs` | 监听 OnAttackHit + OnDamaged 积攒怒气 |
| `Equipment/EquipmentTemplate.cs` | 装备模板 SO，含基础属性与词条池配置 |
| `Equipment/EquipmentInstance.cs` | 运行时实例，由模板 CreateInstance() 随机 roll 词条 |
| `Equipment/EquipmentManager.cs` | 6 槽穿戴 + 背包列表，穿脱时更新 CharacterStats |
| `Equipment/DropPickup.cs` | 地面掉落物，进 Trigger 范围按攻击键拾取（放入背包） |
| `Equipment/LootTable.cs` | 掉落表 SO，DropChance 门槛 + 按权重选模板 |
| `Equipment/LootDropper.cs` | 挂在敌人上，死亡时 Roll() 并 Instantiate 掉落物 |
| `Combat/HealthComponent.cs` | 通用血量组件，TakeDamage / FullHeal / 死亡事件 |
| `Skill/SkillDefinition.cs` | 技能 SO：消耗类型(Mana/FullRage)、效果类型(MeleeArea/Projectile/Dash/Buff/GroundSlam)、多段命中、前后摇 |
| `Skill/SkillManager.cs` | 技能管理器：4 槽位(U/I/O/P)、冷却追踪、施法协程、OverlapCircle/BoxCastAll 判定 |
| `UI/HUDManager.cs` | DNF 式底部 HUD，自动订阅 PlayerRegistry 事件 |
| `UI/InventoryUI.cs` | 背包 UI，穿戴/丢弃/粉碎按钮 |

**已创建的数据资产：**

- `Assets/Data/Equipment/` — 装备模板 SO（示例武器/防具）
- `Assets/Data/Skills/Skill_WukongSweep.asset` — 棍扫千军（Mana 15, CD 3s, MeleeArea, 2.5x）
- `Assets/Data/Skills/Skill_WukongCloudStrike.asset` — 筋斗云冲（Mana 25, CD 6s, Projectile, 3x）
- `Assets/Data/Skills/Skill_WukongClone.asset` — 分身乱打（Mana 35, CD 10s, MeleeArea 5段, 1.5x, 无敌）
- `Assets/Data/Skills/Skill_WukongHavoc.asset` — 大闹天宫（FullRage, CD 15s, GroundSlam 3段, 8x, 无敌）

**架构决策：**

- **DNF 式移动模型：** `Rigidbody2D.gravityScale = 0`，X 轴水平移动，Y 轴 = groundY（纵深）+ jumpHeight（腾空），`DepthSortByY` 按 groundY 排序 sortingOrder
- **属性聚合公式：** `finalValue = (base + ΣflatMods) * (1 + ΣpercentMods)`，修改器按 sourceId 分组管理，dirty-flag 缓存避免每帧计算
- **装备词条随机：** Fisher-Yates 从模板词条池选词条，按稀有度 roll 数值范围
- **技能消耗二分：** 小技能耗蓝（ResourceType.Mana），大绝招需满怒气（ResourceType.Rage，RageAccumulator 通过攻击/受伤积攒）
- **施法流程：** 锁定移动 → 前摇 → 多段判定循环 → 后摇 → 解锁移动 → 开始冷却
- **伤害计算统一走 CharacterStats.CalculateAttackDamage(multiplier)**，含暴击判定
- **拾取设计：** 手动拾取（进 Trigger 范围 + 按攻击键），只放背包不自动穿，保持刷宝惊喜感

**已验证的完整链路：**

1. 场景里有敌人（Slime，HP=1 快速测试） → 攻击击杀
2. 击杀触发 LootDropper.Roll() → 掉落物出现（带稀有度颜色闪烁）
3. 走到掉落物旁按攻击键 → 拾取放入背包
4. 打开背包 UI 可穿戴/丢弃/粉碎
5. 穿戴后 CharacterStats 属性变化 → 攻击伤害数字变大
6. 按 U/I/O/P 释放技能，消耗蓝量/怒气，对敌人造成技能伤害

**与阶段 4 的关系：** 本阶段实现的 SkillManager 是通用技能框架。阶段 4 的"四角色标志性大招"（七十二变/下耙/横扫/佛光）将复用此框架，增加角色专属的视觉效果和独特机制（如变身、治疗队友）。目前 4 个技能 SO 全部是悟空的通用战斗技能，其他角色的专属技能留待阶段 4 设计。

**已知遗留：**

- SkillManager 输入仍用 `UnityEngine.Input.GetKeyDown` 硬编码键位，多人时需接入 `IPlayerInputSource`
- 技能无 VFX/音效，需替换 placeholder sprite 为正式动画
- 技能 HUD 冷却显示尚未实现（SkillSlotUI 待开发）
- LevelSystem 的技能解锁检查已连接但未在 UI 上展示

#### 阶段 3：副本结构

目标**：**随机拼出一个五房间副本，清场才开门，最后一间是 Boss，通关给奖励。**

- 房间模块预制体 + 进出口锚点
- 拼接器：按权重选房间、对齐锚点、保证可通行
- 清场门（DNF 式：房间内敌人全灭才解锁）
- 刷怪器与波次
- Boss 房与保底掉落
- 难度档位缩放怪物属性与掉落品质

#### 阶段 4：角色大招

四个标志性大招，各带冷却或能量条：悟空七十二变、八戒下耙、沙僧长柄横扫、唐僧佛光。

---

### 与其他文档的关系

`GDD.md` 已在阶段 1 完成后统一修订为 v2.0，与本文档保持一致。GDD 负责完整的游戏设计全景（世界观、角色、美术、音效等），本文档负责具体的技术决策与分阶实施细节。两者互相交叉引用。

`SETUP.md` 的测试场景章节（第六步）描述的是单人灰盒场景，阶段 1 完成后需要补充多人加入的验证步骤。
