# AGENTS.md - 都都大冒险 工作手册

## 核心原则

### Unity 操作方式

除了写代码（Coding）之外，所有 Unity 编辑器操作优先使用 unity-mcp 完成。包括但不限于：添加/删除组件、修改 Inspector 参数、编辑 Prefab、场景配置、创建 GameObject 等。

如果 unity-mcp 不支持某个操作，需要先跟用户确认后再决定处理方式，可选方案包括：手动编辑 YAML 文件、让用户在编辑器里手动操作、或其他替代方案。绝不擅自用文件编辑方式绕过 MCP。

### 项目结构

- Unity 项目在 `DuduAdventure/` 子目录，不是仓库根
- 根目录的 `Assets/Scripts` 是重复骨架，改代码只改项目内那份
- unity-mcp 连接配置见 `docs/UNITY_MCP.md`

### 代码约定

- 命名空间: `DuduAdventure.*`（Player、Enemy、Combat、Core、Camera 等）
- 注意 `DuduAdventure.Camera` 命名空间的存在，在 DuduAdventure.* 下裸写 `Camera` 会被解析成命名空间，必须写 `UnityEngine.Camera`
- 同理 `Input` 在 `DuduAdventure.Player.Input` 命名空间下需写 `UnityEngine.Input`
