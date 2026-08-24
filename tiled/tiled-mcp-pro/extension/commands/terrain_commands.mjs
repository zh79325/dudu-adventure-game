/**
 * Terrain Commands - Wang/terrain set management.
 *
 * Tiled API:
 *   tileset.wangSets         - array of WangSets (terrain sets)
 *   tileset.addWangSet(name, type) - add a Wang set
 *   tileset.removeWangSet(ws)      - remove a Wang set
 *   wangSet.name, wangSet.type     - Corner/Edge/Mixed
 *   wangSet.colorCount             - number of terrain colors
 *   wangSet.addWangColor(name, color, probability)
 *   wangSet.removeWangColorAt(index)
 *   wangSet.wangId(tile) / wangSet.setWangId(tile, wangId)
 */

/* global tiled, WangSet */

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

function findWangSet(tileset, name) {
  for (const ws of tileset.wangSets) {
    if (ws.name === name) return ws;
  }
  return null;
}

function describeWangSet(ws) {
  const info = {
    name: ws.name,
    type: ws.type,
    colorCount: ws.colorCount,
  };

  const colors = [];
  for (let i = 1; i <= ws.colorCount; i++) {
    const color = ws.wangColor(i);
    if (color) {
      colors.push({
        index: i,
        name: color.name,
        color: color.color ? color.color.toString() : null,
        probability: color.probability,
      });
    }
  }
  info.colors = colors;

  return info;
}

export function getCommands() {
  return {
    // ── get_terrain_sets ──────────────────────────────────────────────
    get_terrain_sets(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const sets = [];
      for (const ws of ts.wangSets) {
        sets.push(describeWangSet(ws));
      }

      return { tileset: params.tileset, terrainSets: sets };
    },

    // ── get_terrain_set_info ──────────────────────────────────────────
    get_terrain_set_info(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const ws = findWangSet(ts, params.name);
      if (!ws) throw new Error("Terrain set not found: " + params.name);

      const info = describeWangSet(ws);

      // Include tile assignments
      const tileAssignments = [];
      for (let i = 0; i < ts.tileCount; i++) {
        const tile = ts.tile(i);
        if (!tile) continue;
        const wangId = ws.wangId(tile);
        if (wangId && wangId.some(function (v) { return v > 0; })) {
          tileAssignments.push({
            tileId: tile.id,
            wangId: wangId,
          });
        }
      }
      info.tileAssignments = tileAssignments;

      return info;
    },

    // ── add_terrain_set ───────────────────────────────────────────────
    add_terrain_set(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const typeMap = {
        corner: WangSet.Corner,
        edge: WangSet.Edge,
        mixed: WangSet.Mixed,
      };
      const type = typeMap[(params.type || "corner").toLowerCase()];
      if (type === undefined) {
        throw new Error("Invalid terrain set type: " + params.type + ". Use: corner, edge, mixed");
      }

      const ws = ts.addWangSet(params.name, type);

      return {
        success: true,
        tileset: params.tileset,
        name: ws.name,
        type: params.type || "corner",
      };
    },

    // ── remove_terrain_set ────────────────────────────────────────────
    remove_terrain_set(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const ws = findWangSet(ts, params.name);
      if (!ws) throw new Error("Terrain set not found: " + params.name);

      ts.removeWangSet(ws);

      return { success: true, tileset: params.tileset, name: params.name };
    },

    // ── add_terrain_color ─────────────────────────────────────────────
    add_terrain_color(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (!params.name) throw new Error("name is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      const color = params.color || "#ff0000";
      const probability = params.probability !== undefined ? params.probability : 1.0;

      ws.addWangColor(params.name, color, probability);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        colorName: params.name,
        colorCount: ws.colorCount,
      };
    },

    // ── remove_terrain_color ──────────────────────────────────────────
    remove_terrain_color(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (params.index === undefined) throw new Error("color index is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      ws.removeWangColorAt(params.index);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        colorCount: ws.colorCount,
      };
    },

    // ── set_terrain_tile ──────────────────────────────────────────────
    set_terrain_tile(params) {
      const map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");
      if (!params.wang_id) throw new Error("wang_id is required");

      const ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      const ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      const tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      ws.setWangId(tile, params.wang_id);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        tileId: params.tile_id,
        wangId: params.wang_id,
      };
    },
  };
}
