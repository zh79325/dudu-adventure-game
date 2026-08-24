/**
 * Property Commands - custom properties on all element types.
 *
 * Tiled API:
 *   element.property(name)           - get a custom property value
 *   element.setProperty(name, value) - set a custom property
 *   element.removeProperty(name)     - remove a custom property
 *   element.properties               - object with all custom properties
 *   element.resolvedProperties()     - includes inherited properties
 *
 * Elements can be: Map, Layer, MapObject, Tile, Tileset, WangSet, WangColor
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

/**
 * Resolve the target element from params.
 * Supports: "map", "layer:<name>", "object:<layer>:<id>", "tile:<tileset>:<id>"
 */
function resolveTarget(map, params) {
  const target = params.target;
  if (!target) throw new Error("target is required");

  if (target === "map") {
    return map;
  }

  if (target.startsWith("layer:")) {
    const layerName = target.substring(6);
    const layer = findLayer(map, layerName);
    if (!layer) throw new Error("Layer not found: " + layerName);
    return layer;
  }

  if (target.startsWith("object:")) {
    const parts = target.substring(7).split(":");
    if (parts.length < 2) throw new Error("object target format: object:<layer>:<id>");
    const layerName = parts[0];
    const objId = parseInt(parts[1], 10);
    const layer = findLayer(map, layerName);
    if (!layer || !layer.isObjectLayer) {
      throw new Error("Object layer not found: " + layerName);
    }
    for (const obj of layer.objects) {
      if (obj.id === objId) return obj;
    }
    throw new Error("Object not found: id=" + objId + " in layer " + layerName);
  }

  if (target.startsWith("tile:")) {
    const parts = target.substring(5).split(":");
    if (parts.length < 2) throw new Error("tile target format: tile:<tileset>:<id>");
    const tsName = parts[0];
    const tileId = parseInt(parts[1], 10);
    let ts = null;
    for (let i = 0; i < map.tilesets.length; i++) {
      if (map.tilesets[i].name === tsName) { ts = map.tilesets[i]; break; }
    }
    if (!ts) throw new Error("Tileset not found: " + tsName);
    const tile = ts.tile(tileId);
    if (!tile) throw new Error("Tile not found: " + tileId + " in " + tsName);
    return tile;
  }

  // Try as a layer name directly (convenience)
  const layer = findLayer(map, target);
  if (layer) return layer;

  throw new Error(
    "Invalid target: " + target +
    ". Use: 'map', 'layer:<name>', 'object:<layer>:<id>', 'tile:<tileset>:<id>'"
  );
}

export function getCommands() {
  return {
    // ── get_property ──────────────────────────────────────────────────
    get_property(params) {
      const map = requireMap();
      if (!params.name) throw new Error("property name is required");

      const element = resolveTarget(map, params);
      const value = element.property(params.name);

      return {
        target: params.target,
        name: params.name,
        value: value !== undefined ? value : null,
        exists: value !== undefined,
      };
    },

    // ── set_property ──────────────────────────────────────────────────
    set_property(params) {
      const map = requireMap();
      if (!params.name) throw new Error("property name is required");
      if (params.value === undefined) throw new Error("property value is required");

      const element = resolveTarget(map, params);

      map.macro("Set property", function () {
        element.setProperty(params.name, params.value);
      });

      return {
        success: true,
        target: params.target,
        name: params.name,
        value: params.value,
      };
    },

    // ── remove_property ───────────────────────────────────────────────
    remove_property(params) {
      const map = requireMap();
      if (!params.name) throw new Error("property name is required");

      const element = resolveTarget(map, params);

      map.macro("Remove property", function () {
        element.removeProperty(params.name);
      });

      return {
        success: true,
        target: params.target,
        name: params.name,
      };
    },

    // ── get_properties ────────────────────────────────────────────────
    get_properties(params) {
      const map = requireMap();

      const element = resolveTarget(map, params);

      // Get own properties
      const own = {};
      const ownProps = element.properties;
      if (ownProps) {
        for (const key in ownProps) {
          if (Object.prototype.hasOwnProperty.call(ownProps, key)) {
            own[key] = ownProps[key];
          }
        }
      }

      // Get resolved properties (includes inherited)
      const resolved = {};
      try {
        const resolvedProps = element.resolvedProperties();
        for (const key in resolvedProps) {
          if (Object.prototype.hasOwnProperty.call(resolvedProps, key)) {
            resolved[key] = resolvedProps[key];
          }
        }
      } catch (e) {
        // resolvedProperties might not be available on all elements
      }

      return {
        target: params.target,
        properties: own,
        resolved: resolved,
      };
    },

    // ── set_properties_batch ──────────────────────────────────────────
    set_properties_batch(params) {
      const map = requireMap();
      if (!params.properties || typeof params.properties !== "object") {
        throw new Error("properties object is required");
      }

      const element = resolveTarget(map, params);

      map.macro("Set properties batch", function () {
        for (const key in params.properties) {
          if (Object.prototype.hasOwnProperty.call(params.properties, key)) {
            element.setProperty(key, params.properties[key]);
          }
        }
      });

      return {
        success: true,
        target: params.target,
        propertiesSet: Object.keys(params.properties).length,
      };
    },

    // ── get_map_properties ────────────────────────────────────────────
    get_map_properties(_params) {
      const map = requireMap();
      const props = {};
      const ownProps = map.properties;
      if (ownProps) {
        for (const key in ownProps) {
          if (Object.prototype.hasOwnProperty.call(ownProps, key)) {
            props[key] = ownProps[key];
          }
        }
      }
      return { properties: props };
    },

    // ── set_map_property ──────────────────────────────────────────────
    set_map_property(params) {
      const map = requireMap();
      if (!params.name) throw new Error("property name is required");
      if (params.value === undefined) throw new Error("property value is required");

      map.macro("Set map property", function () {
        map.setProperty(params.name, params.value);
      });

      return {
        success: true,
        name: params.name,
        value: params.value,
      };
    },
  };
}
