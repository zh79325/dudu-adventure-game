/**
 * Layer Commands - add/remove/configure layers, visibility, opacity, parallax, tint.
 *
 * Layer types in Tiled scripting API:
 *   TileLayer, ObjectGroup, GroupLayer, ImageLayer
 *   layer.isTileLayer, layer.isObjectLayer, layer.isGroupLayer, layer.isImageLayer
 */

/* global tiled, TileLayer, ObjectGroup, GroupLayer, ImageLayer */

function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

/**
 * Find a layer by name in the map (searches recursively through groups).
 */
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

/**
 * Find the index of a layer by name in the top-level layer list.
 */
function findLayerIndex(map, name) {
  for (let i = 0; i < map.layerCount; i++) {
    if (map.layerAt(i).name === name) return i;
  }
  return -1;
}

function describeLayer(layer, index) {
  const info = {
    index: index,
    name: layer.name,
    visible: layer.visible,
    locked: layer.locked,
    opacity: layer.opacity,
    offset: { x: layer.offset.x, y: layer.offset.y },
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

export function getCommands() {
  return {
    // ── get_layers ────────────────────────────────────────────────────
    get_layers(_params) {
      const map = requireMap();
      const layers = [];

      function collectLayers(parent, depth) {
        for (let i = 0; i < parent.layerCount; i++) {
          const layer = parent.layerAt(i);
          const info = describeLayer(layer, i);
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

    // ── add_tile_layer ────────────────────────────────────────────────
    add_tile_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");

      const layer = new TileLayer(params.name);

      map.macro("Add tile layer", function () {
        if (params.above) {
          const idx = findLayerIndex(map, params.above);
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

    // ── add_object_layer ──────────────────────────────────────────────
    add_object_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");

      const layer = new ObjectGroup(params.name);

      map.macro("Add object layer", function () {
        if (params.above) {
          const idx = findLayerIndex(map, params.above);
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

    // ── add_group_layer ───────────────────────────────────────────────
    add_group_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");

      const layer = new GroupLayer(params.name);
      map.macro("Add group layer", function () {
        map.addLayer(layer);
      });

      return { success: true, name: layer.name, type: "group" };
    },

    // ── add_image_layer ───────────────────────────────────────────────
    add_image_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (!params.image_path) throw new Error("image_path is required");

      const layer = new ImageLayer(params.name);
      layer.imageFileName = params.image_path;

      map.macro("Add image layer", function () {
        map.addLayer(layer);
      });

      return { success: true, name: layer.name, type: "imagelayer" };
    },

    // ── remove_layer ──────────────────────────────────────────────────
    remove_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");

      const layer = findLayer(map, params.name);
      if (!layer) throw new Error("Layer not found: " + params.name);

      map.macro("Remove layer", function () {
        map.removeLayer(layer);
      });

      return { success: true, name: params.name };
    },

    // ── set_layer_property ────────────────────────────────────────────
    set_layer_property(params) {
      const map = requireMap();
      if (!params.layer) throw new Error("layer name is required");
      if (!params.property) throw new Error("property name is required");

      const layer = findLayer(map, params.layer);
      if (!layer) throw new Error("Layer not found: " + params.layer);

      map.macro("Set layer property", function () {
        const prop = params.property;
        const val = params.value;

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

    // ── rename_layer ──────────────────────────────────────────────────
    rename_layer(params) {
      const map = requireMap();
      if (!params.old_name) throw new Error("old_name is required");
      if (!params.new_name) throw new Error("new_name is required");

      const layer = findLayer(map, params.old_name);
      if (!layer) throw new Error("Layer not found: " + params.old_name);

      map.macro("Rename layer", function () {
        layer.name = params.new_name;
      });

      return { success: true, old_name: params.old_name, new_name: params.new_name };
    },

    // ── reorder_layer ─────────────────────────────────────────────────
    reorder_layer(params) {
      const map = requireMap();
      if (!params.name) throw new Error("name is required");
      if (params.index === undefined) throw new Error("index is required");

      const layer = findLayer(map, params.name);
      if (!layer) throw new Error("Layer not found: " + params.name);

      map.macro("Reorder layer", function () {
        map.removeLayer(layer);
        map.insertLayerAt(Math.min(params.index, map.layerCount), layer);
      });

      return { success: true, name: params.name, newIndex: params.index };
    },
  };
}
