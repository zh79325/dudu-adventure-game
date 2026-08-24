/**
 * Tile Commands - get/set tiles, fill, flood fill, stamp, replace, clear.
 *
 * Key Tiled API:
 *   tileLayer.cellAt(x, y) → { tileId, flippedHorizontally, etc. }
 *   tileLayer.edit() → TileLayerEdit
 *   edit.setTile(x, y, tile, flags) / edit.setTile(x, y, tile)
 *   edit.apply()
 *   tileset.tile(localId) → Tile
 */

/* global tiled */

function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

function findLayer(map, name) {
  function search(parent) {
    for (let i = 0; i < parent.layerCount; i++) {
      const layer = parent.layerAt(i);
      if (layer.name === name) return layer;
      if (layer.isGroupLayer) {
        const found = search(layer);
        if (found) return found;
      }
    }
    return null;
  }
  return search(map);
}

function requireTileLayer(map, name) {
  const layer = findLayer(map, name);
  if (!layer) throw new Error("Layer not found: " + name);
  if (!layer.isTileLayer) throw new Error("Layer is not a tile layer: " + name);
  return layer;
}

function findTileset(map, name) {
  for (let i = 0; i < map.tilesets.length; i++) {
    if (map.tilesets[i].name === name) return map.tilesets[i];
  }
  return null;
}

function requireTileset(map, name) {
  const ts = findTileset(map, name);
  if (!ts) throw new Error("Tileset not found: " + name);
  return ts;
}

function getTileFromTileset(tileset, tileId) {
  const tile = tileset.tile(tileId);
  if (!tile) throw new Error("Tile ID " + tileId + " not found in tileset " + tileset.name);
  return tile;
}

export function getCommands() {
  return {
    // ── get_tile ──────────────────────────────────────────────────────
    get_tile(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }

      const layer = requireTileLayer(map, params.layer);
      const cell = layer.cellAt(params.x, params.y);

      if (!cell || cell.tileId === -1) {
        return {
          x: params.x,
          y: params.y,
          empty: true,
          tileId: -1,
        };
      }

      // Find which tileset this tile belongs to
      let tilesetName = null;
      let localId = cell.tileId;
      const tile = layer.tileAt(params.x, params.y);
      if (tile && tile.tileset) {
        tilesetName = tile.tileset.name;
        localId = tile.id;
      }

      return {
        x: params.x,
        y: params.y,
        empty: false,
        tileId: cell.tileId,
        localTileId: localId,
        tileset: tilesetName,
        flippedHorizontally: cell.flippedHorizontally || false,
        flippedVertically: cell.flippedVertically || false,
        flippedAntiDiagonally: cell.flippedAntiDiagonally || false,
        rotatedHexagonal120: cell.rotatedHexagonal120 || false,
      };
    },

    // ── set_tile ──────────────────────────────────────────────────────
    set_tile(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const layer = requireTileLayer(map, params.layer);
      const tileset = requireTileset(map, params.tileset);
      const tile = getTileFromTileset(tileset, params.tile_id);

      map.macro("Set tile", function () {
        const edit = layer.edit();
        edit.setTile(params.x, params.y, tile);
        edit.apply();
      });

      return {
        success: true,
        x: params.x,
        y: params.y,
        tileset: params.tileset,
        tile_id: params.tile_id,
      };
    },

    // ── fill_rect ─────────────────────────────────────────────────────
    fill_rect(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) throw new Error("width and height are required");
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const layer = requireTileLayer(map, params.layer);
      const tileset = requireTileset(map, params.tileset);
      const tile = getTileFromTileset(tileset, params.tile_id);

      const count = params.width * params.height;

      map.macro("Fill rectangle", function () {
        const edit = layer.edit();
        for (let dy = 0; dy < params.height; dy++) {
          for (let dx = 0; dx < params.width; dx++) {
            edit.setTile(params.x + dx, params.y + dy, tile);
          }
        }
        edit.apply();
      });

      return {
        success: true,
        tilesPlaced: count,
        region: {
          x: params.x,
          y: params.y,
          width: params.width,
          height: params.height,
        },
      };
    },

    // ── clear_rect ────────────────────────────────────────────────────
    clear_rect(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) throw new Error("width and height are required");

      const layer = requireTileLayer(map, params.layer);

      map.macro("Clear rectangle", function () {
        const edit = layer.edit();
        for (let dy = 0; dy < params.height; dy++) {
          for (let dx = 0; dx < params.width; dx++) {
            edit.setTile(params.x + dx, params.y + dy, null);
          }
        }
        edit.apply();
      });

      return {
        success: true,
        tilesCleared: params.width * params.height,
      };
    },

    // ── clear_layer ───────────────────────────────────────────────────
    clear_layer(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = requireTileLayer(map, params.layer);

      map.macro("Clear layer", function () {
        const edit = layer.edit();
        for (let y = 0; y < map.height; y++) {
          for (let x = 0; x < map.width; x++) {
            edit.setTile(x, y, null);
          }
        }
        edit.apply();
      });

      return { success: true, layer: params.layer };
    },

    // ── get_used_tiles ────────────────────────────────────────────────
    get_used_tiles(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = requireTileLayer(map, params.layer);
      const tiles = [];

      for (let y = 0; y < map.height; y++) {
        for (let x = 0; x < map.width; x++) {
          const cell = layer.cellAt(x, y);
          if (!cell || cell.tileId === -1) continue;

          const tile = layer.tileAt(x, y);
          const tilesetName = tile && tile.tileset ? tile.tileset.name : null;
          const localId = tile ? tile.id : cell.tileId;

          // Apply filters
          if (params.tileset && tilesetName !== params.tileset) continue;
          if (params.tile_id !== undefined && localId !== params.tile_id) continue;

          tiles.push({
            x: x,
            y: y,
            tileId: cell.tileId,
            localTileId: localId,
            tileset: tilesetName,
          });
        }
      }

      return { layer: params.layer, tiles: tiles, count: tiles.length };
    },

    // ── set_tiles_batch ───────────────────────────────────────────────
    set_tiles_batch(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (!params.tiles || !Array.isArray(params.tiles)) {
        throw new Error("tiles array is required");
      }

      const layer = requireTileLayer(map, params.layer);

      // Pre-resolve all tilesets and tiles
      const resolvedTiles = params.tiles.map(function (t) {
        const tileset = requireTileset(map, t.tileset);
        const tile = getTileFromTileset(tileset, t.tile_id);
        return { x: t.x, y: t.y, tile: tile };
      });

      map.macro("Set tiles batch", function () {
        const edit = layer.edit();
        for (const t of resolvedTiles) {
          edit.setTile(t.x, t.y, t.tile);
        }
        edit.apply();
      });

      return {
        success: true,
        tilesPlaced: resolvedTiles.length,
      };
    },

    // ── flood_fill ────────────────────────────────────────────────────
    flood_fill(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      const layer = requireTileLayer(map, params.layer);
      const tileset = requireTileset(map, params.tileset);
      const newTile = getTileFromTileset(tileset, params.tile_id);

      // Get the tile ID at the start position to know what we're replacing
      const startCell = layer.cellAt(params.x, params.y);
      const targetTileId = startCell ? startCell.tileId : -1;

      // Determine the new tile's global ID for comparison
      // We need to avoid filling if target equals new tile
      // Simple check: if the start position already has the new tile, skip
      const startTile = layer.tileAt(params.x, params.y);
      if (startTile && startTile.tileset === tileset && startTile.id === params.tile_id) {
        return { success: true, tilesFilled: 0, note: "Start position already has the target tile" };
      }

      // BFS flood fill
      const visited = {};
      const queue = [{ x: params.x, y: params.y }];
      const toFill = [];
      const key = function (x, y) { return x + "," + y; };

      visited[key(params.x, params.y)] = true;

      while (queue.length > 0) {
        const pos = queue.shift();
        const x = pos.x;
        const y = pos.y;

        if (x < 0 || x >= map.width || y < 0 || y >= map.height) continue;

        const cell = layer.cellAt(x, y);
        const cellTileId = cell ? cell.tileId : -1;

        if (cellTileId !== targetTileId) continue;

        toFill.push({ x: x, y: y });

        // Check 4 neighbors
        const neighbors = [
          { x: x - 1, y: y },
          { x: x + 1, y: y },
          { x: x, y: y - 1 },
          { x: x, y: y + 1 },
        ];

        for (const n of neighbors) {
          const k = key(n.x, n.y);
          if (!visited[k] && n.x >= 0 && n.x < map.width && n.y >= 0 && n.y < map.height) {
            visited[k] = true;
            queue.push(n);
          }
        }
      }

      map.macro("Flood fill", function () {
        const edit = layer.edit();
        for (const pos of toFill) {
          edit.setTile(pos.x, pos.y, newTile);
        }
        edit.apply();
      });

      return { success: true, tilesFilled: toFill.length };
    },
  };
}
