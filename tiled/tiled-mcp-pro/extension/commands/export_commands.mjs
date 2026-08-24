/**
 * Export Commands - export in various formats.
 *
 * Tiled API:
 *   tiled.mapFormats       - array of available map format names
 *   tiled.mapFormat(name)  - get a specific map format
 *   format.write(map, path) - write map in that format
 *   tiled.tilesetFormats   - array of available tileset format names
 *   tiled.tilesetFormat(name)
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
    // ── get_export_formats ────────────────────────────────────────────
    get_export_formats(_params) {
      const mapFormats = [];
      if (tiled.mapFormats) {
        for (const name of tiled.mapFormats) {
          const fmt = tiled.mapFormat(name);
          mapFormats.push({
            name: name,
            canRead: fmt ? fmt.canRead : false,
            canWrite: fmt ? fmt.canWrite : false,
          });
        }
      }

      const tilesetFormats = [];
      if (tiled.tilesetFormats) {
        for (const name of tiled.tilesetFormats) {
          const fmt = tiled.tilesetFormat(name);
          tilesetFormats.push({
            name: name,
            canRead: fmt ? fmt.canRead : false,
            canWrite: fmt ? fmt.canWrite : false,
          });
        }
      }

      return {
        mapFormats: mapFormats,
        tilesetFormats: tilesetFormats,
      };
    },

    // ── export_map ────────────────────────────────────────────────────
    export_map(params) {
      const map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      const formatName = params.format || "tmx";
      const format = tiled.mapFormat(formatName);

      if (!format) {
        throw new Error("Map format not found: " + formatName +
          ". Available: " + (tiled.mapFormats || []).join(", "));
      }

      if (!format.canWrite) {
        throw new Error("Format '" + formatName + "' does not support writing");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: formatName,
      };
    },

    // ── export_tileset ────────────────────────────────────────────────
    export_tileset(params) {
      if (!params.tileset) throw new Error("tileset name is required");
      if (!params.output_path) throw new Error("output_path is required");

      const map = requireMap();
      let ts = null;
      for (let i = 0; i < map.tilesets.length; i++) {
        if (map.tilesets[i].name === params.tileset) {
          ts = map.tilesets[i];
          break;
        }
      }
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const formatName = params.format || "tsx";
      const format = tiled.tilesetFormat(formatName);

      if (!format) {
        throw new Error("Tileset format not found: " + formatName);
      }

      if (!format.canWrite) {
        throw new Error("Format '" + formatName + "' does not support writing");
      }

      format.write(ts, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        tileset: params.tileset,
        format: formatName,
      };
    },

    // ── export_as_image ───────────────────────────────────────────────
    export_as_image(params) {
      if (!params.output_path) throw new Error("output_path is required");
      const map = requireMap();

      // Use the built-in export as image action
      // This requires the map to be saved first
      // Alternative: use the lua/js format to render
      tiled.trigger("ExportAsImage");

      return {
        success: true,
        note: "Export as Image dialog opened. For programmatic export, use export_map with an image-capable format.",
      };
    },

    // ── export_map_to_json ────────────────────────────────────────────
    export_map_to_json(params) {
      const map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      const format = tiled.mapFormat("json");
      if (!format) {
        throw new Error("JSON map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "json",
      };
    },

    // ── export_map_to_lua ─────────────────────────────────────────────
    export_map_to_lua(params) {
      const map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      const format = tiled.mapFormat("lua");
      if (!format) {
        throw new Error("Lua map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "lua",
      };
    },

    // ── export_map_to_csv ─────────────────────────────────────────────
    export_map_to_csv(params) {
      const map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      const format = tiled.mapFormat("csv");
      if (!format) {
        throw new Error("CSV map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "csv",
      };
    },
  };
}
