/**
 * Object Commands - CRUD for map objects.
 * Object types: rectangle, ellipse, point, polygon, polyline, text, tile.
 *
 * Key Tiled API:
 *   new MapObject(name?)
 *   obj.x, obj.y, obj.width, obj.height, obj.rotation
 *   obj.name, obj.className, obj.shape
 *   obj.polygon, obj.text, obj.tile
 *   objectGroup.addObject(obj), objectGroup.removeObject(obj)
 *   objectGroup.objects - array of objects
 */

/* global tiled, MapObject */

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

function requireObjectLayer(map, name) {
  const layer = findLayer(map, name);
  if (!layer) throw new Error("Layer not found: " + name);
  if (!layer.isObjectLayer) throw new Error("Layer is not an object layer: " + name);
  return layer;
}

function findObjectById(layer, id) {
  for (const obj of layer.objects) {
    if (obj.id === id) return obj;
  }
  return null;
}

function findObjectByName(layer, name) {
  for (const obj of layer.objects) {
    if (obj.name === name) return obj;
  }
  return null;
}

function describeObject(obj) {
  const info = {
    id: obj.id,
    name: obj.name,
    className: obj.className || "",
    x: obj.x,
    y: obj.y,
    width: obj.width,
    height: obj.height,
    rotation: obj.rotation,
    visible: obj.visible,
    shape: obj.shape,
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

export function getCommands() {
  return {
    // ── get_objects ───────────────────────────────────────────────────
    get_objects(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = requireObjectLayer(map, params.layer);
      const objects = [];
      for (const obj of layer.objects) {
        const info = describeObject(obj);
        // Optional filtering
        if (params.class_name && info.className !== params.class_name) continue;
        if (params.name && info.name !== params.name) continue;
        objects.push(info);
      }

      return { layer: params.layer, objects: objects, count: objects.length };
    },

    // ── get_object ────────────────────────────────────────────────────
    get_object(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined && !params.name) {
        throw new Error("id or name is required");
      }

      const layer = requireObjectLayer(map, params.layer);
      let obj;
      if (params.id !== undefined) {
        obj = findObjectById(layer, params.id);
        if (!obj) throw new Error("Object not found with id: " + params.id);
      } else {
        obj = findObjectByName(layer, params.name);
        if (!obj) throw new Error("Object not found with name: " + params.name);
      }

      const info = describeObject(obj);

      // Include custom properties
      const props = {};
      for (const key of Object.keys(obj.resolvedProperties())) {
        props[key] = obj.resolvedProperty(key);
      }
      if (Object.keys(props).length > 0) {
        info.properties = props;
      }

      return info;
    },

    // ── add_object ────────────────────────────────────────────────────
    add_object(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");

      const layer = requireObjectLayer(map, params.layer);
      const obj = new MapObject(params.name || "");

      obj.x = params.x || 0;
      obj.y = params.y || 0;
      obj.width = params.width || 0;
      obj.height = params.height || 0;

      if (params.class_name) obj.className = params.class_name;
      if (params.rotation !== undefined) obj.rotation = params.rotation;
      if (params.visible !== undefined) obj.visible = params.visible;

      // Shape handling
      const shape = (params.shape || "rectangle").toLowerCase();
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

      // Tile object
      if (params.tileset && params.tile_id !== undefined) {
        let ts = null;
        for (let i = 0; i < map.tilesets.length; i++) {
          if (map.tilesets[i].name === params.tileset) {
            ts = map.tilesets[i];
            break;
          }
        }
        if (ts) {
          const tile = ts.tile(params.tile_id);
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
        y: obj.y,
      };
    },

    // ── update_object ─────────────────────────────────────────────────
    update_object(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined) throw new Error("id is required");

      const layer = requireObjectLayer(map, params.layer);
      const obj = findObjectById(layer, params.id);
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
        name: obj.name,
      };
    },

    // ── remove_object ─────────────────────────────────────────────────
    remove_object(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (params.id === undefined) throw new Error("id is required");

      const layer = requireObjectLayer(map, params.layer);
      const obj = findObjectById(layer, params.id);
      if (!obj) throw new Error("Object not found with id: " + params.id);

      map.macro("Remove object", function () {
        layer.removeObject(obj);
      });

      return { success: true, id: params.id };
    },

    // ── add_objects_batch ─────────────────────────────────────────────
    add_objects_batch(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer is required");
      if (!params.objects || !Array.isArray(params.objects)) {
        throw new Error("objects array is required");
      }

      const layer = requireObjectLayer(map, params.layer);
      const ids = [];

      map.macro("Add objects batch", function () {
        for (const spec of params.objects) {
          const obj = new MapObject(spec.name || "");
          obj.x = spec.x || 0;
          obj.y = spec.y || 0;
          obj.width = spec.width || 0;
          obj.height = spec.height || 0;
          if (spec.class_name) obj.className = spec.class_name;
          if (spec.rotation !== undefined) obj.rotation = spec.rotation;

          const shape = (spec.shape || "rectangle").toLowerCase();
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
        count: ids.length,
      };
    },
  };
}
