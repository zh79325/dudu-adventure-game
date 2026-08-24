# Tiled MCP Pro

AI-powered map creation with Tiled Map Editor via the Model Context Protocol.

**122 tools** across **12 categories** for complete control over maps, layers, tiles, tilesets, objects, and export.

## What is Tiled MCP Pro?

Tiled MCP Pro connects AI assistants (Claude, etc.) directly to your running Tiled Map Editor. The AI can create maps, place tiles, manage layers, configure tilesets, and export — all through natural language.

### Tool Categories

| Category | Tools | Description |
|----------|------:|-------------|
| Map | 14 | Create, open, save, resize, configure maps |
| Layer | 16 | Tile, object, image, and group layer management |
| Tile | 12 | Place, fill, stamp, and replace tiles |
| Tileset | 12 | Create, import, and configure tilesets |
| Object | 16 | Shapes, text, tile objects, and transforms |
| Property | 8 | Custom properties on any element |
| Terrain | 8 | Wang sets, terrain types, auto-tiling |
| Editor | 10 | Views, selections, actions, editor config |
| Project | 6 | Project and world management |
| Image | 6 | Image layers and tileset images |
| Export | 6 | Multi-format export |
| Analysis | 8 | Validation, reports, diagnostics |
| **Total** | **122** | |

## Architecture

```
AI Client (Claude Code, etc.)
    ↕ stdio / MCP Protocol
Node.js MCP Server
    ↕ WebSocket
Tiled JavaScript Extension (this repo)
    ↕ Tiled Scripting API
Tiled Map Editor
```

## Installation

### Free Version (Extension Only)

This repository contains the Tiled JavaScript extension. To use it, you need the MCP server component.

1. Download or clone this repository
2. Copy the `extension/` folder to your Tiled extensions directory:
   - **Windows**: `C:\Users\<username>\AppData\Local\Tiled\extensions\tiled-mcp-pro\`
   - **macOS/Linux**: `~/.tiled/extensions/tiled-mcp-pro/`
3. Restart Tiled

### Full Version (Extension + Server)

Get the complete package with the MCP server at:

**[Buy Me a Coffee](https://buymeacoffee.com/)** (coming soon)

## Related Projects

- [Godot MCP Pro](https://godot-mcp.abyo.net/) — AI-powered Godot development (163 tools)
- [Aseprite MCP Pro](https://aseprite-mcp-pro.abyo.net/) — AI-powered pixel art creation (96 tools)

## License

MIT
