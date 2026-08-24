/**
 * Image Commands - image creation, loading, pixel manipulation.
 *
 * Tiled API:
 *   new Image(width, height, format)  - create image
 *   Image.load(path)                  - load image from file
 *   image.save(path)                  - save to file
 *   image.width, image.height
 *   image.pixel(x, y)                 - get pixel color
 *   image.setPixel(x, y, color)       - set pixel color
 *   image.fill(color)                 - fill with color
 *   image.copy(x, y, w, h)           - copy region
 */

/* global tiled, Image */

export function getCommands() {
  return {
    // ── create_image ──────────────────────────────────────────────────
    create_image(params) {
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }
      if (!params.output_path) throw new Error("output_path is required");

      const img = new Image(params.width, params.height);

      if (params.fill_color) {
        img.fill(params.fill_color);
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        width: params.width,
        height: params.height,
      };
    },

    // ── load_image_info ───────────────────────────────────────────────
    load_image_info(params) {
      if (!params.file_path) throw new Error("file_path is required");

      const img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      return {
        file_path: params.file_path,
        width: img.width,
        height: img.height,
      };
    },

    // ── get_pixel ─────────────────────────────────────────────────────
    get_pixel(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }

      const img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      if (params.x < 0 || params.x >= img.width || params.y < 0 || params.y >= img.height) {
        throw new Error("Coordinates out of bounds");
      }

      const color = img.pixel(params.x, params.y);

      return {
        x: params.x,
        y: params.y,
        color: color,
      };
    },

    // ── set_pixels ────────────────────────────────────────────────────
    set_pixels(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (!params.pixels || !Array.isArray(params.pixels)) {
        throw new Error("pixels array is required");
      }
      if (!params.output_path) throw new Error("output_path is required");

      const img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      for (const p of params.pixels) {
        if (p.x >= 0 && p.x < img.width && p.y >= 0 && p.y < img.height) {
          img.setPixel(p.x, p.y, p.color);
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        pixelsModified: params.pixels.length,
      };
    },

    // ── fill_image_rect ───────────────────────────────────────────────
    fill_image_rect(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (!params.output_path) throw new Error("output_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }
      if (!params.color) throw new Error("color is required");

      const img = new Image(params.file_path);
      if (!img || img.width === 0) {
        throw new Error("Failed to load image: " + params.file_path);
      }

      for (let dy = 0; dy < params.height; dy++) {
        for (let dx = 0; dx < params.width; dx++) {
          const px = params.x + dx;
          const py = params.y + dy;
          if (px >= 0 && px < img.width && py >= 0 && py < img.height) {
            img.setPixel(px, py, params.color);
          }
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        region: { x: params.x, y: params.y, width: params.width, height: params.height },
      };
    },

    // ── copy_image_region ─────────────────────────────────────────────
    copy_image_region(params) {
      if (!params.source_path) throw new Error("source_path is required");
      if (!params.output_path) throw new Error("output_path is required");
      if (params.x === undefined || params.y === undefined) {
        throw new Error("x and y are required");
      }
      if (!params.width || !params.height) {
        throw new Error("width and height are required");
      }

      const src = new Image(params.source_path);
      if (!src || src.width === 0) {
        throw new Error("Failed to load source image");
      }

      const region = src.copy(params.x, params.y, params.width, params.height);
      region.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        width: params.width,
        height: params.height,
      };
    },

    // ── generate_tileset_image ────────────────────────────────────────
    generate_tileset_image(params) {
      if (!params.output_path) throw new Error("output_path is required");
      if (!params.tile_width || !params.tile_height) {
        throw new Error("tile_width and tile_height are required");
      }
      if (!params.columns || !params.rows) {
        throw new Error("columns and rows are required");
      }

      const w = params.tile_width * params.columns;
      const h = params.tile_height * params.rows;
      const spacing = params.spacing || 0;
      const margin = params.margin || 0;

      const totalW = margin * 2 + params.columns * params.tile_width + (params.columns - 1) * spacing;
      const totalH = margin * 2 + params.rows * params.tile_height + (params.rows - 1) * spacing;

      const img = new Image(totalW, totalH);
      img.fill(params.background_color || "#00000000"); // Transparent by default

      // Draw grid lines if requested
      if (params.draw_grid) {
        const gridColor = params.grid_color || "#cccccc";
        for (let col = 0; col <= params.columns; col++) {
          const x = margin + col * (params.tile_width + spacing) - (col > 0 ? spacing : 0);
          for (let y = 0; y < totalH; y++) {
            if (x >= 0 && x < totalW) img.setPixel(x, y, gridColor);
          }
        }
        for (let row = 0; row <= params.rows; row++) {
          const y = margin + row * (params.tile_height + spacing) - (row > 0 ? spacing : 0);
          for (let x = 0; x < totalW; x++) {
            if (y >= 0 && y < totalH) img.setPixel(x, y, gridColor);
          }
        }
      }

      img.save(params.output_path);

      return {
        success: true,
        output_path: params.output_path,
        imageWidth: totalW,
        imageHeight: totalH,
        tileCount: params.columns * params.rows,
      };
    },
  };
}
