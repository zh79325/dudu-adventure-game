# 角色美术生产流程

本文档是**都都大冒险**四个玩家角色（悟空 / 八戒 / 沙僧 / 唐僧）美术资产的**强制**生产规范。悟空一整套流程已跑通（提交 `0535eaf`），后续三个角色必须严格照抄本流程；只允许微调**角色设计描述**（Character Design Clause），流程本身不能改。

改流程之前先跟用户确认。


## 0. 为什么必须走这套流程

历史上踩过的坑，全部在本流程里被结构性解决：

- **跨次调用画风漂移严重**——同一段 prompt 分两次调 ImageGen，出来的两组图连头身比都对不上。唯一稳定方案是**一次调用生成一个角色所有帧**。
- **逐帧单独抠图后画布尺寸不一致**——攻击帧 917×894、待机帧 527×951，PPU 512 + Center 轴心，导致渲染出来攻击帧宽 1.79 单位、待机帧宽 1.03 单位，切帧瞬间角色**看起来变胖 74%**。所有帧必须落到**同一张画布**上。
- **绿幕溢色 + 边缘绿边**——固定色距阈值抠图对暖色调角色容易误伤。用 `greenness = G - max(R,B)` 判据，对金黄/红色角色几乎零误伤。
- **邻格漏进来的碎片**——挥棍/伸腿动作会越出理论切线，简单等分切格会把邻格的手/脚一起带进来。用「局部极小值搜索切线」+「连通域碎片剔除」两步兜住。
- **武器不可见 / 挡脸**——Unity 会自动把武器贴图切成子精灵，Prefab 引用到 32×15 的角落碎片。武器**独立生成、独立导入、PPU 独立调**，并由 `WeaponVisualController` 挂在角色子物体上。
- **技能施法只显示待机帧**——`SkillManager.CastRoutine` 不切状态机就只 lock movement，`IsMoving == false` 导致状态机停在 Idle。必须调 `PlayerStateMachine.TriggerCast(duration)`，并把技能帧序列存在 `SkillDefinition.CastFrames` 上，由 `FrameSpriteAnimator` 按 `CastProgress` 均分播放。


## 1. 每个角色的帧清单（18 帧）

| # | 名字 | 状态 | 说明 |
|---|------|------|------|
| 1 | `idle` | Idle | 站立，双臂垂放 |
| 2..5 | `walk1..4` | Run | 完整四帧步态：迈步左、过渡、迈步右、过渡；`FrameSpriteAnimator._walkFPS = 10` 循环播放 |
| 6..8 | `atk1..3` | Attack | 起手 / 击中 / 收势；按 `PlayerStateMachine.AttackProgress` 均分 |
| 9..10 | `sweep1..2` | Cast(Sweep) | 技能 1 施法帧（短，2 帧） |
| 11..12 | `cloud1..2` | Cast(CloudStrike) | 技能 2 施法帧（短，2 帧），俯冲姿势 |
| 13..15 | `clone1..3` | Cast(Clone) | 技能 3 施法帧（长，3 帧） |
| 16..18 | `havoc1..3` | Cast(Havoc) | 技能 4 施法帧（长，3 帧） |

**每个角色一次调用生成，9 列 × 2 行 = 18 格。** 帧顺序在 prompt 里明确到「顶行 1..9、底行 10..18」，导出脚本再按同顺序命名。

技能的短/长（2 帧 / 3 帧）取决于 `SkillDefinition.TotalCastDuration`。改哪个技能是几帧要同步改 prompt。


## 2. ImageGen prompt 模板（唯一可信版本）

以下是**悟空跑通并沉淀下来的**模板。四层结构必须保留：**风格头 → 网格约束 → 角色设计描述 → 姿势清单 → 背景与禁项 → 风格尾**。风格头和风格尾要**互相呼应重复关键词**，否则模型跑到最后一行会把描边和 flat 色丢掉。

调用参数：**size = 2560x1080**（21:9 是唯一稳出 9×2 网格的画幅）。

```
FLAT CEL-SHADED VECTOR GAME SPRITE SHEET, bold black outlines, flat solid colors, hard two-tone shading, no painted texture, no soft gradients, no glow.

A strict grid of NINE columns by TWO rows = EIGHTEEN full-body poses of the EXACT SAME character. Nine in the top row, nine in the bottom row, aligned in columns. Every figure at EXACTLY the same scale with the same head size, shoulder width, torso width and limb thickness. Each row shares one invisible ground line. Draw the character small enough that even the widest lunge fits inside its own narrow column with green space on both sides, never overlapping a neighbour or a canvas edge.

CHARACTER, identical in all eighteen poses: <<<CHARACTER DESIGN CLAUSE>>>. Hands are always EMPTY CLOSED FISTS - no staff, no pole, no weapon, no motion blur, no effects.

TOP ROW: 1 IDLE ... 2 WALK ... 3 WALK ... 4 WALK ... 5 WALK ... 6 ATTACK WINDUP ... 7 ATTACK STRIKE ... 8 ATTACK RECOVER ... 9 SWEEP WINDUP ...

BOTTOM ROW: 10 SWEEP RELEASE ... 11 <SKILL2 FRAME1> ... 12 <SKILL2 FRAME2> ... 13 <SKILL3 FRAME1> ... 14 <SKILL3 FRAME2> ... 15 <SKILL3 FRAME3> ... 16 <SKILL4 FRAME1> ... 17 <SKILL4 FRAME2> ... 18 <SKILL4 FRAME3>.

Background: one solid flat pure green #00FF00 filling the whole canvas. NO drop shadows, no shadow ellipses under the feet, no ground, no gradient, no text, no numbers, no grid lines, no borders.

Style reminder: flat cel-shaded vector, bold black outlines, flat solid colors.
```

写 prompt 时的硬性规则：

- **角色设计描述**（`<<<CHARACTER DESIGN CLAUSE>>>`）在所有 18 个姿势里**逐字一致**，只允许姿势子句变化。变一个字都会让模型重新采样外形。
- **一律 side-front three-quarter view facing RIGHT**（面朝右），画面翻转由 Unity 的 `SpriteRenderer.flipX` 处理，不生成两套镜像。
- **明确写「hands are always EMPTY CLOSED FISTS」**——武器由 Unity 端叠加，AI 画的武器会画风漂移、颜色不匹配、粗细不一。
- **底色一定要 `solid flat pure green #00FF00`**，不能是浅绿、绿色渐变、白色。
- **明确禁止 `drop shadows` / `shadow ellipses under the feet`**——脚下阴影由 `CharacterShadow` 组件运行时绘制，AI 画的阴影会破坏抠图。
- **不要写「in one image」「on one canvas」**——模型会理解成把画面塞满图案，反而挤压人物。用「a strict grid of N columns by M rows」这种量化描述。
- **姿势子句里明确「feet flat」/「heel down」/「toe pushing off」/「knee lifted high」这种关节级细节**，粗略写「walking pose」会得到僵直的立正。

悟空 18 帧的完整姿势子句参见附录 A。八戒 / 沙僧 / 唐僧 生成前先把姿势子句抄一份改写，风格头/尾/网格约束保持不变。

### 2.1 网络失败重试

`ImageGen` 偶尔会在长 prompt 上返回 `Error during image generation: Network attempt failed at response_headers`。**重试策略：把 prompt 压缩到单段落 700~1400 字符**（去掉换行、合并重复形容词、砍掉可推断的解释），几乎必然成功。**不要**把它拆成两次调用生成——那会带来跨次画风漂移。

同一命名的多次调用会自动加时间戳后缀，历史文件不会覆盖。


## 3. 武器 prompt 模板

武器**必须独立生成**，不能画在角色身上。规格：

- 调用参数：**size = 1024x1536**（竖长条武器）；投掷道具/短兵器可用 1024x1024。
- 画面里**只有武器一件**，正中、垂直，四周留白。
- 沿用「flat cel-shaded vector」风格描述，与角色 prompt 的风格头**同源同义**。
- 高度占画布 ~80%，两侧留白让抠图有余量。
- 依然 `solid flat pure green #00FF00` 背景，禁止阴影。
- prompt 保持在单段落 ~700 字符（避免网络失败）。

金箍棒示例：

```
Flat cel-shaded vector cartoon game asset, bold clean black outlines, flat colors only, no gradients, no glow, no shadow. Single object: the golden Ruyi Jingu Bang staff from Journey to the West, standing perfectly vertical and centered. A long slender cylinder about seven times taller than wide, warm golden yellow shaft with one darker gold flat shade on the left side, both ends capped with thick crimson red banded collars trimmed in dark gold with simple swirl carvings and two thin gold rings. Symmetrical top to bottom. Occupies about eighty percent of the canvas height, clear empty margins left and right, nothing else in the frame. Completely flat solid bright green (#00FF00) chroma key background.
```

八戒的九齿钉耙 / 沙僧的降妖宝杖 / 唐僧的锡杖照抄结构改中间那段即可。


## 4. 抠图与切帧：`tools/build_character_frames.py`

**唯一入口**是仓库根的 `tools/build_character_frames.py`。手动 PIL 抠图 / 手动画布对齐是过去反复踩坑的源头，**禁止**用其它方式处理，除非本脚本已经无法覆盖的新情况——那时先把新情况沉淀进脚本再生产。

用法（悟空为例）：

```
python3 tools/build_character_frames.py \
  --sheet vibe_images/wk_master18c_1787568098.png \
  --out   wk_frames \
  --cols 9 --rows 2 \
  --names idle,walk1,walk2,walk3,walk4,atk1,atk2,atk3,sweep1,sweep2,cloud1,cloud2,clone1,clone2,clone3,havoc1,havoc2,havoc3 \
  --bbox-anchor cloud1,cloud2,havoc2 \
  --frag-strict cloud1 \
  --contact-sheet wk_frames/_contact.png \
  --prefix WK_
```

脚本内部做了 5 件事：

1. **绿幕抠像**：`alpha = clip((60 - greenness) / 45, 0, 1)`，`greenness = G - max(R,B)`；同时把边缘吃到的绿压回中性（`newG = max(R,B) + greenness * 0.15`）。
2. **切格**：在理论切线 `k * W / N` 附近 ±70 px 找**列占用最少**的位置。不能用「找空列」——邻格的尾巴/羽毛会互相碰上。
3. **丢碎片**：每格保留面积 ≥ 最大连通域 × 2% 的连通块（默认 `--frag-ratio 0.02`）。**注意**头顶红羽尾尖也是独立块，别调太高。若邻格漏进来一整只手（如悟空 cloud1 吃到了 sweep2 的拳头），改用 `--frag-strict <frame_name>`，那一帧走严格阈值 25%。
4. **统一画布**：`Lmax = max(anchor 到左边界)`、`Rmax = max(anchor 到右边界)`、`Hmax = max(帧高)`；画布 `2 * ceil(max(Lmax, Rmax)) + 2*pad` 宽 × `Hmax + 2*pad` 高；脚底基线固定在 `CH - pad`。所有帧共用同一基线 + 同一水平锚点。
5. **水平锚点**：默认取「躯干重心」——身高 30%~55% 范围的 alpha 质心（挥棍 / 伸腿时躯干最稳定）。躯干本身横过来的姿势（俯冲 / 砸地）用 `--bbox-anchor <name>` 改为 bbox 中点。

产出：

- `<out>/<Prefix><name>.png` 每帧一张，尺寸严格相同。
- `<out>/_canvas.json` 记录 `width / height / anchor_x / baseline_row / pad / frames`。**必须保留**，Unity 导入参数和 `CharacterShadow._footOffset` 都要根据它计算。
- `--contact-sheet <path>` 输出的对照图：棋盘格背景 + 蓝色锚点竖线 + 红色基线，用来目检对齐。**每次跑完必须打开这张图看一眼**：
  - 每个人物脚底都压在红线上；
  - 每个人物躯干都居中在蓝线上；
  - 没有邻格漏进来的手/脚（若有，把该帧加进 `--frag-strict`）；
  - 头身比在所有帧里目测一致（若明显不一致，说明画风漂了，重跑 ImageGen）。
- 若 `total green spill px = 0` 就是抠图干净；不为 0 要调 `chroma_key` 的阈值。

**回归验证**：对同一张 sheet 反复跑本脚本应得到完全相同的结果。悟空的回归产出 `canvas 234x350, anchor_x=117, baseline_row=338`。


## 5. Unity 导入设置

新帧和武器都放到 Unity 项目里：

- 角色帧：`DuduAdventure/Assets/Art/Sprites/Characters/<Name>/<Prefix><frame>.png`。为避免与旧文件冲突（旧文件的 `spriteMode: 2` 内部 SubAsset ID 已被 Prefab 引用），**用新文件名**（如 `WK_Idle.png` 而不是覆盖 `Wukong_Idle.png`）。
- 武器：`DuduAdventure/Assets/Art/Sprites/Weapons/<Prefix>_<WeaponName>.png`。

导入器参数（用 `AssetImporter` / `SerializedObject` 脚本化设置，不要手拖）：

| 参数 | 值 | 为什么 |
|------|-----|--------|
| `textureType` | `Sprite` | — |
| `spriteMode` | `Single` (=1) | **必须 Single**。`Multiple` (=2) 会自动切子精灵，Prefab 引用容易撞到碎片（historic bug 就是这么产生的） |
| `spritePixelsPerUnit` | 角色 **175**，武器根据长度调（金箍棒 **660** → 1.61 单位 ≈ 角色身高 0.89） | 175 是让 234×349 画布落到 1.337 × 1.994 世界单位的数值，跟场景相机 ortho 5.625 匹配 |
| `spriteAlignment` | `Center` (=0) | 用统一画布 + 统一基线之后，Center 相当于脚下方 `pad` 像素处，`BoxCollider2D` / `GroundCheck` / `CharacterShadow` 都不用改 |
| `spriteMeshType` | `FullRect` (=1) | Tight 会把透明边裁掉，破坏统一画布的对齐前提 |
| `spriteExtrude` | `1` | 减少 UV 采样锯齿 |
| `alphaIsTransparency` | `true` | — |
| `mipmapEnabled` | `true` | 高分辨率精灵缩到 iPad 分辨率会闪，需要 mipmap |
| `filterMode` | `Bilinear` (=1) | **不要用 Point**——Point 是给像素画准备的，高分辨率手绘 vector 在 Point 下会锯齿严重 |
| `textureCompression` | `Uncompressed` (=0) | 项目当前阶段优先画质；后续 iPad 打包若吃紧再改 ASTC 6x6 |

改完必须 `AssetDatabase.Refresh()` 触发重编译，之后 `AssetImporter.SaveAndReimport()` 每张贴图各一次。

`Assets/Prefabs/` 下老的美术贴图（`Wukong_Idle.png` 等）**保留不删**——它们的 SubAsset ID 已经写死在 Prefab 里，删掉会让别处引用报错。生产新角色时给新贴图**用新前缀**。


## 6. Prefab 接线

角色 Prefab 位于 `Assets/Prefabs/Player_<Name>.prefab`。用 `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents` 脚本化改。

必须挂到位的字段：

- `SpriteRenderer.sprite = <Prefix>Idle`，`color = Color.white`（**绝不加 tint**——上一版悟空用 `(1, 0.75, 0.3)` 强染成金色，导致新美术叠 tint 后完全变色）。
- `FrameSpriteAnimator`
  - `_idleSprite = <Prefix>Idle`
  - `_walkFrames = [<Prefix>Walk1..4]`（顺序：迈步左 → 过渡 → 迈步右 → 过渡）
  - `_attackFrames = [<Prefix>Atk1..3]`
  - `_jumpSprite = <Prefix>Walk2`（跳跃中显示抬腿帧；2.5D 里几乎不会看到）
  - `_walkFPS = 10`
- `WeaponVisualController`
  - `_defaultWeaponSprite = <角色武器>`
  - `_weaponScale = 1`（武器长度靠 PPU 控制，不靠 scale）
  - `_weaponOffset = (0.34, -0.25)`（金箍棒实测值。改武器要重看：站立时武器顶端应在头右上、末端应过腰）
  - `_idleAngle = -25`（度）
  - `_walkSwayAmplitude = 10`
  - `_attackStartAngle = 55`，`_attackEndAngle = -115`，`_attackDuration = 0.28`
- `CharacterShadow._footOffset = (0, 0.067)`（234×349 画布下方留白 11 px 的补偿；换尺寸时用 `pad / height * world_height` 重算）

`SkillDefinition` 资产（`Assets/Data/Skills/Skill_<Name><Skill>.asset`）新增字段：

- `CastFrames = [<Prefix>Sweep1..2]`（技能 1）/ `[<Prefix>Cloud1..2]`（技能 2）/ `[<Prefix>Clone1..3]`（技能 3）/ `[<Prefix>Havoc1..3]`（技能 4）
- 位移型技能（如 CloudStrike / Havoc）`LockFacing = true`，否则位移中角色会因输入朝向翻转。

装备数据资产（`Assets/Data/Equipment/Weapon_<...>.asset`）的 `WeaponSprite` 字段也要指向对应武器 Sprite——`WeaponVisualController` 会优先用装备里的图，缺失时才回落到 `_defaultWeaponSprite`。


## 7. 验证流程

**每个新角色接完线**必须跑完整验证再进下一步：

1. **对照图目检**：`tools/build_character_frames.py --contact-sheet ...` 输出的棋盘图，逐帧检查基线 / 锚点 / 头身比 / 无碎片。
2. **script-execute 拉进 Play 模式**：用 `mcp__unity-mcp__script-execute` 调 `EnterPlaymode()`，等下一次调用生效后再操作场景。参数名是 `csharpCode`，`className` 必须和内部类名一致，`public static string Main()` 才有返回值。
3. **RenderTexture 抓图**：`cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(...); File.WriteAllBytes(path, tex.EncodeToPNG());`。**不要**用 `ScreenCapture.CaptureScreenshot`——它在编辑器 Play 模式下会静默失败。
4. **一次触发所有状态**：Idle 站桩 → 移动一段（观察走路循环）→ 按攻击键（观察 3 帧攻击 + 武器挥棍）→ 依次触发 4 个技能（观察 CastFrames 播放）。每个状态截一张，拼到对照图。
5. **`mcp__unity-mcp__console-get-logs`** 过滤 `logTypeFilter: Error` + `lastMinutes: 15` + `maxEntries: 50`（**不能**不加过滤——默认输出可以到 44 万字符）。0 Error 才算过。
6. **`ExitPlaymode()` + 保存**：这俩不能在同一次 `script-execute` 里完成，`ExitPlaymode()` 需要下一次调用才生效。
7. 通过后 `git commit`，把对照图和 Play 模式抓图放到 workspace outputs 让用户看。


## 8. 增量验证 & 用户确认

**硬性规则**：一次只做一个角色。上一个角色未经用户在游戏里确认之前，**不要**开始下一个角色的美术。

正确顺序：悟空 ✅ → 用户确认 → 八戒 → 用户确认 → 沙僧 → 用户确认 → 唐僧。

跳步骤会导致：一旦流程里某个参数需要调（比如 PPU、weapon offset），三个角色都要返工。


## 9. 附录 A：悟空姿势子句（完整版）

保留作为**参考模板**，其他角色抄结构改内容。完整的 prompt（含风格头/尾/网格约束/背景禁项）见 §2；这里只列**角色设计描述**和**姿势清单**部分。

**Character Design Clause（悟空）**：

```
Sun Wukong the Monkey King as a side-scrolling brawler sprite, side-front three-quarter view facing RIGHT, heroic-chibi build 4.5 heads tall, slim. Golden-yellow furred monkey face, cream muzzle, red-orange mask markings around sharp dark eyes, one small fang. Spiky golden mane. Gold headband crown with a red gem and two long crimson pheasant feathers arching up and back. Red sleeveless martial tunic, red neck knot, one gold scrollwork shoulder pauldron, gold-edged red skirt panels with golden flame motifs. Brown belt with a round gold buckle. Golden fur forearm bracers and ankle cuffs. Brown boots with gold trim. Long slender golden tail curving up behind.
```

**姿势清单（悟空）**：

```
TOP ROW:
1 IDLE upright, feet flat apart, arms at sides.
2 WALK big scissor stride, near leg far forward heel down, far leg far back toe pushing off.
3 WALK near leg straight under body, far knee lifted high foot off ground.
4 WALK mirror of 2, the OPPOSITE leg leads forward, arms swapped.
5 WALK mirror of 3, far leg straight under body, NEAR knee lifted high.
6 ATTACK WINDUP both fists stacked in a two-handed grip raised high above and behind the head, torso twisted back, shouting.
7 ATTACK STRIKE deep forward lunge, both fists thrust straight forward at chest height in a two-handed grip, arms extended, shouting.
8 ATTACK RECOVER feet together, slight crouch, both fists pulled down in front of the waist.
9 SWEEP WINDUP wide stance, torso coiled back, both fists together low beside the rear hip at knee height.

BOTTOM ROW:
10 SWEEP RELEASE wide low stance legs far apart, torso rotated forward, both fists swung out horizontally to the front at hip height.
11 CLOUD CROUCH crouched low, knees deeply bent, body compressed, both fists low in front of the chest.
12 CLOUD DASH airborne flying charge, body stretched level leaning far forward, legs trailing straight behind, both fists thrust forward ahead of the head.
13 CLONE WINDUP upright, arms crossed in front of the chest, chin tucked, feet together.
14 CLONE FLURRY rapid-punch stance, one fist snapping forward, the other cocked back at the ribs, torso twisted.
15 CLONE END standing straight, hands relaxed at the sides, feet together, confident smile.
16 HAVOC CHARGE both fists raised straight overhead, head tilted back, back arched, feet wide apart, roaring.
17 HAVOC SLAM both fists smashed straight down to the ground in front, body folded forward in a deep wide squat.
18 HAVOC END rising up, torso half upright, fists low and wide at the sides, feet wide apart, panting.
```


## 10. 附录 B：其他三角色出发点

**八戒 / 沙僧 / 唐僧的 Character Design Clause 起草时的原则：**

- 保持「side-front three-quarter view facing RIGHT, heroic-chibi build 4.5 heads tall」这几条骨架句原封不动，只改**材质 / 服饰 / 面部 / 附加装饰**。
- 头身比一致（4.5 heads tall）——这是让四个角色能共用同一套碰撞盒、同一套阴影偏移的前提。
- 武器不入 body sheet，由独立武器 sprite 挂 `WeaponVisualController`。
- 技能名 / 姿势要跟对应的 `SkillDefinition` 对齐；八戒 / 沙僧 / 唐僧的技能定义在 `Assets/Data/Skills/` 下（详见 `docs/DUNGEON_COOP.md`）。

生成前先起草 Clause 交用户确认，避免生成完发现方向不对。
