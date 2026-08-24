const express = require('express');
const readline = require('readline');

// ══════════════════════════════════════════════════════════════════════════
// Configuration
// ══════════════════════════════════════════════════════════════════════════

const PORTS = [6510, 6511, 6512, 6513, 6514];
const HEARTBEAT_INTERVAL = 10000; // 10 seconds

// ══════════════════════════════════════════════════════════════════════════
// State Management
// ══════════════════════════════════════════════════════════════════════════

let commandQueue = [];      // Commands waiting for Tiled to poll
let responseBuffer = [];    // Responses from Tiled waiting to send to AI
let activePort = null;      // Current working port
let mcpConnected = false;   // Whether AI client is connected via stdio
let lastHeartbeat = Date.now();

// ══════════════════════════════════════════════════════════════════════════
// Tool Definitions (Simplified - Core Map Operations)
// ══════════════════════════════════════════════════════════════════════════

const TOOLS = [
  {
    name: "create_map",
    description: "Create a new Tiled map with specified dimensions and orientation",
    inputSchema: {
      type: "object",
      properties: {
        width: { type: "integer", description: "Map width in tiles" },
        height: { type: "integer", description: "Map height in tiles" },
        tileWidth: { type: "integer", description: "Tile width in pixels", default: 32 },
        tileHeight: { type: "integer", description: "Tile height in pixels", default: 32 },
        orientation: { 
          type: "string", 
          enum: ["orthogonal", "isometric", "staggered", "hexagonal"],
          default: "orthogonal"
        }
      },
      required: ["width", "height"]
    }
  },
  {
    name: "open_map",
    description: "Open an existing map file in Tiled",
    inputSchema: {
      type: "object",
      properties: {
        filePath: { type: "string", description: "Path to the .tmx or .json map file" }
      },
      required: ["filePath"]
    }
  },
  {
    name: "save_map",
    description: "Save the current map to disk",
    inputSchema: {
      type: "object",
      properties: {
        filePath: { type: "string", description: "Optional path to save as (defaults to current file)" }
      }
    }
  },
  {
    name: "create_tile_layer",
    description: "Create a new tile layer in the current map",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string", description: "Layer name" },
        visible: { type: "boolean", default: true },
        opacity: { type: "number", minimum: 0, maximum: 1, default: 1 }
      },
      required: ["name"]
    }
  },
  {
    name: "place_tile",
    description: "Place a tile at specific coordinates",
    inputSchema: {
      type: "object",
      properties: {
        layer: { type: "string", description: "Target layer name" },
        x: { type: "integer", description: "X coordinate (tile units)" },
        y: { type: "integer", description: "Y coordinate (tile units)" },
        tilesetId: { type: "string", description: "Tileset name or ID" },
        tileId: { type: "integer", description: "Tile ID within the tileset" }
      },
      required: ["layer", "x", "y", "tilesetId", "tileId"]
    }
  },
  {
    name: "fill_tiles",
    description: "Fill a rectangular area with the same tile",
    inputSchema: {
      type: "object",
      properties: {
        layer: { type: "string", description: "Target layer name" },
        x: { type: "integer", description: "Start X coordinate" },
        y: { type: "integer", description: "Start Y coordinate" },
        width: { type: "integer", description: "Fill width in tiles" },
        height: { type: "integer", description: "Fill height in tiles" },
        tilesetId: { type: "string", description: "Tileset name" },
        tileId: { type: "integer", description: "Tile ID" }
      },
      required: ["layer", "x", "y", "width", "height", "tilesetId", "tileId"]
    }
  },
  {
    name: "create_object",
    description: "Create an object (rectangle, point, etc.) on an object layer",
    inputSchema: {
      type: "object",
      properties: {
        layer: { type: "string", description: "Object layer name" },
        name: { type: "string", description: "Object name" },
        type: { type: "string", enum: ["rectangle", "ellipse", "point", "polygon"], default: "rectangle" },
        x: { type: "number", description: "X position" },
        y: { type: "number", description: "Y position" },
        width: { type: "number", description: "Width (for rectangle/ellipse)" },
        height: { type: "number", description: "Height (for rectangle/ellipse)" }
      },
      required: ["layer", "name", "x", "y"]
    }
  },
  {
    name: "export_map",
    description: "Export the current map to a specific format",
    inputSchema: {
      type: "object",
      properties: {
        format: { type: "string", enum: ["json", "csv", "xml", "png"], default: "json" },
        outputPath: { type: "string", description: "Output file path" }
      },
      required: ["outputPath"]
    }
  },
  {
    name: "get_map_info",
    description: "Get information about the current map",
    inputSchema: {
      type: "object",
      properties: {}
    }
  },
  {
    name: "ping",
    description: "Heartbeat check - returns ok if server is running",
    inputSchema: {
      type: "object",
      properties: {}
    }
  }
];

// ══════════════════════════════════════════════════════════════════════════
// HTTP Server Setup
// ══════════════════════════════════════════════════════════════════════════

function createServer(port) {
  const app = express();
  app.use(express.json({ limit: '10mb' }));

  // Health check endpoint
  app.get('/status', (req, res) => {
    res.json({
      status: 'ok',
      port: port,
      connected: mcpConnected,
      queuedCommands: commandQueue.length,
      pendingResponses: responseBuffer.length,
      uptime: Math.floor((Date.now() - startTime) / 1000)
    });
  });

  // Poll endpoint - Tiled extension calls this to get commands
  app.get('/poll', (req, res) => {
    const commands = [...commandQueue];
    commandQueue = []; // Clear queue after sending
    
    // Update heartbeat
    lastHeartbeat = Date.now();
    
    res.json(commands);
  });

  // Response endpoint - Tiled extension posts execution results here
  app.post('/response', (req, res) => {
    const responseData = req.body;
    responseBuffer.push(responseData);
    
    // Forward to AI client via stdio
    forwardToAIClient(responseData);
    
    res.json({ ok: true });
  });

  // Enqueue endpoint - AI can also push commands directly (optional)
  app.post('/enqueue', (req, res) => {
    commandQueue.push(req.body);
    res.json({ ok: true, queued: commandQueue.length });
  });

  return app;
}

// ══════════════════════════════════════════════════════════════════════════
// Port Discovery & Server Start
// ══════════════════════════════════════════════════════════════════════════

async function findAvailablePort() {
  for (const port of PORTS) {
    try {
      await new Promise((resolve, reject) => {
        const testApp = express();
        const server = testApp.listen(port, () => {
          server.close(() => resolve(port));
        });
        server.on('error', () => reject());
      });
      return port;
    } catch (e) {
      continue;
    }
  }
  throw new Error(`No available ports in range ${PORTS.join(', ')}`);
}

// ══════════════════════════════════════════════════════════════════════════
// MCP Protocol over stdio
// ══════════════════════════════════════════════════════════════════════════

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

function sendToAI(message) {
  const jsonStr = JSON.stringify(message);
  process.stdout.write(jsonStr + '\n');
}

function forwardToAIClient(responseData) {
  // Convert Tiled response to MCP-compatible format
  const mcpResponse = {
    jsonrpc: "2.0",
    result: responseData.result || responseData,
    id: responseData.id || null
  };
  sendToAI(mcpResponse);
}

// Handle incoming messages from AI client via stdin
rl.on('line', (line) => {
  try {
    const message = JSON.parse(line);
    handleMCPMessage(message);
  } catch (e) {
    console.error(`[MCP Server] Failed to parse stdin: ${e.message}`);
  }
});

function handleMCPMessage(message) {
  const { method, params, id } = message;

  // Handle MCP initialization
  if (method === 'initialize') {
    mcpConnected = true;
    sendToAI({
      jsonrpc: "2.0",
      result: {
        protocolVersion: "2024-11-05",
        capabilities: {
          tools: { listChanged: false }
        },
        serverInfo: {
          name: "Tiled MCP Server",
          version: "1.0.0"
        }
      },
      id: id
    });
    return;
  }

  // Handle tool listing
  if (method === 'tools/list') {
    sendToAI({
      jsonrpc: "2.0",
      result: { tools: TOOLS },
      id: id
    });
    return;
  }

  // Handle tool calls
  if (method === 'tools/call') {
    const { name, arguments: args } = params || {};
    
    // Validate tool exists
    const tool = TOOLS.find(t => t.name === name);
    if (!tool) {
      sendToAI({
        jsonrpc: "2.0",
        error: {
          code: -32601,
          message: `Tool not found: ${name}`
        },
        id: id
      });
      return;
    }

    // Create JSON-RPC command for Tiled
    const tiledCommand = {
      jsonrpc: "2.0",
      method: name,
      params: args || {},
      id: id
    };

    // Queue the command for Tiled to pick up
    commandQueue.push(tiledCommand);
    
    // Acknowledge immediately (actual result comes via /response)
    sendToAI({
      jsonrpc: "2.0",
      result: {
        content: [{
          type: "text",
          text: `Command "${name}" queued for Tiled execution`
        }]
      },
      id: id
    });
    return;
  }

  // Handle ping/heartbeat
  if (method === 'ping') {
    lastHeartbeat = Date.now();
    sendToAI({
      jsonrpc: "2.0",
      result: { ok: true },
      id: id
    });
    return;
  }

  // Unknown method
  sendToAI({
    jsonrpc: "2.0",
    error: {
      code: -32601,
      message: `Method not found: ${method}`
    },
    id: id
  });
}

// ══════════════════════════════════════════════════════════════════════════
// Heartbeat Monitor
// ══════════════════════════════════════════════════════════════════════════

function startHeartbeatMonitor() {
  setInterval(() => {
    const timeSinceLastBeat = Date.now() - lastHeartbeat;
    if (timeSinceLastBeat > HEARTBEAT_INTERVAL * 2 && mcpConnected) {
      console.log('[MCP Server] Warning: No heartbeat received from Tiled');
      mcpConnected = false;
    }
  }, HEARTBEAT_INTERVAL);
}

// ══════════════════════════════════════════════════════════════════════════
// Main Entry Point
// ══════════════════════════════════════════════════════════════════════════

const startTime = Date.now();

async function main() {
  try {
    // Find available port
    activePort = await findAvailablePort();
    console.log(`[MCP Server] Starting on port ${activePort}`);

    // Create and start HTTP server
    const app = createServer(activePort);
    const server = app.listen(activePort, () => {
      console.log(`[MCP Server] HTTP server listening on http://127.0.0.1:${activePort}`);
      console.log(`[MCP Server] Available tools: ${TOOLS.length}`);
      console.log(`[MCP Server] Waiting for AI client connection via stdio...`);
    });

    // Start heartbeat monitoring
    startHeartbeatMonitor();

    // Graceful shutdown
    process.on('SIGINT', () => {
      console.log('\n[MCP Server] Shutting down...');
      server.close();
      process.exit(0);
    });

  } catch (error) {
    console.error(`[MCP Server] Failed to start: ${error.message}`);
    process.exit(1);
  }
}

main();
