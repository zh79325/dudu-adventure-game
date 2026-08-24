/// <reference types="@mapeditor/tiled-api" />

/**
 * Tiled MCP Pro Extension
 *
 * Main entry point. Polls the MCP server's HTTP endpoint for commands
 * and sends responses back via HTTP POST.
 *
 * Architecture:
 *   Claude <-stdio-> MCP Server (TypeScript, HTTP on :6510) <-HTTP (curl)-> This Extension
 *
 * Polling strategy (Tiled's QJSEngine has no setInterval):
 *   1. Signal-driven: activeAssetChanged, assetSaved, assetOpened, etc.
 *   2. Manual: Ctrl+Shift+M keyboard shortcut
 *   3. Each poll uses Process.exec("curl") which blocks but is ~10ms to localhost
 */

/* global tiled, Process, TextFile, FileInfo, Qt, __filename */

// CRITICAL: __filename is deleted after module evaluation.
// Must capture at top level immediately.
const EXTENSION_FILE = (typeof __filename !== "undefined") ? __filename : "";
const EXTENSION_DIR = (() => {
  if (!EXTENSION_FILE) return "";
  const lastSlash = Math.max(EXTENSION_FILE.lastIndexOf("/"), EXTENSION_FILE.lastIndexOf("\\"));
  return EXTENSION_FILE.substring(0, lastSlash);
})();

import { CommandRouter } from "./command_router.mjs";
import { getCommands as getMapCommands } from "./commands/map_commands.mjs";
import { getCommands as getLayerCommands } from "./commands/layer_commands.mjs";
import { getCommands as getTileCommands } from "./commands/tile_commands.mjs";
import { getCommands as getTilesetCommands } from "./commands/tileset_commands.mjs";
import { getCommands as getObjectCommands } from "./commands/object_commands.mjs";
import { getCommands as getPropertyCommands } from "./commands/property_commands.mjs";
import { getCommands as getTerrainCommands } from "./commands/terrain_commands.mjs";
import { getCommands as getEditorCommands } from "./commands/editor_commands.mjs";
import { getCommands as getProjectCommands } from "./commands/project_commands.mjs";
import { getCommands as getImageCommands } from "./commands/image_commands.mjs";
import { getCommands as getExportCommands } from "./commands/export_commands.mjs";
import { getCommands as getAnalysisCommands } from "./commands/analysis_commands.mjs";

// ── Router Setup ─────────────────────────────────────────────────────────

const router = new CommandRouter();

router.register(getMapCommands());
router.register(getLayerCommands());
router.register(getTileCommands());
router.register(getTilesetCommands());
router.register(getObjectCommands());
router.register(getPropertyCommands());
router.register(getTerrainCommands());
router.register(getEditorCommands());
router.register(getProjectCommands());
router.register(getImageCommands());
router.register(getExportCommands());
router.register(getAnalysisCommands());

// Override get_method_list to use the router
router.handlers["get_method_list"] = function (_params) {
  return { methods: router.getMethodList() };
};

tiled.log("[Tiled MCP] Registered " + router.getMethodList().length + " command handlers");

// ── HTTP Communication ──────────────────────────────────────────────────

const PORTS = [6510, 6511, 6512, 6513, 6514];
let activePort = null;  // Cached working port
let mcpConnected = false;
let polling = false;  // Guard against re-entrant polling

/**
 * Execute curl and return stdout. Process.exec blocks until done.
 * Returns null on failure.
 */
function curlGet(url) {
  const p = new Process();
  try {
    p.exec("curl", ["-s", "--max-time", "3", url], true);
    const exitCode = p.exitCode;
    if (exitCode !== 0) {
      return null;
    }
    const output = p.readStdOut();
    return output || null;
  } catch (e) {
    return null;
  } finally {
    p.close();
  }
}

/**
 * Execute curl POST with JSON body. Returns true on success.
 */
function curlPost(url, data) {
  const p = new Process();
  try {
    const jsonStr = JSON.stringify(data);
    p.exec("curl", [
      "-s",
      "--max-time", "3",
      "-X", "POST",
      "-H", "Content-Type: application/json",
      "-d", jsonStr,
      url
    ], true);
    const exitCode = p.exitCode;
    return exitCode === 0;
  } catch (e) {
    return false;
  } finally {
    p.close();
  }
}

/**
 * Find the MCP server by trying ports in order.
 * Returns the working port or null.
 */
function discoverPort() {
  // Try cached port first
  if (activePort !== null) {
    const result = curlGet("http://127.0.0.1:" + activePort + "/status");
    if (result !== null) {
      return activePort;
    }
    activePort = null;
  }

  // Try all ports
  for (let i = 0; i < PORTS.length; i++) {
    const port = PORTS[i];
    const result = curlGet("http://127.0.0.1:" + port + "/status");
    if (result !== null) {
      tiled.log("[Tiled MCP] Found MCP server on port " + port);
      activePort = port;
      return port;
    }
  }
  return null;
}

/**
 * Poll for pending commands from the MCP server.
 * GET /poll → array of JSON-RPC requests
 * Process each → POST /response with JSON-RPC response
 */
function pollCommands() {
  if (polling) return;
  polling = true;

  try {
    const port = discoverPort();
    if (port === null) {
      if (mcpConnected) {
        tiled.log("[Tiled MCP] Lost connection to MCP server");
        mcpConnected = false;
      }
      return;
    }

    if (!mcpConnected) {
      tiled.log("[Tiled MCP] Connected to MCP server on port " + port);
      mcpConnected = true;
    }

    const baseUrl = "http://127.0.0.1:" + port;

    // Poll for commands
    const rawResponse = curlGet(baseUrl + "/poll");
    if (!rawResponse || rawResponse.trim().length === 0) return;

    let commands;
    try {
      commands = JSON.parse(rawResponse);
    } catch (e) {
      tiled.log("[Tiled MCP] Failed to parse poll response: " + e);
      return;
    }

    if (!Array.isArray(commands) || commands.length === 0) return;

    tiled.log("[Tiled MCP] Received " + commands.length + " command(s)");

    // Process each command
    for (let i = 0; i < commands.length; i++) {
      const request = commands[i];
      const response = processCommand(request);
      if (response) {
        const ok = curlPost(baseUrl + "/response", response);
        if (!ok) {
          tiled.log("[Tiled MCP] Failed to send response for " + request.method);
        }
      }
    }
  } finally {
    polling = false;
  }
}

// ── Command Processing ───────────────────────────────────────────────────

/**
 * Process a single JSON-RPC request and return the response object.
 */
function processCommand(request) {
  const method = request.method;
  const params = request.params || {};
  const id = request.id;

  // Handle ping specially (heartbeat from server)
  if (method === "ping") {
    return {
      jsonrpc: "2.0",
      result: { ok: true },
      id: id,
    };
  }

  if (!router.hasMethod(method)) {
    tiled.log("[Tiled MCP] Unknown method: " + method);
    return {
      jsonrpc: "2.0",
      error: {
        code: -32601,
        message: "Method not found: " + method,
      },
      id: id,
    };
  }

  try {
    const result = router.execute(method, params);
    return {
      jsonrpc: "2.0",
      result: result,
      id: id,
    };
  } catch (e) {
    const errorMsg = e.message || String(e);
    tiled.log("[Tiled MCP] Command error (" + method + "): " + errorMsg);
    return {
      jsonrpc: "2.0",
      error: {
        code: -32603,
        message: errorMsg,
      },
      id: id,
    };
  }
}

// ── Signal-driven Polling ────────────────────────────────────────────────
// These ensure commands are processed whenever something happens in Tiled.

if (tiled.activeAssetChanged) {
  tiled.activeAssetChanged.connect(function () {
    pollCommands();
  });
}

if (tiled.assetOpened) {
  tiled.assetOpened.connect(function () {
    pollCommands();
  });
}

if (tiled.assetSaved) {
  tiled.assetSaved.connect(function () {
    pollCommands();
  });
}

if (tiled.assetAboutToBeSaved) {
  tiled.assetAboutToBeSaved.connect(function () {
    pollCommands();
  });
}

if (tiled.assetAboutToBeClosed) {
  tiled.assetAboutToBeClosed.connect(function () {
    pollCommands();
  });
}

// ── Menu Actions ─────────────────────────────────────────────────────────

// Manual poll action with keyboard shortcut
let pollAction = tiled.registerAction("TiledMCP_Poll", function () {
  pollCommands();
});
pollAction.text = "MCP: Check for Commands";
pollAction.shortcut = "Ctrl+Shift+M";

// Reconnect action (re-discover port)
let reconnectAction = tiled.registerAction("TiledMCP_Reconnect", function () {
  tiled.log("[Tiled MCP] Reconnecting...");
  activePort = null;
  mcpConnected = false;
  pollCommands();
});
reconnectAction.text = "MCP: Reconnect";

// Status action
let statusAction = tiled.registerAction("TiledMCP_Status", function () {
  const status = mcpConnected
    ? "Connected (port " + activePort + ")"
    : "Not connected";
  const methods = router.getMethodList().length + " methods registered";
  tiled.log("[Tiled MCP] Status: " + status + ", " + methods);
  tiled.alert(
    "Tiled MCP Pro Status\n\n" +
    status + "\n" +
    methods + "\n\n" +
    "Version: 1.0.0\n" +
    "Architecture: HTTP polling"
  );
});
statusAction.text = "MCP: Show Status";

// Add to Map menu
tiled.extendMenu("Map", [
  { separator: true },
  { action: "TiledMCP_Poll" },
  { action: "TiledMCP_Reconnect" },
  { action: "TiledMCP_Status" },
]);

// ── Extension Lifecycle ──────────────────────────────────────────────────

// Try initial connection
pollCommands();

tiled.log(
  "[Tiled MCP Pro] Extension loaded - " +
  router.getMethodList().length +
  " commands available, HTTP polling on ports 6510-6514"
);
