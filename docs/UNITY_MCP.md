## Unity-MCP 安装与接入指南

> 让 AI 助手（QoderWork）直接操作 Unity 编辑器：创建 GameObject、挂脚本、改 Inspector 参数、读 Console 报错、跑测试。
>
> 项目地址：https://github.com/IvanMurzak/Unity-MCP

---

### 它是怎么工作的

三段结构，缺一环都连不上：

```
QoderWork (AI Client)  ←→  MCP Server  ←→  Unity 编辑器插件
   HTTP (streamableHttp)      SignalR           Unity Editor
     localhost:21412       /hub/mcp-server
```

中间那个 MCP Server 是一个 .NET 可执行文件（约 110 MB），**不需要你手动下载**——Unity 插件装好后会自己把它拉到项目里，并且由插件负责启动：

```
dudu-adventure-game/DuduAdventure/Library/mcp-server/osx-arm64/gamedev-mcp-server
```

`osx-arm64` 对应 Apple Silicon（M 系列芯片）。因为它落在 `Library/` 下，而 `Library/` 是被 gitignore 的，所以**换机器要重新装插件**，这是正常的。

这也解释了为什么必须先建 Unity 项目、先装插件，最后才能在 QoderWork 里注册 MCP：server 没跑起来之前，那个 HTTP 端口是不存在的，注册上去只会是 Disconnected。

---

### 前置条件

Unity 项目位置：

```
/Users/eleme/code/github/dudu-adventure-game/DuduAdventure
```

已确认：Unity `6000.5.9f1`，Universal 2D（URP）模板，插件 `0.89.0`。

**项目路径不能有空格。** 上面这个路径没有空格，符合要求。

---

### 第一步：装 Unity 插件

**已装好。** 当前这套是项目重建后用插件安装器 + 窗口内自动下载 server 二进制装成的，`Packages/manifest.json` 保持 Unity 6.5 Universal 2D 的原始状态，没加任何 scopedRegistry。

验证是否真的装好，看这三处（都在 `DuduAdventure/` 下）：

```bash
ls -l Library/mcp-server/osx-arm64/gamedev-mcp-server   # 约 110 MB，可执行
ls Library/ScriptAssemblies/ | grep -i ivanmurzak        # 应有 Editor/Runtime/DependencyResolver 三个 dll
grep -c "error CS" Logs/Editor.log                       # 应为 0
```

三条都过，菜单栏就会有 **Window → AI Game Developer — MCP**。

#### 备用方案

装不上时按顺序试：

**unitypackage 安装器**（最稳，自带依赖、全程离线）：从 https://github.com/IvanMurzak/Unity-MCP/releases 下载 `AI-Game-Dev-Installer.unitypackage`，Unity 里 **Assets → Import Package → Custom Package** 导入，全选 Import。

**改 manifest**（走 OpenUPM）：

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.ivanmurzak", "extensions.unity"]
    }
  ],
  "dependencies": {
    "com.ivanmurzak.unity.mcp": "0.89.0"
  }
}
```

`extensions.unity` 这个 scope 也是必需的——插件依赖 OpenUPM 上的 `extensions.unity.playerprefsex`，少了它会解析失败。改完把 Unity 切到前台，它会自动开始解析。

**命令行**（需要 Node.js）：

```bash
npm install -g unity-mcp-cli
cd /Users/eleme/code/github/dudu-adventure-game/DuduAdventure
unity-mcp-cli install-plugin . --plugin-version 0.89.0
```

`--plugin-version` 不是可选的装饰。国内网络下不加它会直接失败：

```
✖ Failed to resolve the latest version of com.ivanmurzak.unity.mcp from OpenUPM.
```

CLI 要先问 OpenUPM 的 registry 元数据才知道「最新版是几」，这一步够不到就没法往下走。而 OpenUPM **没有可用的国内镜像**（`package.openupm.cn`、`openupm.cn` 都不通，`registry.npmmirror.com` 返回 404），所以只能手工把版本号写死，跳过版本协商。

---

### 第二步：拿到真实配置

装完之后，Unity 菜单栏出现 **Window → AI Game Developer — MCP**，打开它。

注意菜单名末尾那个「— MCP」是插件源码里写死的（`[MenuItem("Window/AI Game Developer — MCP %&a")]`），破折号是长破折号，肉眼容易看漏。快捷键是 **⌘⌥A**，直接按更快。

另外插件还注册了一整棵 **Tools → AI Game Developer** 子菜单，其中 **Server → Download Binaries** 可以手动触发 server 二进制下载。如果 `Library/mcp-server/` 一直不出现，就点这一项。同一层还有 `Server → Open Logs` 和 `Open Log Errors`，排查时很有用。

窗口顶部的 Connection 开关有 **Custom / Cloud** 两档。**必须选 Custom。** Cloud 是作者的托管服务，会弹「Authorization Required」要你登录授权，本地联调完全不需要。

窗口里的设置最终落到这个文件，它才是**权威配置来源**（比窗口截图可靠，也方便我直接读）：

```
DuduAdventure/UserSettings/AI-Game-Developer-Config.json
```

本项目的实际值：

```json
{
  "host": "http://localhost:21412",
  "transportMethod": "streamableHttp",
  "authOption": "none",
  "connectionMode": "Custom",
  "keepServerRunning": false,
  "timeoutMs": 10000
}
```

几点值得留意：

端口是 **21412，不是文档里常见的 8080**。插件用项目目录哈希算端口（`GeneratePortFromDirectory()`），好处是多个 Unity 项目同时开着也不会撞端口，代价是这个数字得现查——换个项目路径它就变。

传输是 **streamableHttp 而非 stdio**。本地 server 固定用 loopback HTTP 端点，QoderWork 侧按 URL 接，不需要填那个二进制路径。`authOption: none` + `bind: loopback` 意味着只监听本机、不校验 token，所以配置里那个 `token` 字段在本地模式下用不上（它是给 Cloud 模式的）。

`keepServerRunning: false` 表示 **server 随 Unity 退出**。关掉 Unity 之后 QoderWork 这边会变 Disconnected，重开 Unity 自动恢复，属正常现象。

---

### 第三步：在 QoderWork 注册

用 HTTP 型 connector，填 URL 就行：

```
qw_action({
  key: "qoderwork.settings.connector.custom",
  action: "add",
  params: {
    name: "unity-mcp",
    config: { url: "http://localhost:21412" }
  }
})
```

注册完查一次状态：

```
qw_query({ key: "qoderwork.settings.connector.custom.unity-mcp" })
```

期望 `transport: "http"`、`status: "connected"`、`auth.authType: "none"`，并且能列出 37 个工具。

---

### 第四步：验证

**已验证通过**（2026-08-24）：

- `scene-list-opened` 读到 `Assets/Scenes/SampleScene.unity`，说明读链路通
- 建了个 `MCP_ConnectionTest` 空 GameObject 又删掉，说明写链路通
- `console-get-logs` 能取到 Console，说明报错可以直接由我看

**读 Console 有个坑**：`console-get-logs` 默认把整个 Editor Console 全量吐出来，本项目一次就 44 万字符，直接把上下文打爆。而且 `includeStackTrace: false` 并不生效，堆栈照样带出来。实用姿势是三个参数一起收紧：

```
{ "logTypeFilter": "Error", "lastMinutes": 5, "maxEntries": 20 }
```

---

### 国内网络踩坑记录（真实遇到过）

**症状**：Unity 报 `An error occurred while resolving packages`，具体是

```
com.unity.visualscripting: Cannot connect to 'download.packages.unity.com'
(error code: ECONNRESET)
```

**为什么会卡住整个插件安装**：Unity 的包解析是「一个失败、全盘回滚」。只要有任何一个包下不下来，整次 resolve 就失败，插件的主程序集也就编译不出来，`Window → AI Game Developer — MCP` 菜单自然不出现。所以报错的包看起来跟 MCP 八竿子打不着，却真的会挡住它。

**诊断方法**：比对 `Packages/manifest.json` 里声明的依赖和 `Library/PackageCache/` 里已缓存的目录，找出「声明了但没缓存」的那几个——它们才是真正需要联网的。已经在 PackageCache 里的包不需要再下载。

```bash
cd DuduAdventure
ls Library/PackageCache/ | sed 's/@.*//' | sort > /tmp/cached.txt
python3 -c "
import json
d = json.load(open('Packages/manifest.json'))
cached = set(l.strip() for l in open('/tmp/cached.txt'))
print([k for k in d['dependencies'] if k not in cached])
"
```

**解决**：把用不上的包从 manifest 里删掉，让「需要联网」的清单变空。本项目删掉了 `com.unity.visualscripting`（Bolt 可视化脚本，我们写 C# 用不到，也没有别的包依赖它）。删之前用 `packages-lock.json` 确认过没有反向依赖。

**还需要通的域名**：插件的主程序集依赖一批 .NET 库，由 `DependencyResolver` 从 `https://api.nuget.org/v3-flatcontainer/` 下载。这个域名必须能访问。如果也被挡，改用 unitypackage 安装器——它自带依赖，全程离线。

---

### 更深一层的坑：2D 包族版本错配

上面删掉 `visualscripting` 之后 resolve 过了，NuGet 依赖也下来了，插件的 `Editor.dll` / `Runtime.dll` 都编译出来了——但菜单依然不出现。

**教训先说**：只比对包名判断「是否需要联网」是不够的，**必须比对版本**。PackageCache 里有 `com.unity.2d.animation` 这个目录，不等于它是 Unity 6.5 要的那个版本。

**真实原因**：Unity 6.5 打开这个项目时想把整个 2D 包族升级（`Packages-Update.log` 里记着 `2d.animation 10.1.4 → 15.1.0` 这类条目）。结果只有一部分下载成功——`com.unity.2d.common` 升到了 14.0.1，而依赖它的 `2d.animation`、`2d.aseprite`、`2d.spriteshape`、`2d.psdimporter`、`2d.tilemap.extras`、`2d.tooling` 全都卡在旧版本。新的 common 配旧的兄弟包，直接编译报错：

```
error CS0619: 'Object.GetInstanceID()' is obsolete: 'Use GetEntityId instead.'
error CS7036: There is no argument given that corresponds to the required
              parameter 'spriteRenderer' of 'InternalEngineBridge.IsGPUSkinningEnabled'
error CS0117: 'InternalEngineBridge' does not contain a definition for
              'SetBatchBoneTransformsAABBArray'
```

**为什么会连带干掉菜单**：编译错误会让域重载（domain reload）失败，`[MenuItem]` 和 `[InitializeOnLoad]` 根本没机会注册。判断依据是 `Library/ScriptAssemblies/` 里连 `Assembly-CSharp.dll` 都没有——连我们自己的脚本都没编译成功。

**解决**：`packages.unity.cn` 是 Unity 官方的中国镜像，它有 Unity 6.5 需要的那些新版本，而且在国内可达。于是给这几个包单独配镜像源：

```json
{
  "name": "Unity China Mirror",
  "url": "https://packages.unity.cn",
  "scopes": [
    "com.unity.2d.animation",
    "com.unity.2d.aseprite",
    "com.unity.2d.common",
    "com.unity.2d.psdimporter",
    "com.unity.2d.spriteshape",
    "com.unity.2d.tilemap.extras",
    "com.unity.2d.tooling"
  ]
}
```

这里**故意逐个列包名而不是用 `com.unity.2d` 前缀**。因为 `com.unity.2d.sprite` 和 `com.unity.2d.tilemap` 是编辑器内置模块，镜像上没有；一旦被前缀匹配走到镜像就会解析失败。

同时把其余每个包都钉到 PackageCache 里**已有的那个确切版本**（URP `17.6.0 → 17.5.0`，test-framework `1.4.5 → 1.7.0`，ugui `2.0.0 → 2.5.0`），这样它们一次网络请求都不发。另外删掉了 `com.unity.ai.inference`——它自己也在报 CS0619，而且没有任何包依赖它。

最终只剩 6 个 `com.unity.2d.*` 包需要从镜像下载，它们的非 2D 依赖（`collections`、`mathematics`、`2d.common`）本地版本都已满足或更高。

---

### 常见问题

| 现象 | 原因 | 处理 |
|------|------|------|
| `Library/mcp-server/` 目录不存在 | server 二进制没下载完 | `Tools → AI Game Developer → Server → Download Binaries` 手动拉一次 |
| 菜单栏找不到插件菜单 | 项目有编译错误，域重载失败，`[MenuItem]` 没注册 | 看 `Library/ScriptAssemblies/` 有没有 `Assembly-CSharp.dll`；先把 CS 错误清零 |
| 窗口一直要 Authorize | Connection 开关停在 **Cloud** 档 | 切到 **Custom**，本地联调不需要授权 |
| QoderWork 侧 Disconnected | URL 对不上 | 以 `UserSettings/AI-Game-Developer-Config.json` 里的 `host` 为准（端口按项目路径哈希生成，换项目就变） |
| 连上了但一调用就超时 | Unity 正在编译，或 `timeoutMs` 太小 | 等编译完再试；把 `timeoutMs` 提到 30000 |
| 关掉 Unity 就断连 | `keepServerRunning: false`，server 随编辑器退出 | 正常，重开 Unity 会自动重连；必要时在 QoderWork 里 disable → enable 一次 |
| 换了台机器 / 删了 Library | server 二进制没进 git | 重新装一次插件即可 |

---

### 装完能干什么

装好之后，很多原本要手点的活可以直接说出来：

- 「给 Player 加上 PlayerController 和 PlayerCombat，把 Ground Layer 设成 Ground」
- 「Console 里报了什么错，帮我看看」
- 「把 PlayerController 的 jumpForce 调到 16 试试手感」
- 「按 GDD 里花果山的描述搭一个测试关卡的 Tilemap」

对新手来说最有价值的其实是**读 Console**——报错不用截图不用抄，我直接看。
