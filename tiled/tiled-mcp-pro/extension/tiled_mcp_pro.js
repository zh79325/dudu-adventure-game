/// <reference types="@mapeditor/tiled-api" />

/**
 * Tiled MCP Pro Extension (Single-file bundle)
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

/* global tiled, Process, TextFile, FileInfo, File, Qt, __filename,
          TileMap, TileLayer, ObjectGroup, GroupLayer, ImageLayer,
          Tileset, Tile, MapObject, WangSet, Image */

(function() {

// CRITICAL: __filename is deleted after module evaluation.
// Must capture at top level immediately.
var EXTENSION_FILE = (typeof __filename !== "undefined") ? __filename : "";
var EXTENSION_DIR = (function() {
  if (!EXTENSION_FILE) return "";
  var lastSlash = Math.max(EXTENSION_FILE.lastIndexOf("/"), EXTENSION_FILE.lastIndexOf("\\"));
  return EXTENSION_FILE.substring(0, lastSlash);
})();

// ══════════════════════════════════════════════════════════════════════════
// CommandRouter
// ══════════════════════════════════════════════════════════════════════════

function CommandRouter() {
  this.handlers = {};
}

CommandRouter.prototype.register = function(commands) {
  for (var key in commands) {
    if (Object.prototype.hasOwnProperty.call(commands, key)) {
      this.handlers[key] = commands[key];
    }
  }
};

CommandRouter.prototype.execute = function(method, params) {
  var handler = this.handlers[method];
  if (!handler) {
    throw new Error("Method not found: " + method);
  }
  return handler(params || {});
};

CommandRouter.prototype.getMethodList = function() {
  return Object.keys(this.handlers);
};

CommandRouter.prototype.hasMethod = function(method) {
  return method in this.handlers;
};

// ══════════════════════════════════════════════════════════════════════════
// Shared Helpers
// ══════════════════════════════════════════════════════════════════════════

function requireMap() {
  var asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

function findLayer(map, name) {
  function search(parent) {
    for (var i = 0; i < parent.layerCount; i++) {
      var layer = parent.layerAt(i);
      if (layer.name === name) return layer;
      if (layer.isGroupLayer) {
        var found = search(layer);
        if (found) return found;
      }
    }
    return null;
  }
  return search(map);
}

function findLayerIndex(map, name) {
  for (var i = 0; i < map.layerCount; i++) {
    if (map.layerAt(i).name === name) return i;
  }
  return -1;
}

function requireTileLayer(map, name) {
  var layer = findLayer(map, name);
  if (!layer) throw new Error("Layer not found: " + name);
  if (!layer.isTileLayer) throw new Error("Layer is not a tile layer: " + name);
  return layer;
}

function requireObjectLayer(map, name) {
  var layer = findLayer(map, name);
  if (!layer) throw new Error("Layer not found: " + name);
  if (!layer.isObjectLayer) throw new Error("Layer is not an object layer: " + name);
  return layer;
}

function findTileset(map, name) {
  for (var i = 0; i < map.tilesets.length; i++) {
    if (map.tilesets[i].name === name) return map.tilesets[i];
  }
  return null;
}

function requireTileset(map, name) {
  var ts = findTileset(map, name);
  if (!ts) throw new Error("Tileset not found: " + name);
  return ts;
}

function getTileFromTileset(tileset, tileId) {
  var tile = tileset.tile(tileId);
  if (!tile) throw new Error("Tile ID " + tileId + " not found in tileset " + tileset.name);
  return tile;
}

function findObjectById(layer, id) {
  var objects = layer.objects;
  for (var i = 0; i < objects.length; i++) {
    if (objects[i].id === id) return objects[i];
  }
  return null;
}

function findObjectByName(layer, name) {
  var objects = layer.objects;
  for (var i = 0; i < objects.length; i++) {
    if (objects[i].name === name) return objects[i];
  }
  return null;
}

function findWangSet(tileset, name) {
  var sets = tileset.wangSets;
  for (var i = 0; i < sets.length; i++) {
    if (sets[i].name === name) return sets[i];
  }
  return null;
}

function describeLayer(layer, index) {
  var info = {
    index: index,
    name: layer.name,
    visible: layer.visible,
    locked: layer.locked,
    opacity: layer.opacity,
    offset: { x: layer.offset.x, y: layer.offset.y }
  };

  if (layer.isTileLayer) {
    info.type = "tilelayer";
  } else if (layer.isObjectLayer) {
    info.type = "objectgroup";
    info.objectCount = layer.objectCount;
  } else if (layer.isGroupLayer) {
    info.type = "group";
    info.childCount = layer.layerCount;
  } else if (layer.isImageLayer) {
    info.type = "imagelayer";
    info.imageFileName = layer.imageFileName || null;
  } else {
    info.type = "unknown";
  }

  if (layer.parallaxFactor) {
    info.parallaxFactor = { x: layer.parallaxFactor.x, y: layer.parallaxFactor.y };
  }
  if (layer.tintColor) {
    info.tintColor = layer.tintColor.toString();
  }

  return info;
}

function describeObject(obj) {
  var info = {
    id: obj.id,
    name: obj.name,
    className: obj.className || "",
    x: obj.x,
    y: obj.y,
    width: obj.width,
    height: obj.height,
    rotation: obj.rotation,
    visible: obj.visible,
    shape: obj.shape
  };

  if (obj.shape === MapObject.Polygon || obj.shape === MapObject.Polyline) {
    if (obj.polygon) {
      info.polygon = obj.polygon.map(function (p) {
        return { x: p.x, y: p.y };
      });
    }
  }

  if (obj.shape === MapObject.Text) {
    info.text = obj.text;
    info.font = obj.font;
    info.wordWrap = obj.wordWrap;
    info.textColor = obj.textColor ? obj.textColor.toString() : null;
    info.textAlignment = obj.textAlignment;
  }

  if (obj.tile) {
    info.tileId = obj.tile.id;
    info.tileset = obj.tile.tileset ? obj.tile.tileset.name : null;
  }

  return info;
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
    isCollection: ts.tileCount > 0 && !ts.image
  };
}

function describeWangSet(ws) {
  var info = {
    name: ws.name,
    type: ws.type,
    colorCount: ws.colorCount
  };

  var colors = [];
  for (var i = 1; i <= ws.colorCount; i++) {
    var color = ws.wangColor(i);
    if (color) {
      colors.push({
        index: i,
        name: color.name,
        color: color.color ? color.color.toString() : null,
        probability: color.probability
      });
    }
  }
  info.colors = colors;

  return info;
}

/**
 * Resolve a property target element.
 * Supports: "map", "layer:<name>", "object:<layer>:<id>", "tile:<tileset>:<id>"
 */
function resolveTarget(map, params) {
  var target = params.target;
  if (!target) throw new Error("target is required");

  if (target === "map") {
    return map;
  }

  if (target.startsWith("layer:")) {
    var layerName = target.substring(6);
    var layer = findLayer(map, layerName);
    if (!layer) throw new Error("Layer not found: " + layerName);
    return layer;
  }

  if (target.startsWith("object:")) {
    var parts = target.substring(7).split(":");
    if (parts.length < 2) throw new Error("object target format: object:<layer>:<id>");
    var objLayerName = parts[0];
    var objId = parseInt(parts[1], 10);
    var objLayer = findLayer(map, objLayerName);
    if (!objLayer || !objLayer.isObjectLayer) {
      throw new Error("Object layer not found: " + objLayerName);
    }
    var objects = objLayer.objects;
    for (var i = 0; i < objects.length; i++) {
      if (objects[i].id === objId) return objects[i];
    }
    throw new Error("Object not found: id=" + objId + " in layer " + objLayerName);
  }

  if (target.startsWith("tile:")) {
    var tileParts = target.substring(5).split(":");
    if (tileParts.length < 2) throw new Error("tile target format: tile:<tileset>:<id>");
    var tsName = tileParts[0];
    var tileId = parseInt(tileParts[1], 10);
    var ts = null;
    for (var j = 0; j < map.tilesets.length; j++) {
      if (map.tilesets[j].name === tsName) { ts = map.tilesets[j]; break; }
    }
    if (!ts) throw new Error("Tileset not found: " + tsName);
    var tile = ts.tile(tileId);
    if (!tile) throw new Error("Tile not found: " + tileId + " in " + tsName);
    return tile;
  }

  // Try as a layer name directly (convenience)
  var directLayer = findLayer(map, target);
  if (directLayer) return directLayer;

  throw new Error(
    "Invalid target: " + target +
    ". Use: 'map', 'layer:<name>', 'object:<layer>:<id>', 'tile:<tileset>:<id>'"
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Map Commands
// ══════════════════════════════════════════════════════════════════════════

function getMapCommands() {
  return {
    get_map_info: function(_params) {
      var map = requireMap();
      var layers = [];
      for (var i = 0; i < map.layerCount; i++) {
        layers.push(describeLayer(map.layerAt(i), i));
      }
      var tilesets = [];
      for (var j = 0; j < map.tilesets.length; j++) {
        var ts = map.tilesets[j];
        tilesets.push({
          name: ts.name,
          tileWidth: ts.tileWidth,
          tileHeight: ts.tileHeight,
          tileCount: ts.tileCount,
          columnCount: ts.columnCount,
          imageWidth: ts.imageWidth,
          imageHeight: ts.imageHeight
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
        renderOrder: map.renderOrder
      };
    },

    create_map: function(params) {
      var name = params.name || "untitled.tmx";
      var w = params.width || 20;
      var h = params.height || 15;
      var tw = params.tileWidth || 32;
      var th = params.tileHeight || 32;

      var map = new TileMap();
      map.setSize(w, h);
      map.setTileSize(tw, th);

      if (params.orientation) {
        var orientations = {
          orthogonal: TileMap.Orthogonal,
          isometric: TileMap.Isometric,
          staggered: TileMap.Staggered,
          hexagonal: TileMap.Hexagonal
        };
        if (orientations[params.orientation] !== undefined) {
          map.orientation = orientations[params.orientation];
        }
      }

      if (params.infinite !== undefined) {
        map.infinite = params.infinite;
      }

      var layer = new TileLayer(name.replace(/\.\w+$/, ""));
      map.addLayer(layer);

      tiled.activeAsset = map;

      return {
        success: true,
        width: map.width,
        height: map.height,
        tileWidth: map.tileWidth,
        tileHeight: map.tileHeight
      };
    },

    open_map: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      var asset = tiled.open(params.file_path);
      if (!asset) {
        throw new Error("Failed to open file: " + params.file_path);
      }
      return {
        success: true,
        fileName: asset.fileName,
        isTileMap: asset.isTileMap
      };
    },

    save_map: function(params) {
      var map = requireMap();
      if (params.file_path) {
        var format = tiled.mapFormat("tmx");
        if (format) {
          format.write(map, params.file_path);
        } else {
          map.fileName = params.file_path;
          tiled.trigger("SaveAs");
        }
      } else {
        tiled.trigger("Save");
      }
      return { success: true, fileName: map.fileName };
    },

    close_map: function(params) {
      var asset;
      if (params.file_path) {
        asset = null;
        var openAssets = tiled.openAssets;
        for (var i = 0; i < openAssets.length; i++) {
          if (openAssets[i].fileName === params.file_path) {
            asset = openAssets[i];
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

    resize_map: function(params) {
      var map = requireMap();
      var w = params.width;
      var h = params.height;
      if (!w || !h) throw new Error("width and height are required");

      if (params.offset_x !== undefined || params.offset_y !== undefined) {
        var ox = params.offset_x || 0;
        var oy = params.offset_y || 0;
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

    set_map_orientation: function(params) {
      var map = requireMap();
      var orientations = {
        orthogonal: TileMap.Orthogonal,
        isometric: TileMap.Isometric,
        staggered: TileMap.Staggered,
        hexagonal: TileMap.Hexagonal
      };
      var val = orientations[params.orientation];
      if (val === undefined) {
        throw new Error("Invalid orientation: " + params.orientation);
      }
      map.macro("Set orientation", function () {
        map.orientation = val;
      });
      return { success: true, orientation: params.orientation };
    },

    set_map_tile_size: function(params) {
      var map = requireMap();
      if (!params.tile_width || !params.tile_height) {
        throw new Error("tile_width and tile_height are required");
      }
      map.macro("Set tile size", function () {
        map.setTileSize(params.tile_width, params.tile_height);
      });
      return {
        success: true,
        tileWidth: map.tileWidth,
        tileHeight: map.tileHeight
      };
    },

    get_open_maps: function(_params) {
      var maps = [];
      var openAssets = tiled.openAssets;
      for (var i = 0; i < openAssets.length; i++) {
        var asset = openAssets[i];
        if (asset.isTileMap) {
          maps.push({
            fileName: asset.fileName,
            width: asset.width,
            height: asset.height,
            isActive: asset === tiled.activeAsset
          });
        }
      }
      return { maps: maps };
    },

    set_active_map: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      var openAssets = tiled.openAssets;
      for (var i = 0; i < openAssets.length; i++) {
        if (openAssets[i].fileName === params.file_path) {
          tiled.activeAsset = openAssets[i];
          return { success: true, fileName: openAssets[i].fileName };
        }
      }
      throw new Error("Map not found among open assets: " + params.file_path);
    },

    render_map_to_image: function(params) {
      if (!params.output_path) throw new Error("output_path is required");
      var map = requireMap();
      var img = map.toImage();
      if (!img || img.width === 0) throw new Error("Failed to render map to image");
      img.save(params.output_path);
      return {
        success: true,
        output_path: params.output_path,
        width: img.width,
        height: img.height
      };
    },

    auto_map: function(params) {
      var map = requireMap();
      if (
        params.region_x !== undefined &&
        params.region_y !== undefined &&
        params.region_width !== undefined &&
        params.region_height !== undefined
      ) {
        var rgn = new region(
          params.region_x,
          params.region_y,
          params.region_width,
          params.region_height
        );
        map.autoMap(rgn);
      } else {
        map.autoMap();
      }
      return { success: true };
    },

    undo_map: function(_params) {
      tiled.trigger("Undo");
      return { success: true };
    },

    redo_map: function(_params) {
      tiled.trigger("Redo");
      return { success: true };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Layer Commands
// ══════════════════════════════════════════════════════════════════════════

function getLayerCommands() {
  return {
    get_layers: function(_params) {
      var map = requireMap();
      var layers = [];

      function collectLayers(parent, depth) {
        for (var i = 0; i < parent.layerCount; i++) {
          var layer = parent.layerAt(i);
          var info = describeLayer(layer, i);
          info.depth = depth;
          layers.push(info);
          if (layer.isGroupLayer) {
            collectLayers(layer, depth + 1);
          }
        }
      }

      collectLayers(map, 0);
      return { layers: layers };
    },

    add_tile_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");

      var layer = new TileLayer(params.name);

      map.macro("Add tile layer", function () {
        if (params.above) {
          var idx = findLayerIndex(map, params.above);
          if (idx >= 0) {
            map.insertLayerAt(idx + 1, layer);
          } else {
            map.addLayer(layer);
          }
        } else {
          map.addLayer(layer);
        }
      });

      return { success: true, name: layer.name, type: "tilelayer" };
    },

    add_object_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");

      var layer = new ObjectGroup(params.name);

      map.macro("Add object layer", function () {
        if (params.above) {
          var idx = findLayerIndex(map, params.above);
          if (idx >= 0) {
            map.insertLayerAt(idx + 1, layer);
          } else {
            map.addLayer(layer);
          }
        } else {
          map.addLayer(layer);
        }
      });

      return { success: true, name: layer.name, type: "objectgroup" };
    },

    add_group_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");

      var layer = new GroupLayer(params.name);
      map.macro("Add group layer", function () {
        map.addLayer(layer);
      });

      return { success: true, name: layer.name, type: "group" };
    },

    add_image_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (!params.image_path) throw new Error("image_path is required");

      var layer = new ImageLayer(params.name);
      layer.imageFileName = params.image_path;

      map.macro("Add image layer", function () {
        map.addLayer(layer);
      });

      return { success: true, name: layer.name, type: "imagelayer" };
    },

    remove_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");

      var layer = findLayer(map, params.name);
      if (!layer) throw new Error("Layer not found: " + params.name);

      map.macro("Remove layer", function () {
        map.removeLayer(layer);
      });

      return { success: true, name: params.name };
    },

    set_layer_property: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer name is required");
      if (!params.property) throw new Error("property name is required");

      var layer = findLayer(map, params.layer);
      if (!layer) throw new Error("Layer not found: " + params.layer);

      map.macro("Set layer property", function () {
        var prop = params.property;
        var val = params.value;

        switch (prop) {
          case "visible":
            layer.visible = !!val;
            break;
          case "locked":
            layer.locked = !!val;
            break;
          case "opacity":
            layer.opacity = Number(val);
            break;
          case "offset":
          case "offset_x":
            if (prop === "offset" && typeof val === "object") {
              layer.offset = Qt.point(val.x || 0, val.y || 0);
            } else {
              layer.offset = Qt.point(Number(val), layer.offset.y);
            }
            break;
          case "offset_y":
            layer.offset = Qt.point(layer.offset.x, Number(val));
            break;
          case "parallaxFactor":
          case "parallax_factor":
            if (typeof val === "object") {
              layer.parallaxFactor = Qt.point(val.x || 1, val.y || 1);
            }
            break;
          case "tintColor":
          case "tint_color":
            layer.tintColor = val;
            break;
          default:
            throw new Error("Unknown layer property: " + prop);
        }
      });

      return { success: true, layer: params.layer, property: params.property };
    },

    rename_layer: function(params) {
      var map = requireMap();
      if (!params.old_name) throw new Error("old_name is required");
      if (!params.new_name) throw new Error("new_name is required");

      var layer = findLayer(map, params.old_name);
      if (!layer) throw new Error("Layer not found: " + params.old_name);

      map.macro("Rename layer", function () {
        layer.name = params.new_name;
      });

      return { success: true, old_name: params.old_name, new_name: params.new_name };
    },

    reorder_layer: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (params.index === undefined) throw new Error("index is required");

      var layer = findLayer(map, params.name);
      if (!layer) throw new Error("Layer not found: " + params.name);

      map.macro("Reorder layer", function () {
        map.removeLayer(layer);
        map.insertLayerAt(Math.min(params.index, map.layerCount), layer);
      });

      return { success: true, name: params.name, newIndex: params.index };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Tile Commands
// ══════════════════════════════════════════════════════════════════════════

function getTileCommands() {
  return {
    get_tile: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }

      var layer = requireTileLayer(map, params.layer);
      var cell = layer.cellAt(params.x, params.y);

      if (!cell || cell.tileId === -1) {
        return {
          x: params.x,
          y: params.y,
          empty: true,
          tileId: -1
        };
      }

      var tilesetName = null;
      var localId = cell.tileId;
      var tile = layer.tileAt(params.x, params.y);
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
        rotatedHexagonal120: cell.rotatedHexagonal120 || false
      };
    },

    set_tile: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var layer = requireTileLayer(map, params.layer);
      var tileset = requireTileset(map, params.tileset);
      var tile = getTileFromTileset(tileset, params.tile_id);

      map.macro("Set tile", function () {
        var edit = layer.edit();
        edit.setTile(params.x, params.y, tile);
        edit.apply();
      });

      return {
        success: true,
        x: params.x,
        y: params.y,
        tileset: params.tileset,
        tile_id: params.tile_id
      };
    },

    fill_rect: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) throw new Error("width and height are required");
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var layer = requireTileLayer(map, params.layer);
      var tileset = requireTileset(map, params.tileset);
      var tile = getTileFromTileset(tileset, params.tile_id);

      var count = params.width * params.height;

      map.macro("Fill rectangle", function () {
        var edit = layer.edit();
        for (var dy = 0; dy < params.height; dy++) {
          for (var dx = 0; dx < params.width; dx++) {
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
          height: params.height
        }
      };
    },

    clear_rect: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) throw new Error("width and height are required");

      var layer = requireTileLayer(map, params.layer);

      map.macro("Clear rectangle", function () {
        var edit = layer.edit();
        for (var dy = 0; dy < params.height; dy++) {
          for (var dx = 0; dx < params.width; dx++) {
            edit.setTile(params.x + dx, params.y + dy, null);
          }
        }
        edit.apply();
      });

      return {
        success: true,
        tilesCleared: params.width * params.height
      };
    },

    clear_layer: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = requireTileLayer(map, params.layer);

      map.macro("Clear layer", function () {
        var edit = layer.edit();
        for (var y = 0; y < map.height; y++) {
          for (var x = 0; x < map.width; x++) {
            edit.setTile(x, y, null);
          }
        }
        edit.apply();
      });

      return { success: true, layer: params.layer };
    },

    get_used_tiles: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = requireTileLayer(map, params.layer);
      var tiles = [];

      for (var y = 0; y < map.height; y++) {
        for (var x = 0; x < map.width; x++) {
          var cell = layer.cellAt(x, y);
          if (!cell || cell.tileId === -1) continue;

          var tile = layer.tileAt(x, y);
          var tilesetName = tile && tile.tileset ? tile.tileset.name : null;
          var localId = tile ? tile.id : cell.tileId;

          if (params.tileset && tilesetName !== params.tileset) continue;
          if (params.tile_id !== undefined && localId !== params.tile_id) continue;

          tiles.push({
            x: x,
            y: y,
            tileId: cell.tileId,
            localTileId: localId,
            tileset: tilesetName
          });
        }
      }

      return { layer: params.layer, tiles: tiles, count: tiles.length };
    },

    set_tiles_batch: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (!params.tiles || !Array.isArray(params.tiles)) {
        throw new Error("tiles array is required");
      }

      var layer = requireTileLayer(map, params.layer);

      var resolvedTiles = params.tiles.map(function (t) {
        var tileset = requireTileset(map, t.tileset);
        var tile = getTileFromTileset(tileset, t.tile_id);
        return { x: t.x, y: t.y, tile: tile };
      });

      map.macro("Set tiles batch", function () {
        var edit = layer.edit();
        for (var i = 0; i < resolvedTiles.length; i++) {
          var t = resolvedTiles[i];
          edit.setTile(t.x, t.y, t.tile);
        }
        edit.apply();
      });

      return {
        success: true,
        tilesPlaced: resolvedTiles.length
      };
    },

    flood_fill: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var layer = requireTileLayer(map, params.layer);
      var tileset = requireTileset(map, params.tileset);
      var newTile = getTileFromTileset(tileset, params.tile_id);

      var startCell = layer.cellAt(params.x, params.y);
      var targetTileId = startCell ? startCell.tileId : -1;

      var startTile = layer.tileAt(params.x, params.y);
      if (startTile && startTile.tileset === tileset && startTile.id === params.tile_id) {
        return { success: true, tilesFilled: 0, note: "Start position already has the target tile" };
      }

      var visited = {};
      var queue = [{ x: params.x, y: params.y }];
      var toFill = [];
      var key = function (x, y) { return x + "," + y; };

      visited[key(params.x, params.y)] = true;

      while (queue.length > 0) {
        var pos = queue.shift();
        var px = pos.x;
        var py = pos.y;

        if (px < 0 || px >= map.width || py < 0 || py >= map.height) continue;

        var cell = layer.cellAt(px, py);
        var cellTileId = cell ? cell.tileId : -1;

        if (cellTileId !== targetTileId) continue;

        toFill.push({ x: px, y: py });

        var neighbors = [
          { x: px - 1, y: py },
          { x: px + 1, y: py },
          { x: px, y: py - 1 },
          { x: px, y: py + 1 }
        ];

        for (var ni = 0; ni < neighbors.length; ni++) {
          var n = neighbors[ni];
          var k = key(n.x, n.y);
          if (!visited[k] && n.x >= 0 && n.x < map.width && n.y >= 0 && n.y < map.height) {
            visited[k] = true;
            queue.push(n);
          }
        }
      }

      map.macro("Flood fill", function () {
        var edit = layer.edit();
        for (var fi = 0; fi < toFill.length; fi++) {
          edit.setTile(toFill[fi].x, toFill[fi].y, newTile);
        }
        edit.apply();
      });

      return { success: true, tilesFilled: toFill.length };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Tileset Commands
// ══════════════════════════════════════════════════════════════════════════

function getTilesetCommands() {
  return {
    get_tilesets: function(_params) {
      var map = requireMap();
      var tilesets = [];
      for (var i = 0; i < map.tilesets.length; i++) {
        tilesets.push(describeTileset(map.tilesets[i]));
      }
      return { tilesets: tilesets };
    },

    get_tileset_info: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");
      var ts = findTileset(map, params.name);
      if (!ts) throw new Error("Tileset not found: " + params.name);

      var info = describeTileset(ts);

      var tiles = [];
      for (var i = 0; i < ts.tileCount; i++) {
        var tile = ts.tile(i);
        if (tile) {
          var tileInfo = {
            id: tile.id,
            width: tile.width,
            height: tile.height
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

    add_tileset: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");

      if (params.file_path) {
        var asset = tiled.open(params.file_path);
        if (!asset) throw new Error("Failed to open tileset: " + params.file_path);
        map.addTileset(asset);
        return { success: true, name: asset.name, loaded: true };
      }

      var ts = new Tileset(params.name);
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
        created: true
      };
    },

    remove_tileset: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");
      var ts = findTileset(map, params.name);
      if (!ts) throw new Error("Tileset not found: " + params.name);

      map.removeTileset(ts);
      return { success: true, name: params.name };
    },

    set_tileset_image: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (!params.image) throw new Error("image path is required");

      var ts = findTileset(map, params.name);
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
        tileCount: ts.tileCount
      };
    },

    add_tile_to_collection: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var tile = ts.addTile();

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
        tileCount: ts.tileCount
      };
    },

    remove_tile_from_collection: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      ts.removeTiles([tile]);

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        tileCount: ts.tileCount
      };
    },

    set_tile_class: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      tile.className = params.class_name || "";

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        className: tile.className
      };
    },

    set_tile_probability: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");
      if (params.probability === undefined) throw new Error("probability is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      tile.probability = params.probability;

      return {
        success: true,
        tileset: params.tileset,
        tileId: params.tile_id,
        probability: tile.probability
      };
    },

    get_tile_info: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      var info = {
        id: tile.id,
        width: tile.width,
        height: tile.height,
        className: tile.className || "",
        probability: tile.probability,
        imageFileName: tile.imageFileName || null
      };

      var props = {};
      var resolved = tile.resolvedProperties();
      var resolvedKeys = Object.keys(resolved);
      for (var i = 0; i < resolvedKeys.length; i++) {
        props[resolvedKeys[i]] = tile.resolvedProperty(resolvedKeys[i]);
      }
      if (Object.keys(props).length > 0) {
        info.properties = props;
      }

      if (tile.frames && tile.frames.length > 0) {
        info.animation = tile.frames.map(function (f) {
          return { tileId: f.tileId, duration: f.duration };
        });
      }

      if (tile.objectGroup) {
        info.objectCount = tile.objectGroup.objectCount;
      }

      return info;
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Object Commands
// ══════════════════════════════════════════════════════════════════════════

function getObjectCommands() {
  return {
    get_objects: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = requireObjectLayer(map, params.layer);
      var objects = [];
      var layerObjs = layer.objects;
      for (var i = 0; i < layerObjs.length; i++) {
        var info = describeObject(layerObjs[i]);
        if (params.class_name && info.className !== params.class_name) continue;
        if (params.name && info.name !== params.name) continue;
        objects.push(info);
      }

      return { layer: params.layer, objects: objects, count: objects.length };
    },

    get_object: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined && !params.name) {
        throw new Error("id or name is required");
      }

      var layer = requireObjectLayer(map, params.layer);
      var obj;
      if (params.id !== undefined) {
        obj = findObjectById(layer, params.id);
        if (!obj) throw new Error("Object not found with id: " + params.id);
      } else {
        obj = findObjectByName(layer, params.name);
        if (!obj) throw new Error("Object not found with name: " + params.name);
      }

      var info = describeObject(obj);

      var props = {};
      var resolved = obj.resolvedProperties();
      var resolvedKeys = Object.keys(resolved);
      for (var i = 0; i < resolvedKeys.length; i++) {
        props[resolvedKeys[i]] = obj.resolvedProperty(resolvedKeys[i]);
      }
      if (Object.keys(props).length > 0) {
        info.properties = props;
      }

      return info;
    },

    add_object: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = requireObjectLayer(map, params.layer);
      var obj = new MapObject(params.name || "");

      obj.x = params.x || 0;
      obj.y = params.y || 0;
      obj.width = params.width || 0;
      obj.height = params.height || 0;

      if (params.class_name) obj.className = params.class_name;
      if (params.rotation !== undefined) obj.rotation = params.rotation;
      if (params.visible !== undefined) obj.visible = params.visible;

      var shape = (params.shape || "rectangle").toLowerCase();
      switch (shape) {
        case "rectangle":
          obj.shape = MapObject.Rectangle;
          break;
        case "ellipse":
          obj.shape = MapObject.Ellipse;
          break;
        case "point":
          obj.shape = MapObject.Point;
          break;
        case "polygon":
          obj.shape = MapObject.Polygon;
          if (params.polygon) {
            obj.polygon = params.polygon.map(function (p) {
              return Qt.point(p.x, p.y);
            });
          }
          break;
        case "polyline":
          obj.shape = MapObject.Polyline;
          if (params.polygon) {
            obj.polygon = params.polygon.map(function (p) {
              return Qt.point(p.x, p.y);
            });
          }
          break;
        case "text":
          obj.shape = MapObject.Text;
          if (params.text) obj.text = params.text;
          if (params.word_wrap !== undefined) obj.wordWrap = params.word_wrap;
          break;
      }

      if (params.tileset && params.tile_id !== undefined) {
        var ts = null;
        for (var i = 0; i < map.tilesets.length; i++) {
          if (map.tilesets[i].name === params.tileset) {
            ts = map.tilesets[i];
            break;
          }
        }
        if (ts) {
          var tile = ts.tile(params.tile_id);
          if (tile) {
            obj.tile = tile;
          }
        }
      }

      map.macro("Add object", function () {
        layer.addObject(obj);
      });

      return {
        success: true,
        id: obj.id,
        name: obj.name,
        shape: shape,
        x: obj.x,
        y: obj.y
      };
    },

    update_object: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined) throw new Error("id is required");

      var layer = requireObjectLayer(map, params.layer);
      var obj = findObjectById(layer, params.id);
      if (!obj) throw new Error("Object not found with id: " + params.id);

      map.macro("Update object", function () {
        if (params.name !== undefined) obj.name = params.name;
        if (params.class_name !== undefined) obj.className = params.class_name;
        if (params.x !== undefined) obj.x = params.x;
        if (params.y !== undefined) obj.y = params.y;
        if (params.width !== undefined) obj.width = params.width;
        if (params.height !== undefined) obj.height = params.height;
        if (params.rotation !== undefined) obj.rotation = params.rotation;
        if (params.visible !== undefined) obj.visible = params.visible;
        if (params.polygon) {
          obj.polygon = params.polygon.map(function (p) {
            return Qt.point(p.x, p.y);
          });
        }
        if (params.text !== undefined) obj.text = params.text;
      });

      return {
        success: true,
        id: obj.id,
        name: obj.name
      };
    },

    remove_object: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined) throw new Error("id is required");

      var layer = requireObjectLayer(map, params.layer);
      var obj = findObjectById(layer, params.id);
      if (!obj) throw new Error("Object not found with id: " + params.id);

      map.macro("Remove object", function () {
        layer.removeObject(obj);
      });

      return { success: true, id: params.id };
    },

    add_objects_batch: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (!params.objects || !Array.isArray(params.objects)) {
        throw new Error("objects array is required");
      }

      var layer = requireObjectLayer(map, params.layer);
      var ids = [];

      map.macro("Add objects batch", function () {
        for (var i = 0; i < params.objects.length; i++) {
          var spec = params.objects[i];
          var obj = new MapObject(spec.name || "");
          obj.x = spec.x || 0;
          obj.y = spec.y || 0;
          obj.width = spec.width || 0;
          obj.height = spec.height || 0;
          if (spec.class_name) obj.className = spec.class_name;
          if (spec.rotation !== undefined) obj.rotation = spec.rotation;

          var shape = (spec.shape || "rectangle").toLowerCase();
          if (shape === "rectangle") obj.shape = MapObject.Rectangle;
          else if (shape === "ellipse") obj.shape = MapObject.Ellipse;
          else if (shape === "point") obj.shape = MapObject.Point;

          layer.addObject(obj);
          ids.push(obj.id);
        }
      });

      return {
        success: true,
        objectIds: ids,
        count: ids.length
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Property Commands
// ══════════════════════════════════════════════════════════════════════════

function getPropertyCommands() {
  return {
    get_property: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("property name is required");

      var element = resolveTarget(map, params);
      var value = element.property(params.name);

      return {
        target: params.target,
        name: params.name,
        value: value !== undefined ? value : null,
        exists: value !== undefined
      };
    },

    set_property: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("property name is required");
      if (params.value === undefined) throw new Error("property value is required");

      var element = resolveTarget(map, params);

      map.macro("Set property", function () {
        element.setProperty(params.name, params.value);
      });

      return {
        success: true,
        target: params.target,
        name: params.name,
        value: params.value
      };
    },

    remove_property: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("property name is required");

      var element = resolveTarget(map, params);

      map.macro("Remove property", function () {
        element.removeProperty(params.name);
      });

      return {
        success: true,
        target: params.target,
        name: params.name
      };
    },

    get_properties: function(params) {
      var map = requireMap();

      var element = resolveTarget(map, params);

      var own = {};
      var ownProps = element.properties;
      if (ownProps) {
        for (var key in ownProps) {
          if (Object.prototype.hasOwnProperty.call(ownProps, key)) {
            own[key] = ownProps[key];
          }
        }
      }

      var resolved = {};
      try {
        var resolvedProps = element.resolvedProperties();
        for (var rkey in resolvedProps) {
          if (Object.prototype.hasOwnProperty.call(resolvedProps, rkey)) {
            resolved[rkey] = resolvedProps[rkey];
          }
        }
      } catch (e) {
        // resolvedProperties might not be available on all elements
      }

      return {
        target: params.target,
        properties: own,
        resolved: resolved
      };
    },

    set_properties_batch: function(params) {
      var map = requireMap();
      if (!params.properties || typeof params.properties !== "object") {
        throw new Error("properties object is required");
      }

      var element = resolveTarget(map, params);

      map.macro("Set properties batch", function () {
        for (var key in params.properties) {
          if (Object.prototype.hasOwnProperty.call(params.properties, key)) {
            element.setProperty(key, params.properties[key]);
          }
        }
      });

      return {
        success: true,
        target: params.target,
        propertiesSet: Object.keys(params.properties).length
      };
    },

    get_map_properties: function(_params) {
      var map = requireMap();
      var props = {};
      var ownProps = map.properties;
      if (ownProps) {
        for (var key in ownProps) {
          if (Object.prototype.hasOwnProperty.call(ownProps, key)) {
            props[key] = ownProps[key];
          }
        }
      }
      return { properties: props };
    },

    set_map_property: function(params) {
      var map = requireMap();
      if (!params.name) throw new Error("property name is required");
      if (params.value === undefined) throw new Error("property value is required");

      map.macro("Set map property", function () {
        map.setProperty(params.name, params.value);
      });

      return {
        success: true,
        name: params.name,
        value: params.value
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Terrain Commands
// ══════════════════════════════════════════════════════════════════════════

function getTerrainCommands() {
  return {
    get_terrain_sets: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var sets = [];
      var wangSets = ts.wangSets;
      for (var i = 0; i < wangSets.length; i++) {
        sets.push(describeWangSet(wangSets[i]));
      }

      return { tileset: params.tileset, terrainSets: sets };
    },

    get_terrain_set_info: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var ws = findWangSet(ts, params.name);
      if (!ws) throw new Error("Terrain set not found: " + params.name);

      var info = describeWangSet(ws);

      var tileAssignments = [];
      for (var i = 0; i < ts.tileCount; i++) {
        var tile = ts.tile(i);
        if (!tile) continue;
        var wangId = ws.wangId(tile);
        if (wangId && wangId.some(function (v) { return v > 0; })) {
          tileAssignments.push({
            tileId: tile.id,
            wangId: wangId
          });
        }
      }
      info.tileAssignments = tileAssignments;

      return info;
    },

    add_terrain_set: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var typeMap = {
        corner: WangSet.Corner,
        edge: WangSet.Edge,
        mixed: WangSet.Mixed
      };
      var type = typeMap[(params.type || "corner").toLowerCase()];
      if (type === undefined) {
        throw new Error("Invalid terrain set type: " + params.type + ". Use: corner, edge, mixed");
      }

      var ws = ts.addWangSet(params.name, type);

      return {
        success: true,
        tileset: params.tileset,
        name: ws.name,
        type: params.type || "corner"
      };
    },

    remove_terrain_set: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.name) throw new Error("name is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var ws = findWangSet(ts, params.name);
      if (!ws) throw new Error("Terrain set not found: " + params.name);

      ts.removeWangSet(ws);

      return { success: true, tileset: params.tileset, name: params.name };
    },

    add_terrain_color: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (!params.name) throw new Error("name is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      var color = params.color || "#ff0000";
      var probability = params.probability !== undefined ? params.probability : 1.0;

      ws.addWangColor(params.name, color, probability);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        colorName: params.name,
        colorCount: ws.colorCount
      };
    },

    remove_terrain_color: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (params.index === undefined) throw new Error("color index is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      ws.removeWangColorAt(params.index);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        colorCount: ws.colorCount
      };
    },

    set_terrain_tile: function(params) {
      var map = requireMap();
      if (!params.tileset) throw new Error("tileset is required");
      if (!params.terrain_set) throw new Error("terrain_set is required");
      if (params.tile_id === undefined) throw new Error("tile_id is required");
      if (!params.wang_id) throw new Error("wang_id is required");

      var ts = findTileset(map, params.tileset);
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var ws = findWangSet(ts, params.terrain_set);
      if (!ws) throw new Error("Terrain set not found: " + params.terrain_set);

      var tile = ts.tile(params.tile_id);
      if (!tile) throw new Error("Tile not found: " + params.tile_id);

      ws.setWangId(tile, params.wang_id);

      return {
        success: true,
        tileset: params.tileset,
        terrainSet: params.terrain_set,
        tileId: params.tile_id,
        wangId: params.wang_id
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Editor Commands
// ══════════════════════════════════════════════════════════════════════════

function getEditorCommands() {
  return {
    ping: function(_params) {
      return { pong: true, timestamp: Date.now() };
    },

    pong: function(_params) {
      return { ok: true };
    },

    get_editor_info: function(_params) {
      var info = {
        version: tiled.version,
        platform: tiled.platform,
        extensionVersion: "1.0.0"
      };

      if (tiled.activeAsset) {
        info.activeAsset = {
          fileName: tiled.activeAsset.fileName,
          isTileMap: tiled.activeAsset.isTileMap
        };
      }

      info.openAssetCount = tiled.openAssets ? tiled.openAssets.length : 0;

      return info;
    },

    get_open_assets: function(_params) {
      var assets = [];
      if (tiled.openAssets) {
        var openAssets = tiled.openAssets;
        for (var i = 0; i < openAssets.length; i++) {
          var asset = openAssets[i];
          assets.push({
            fileName: asset.fileName,
            isTileMap: asset.isTileMap,
            isActive: asset === tiled.activeAsset,
            modified: asset.modified || false
          });
        }
      }
      return { assets: assets };
    },

    trigger_action: function(params) {
      if (!params.action) throw new Error("action is required");
      tiled.trigger(params.action);
      return { success: true, action: params.action };
    },

    log_message: function(params) {
      var msg = params.message || "";
      tiled.log("[MCP] " + msg);
      return { success: true };
    },

    get_current_tool: function(_params) {
      var result = {};
      if (tiled.mapEditor) {
        result.currentTool = tiled.mapEditor.currentToolName || null;
      }
      return result;
    },

    set_active_asset: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      var openAssets = tiled.openAssets;
      for (var i = 0; i < openAssets.length; i++) {
        if (openAssets[i].fileName === params.file_path) {
          tiled.activeAsset = openAssets[i];
          return { success: true, fileName: openAssets[i].fileName };
        }
      }
      throw new Error("Asset not found: " + params.file_path);
    },

    get_selected_objects: function(_params) {
      var map = requireMap();
      var selected = [];
      if (tiled.mapEditor && tiled.mapEditor.selectedObjects) {
        var selObjs = tiled.mapEditor.selectedObjects;
        for (var i = 0; i < selObjs.length; i++) {
          var obj = selObjs[i];
          selected.push({
            id: obj.id,
            name: obj.name,
            className: obj.className || "",
            x: obj.x,
            y: obj.y
          });
        }
      }
      return { selectedObjects: selected, count: selected.length };
    },

    get_selected_area: function(_params) {
      var map = requireMap();
      var result = { hasSelection: false };
      if (tiled.mapEditor && tiled.mapEditor.selectedArea) {
        var area = tiled.mapEditor.selectedArea;
        if (area && area.boundingRect) {
          var rect = area.boundingRect;
          result.hasSelection = true;
          result.boundingRect = {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height
          };
        }
      }
      return result;
    },

    reload_extension: function(_params) {
      tiled.trigger("ReloadScriptingExtensions");
      return {
        success: true,
        note: "Extensions are being reloaded. Connection will be re-established."
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Project Commands
// ══════════════════════════════════════════════════════════════════════════

function getProjectCommands() {
  return {
    get_project_info: function(_params) {
      var info = {
        hasProject: false
      };

      if (tiled.project) {
        info.hasProject = true;
        info.fileName = tiled.project.fileName || null;
        info.extensionPath = tiled.project.extensionPath || null;

        if (tiled.project.folders) {
          info.folders = [];
          var folders = tiled.project.folders;
          for (var i = 0; i < folders.length; i++) {
            info.folders.push(folders[i]);
          }
        }
      }

      info.openAssets = [];
      var openAssets = tiled.openAssets;
      for (var j = 0; j < openAssets.length; j++) {
        info.openAssets.push({
          fileName: openAssets[j].fileName,
          isTileMap: openAssets[j].isTileMap
        });
      }

      return info;
    },

    list_files: function(params) {
      if (!params.directory) throw new Error("directory is required");

      var dir = params.directory;
      var filter = params.filter || "*";
      var recursive = params.recursive || false;

      var files = [];

      function listDir(path, depth) {
        try {
          var entries = File.directoryEntries(path);
          for (var i = 0; i < entries.length; i++) {
            var entry = entries[i];
            if (entry === "." || entry === "..") continue;
            var fullPath = path + "/" + entry;
            var isDir = File.exists(fullPath + "/.");

            if (isDir) {
              files.push({ path: fullPath, type: "directory" });
              if (recursive && depth < 10) {
                listDir(fullPath, depth + 1);
              }
            } else {
              if (filter === "*" || entry.endsWith(filter.replace("*", ""))) {
                files.push({ path: fullPath, type: "file", name: entry });
              }
            }
          }
        } catch (e) {
          // Directory might not exist or be inaccessible
        }
      }

      listDir(dir, 0);

      return { directory: dir, files: files, count: files.length };
    },

    read_file: function(params) {
      if (!params.file_path) throw new Error("file_path is required");

      try {
        var file = new TextFile(params.file_path, TextFile.ReadOnly);
        var content = file.readAll();
        file.close();

        return {
          file_path: params.file_path,
          content: content,
          size: content.length
        };
      } catch (e) {
        throw new Error("Failed to read file: " + params.file_path + " - " + e);
      }
    },

    write_file: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (params.content === undefined) throw new Error("content is required");

      try {
        var file = new TextFile(params.file_path, TextFile.WriteOnly);
        file.write(params.content);
        file.close();

        return {
          success: true,
          file_path: params.file_path,
          size: params.content.length
        };
      } catch (e) {
        throw new Error("Failed to write file: " + params.file_path + " - " + e);
      }
    },

    file_exists: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      return {
        file_path: params.file_path,
        exists: File.exists(params.file_path)
      };
    },

    get_worlds: function(_params) {
      var worlds = [];
      if (tiled.worlds) {
        var tiledWorlds = tiled.worlds;
        for (var i = 0; i < tiledWorlds.length; i++) {
          var world = tiledWorlds[i];
          var maps = [];
          if (world.maps) {
            var worldMaps = world.maps;
            for (var j = 0; j < worldMaps.length; j++) {
              var wm = worldMaps[j];
              maps.push({
                fileName: wm.fileName,
                rect: wm.rect ? {
                  x: wm.rect.x,
                  y: wm.rect.y,
                  width: wm.rect.width,
                  height: wm.rect.height
                } : null
              });
            }
          }
          worlds.push({
            fileName: world.fileName,
            maps: maps
          });
        }
      }
      return { worlds: worlds };
    },

    open_file: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      var asset = tiled.open(params.file_path);
      if (!asset) throw new Error("Failed to open: " + params.file_path);
      return {
        success: true,
        fileName: asset.fileName,
        isTileMap: asset.isTileMap
      };
    },

    close_file: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      var openAssets = tiled.openAssets;
      for (var i = 0; i < openAssets.length; i++) {
        if (openAssets[i].fileName === params.file_path) {
          tiled.close(openAssets[i]);
          return { success: true, fileName: params.file_path };
        }
      }
      throw new Error("Asset not open: " + params.file_path);
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Image Commands
// ══════════════════════════════════════════════════════════════════════════

function getImageCommands() {
  return {
    create_image: function(params) {
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }
      if (!params.output_path) throw new Error("output_path is required");

      var img = new Image(params.width, params.height);

      if (params.fill_color) {
        img.fill(params.fill_color);
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        width: params.width,
        height: params.height
      };
    },

    load_image_info: function(params) {
      if (!params.file_path) throw new Error("file_path is required");

      var img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      return {
        file_path: params.file_path,
        width: img.width,
        height: img.height
      };
    },

    get_pixel: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }

      var img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      if (params.x < 0 || params.x >= img.width || params.y < 0 || params.y >= img.height) {
        throw new Error("Coordinates out of bounds");
      }

      var color = img.pixel(params.x, params.y);

      return {
        x: params.x,
        y: params.y,
        color: color
      };
    },

    set_pixels: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (!params.pixels || !Array.isArray(params.pixels)) {
        throw new Error("pixels array is required");
      }
      if (!params.output_path) throw new Error("output_path is required");

      var img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      for (var i = 0; i < params.pixels.length; i++) {
        var p = params.pixels[i];
        if (p.x >= 0 && p.x < img.width && p.y >= 0 && p.y < img.height) {
          img.setPixel(p.x, p.y, p.color);
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        pixelsModified: params.pixels.length
      };
    },

    fill_image_rect: function(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (!params.output_path) throw new Error("output_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }
      if (!params.color) throw new Error("color is required");

      var img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      for (var dy = 0; dy < params.height; dy++) {
        for (var dx = 0; dx < params.width; dx++) {
          var px = params.x + dx;
          var py = params.y + dy;
          if (px >= 0 && px < img.width && py >= 0 && py < img.height) {
            img.setPixel(px, py, params.color);
          }
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        region: { x: params.x, y: params.y, width: params.width, height: params.height }
      };
    },

    copy_image_region: function(params) {
      if (!params.source_path) throw new Error("source_path is required");
      if (!params.output_path) throw new Error("output_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }

      var src = new Image(params.source_path);
      if (!src || src.width === 0) {
        throw new Error("Failed to load source image");
      }

      var region = src.copy(params.x, params.y, params.width, params.height);
      region.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        width: params.width,
        height: params.height
      };
    },

    generate_tileset_image: function(params) {
      if (!params.output_path) throw new Error("output_path is required");
      if (!params.tile_width || !params.tile_height) {
        throw new Error("tile_width and tile_height are required");
      }
      if (!params.columns || !params.rows) {
        throw new Error("columns and rows are required");
      }

      var spacing = params.spacing || 0;
      var margin = params.margin || 0;

      var totalW = margin * 2 + params.columns * params.tile_width + (params.columns - 1) * spacing;
      var totalH = margin * 2 + params.rows * params.tile_height + (params.rows - 1) * spacing;

      var img = new Image(totalW, totalH);
      img.fill(params.background_color || "#00000000");

      if (params.draw_grid) {
        var gridColor = params.grid_color || "#cccccc";
        for (var col = 0; col <= params.columns; col++) {
          var gx = margin + col * (params.tile_width + spacing) - (col > 0 ? spacing : 0);
          for (var gy = 0; gy < totalH; gy++) {
            if (gx >= 0 && gx < totalW) img.setPixel(gx, gy, gridColor);
          }
        }
        for (var row = 0; row <= params.rows; row++) {
          var ry = margin + row * (params.tile_height + spacing) - (row > 0 ? spacing : 0);
          for (var rx = 0; rx < totalW; rx++) {
            if (ry >= 0 && ry < totalH) img.setPixel(rx, ry, gridColor);
          }
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        imageWidth: totalW,
        imageHeight: totalH,
        tileCount: params.columns * params.rows
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Export Commands
// ══════════════════════════════════════════════════════════════════════════

function getExportCommands() {
  return {
    get_export_formats: function(_params) {
      var mapFormats = [];
      if (tiled.mapFormats) {
        var mfNames = tiled.mapFormats;
        for (var i = 0; i < mfNames.length; i++) {
          var fmt = tiled.mapFormat(mfNames[i]);
          mapFormats.push({
            name: mfNames[i],
            canRead: fmt ? fmt.canRead : false,
            canWrite: fmt ? fmt.canWrite : false
          });
        }
      }

      var tilesetFormats = [];
      if (tiled.tilesetFormats) {
        var tfNames = tiled.tilesetFormats;
        for (var j = 0; j < tfNames.length; j++) {
          var tfmt = tiled.tilesetFormat(tfNames[j]);
          tilesetFormats.push({
            name: tfNames[j],
            canRead: tfmt ? tfmt.canRead : false,
            canWrite: tfmt ? tfmt.canWrite : false
          });
        }
      }

      return {
        mapFormats: mapFormats,
        tilesetFormats: tilesetFormats
      };
    },

    export_map: function(params) {
      var map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      var formatName = params.format || "tmx";
      var format = tiled.mapFormat(formatName);

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
        format: formatName
      };
    },

    export_tileset: function(params) {
      if (!params.tileset) throw new Error("tileset name is required");
      if (!params.output_path) throw new Error("output_path is required");

      var map = requireMap();
      var ts = null;
      for (var i = 0; i < map.tilesets.length; i++) {
        if (map.tilesets[i].name === params.tileset) {
          ts = map.tilesets[i];
          break;
        }
      }
      if (!ts) throw new Error("Tileset not found: " + params.tileset);

      var formatName = params.format || "tsx";
      var format = tiled.tilesetFormat(formatName);

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
        format: formatName
      };
    },

    export_as_image: function(params) {
      if (!params.output_path) throw new Error("output_path is required");
      var map = requireMap();

      tiled.trigger("ExportAsImage");

      return {
        success: true,
        note: "Export as Image dialog opened. For programmatic export, use export_map with an image-capable format."
      };
    },

    export_map_to_json: function(params) {
      var map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      var format = tiled.mapFormat("json");
      if (!format) {
        throw new Error("JSON map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "json"
      };
    },

    export_map_to_lua: function(params) {
      var map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      var format = tiled.mapFormat("lua");
      if (!format) {
        throw new Error("Lua map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "lua"
      };
    },

    export_map_to_csv: function(params) {
      var map = requireMap();
      if (!params.output_path) throw new Error("output_path is required");

      var format = tiled.mapFormat("csv");
      if (!format) {
        throw new Error("CSV map format not available");
      }

      format.write(map, params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        format: "csv"
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Analysis Commands
// ══════════════════════════════════════════════════════════════════════════

function getAnalysisCommands() {
  return {
    get_map_statistics: function(_params) {
      var map = requireMap();

      var tileLayerCount = 0;
      var objectLayerCount = 0;
      var groupLayerCount = 0;
      var imageLayerCount = 0;
      var totalObjects = 0;
      var totalTilesPlaced = 0;
      var emptyTiles = 0;

      function analyzeLayer(layer) {
        if (layer.isTileLayer) {
          tileLayerCount++;
          for (var y = 0; y < map.height; y++) {
            for (var x = 0; x < map.width; x++) {
              var cell = layer.cellAt(x, y);
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
          for (var i = 0; i < layer.layerCount; i++) {
            analyzeLayer(layer.layerAt(i));
          }
        } else if (layer.isImageLayer) {
          imageLayerCount++;
        }
      }

      for (var i = 0; i < map.layerCount; i++) {
        analyzeLayer(map.layerAt(i));
      }

      var totalCells = map.width * map.height * tileLayerCount;
      var fillPercentage = totalCells > 0
        ? Math.round((totalTilesPlaced / totalCells) * 10000) / 100
        : 0;

      var tilesetUsage = {};
      function countTilesetUsage(layer) {
        if (layer.isTileLayer) {
          for (var y = 0; y < map.height; y++) {
            for (var x = 0; x < map.width; x++) {
              var tile = layer.tileAt(x, y);
              if (tile && tile.tileset) {
                var name = tile.tileset.name;
                tilesetUsage[name] = (tilesetUsage[name] || 0) + 1;
              }
            }
          }
        } else if (layer.isGroupLayer) {
          for (var i = 0; i < layer.layerCount; i++) {
            countTilesetUsage(layer.layerAt(i));
          }
        }
      }
      for (var j = 0; j < map.layerCount; j++) {
        countTilesetUsage(map.layerAt(j));
      }

      return {
        mapSize: { width: map.width, height: map.height },
        tileSize: { width: map.tileWidth, height: map.tileHeight },
        pixelSize: {
          width: map.width * map.tileWidth,
          height: map.height * map.tileHeight
        },
        layers: {
          total: map.layerCount,
          tileLayers: tileLayerCount,
          objectLayers: objectLayerCount,
          groupLayers: groupLayerCount,
          imageLayers: imageLayerCount
        },
        tiles: {
          placed: totalTilesPlaced,
          empty: emptyTiles,
          totalCells: totalCells,
          fillPercentage: fillPercentage
        },
        objects: {
          total: totalObjects
        },
        tilesets: {
          count: map.tilesets.length,
          usage: tilesetUsage
        }
      };
    },

    get_layer_statistics: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = findLayer(map, params.layer);
      if (!layer) throw new Error("Layer not found: " + params.layer);

      var result = {
        name: layer.name,
        visible: layer.visible,
        locked: layer.locked,
        opacity: layer.opacity
      };

      if (layer.isTileLayer) {
        result.type = "tilelayer";
        var placed = 0;
        var empty = 0;
        var tileIds = {};

        for (var y = 0; y < map.height; y++) {
          for (var x = 0; x < map.width; x++) {
            var cell = layer.cellAt(x, y);
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

        var sorted = Object.entries(tileIds)
          .sort(function (a, b) { return b[1] - a[1]; })
          .slice(0, 10);
        result.mostUsedTiles = sorted.map(function (entry) {
          return { tileId: parseInt(entry[0]), count: entry[1] };
        });
      } else if (layer.isObjectLayer) {
        result.type = "objectgroup";
        result.objectCount = layer.objectCount;

        var classCounts = {};
        var shapeCounts = {};
        var objs = layer.objects;
        for (var oi = 0; oi < objs.length; oi++) {
          var obj = objs[oi];
          var cls = obj.className || "(none)";
          classCounts[cls] = (classCounts[cls] || 0) + 1;
          var shp = obj.shape !== undefined ? String(obj.shape) : "unknown";
          shapeCounts[shp] = (shapeCounts[shp] || 0) + 1;
        }
        result.classCounts = classCounts;
        result.shapeCounts = shapeCounts;
      }

      return result;
    },

    validate_map: function(params) {
      var map = requireMap();
      var issues = [];

      function checkLayers(parent) {
        for (var i = 0; i < parent.layerCount; i++) {
          var layer = parent.layerAt(i);

          if (layer.isTileLayer) {
            var hasContent = false;
            for (var y = 0; y < map.height && !hasContent; y++) {
              for (var x = 0; x < map.width && !hasContent; x++) {
                var cell = layer.cellAt(x, y);
                if (cell && cell.tileId !== -1) {
                  hasContent = true;
                }
              }
            }
            if (!hasContent) {
              issues.push({
                type: "warning",
                category: "empty_layer",
                message: "Tile layer '" + layer.name + "' has no tiles placed"
              });
            }
          }

          if (layer.isObjectLayer && layer.objectCount === 0) {
            issues.push({
              type: "info",
              category: "empty_layer",
              message: "Object layer '" + layer.name + "' has no objects"
            });
          }

          if (layer.isGroupLayer) {
            if (layer.layerCount === 0) {
              issues.push({
                type: "info",
                category: "empty_layer",
                message: "Group layer '" + layer.name + "' has no children"
              });
            }
            checkLayers(layer);
          }
        }
      }
      checkLayers(map);

      var usedTilesets = {};
      function collectUsedTilesets(parent) {
        for (var i = 0; i < parent.layerCount; i++) {
          var layer = parent.layerAt(i);
          if (layer.isTileLayer) {
            for (var y = 0; y < map.height; y++) {
              for (var x = 0; x < map.width; x++) {
                var tile = layer.tileAt(x, y);
                if (tile && tile.tileset) {
                  usedTilesets[tile.tileset.name] = true;
                }
              }
            }
          }
          if (layer.isGroupLayer) collectUsedTilesets(layer);
        }
      }
      collectUsedTilesets(map);

      var tilesets = map.tilesets;
      for (var ti = 0; ti < tilesets.length; ti++) {
        if (!usedTilesets[tilesets[ti].name]) {
          issues.push({
            type: "warning",
            category: "unused_tileset",
            message: "Tileset '" + tilesets[ti].name + "' is not used by any tile layer"
          });
        }
      }

      for (var mi = 0; mi < tilesets.length; mi++) {
        if (tilesets[mi].image && tilesets[mi].imageWidth === 0) {
          issues.push({
            type: "error",
            category: "missing_image",
            message: "Tileset '" + tilesets[mi].name + "' has a missing or broken image"
          });
        }
      }

      if (map.modified) {
        issues.push({
          type: "info",
          category: "unsaved",
          message: "Map has unsaved changes"
        });
      }

      if (!map.fileName) {
        issues.push({
          type: "warning",
          category: "no_filename",
          message: "Map has not been saved to a file yet"
        });
      }

      return {
        valid: issues.filter(function (i) { return i.type === "error"; }).length === 0,
        issues: issues,
        errorCount: issues.filter(function (i) { return i.type === "error"; }).length,
        warningCount: issues.filter(function (i) { return i.type === "warning"; }).length,
        infoCount: issues.filter(function (i) { return i.type === "info"; }).length
      };
    },

    compare_layers: function(params) {
      var map = requireMap();
      if (!params.layer_a) throw new Error("layer_a is required");
      if (!params.layer_b) throw new Error("layer_b is required");

      var a = findLayer(map, params.layer_a);
      var b = findLayer(map, params.layer_b);
      if (!a) throw new Error("Layer not found: " + params.layer_a);
      if (!b) throw new Error("Layer not found: " + params.layer_b);
      if (!a.isTileLayer || !b.isTileLayer) {
        throw new Error("Both layers must be tile layers");
      }

      var matching = 0;
      var different = 0;
      var onlyInA = 0;
      var onlyInB = 0;
      var differences = [];

      for (var y = 0; y < map.height; y++) {
        for (var x = 0; x < map.width; x++) {
          var cellA = a.cellAt(x, y);
          var cellB = b.cellAt(x, y);
          var aEmpty = !cellA || cellA.tileId === -1;
          var bEmpty = !cellB || cellB.tileId === -1;

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
                tileIdB: cellB.tileId
              });
            }
          }
        }
      }

      var total = map.width * map.height;

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
        differencesTruncated: differences.length >= 100
      };
    },

    find_tile_patterns: function(params) {
      var map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      var layer = findLayer(map, params.layer);
      if (!layer || !layer.isTileLayer) {
        throw new Error("Tile layer not found: " + params.layer);
      }

      var patternSize = params.size || 2;
      var patterns = {};

      for (var y = 0; y <= map.height - patternSize; y++) {
        for (var x = 0; x <= map.width - patternSize; x++) {
          var key = [];
          for (var dy = 0; dy < patternSize; dy++) {
            for (var dx = 0; dx < patternSize; dx++) {
              var cell = layer.cellAt(x + dx, y + dy);
              key.push(cell ? cell.tileId : -1);
            }
          }
          var patternKey = key.join(",");
          if (!patterns[patternKey]) {
            patterns[patternKey] = { count: 0, firstAt: { x: x, y: y }, tileIds: key };
          }
          patterns[patternKey].count++;
        }
      }

      var sorted = Object.values(patterns)
        .filter(function (p) { return p.count > 1; })
        .sort(function (a, b) { return b.count - a.count; })
        .slice(0, params.limit || 20);

      return {
        layer: params.layer,
        patternSize: patternSize,
        uniquePatterns: Object.keys(patterns).length,
        repeatingPatterns: sorted.length,
        topPatterns: sorted
      };
    }
  };
}

// ══════════════════════════════════════════════════════════════════════════
// Router Setup
// ══════════════════════════════════════════════════════════════════════════

var router = new CommandRouter();

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

// ══════════════════════════════════════════════════════════════════════════
// HTTP Communication
// ══════════════════════════════════════════════════════════════════════════

var PORTS = [6510, 6511, 6512, 6513, 6514];
var activePort = null;
var mcpConnected = false;
var polling = false;

/**
 * Execute curl and return stdout. Process.exec blocks until done.
 * Returns null on failure.
 */
function curlGet(url) {
  var p = new Process();
  try {
    p.exec("curl", ["-s", "--max-time", "3", url]);
    var output = p.readStdOut();
    return output || null;
  } catch (e) {
    return null;
  }
}

/**
 * Execute curl POST with JSON body. Returns true on success.
 */
function curlPost(url, data) {
  var p = new Process();
  try {
    var jsonStr = JSON.stringify(data);
    p.exec("curl", [
      "-s",
      "--max-time", "3",
      "-X", "POST",
      "-H", "Content-Type: application/json",
      "-d", jsonStr,
      url
    ]);
    return true;
  } catch (e) {
    return false;
  }
}

/**
 * Find the MCP server by trying ports in order.
 * Returns the working port or null.
 */
function discoverPort() {
  if (activePort !== null) {
    var result = curlGet("http://127.0.0.1:" + activePort + "/status");
    if (result !== null) {
      return activePort;
    }
    activePort = null;
  }

  for (var i = 0; i < PORTS.length; i++) {
    var port = PORTS[i];
    var portResult = curlGet("http://127.0.0.1:" + port + "/status");
    if (portResult !== null) {
      tiled.log("[Tiled MCP] Found MCP server on port " + port);
      activePort = port;
      return port;
    }
  }
  return null;
}

/**
 * Poll for pending commands from the MCP server.
 */
function pollCommands() {
  if (polling) return;
  polling = true;

  try {
    var port = discoverPort();
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

    var baseUrl = "http://127.0.0.1:" + port;

    var rawResponse = curlGet(baseUrl + "/poll");
    if (!rawResponse || rawResponse.trim().length === 0) return;

    var commands;
    try {
      commands = JSON.parse(rawResponse);
    } catch (e) {
      tiled.log("[Tiled MCP] Failed to parse poll response: " + e);
      return;
    }

    if (!Array.isArray(commands) || commands.length === 0) return;

    tiled.log("[Tiled MCP] Received " + commands.length + " command(s)");

    for (var i = 0; i < commands.length; i++) {
      var request = commands[i];
      var response = processCommand(request);
      if (response) {
        var ok = curlPost(baseUrl + "/response", response);
        if (!ok) {
          tiled.log("[Tiled MCP] Failed to send response for " + request.method);
        }
      }
    }
  } finally {
    polling = false;
  }
}

// ══════════════════════════════════════════════════════════════════════════
// Command Processing
// ══════════════════════════════════════════════════════════════════════════

function processCommand(request) {
  var method = request.method;
  var params = request.params || {};
  var id = request.id;

  if (method === "ping") {
    return {
      jsonrpc: "2.0",
      result: { ok: true },
      id: id
    };
  }

  if (!router.hasMethod(method)) {
    tiled.log("[Tiled MCP] Unknown method: " + method);
    return {
      jsonrpc: "2.0",
      error: {
        code: -32601,
        message: "Method not found: " + method
      },
      id: id
    };
  }

  try {
    var result = router.execute(method, params);
    return {
      jsonrpc: "2.0",
      result: result,
      id: id
    };
  } catch (e) {
    var errorMsg = e.message || String(e);
    tiled.log("[Tiled MCP] Command error (" + method + "): " + errorMsg);
    return {
      jsonrpc: "2.0",
      error: {
        code: -32603,
        message: errorMsg
      },
      id: id
    };
  }
}

// ══════════════════════════════════════════════════════════════════════════
// Automatic Polling via Qt.callLater + Process timer
// ══════════════════════════════════════════════════════════════════════════
//
// Tiled's QJSEngine has no setInterval/setTimeout. We use Qt.callLater
// (which fires on next event loop iteration) combined with a non-blocking
// Process.start("node -e setTimeout(...)") as a delay mechanism.
// This gives us ~500ms polling with no UI blocking and no crashes.

var POLL_INTERVAL_MS = 500;
var _timerProcess = null;
var _autoPolling = true;

function autoPollTick() {
  if (!_autoPolling) return;

  if (_timerProcess === null) {
    // Start a new delay process
    _timerProcess = new Process();
    _timerProcess.start("node", [
      "-e",
      "setTimeout(function(){process.stdout.write('x');process.exit()}," + POLL_INTERVAL_MS + ")"
    ]);
  }

  try {
    var output = _timerProcess.readStdOut();
    if (output && output.length > 0) {
      // Timer fired — poll and restart
      _timerProcess = null;
      pollCommands();
    }
  } catch (e) {
    // Process was auto-closed, restart
    _timerProcess = null;
  }

  Qt.callLater(autoPollTick);
}

// Also poll on signal events for immediate responsiveness
if (tiled.activeAssetChanged) {
  tiled.activeAssetChanged.connect(function () { pollCommands(); });
}
if (tiled.assetOpened) {
  tiled.assetOpened.connect(function () { pollCommands(); });
}

// ══════════════════════════════════════════════════════════════════════════
// Menu Actions
// ══════════════════════════════════════════════════════════════════════════

var pollAction = tiled.registerAction("TiledMCP_Poll", function () {
  pollCommands();
});
pollAction.text = "MCP: Check for Commands";
pollAction.shortcut = "Ctrl+Shift+M";

var reconnectAction = tiled.registerAction("TiledMCP_Reconnect", function () {
  tiled.log("[Tiled MCP] Reconnecting...");
  activePort = null;
  mcpConnected = false;
  pollCommands();
});
reconnectAction.text = "MCP: Reconnect";

var statusAction = tiled.registerAction("TiledMCP_Status", function () {
  var status = mcpConnected
    ? "Connected (port " + activePort + ")"
    : "Not connected";
  var methods = router.getMethodList().length + " methods registered";
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

tiled.extendMenu("Map", [
  { separator: true },
  { action: "TiledMCP_Poll" },
  { action: "TiledMCP_Reconnect" },
  { action: "TiledMCP_Status" }
]);

// ══════════════════════════════════════════════════════════════════════════
// Extension Lifecycle
// ══════════════════════════════════════════════════════════════════════════

// Start auto-polling loop
autoPollTick();

tiled.log(
  "[Tiled MCP Pro] Extension loaded - " +
  router.getMethodList().length +
  " commands available, auto-polling every " + POLL_INTERVAL_MS + "ms"
);

})();
