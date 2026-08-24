/**
 * Tileset Commands - manage tilesets, tiles, collection tilesets.
 *
 * Key Tiled API:
 *   map.tilesets          - array of tilesets used by the map
 *   map.addTileset(ts)    - add a tileset to the map
 *   map.removeTileset(ts) - remove a tileset from the map
 *   tileset.tile(id)      - get tile by local ID
 *   tileset.tiles         - array of all tiles
 *   tileset.tileCount     - total number of tiles
 *   tiled.open(path)      - open .tsx file
 */

/* global tiled, Tileset, Tile */

function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

function findTileset(map, name) {
  for (let i = 0; i < map.tilesets.length; i++) {
    if (map.tilesets[i].name === name) return map.tilesets[i];
  }
  return null;
}

function describeTileset(ts) {
  return {
    name: ts.name,
    tileWidth: ts.tileWidth,
    tileHeight: ts.tileHeight,
    tileCount: ts.tileCount,
    columnCount: ts.columnCount,
    spacing: ts.tileSpacing,
    margin: ts.margin,
    imageWidth: ts.imageWidth,
    imageHeight: ts.imageHeight,
    image: ts.image || null,
    objectAlignment: ts.objectAlignment,
    isCollection: ts.tileCount > 0 && !ts.image,
  };
}

export function getCommands() {
  return {
    // ── get_tilesets ──────────────────────────────────────────────────
    get_tilesets(_params) {
      const map = requireMap();
      const tilesets = [];
      for (let i = 0; i < map.tilesets.length; i++) {
        tilesets.push(describeTileset(map.tilesets[i]));
      }
      return { tilesets: tilesets };
    },

    // ── get_tileset_info ──────────────────────────────────────────────
    get_tileset_info(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");
      const ts = findTileset(map, params.name);
      if (!ts) throw new Error("Tileset not found: " + params.name);

      const info = describeTileset(ts);

      // Include tile details
      const tiles = [];
      for (let i = 0; i < ts.tileCount; i++) {
        const tile = ts.tile(i);
        if (tile) {
          const tileInfo = {
            id: tile.id,
            width: tile.width,
            height: tile.height,
          };
          if (tile.className) tileInfo.className = tile.className;
          if (tile.probability !== undefined && tile.probability !== 1) {
            tileInfo.probability = tile.probability;
          }
          if (tile.imageFileName) tileInfo.imageFileName = tile.imageFileName;
          tiles.push(tileInfo);
        }
      }
      info.tiles = tiles;

      return info;
    },

    // ── add_tileset ───────────────────────────────────────────────────
    add_tileset(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");

      // Check if loading an existing .tsx file
      if (params.file_path) {
        const asset = tiled.open(params.file_path);
        if (!asset) throw new Error("Failed to open tileset: " + params.file_path);
        map.addTileset(asset);
        return { success: true, name: asset.name, loaded: true };
      }

      // Create a new tileset
      const ts = new Tileset(params.name);
      ts.tileWidth = params.tile_width || 32;
      ts.tileHeight = params.tile_height || 32;

      if (params.image) {
        ts.image = params.image;
      }

      if (params.tile_spacing !== undefined) ts.tileSpacing = params.tile_spacing;
      if (params.margin !== undefined) ts.margin = params.margin;
      if (params.columns !== undefined) ts.columnCount = params.columns;

      map.addTileset(ts);

      return {
        success: true,
        name: ts.name,
        tileWidth: ts.tileWidth,
        tileHeight: ts.tileHeight,
        created: true,
      };
    },

    // ── remove_tileset ────────────────────────────────────────────────
    remove_tileset(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");
      const ts = findTileset(map, params.name);
      if (!ts) throw new Error("Tileset not found: " + params.name);

      map.removeTileset(ts);
      return { success: true, name: params.name };
    },

    // ── set_tileset_image ─────────────────────────────────────────────
    set_tileset_image(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (!params.image) throw new Error("image path is required");

      const ts = findTileset(map, params.name);
      if (!ts) throw new Error("Tileset not found: " + params.name);

      ts.image = params.image;

      if (params.tile_width) ts.tileWidth = params.tile_width;
      if (params.tile_height) ts.tileHeight = params.tile_height;
      if (params.tile_spacing !== undefined) ts.tileSpacing = params.tile_spacing;
      if (params.margin !== undefined) ts.margin = params.margin;

      return {
        success: true,
        name: ts.name,
        image: ts.image,
        tileCount: ts.tileCount,
      };
    },

    // ── add_tile_to_collection ────────────────────────────────────────
    add_tile_to_collection(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const tile = ts.addTile();

      if (params.image) {
        tile.imageFileName = params.image;
      }
      if (params.class_name) {
        tile.className = params.class_name;
      }

      return {
        success: true,
        tileset: params.tileset,
        tileId: tile.id,
        tileCount: ts.tileCount,
      };
    },

    // ── remove_tile_from_collection ───────────────────────────────────
    remove_tile_from_collection(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      ts.removeTiles([tile]);

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        tileCount: ts.tileCount,
      };
    },

    // ── set_tile_class ────────────────────────────────────────────────
    set_tile_class(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      tile.className = params.class_name || "";

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        className: tile.className,
      };
    },

    // ── set_tile_probability ──────────────────────────────────────────
    set_tile_probability(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");
      if (params.probability === undefined) throw new Error("probability is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      tile.probability = params.probability;

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        probability: tile.probability,
      };
    },

    // ── get_tile_info ─────────────────────────────────────────────────
    get_tile_info(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      const info = {
        id: tile.id,
        width: tile.width,
        height: tile.height,
        className: tile.className || "",
        probability: tile.probability,
        imageFileName: tile.imageFileName || null,
      };

      // Collect custom properties
      const props = {};
      for (const key of Object.keys(tile.resolvedProperties())) {
        props[key] = tile.resolvedProperty(key);
      }
      if (Object.keys(props).length > 0) {
        info.properties = props;
      }

      // Animation frames
      if (tile.frames && tile.frames.length > 0) {
        info.animation = tile.frames.map(function (f) {
          return { tileId: f.tileId, duration: f.duration };
        });
      }

      // Object group (collision shapes)
      if (tile.objectGroup) {
        info.objectCount = tile.objectGroup.objectCount;
      }

      return info;
    },
  };
}
