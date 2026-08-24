/**
 * Analysis Commands - map statistics, validation, comparison.
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

export function getCommands() {
  return {
    // ── get_map_statistics ────────────────────────────────────────────
    get_map_statistics(_params) {
      const map = requireMap();

      let tileLayerCount = 0;
      let objectLayerCount = 0;
      let groupLayerCount = 0;
      let imageLayerCount = 0;
      let totalObjects = 0;
      let totalTilesPlaced = 0;
      let emptyTiles = 0;

      function analyzeLayer(layer) {
        if (layer.isTileLayer) {
          tileLayerCount++;
          for (let y = 0; y < map.height; y++) {
            for (let x = 0; x < map.width; x++) {
              const cell = layer.cellAt(x, y);
              if (cell && cell.tileId !== -1) {
                totalTilesPlaced++;
              } else {
                emptyTiles++;
              }
            }
          }
        } else if (layer.isObjectLayer) {
          objectLayerCount++;
          totalObjects += layer.objectCount;
        } else if (layer.isGroupLayer) {
          groupLayerCount++;
          for (let i = 0; i < layer.layerCount; i++) {
            analyzeLayer(layer.layerAt(i));
          }
        } else if (layer.isImageLayer) {
          imageLayerCount++;
        }
      }

      for (let i = 0; i < map.layerCount; i++) {
        analyzeLayer(map.layerAt(i));
      }

      const totalCells = map.width * map.height * tileLayerCount;
      const fillPercentage = totalCells > 0
        ? Math.round((totalTilesPlaced / totalCells) * 10000) / 100
        : 0;

      // Tileset usage
      const tilesetUsage = {};
      function countTilesetUsage(layer) {
        if (layer.isTileLayer) {
          for (let y = 0; y < map.height; y++) {
            for (let x = 0; x < map.width; x++) {
              const tile = layer.tileAt(x, y);
              if (tile && tile.tileset) {
                const name = tile.tileset.name;
                tilesetUsage[name] = (tilesetUsage[name] || 0) + 1;
              }
            }
          }
        } else if (layer.isGroupLayer) {
          for (let i = 0; i < layer.layerCount; i++) {
            countTilesetUsage(layer.layerAt(i));
          }
        }
      }
      for (let i = 0; i < map.layerCount; i++) {
        countTilesetUsage(map.layerAt(i));
      }

      return {
        mapSize: { width: map.width, height: map.height },
        tileSize: { width: map.tileWidth, height: map.tileHeight },
        pixelSize: {
          width: map.width * map.tileWidth,
          height: map.height * map.tileHeight,
        },
        layers: {
          total: map.layerCount,
          tileLayers: tileLayerCount,
          objectLayers: objectLayerCount,
          groupLayers: groupLayerCount,
          imageLayers: imageLayerCount,
        },
        tiles: {
          placed: totalTilesPlaced,
          empty: emptyTiles,
          totalCells: totalCells,
          fillPercentage: fillPercentage,
        },
        objects: {
          total: totalObjects,
        },
        tilesets: {
          count: map.tilesets.length,
          usage: tilesetUsage,
        },
      };
    },

    // ── get_layer_statistics ──────────────────────────────────────────
    get_layer_statistics(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = findLayer(map, params.layer);
      if (!layer) throw new Error("Layer not found: " + params.layer);

      const result = {
        name: layer.name,
        visible: layer.visible,
        locked: layer.locked,
        opacity: layer.opacity,
      };

      if (layer.isTileLayer) {
        result.type = "tilelayer";
        let placed = 0;
        let empty = 0;
        const tileIds = {};

        for (let y = 0; y < map.height; y++) {
          for (let x = 0; x < map.width; x++) {
            const cell = layer.cellAt(x, y);
            if (cell && cell.tileId !== -1) {
              placed++;
              tileIds[cell.tileId] = (tileIds[cell.tileId] || 0) + 1;
            } else {
              empty++;
            }
          }
        }

        result.tilesPlaced = placed;
        result.tilesEmpty = empty;
        result.uniqueTileIds = Object.keys(tileIds).length;
        result.fillPercentage =
          Math.round((placed / (map.width * map.height)) * 10000) / 100;

        // Most used tiles
        const sorted = Object.entries(tileIds)
          .sort(function (a, b) { return b[1] - a[1]; })
          .slice(0, 10);
        result.mostUsedTiles = sorted.map(function (entry) {
          return { tileId: parseInt(entry[0]), count: entry[1] };
        });
      } else if (layer.isObjectLayer) {
        result.type = "objectgroup";
        result.objectCount = layer.objectCount;

        const classCounts = {};
        const shapeCounts = {};
        for (const obj of layer.objects) {
          const cls = obj.className || "(none)";
          classCounts[cls] = (classCounts[cls] || 0) + 1;
          const shape = obj.shape !== undefined ? String(obj.shape) : "unknown";
          shapeCounts[shape] = (shapeCounts[shape] || 0) + 1;
        }
        result.classCounts = classCounts;
        result.shapeCounts = shapeCounts;
      }

      return result;
    },

    // ── validate_map ──────────────────────────────────────────────────
    validate_map(params) {
      const map = requireMap();
      const issues = [];

      // Check for empty tile layers
      function checkLayers(parent) {
        for (let i = 0; i < parent.layerCount; i++) {
          const layer = parent.layerAt(i);

          if (layer.isTileLayer) {
            let hasContent = false;
            outer:
            for (let y = 0; y < map.height; y++) {
              for (let x = 0; x < map.width; x++) {
                const cell = layer.cellAt(x, y);
                if (cell && cell.tileId !== -1) {
                  hasContent = true;
                  break outer;
                }
              }
            }
            if (!hasContent) {
              issues.push({
                type: "warning",
                category: "empty_layer",
                message: "Tile layer '" + layer.name + "' has no tiles placed",
              });
            }
          }

          if (layer.isObjectLayer && layer.objectCount === 0) {
            issues.push({
              type: "info",
              category: "empty_layer",
              message: "Object layer '" + layer.name + "' has no objects",
            });
          }

          if (layer.isGroupLayer) {
            if (layer.layerCount === 0) {
              issues.push({
                type: "info",
                category: "empty_layer",
                message: "Group layer '" + layer.name + "' has no children",
              });
            }
            checkLayers(layer);
          }
        }
      }
      checkLayers(map);

      // Check for unused tilesets
      const usedTilesets = new Set();
      function collectUsedTilesets(parent) {
        for (let i = 0; i < parent.layerCount; i++) {
          const layer = parent.layerAt(i);
          if (layer.isTileLayer) {
            for (let y = 0; y < map.height; y++) {
              for (let x = 0; x < map.width; x++) {
                const tile = layer.tileAt(x, y);
                if (tile && tile.tileset) {
                  usedTilesets.add(tile.tileset.name);
                }
              }
            }
          }
          if (layer.isGroupLayer) collectUsedTilesets(layer);
        }
      }
      collectUsedTilesets(map);

      for (const ts of map.tilesets) {
        if (!usedTilesets.has(ts.name)) {
          issues.push({
            type: "warning",
            category: "unused_tileset",
            message: "Tileset '" + ts.name + "' is not used by any tile layer",
          });
        }
      }

      // Check for missing tileset images
      for (const ts of map.tilesets) {
        if (ts.image && ts.imageWidth === 0) {
          issues.push({
            type: "error",
            category: "missing_image",
            message: "Tileset '" + ts.name + "' has a missing or broken image",
          });
        }
      }

      // Check for unsaved changes
      if (map.modified) {
        issues.push({
          type: "info",
          category: "unsaved",
          message: "Map has unsaved changes",
        });
      }

      // Check for no file name
      if (!map.fileName) {
        issues.push({
          type: "warning",
          category: "no_filename",
          message: "Map has not been saved to a file yet",
        });
      }

      return {
        valid: issues.filter(function (i) { return i.type === "error"; }).length === 0,
        issues: issues,
        errorCount: issues.filter(function (i) { return i.type === "error"; }).length,
        warningCount: issues.filter(function (i) { return i.type === "warning"; }).length,
        infoCount: issues.filter(function (i) { return i.type === "info"; }).length,
      };
    },

    // ── compare_layers ────────────────────────────────────────────────
    compare_layers(params) {
      const map = requireMap();
      if (!params.layer_a) throw new Error("layer_a is required");
      if (!params.layer_b) throw new Error("layer_b is required");

      const a = findLayer(map, params.layer_a);
      const b = findLayer(map, params.layer_b);
      if (!a) throw new Error("Layer not found: " + params.layer_a);
      if (!b) throw new Error("Layer not found: " + params.layer_b);
      if (!a.isTileLayer || !b.isTileLayer) {
        throw new Error("Both layers must be tile layers");
      }

      let matching = 0;
      let different = 0;
      let onlyInA = 0;
      let onlyInB = 0;
      const differences = [];

      for (let y = 0; y < map.height; y++) {
        for (let x = 0; x < map.width; x++) {
          const cellA = a.cellAt(x, y);
          const cellB = b.cellAt(x, y);
          const aEmpty = !cellA || cellA.tileId === -1;
          const bEmpty = !cellB || cellB.tileId === -1;

          if (aEmpty && bEmpty) {
            matching++;
          } else if (aEmpty) {
            onlyInB++;
            if (differences.length < 100) {
              differences.push({ x: x, y: y, type: "only_in_b" });
            }
          } else if (bEmpty) {
            onlyInA++;
            if (differences.length < 100) {
              differences.push({ x: x, y: y, type: "only_in_a" });
            }
          } else if (cellA.tileId === cellB.tileId) {
            matching++;
          } else {
            different++;
            if (differences.length < 100) {
              differences.push({
                x: x,
                y: y,
                type: "different",
                tileIdA: cellA.tileId,
                tileIdB: cellB.tileId,
              });
            }
          }
        }
      }

      const total = map.width * map.height;

      return {
        layerA: params.layer_a,
        layerB: params.layer_b,
        totalCells: total,
        matching: matching,
        different: different,
        onlyInA: onlyInA,
        onlyInB: onlyInB,
        similarity: Math.round((matching / total) * 10000) / 100,
        differences: differences,
        differencesTruncated: differences.length >= 100,
      };
    },

    // ── find_tile_patterns ────────────────────────────────────────────
    find_tile_patterns(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = findLayer(map, params.layer);
      if (!layer || !layer.isTileLayer) {
        throw new Error("Tile layer not found: " + params.layer);
      }

      const patternSize = params.size || 2;
      const patterns = {};

      for (let y = 0; y <= map.height - patternSize; y++) {
        for (let x = 0; x <= map.width - patternSize; x++) {
          const key = [];
          for (let dy = 0; dy < patternSize; dy++) {
            for (let dx = 0; dx < patternSize; dx++) {
              const cell = layer.cellAt(x + dx, y + dy);
              key.push(cell ? cell.tileId : -1);
            }
          }
          const patternKey = key.join(",");
          if (!patterns[patternKey]) {
            patterns[patternKey] = { count: 0, firstAt: { x: x, y: y }, tileIds: key };
          }
          patterns[patternKey].count++;
        }
      }

      // Sort by frequency, return top patterns
      const sorted = Object.values(patterns)
        .filter(function (p) { return p.count > 1; })
        .sort(function (a, b) { return b.count - a.count; })
        .slice(0, params.limit || 20);

      return {
        layer: params.layer,
        patternSize: patternSize,
        uniquePatterns: Object.keys(patterns).length,
        repeatingPatterns: sorted.length,
        topPatterns: sorted,
      };
    },
  };
}
