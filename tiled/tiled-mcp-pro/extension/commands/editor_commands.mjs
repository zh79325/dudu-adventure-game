/**
 * Editor Commands - editor state, actions, tool selection.
 *
 * Tiled API:
 *   tiled.version           - Tiled version string
 *   tiled.activeAsset       - currently active asset
 *   tiled.openAssets         - array of open assets
 *   tiled.trigger(action)    - trigger an editor action
 *   tiled.log(message)       - log to Tiled console
 *   tiled.alert(message)     - show alert dialog
 *   tiled.confirm(msg, fn)   - show confirmation dialog
 *   tiled.mapEditor          - the map editor object
 *   tiled.tilesetEditor      - the tileset editor object
 *   mapEditor.currentBrush   - currently selected brush
 *   mapEditor.currentTool    - current tool name
 */

/* global tiled */

function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

export function getCommands() {
  return {
    // ── ping ──────────────────────────────────────────────────────────
    ping(_params) {
      return { pong: true, timestamp: Date.now() };
    },

    // ── pong (heartbeat response) ─────────────────────────────────────
    pong(_params) {
      // No-op: this is just the heartbeat response
      return { ok: true };
    },

    // ── get_editor_info ───────────────────────────────────────────────
    get_editor_info(_params) {
      const info = {
        version: tiled.version,
        platform: tiled.platform,
        extensionVersion: "1.0.0",
      };

      // Active asset info
      if (tiled.activeAsset) {
        info.activeAsset = {
          fileName: tiled.activeAsset.fileName,
          isTileMap: tiled.activeAsset.isTileMap,
        };
      }

      // Count open assets
      info.openAssetCount = tiled.openAssets ? tiled.openAssets.length : 0;

      return info;
    },

    // ── get_open_assets ───────────────────────────────────────────────
    get_open_assets(_params) {
      const assets = [];
      if (tiled.openAssets) {
        for (const asset of tiled.openAssets) {
          assets.push({
            fileName: asset.fileName,
            isTileMap: asset.isTileMap,
            isActive: asset === tiled.activeAsset,
            modified: asset.modified || false,
          });
        }
      }
      return { assets: assets };
    },

    // ── trigger_action ────────────────────────────────────────────────
    trigger_action(params) {
      if (!params.action) throw new Error("action is required");
      tiled.trigger(params.action);
      return { success: true, action: params.action };
    },

    // ── log_message ───────────────────────────────────────────────────
    log_message(params) {
      const msg = params.message || "";
      tiled.log("[MCP] " + msg);
      return { success: true };
    },

    // ── get_current_tool ──────────────────────────────────────────────
    get_current_tool(_params) {
      const result = {};
      if (tiled.mapEditor) {
        result.currentTool = tiled.mapEditor.currentToolName || null;
      }
      return result;
    },

    // ── set_active_asset ──────────────────────────────────────────────
    set_active_asset(params) {
      if (!params.file_path) throw new Error("file_path is required");
      for (const asset of tiled.openAssets) {
        if (asset.fileName === params.file_path) {
          tiled.activeAsset = asset;
          return { success: true, fileName: asset.fileName };
        }
      }
      throw new Error("Asset not found: " + params.file_path);
    },

    // ── get_selected_objects ──────────────────────────────────────────
    get_selected_objects(_params) {
      const map = requireMap();
      const selected = [];
      if (tiled.mapEditor && tiled.mapEditor.selectedObjects) {
        for (const obj of tiled.mapEditor.selectedObjects) {
          selected.push({
            id: obj.id,
            name: obj.name,
            className: obj.className || "",
            x: obj.x,
            y: obj.y,
          });
        }
      }
      return { selectedObjects: selected, count: selected.length };
    },

    // ── get_selected_area ─────────────────────────────────────────────
    get_selected_area(_params) {
      const map = requireMap();
      const result = { hasSelection: false };
      if (tiled.mapEditor && tiled.mapEditor.selectedArea) {
        const area = tiled.mapEditor.selectedArea;
        if (area && area.boundingRect) {
          const rect = area.boundingRect;
          result.hasSelection = true;
          result.boundingRect = {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height,
          };
        }
      }
      return result;
    },

    // ── get_method_list ───────────────────────────────────────────────
    // Returns all registered methods - useful for debugging
    get_method_list(_params) {
      // This will be populated by the main extension after all commands are registered
      return { note: "Use the router's getMethodList() instead" };
    },

    // ── reload_extension ──────────────────────────────────────────────
    reload_extension(_params) {
      // Trigger extension reload
      tiled.trigger("ReloadScriptingExtensions");
      return {
        success: true,
        note: "Extensions are being reloaded. Connection will be re-established.",
      };
    },
  };
}
