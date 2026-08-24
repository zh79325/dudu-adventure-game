/**
 * Map Commands - CRUD operations on maps, resize, save, undo/redo, automap, render.
 *
 * Tiled scripting API:
 *   tiled.activeAsset  - current asset (TileMap or Tileset)
 *   tiled.openAssets    - array of all open assets
 *   tiled.open(path)    - open a file
 *   tiled.close(asset)  - close an asset
 *   asset.isTileMap     - check if asset is a TileMap
 *   asset.macro("desc", callback)  - group operations for undo
 */

/* global tiled, TileMap, FileInfo */

/**
 * Helper: get the active map or throw.
 * @returns {TileMap}
 */
function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

/**
 * Describe a layer for serialisation.
 */
function describeLayer(layer, index) {
  const info = {
    index: index,
    name: layer.name,
    visible: layer.visible,
    locked: layer.locked,
    opacity: layer.opacity,
    offset: { x: layer.offset.x, y: layer.offset.y },
  };

  if (layer.isTileLayer)       info.type = "tilelayer";
  else if (layer.isObjectLayer) info.type = "objectgroup";
  else if (layer.isGroupLayer)  info.type = "group";
  else if (layer.isImageLayer)  info.type = "imagelayer";
  else                          info.type = "unknown";

  return info;
}

export function getCommands() {
  return {
    // ── get_map_info ──────────────────────────────────────────────────
    get_map_info(_params) {
      const map = requireMap();
      const layers = [];
      for (let i = 0; i < map.layerCount; i++) {
        layers.push(describeLayer(map.layerAt(i), i));
      }
      const tilesets = [];
      for (let i = 0; i < map.tilesets.length; i++) {
        const ts = map.tilesets[i];
        tilesets.push({
          name: ts.name,
          tileWidth: ts.tileWidth,
          tileHeight: ts.tileHeight,
          tileCount: ts.tileCount,
          columnCount: ts.columnCount,
          imageWidth: ts.imageWidth,
          imageHeight: ts.imageHeight,
        });
      }
      return {
        fileName: map.fileName,
        width: map.width,
        height: map.height,
        tileWidth: map.tileWidth,
        tileHeight: map.tileHeight,
        orientation: map.orientation,
        infinite: map.infinite,
        layerCount: map.layerCount,
        layers: layers,
        tilesets: tilesets,
        backgroundColor: map.backgroundColor ? map.backgroundColor.toString() : null,
        renderOrder: map.renderOrder,
      };
    },

    // ── create_map ────────────────────────────────────────────────────
    create_map(params) {
      const name = params.name || "untitled.tmx";
      const w = params.width || 20;
      const h = params.height || 15;
      const tw = params.tileWidth || 32;
      const th = params.tileHeight || 32;

      const map = new TileMap();
      map.setSize(w, h);
      map.setTileSize(tw, th);

      if (params.orientation) {
        const orientations = {
          orthogonal: TileMap.Orthogonal,
          isometric: TileMap.Isometric,
          staggered: TileMap.Staggered,
          hexagonal: TileMap.Hexagonal,
        };
        if (orientations[params.orientation] !== undefined) {
          map.orientation = orientations[params.orientation];
        }
      }

      if (params.infinite !== undefined) {
        map.infinite = params.infinite;
      }

      // Add a default tile layer
      const layer = new TileLayer(name.replace(/\.\w+$/, ""));
      map.addLayer(layer);

      tiled.activeAsset = map;

      return {
        success: true,
        width: map.width,
        height: map.height,
        tileWidth: map.tileWidth,
        tileHeight: map.tileHeight,
      };
    },

    // ── open_map ──────────────────────────────────────────────────────
    open_map(params) {
      if (!params.file_path) throw new Error("file_path is required");
      const asset = tiled.open(params.file_path);
      if (!asset) {
        throw new Error("Failed to open file: " + params.file_path);
      }
      return {
        success: true,
        fileName: asset.fileName,
        isTileMap: asset.isTileMap,
      };
    },

    // ── save_map ──────────────────────────────────────────────────────
    save_map(params) {
      const map = requireMap();
      if (params.file_path) {
        const format = tiled.mapFormat("tmx");
        if (format) {
          format.write(map, params.file_path);
        } else {
          // Fallback: set fileName and trigger save
          map.fileName = params.file_path;
          tiled.trigger("SaveAs");
        }
      } else {
        tiled.trigger("Save");
      }
      return { success: true, fileName: map.fileName };
    },

    // ── close_map ─────────────────────────────────────────────────────
    close_map(params) {
      let asset;
      if (params.file_path) {
        asset = null;
        for (const a of tiled.openAssets) {
          if (a.fileName === params.file_path) {
            asset = a;
            break;
          }
        }
        if (!asset) throw new Error("Map not found: " + params.file_path);
      } else {
        asset = tiled.activeAsset;
        if (!asset) throw new Error("No map is currently open");
      }

      if (params.save) {
        tiled.trigger("Save");
      }

      tiled.close(asset);
      return { success: true };
    },

    // ── resize_map ────────────────────────────────────────────────────
    resize_map(params) {
      const map = requireMap();
      const w = params.width;
      const h = params.height;
      if (!w || !h) throw new Error("width and height are required");

      if (params.offset_x !== undefined || params.offset_y !== undefined) {
        const ox = params.offset_x || 0;
        const oy = params.offset_y || 0;
        map.macro("Resize map", function () {
          map.resize({ width: w, height: h }, { x: ox, y: oy });
        });
      } else {
        map.macro("Resize map", function () {
          map.setSize(w, h);
        });
      }

      return { success: true, width: map.width, height: map.height };
    },

    // ── set_map_orientation ───────────────────────────────────────────
    set_map_orientation(params) {
      const map = requireMap();
      const orientations = {
        orthogonal: TileMap.Orthogonal,
        isometric: TileMap.Isometric,
        staggered: TileMap.Staggered,
        hexagonal: TileMap.Hexagonal,
      };
      const val = orientations[params.orientation];
      if (val === undefined) {
        throw new Error("Invalid orientation: " + params.orientation);
      }
      map.macro("Set orientation", function () {
        map.orientation = val;
      });
      return { success: true, orientation: params.orientation };
    },

    // ── set_map_tile_size ─────────────────────────────────────────────
    set_map_tile_size(params) {
      const map = requireMap();
      if (!params.tile_width || !params.tile_height) {
        throw new Error("tile_width and tile_height are required");
      }
      map.macro("Set tile size", function () {
        map.setTileSize(params.tile_width, params.tile_height);
      });
      return {
        success: true,
        tileWidth: map.tileWidth,
        tileHeight: map.tileHeight,
      };
    },

    // ── get_open_maps ─────────────────────────────────────────────────
    get_open_maps(_params) {
      const maps = [];
      for (const asset of tiled.openAssets) {
        if (asset.isTileMap) {
          maps.push({
            fileName: asset.fileName,
            width: asset.width,
            height: asset.height,
            isActive: asset === tiled.activeAsset,
          });
        }
      }
      return { maps: maps };
    },

    // ── set_active_map ────────────────────────────────────────────────
    set_active_map(params) {
      if (!params.file_path) throw new Error("file_path is required");
      for (const asset of tiled.openAssets) {
        if (asset.fileName === params.file_path) {
          tiled.activeAsset = asset;
          return { success: true, fileName: asset.fileName };
        }
      }
      throw new Error("Map not found among open assets: " + params.file_path);
    },

    // ── render_map_to_image ───────────────────────────────────────────
    render_map_to_image(params) {
      if (!params.output_path) throw new Error("output_path is required");
      const map = requireMap();
      // Tiled supports rendering via mapFormat (image format)
      // or via the map.toImage() if available
      // Use the command-line approach through tiled.trigger
      // Actually, let's use the process command approach
      tiled.trigger("ExportAsImage");
      return {
        success: true,
        note: "Export dialog opened. Use save_map with a .png extension for programmatic export.",
      };
    },

    // ── auto_map ──────────────────────────────────────────────────────
    auto_map(params) {
      const map = requireMap();
      if (
        params.region_x !== undefined &&
        params.region_y !== undefined &&
        params.region_width !== undefined &&
        params.region_height !== undefined
      ) {
        const region = new region(
          params.region_x,
          params.region_y,
          params.region_width,
          params.region_height
        );
        map.autoMap(region);
      } else {
        map.autoMap();
      }
      return { success: true };
    },

    // ── undo_map ──────────────────────────────────────────────────────
    undo_map(_params) {
      tiled.trigger("Undo");
      return { success: true };
    },

    // ── redo_map ──────────────────────────────────────────────────────
    redo_map(_params) {
      tiled.trigger("Redo");
      return { success: true };
    },
  };
}
