# Tiled MCP Server

简易版 Tiled Map Editor MCP 服务器，用于 AI 助手与 Tiled 编辑器集成。

## 架构

```
AI Client (Claude/Cursor) ←stdio→ MCP Server (:6510) ←HTTP polling→ Tiled Extension
```

## 快速开始

### 1. 安装依赖

```bash
cd /Users/eleme/code/github/dudu-adventure-game/tiled/mcp-server
npm install
```

### 2. 启动服务器

```bash
npm start
```

服务器会自动扫描端口 6510-6514，找到可用端口后启动 HTTP 服务。

### 3. 配置 AI 客户端

#### Claude Desktop 配置

编辑 `~/Library/Application Support/Claude/claude_desktop_config.json`：

```json
{
  "mcpServers": {
    "tiled": {
      "command": "node",
      "args": ["/Users/eleme/code/github/dudu-adventure-game/tiled/mcp-server/src/index.js"]
    }
  }
}
```

#### Cursor 配置

在项目根目录创建 `.cursor/mcp.json`：

```json
{
  "mcpServers": {
    "tiled-map-editor": {
      "command": "node",
      "args": ["/Users/eleme/code/github/dudu-adventure-game/tiled/mcp-server/src/index.js"]
    }
  }
}
```

### 4. 安装 Tiled 扩展

将 `tiled-mcp-pro-public/extension/` 文件夹复制到 Tiled 扩展目录：

**macOS:**
```bash
cp -r ~/Desktop/AIWorker/game/tiled-mcp-pro-public/extension \
      ~/.tiled/extensions/tiled-mcp-pro/
```

**重启 Tiled**，检查日志是否显示连接成功。

## 可用工具

| 工具名 | 说明 |
|--------|------|
| `create_map` | 创建新地图 |
| `open_map` | 打开现有地图文件 |
| `save_map` | 保存当前地图 |
| `create_tile_layer` | 创建瓦片图层 |
| `place_tile` | 在指定位置放置瓦片 |
| `fill_tiles` | 填充矩形区域的瓦片 |
| `create_object` | 创建对象（矩形、点等） |
| `export_map` | 导出地图为 JSON/CSV/PNG |
| `get_map_info` | 获取当前地图信息 |
| `ping` | 心跳检测 |

## 使用示例

### 通过 AI 创建地图

```
用户: "创建一个 100x100 的等轴测地图"

AI 自动调用:
{
  "method": "tools/call",
  "params": {
    "name": "create_map",
    "arguments": {
      "width": 100,
      "height": 100,
      "tileWidth": 32,
      "tileHeight": 16,
      "orientation": "isometric"
    }
  }
}
```

### 放置瓦片

```
用户: "在 Ground 层的 (10, 15) 位置放置石板路瓦片"

AI 调用:
{
  "method": "tools/call",
  "params": {
    "name": "place_tile",
    "arguments": {
      "layer": "Ground",
      "x": 10,
      "y": 15,
      "tilesetId": "stone_paths",
      "tileId": 5
    }
  }
}
```

## API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| `/status` | GET | 健康检查 |
| `/poll` | GET | Tiled 扩展轮询命令 |
| `/response` | POST | Tiled 扩展返回执行结果 |
| `/enqueue` | POST | 直接入队命令（可选） |

## 开发说明

### 添加新工具

在 `src/index.js` 的 `TOOLS` 数组中添加新工具定义：

```javascript
{
  name: "your_tool_name",
  description: "工具描述",
  inputSchema: {
    type: "object",
    properties: {
      // 参数定义
    },
    required: ["必填参数"]
  }
}
```

然后在 Tiled 扩展中实现对应的处理逻辑。

### 调试

```bash
# 查看服务器日志
npm start

# 测试 HTTP 端点
curl http://127.0.0.1:6510/status
```

## 注意事项

1. **必须先启动 Tiled** 并加载扩展，服务器才能正常工作
2. **端口冲突**：如果 6510-6514 都被占用，修改 `src/index.js` 中的 `PORTS` 数组
3. **防火墙**：确保本地回环地址 (127.0.0.1) 通信未被阻止
4. **Node.js 版本**：需要 Node.js 16+ 

## License

MIT
