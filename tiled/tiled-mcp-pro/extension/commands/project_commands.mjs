/**
 * Project Commands - project info, file system, worlds.
 *
 * Tiled API:
 *   tiled.project                 - current project
 *   tiled.project.fileName        - project file path
 *   tiled.project.folders         - project folders
 *   tiled.project.extensionPath   - path to project extensions
 *   tiled.openAssets               - open files
 *   tiled.open(path)               - open a file
 *   File/FileInfo                  - file system utilities
 */

/* global tiled, File, FileInfo, TextFile */

function requireMap() {
  const asset = tiled.activeAsset;
  if (!asset || !asset.isTileMap) {
    throw new Error("No map is currently open in Tiled");
  }
  return asset;
}

export function getCommands() {
  return {
    // ── get_project_info ──────────────────────────────────────────────
    get_project_info(_params) {
      const info = {
        hasProject: false,
      };

      if (tiled.project) {
        info.hasProject = true;
        info.fileName = tiled.project.fileName || null;
        info.extensionPath = tiled.project.extensionPath || null;

        if (tiled.project.folders) {
          info.folders = [];
          for (const folder of tiled.project.folders) {
            info.folders.push(folder);
          }
        }
      }

      // List open assets
      info.openAssets = [];
      for (const asset of tiled.openAssets) {
        info.openAssets.push({
          fileName: asset.fileName,
          isTileMap: asset.isTileMap,
        });
      }

      return info;
    },

    // ── list_files ────────────────────────────────────────────────────
    list_files(params) {
      if (!params.directory) throw new Error("directory is required");

      const dir = params.directory;
      const filter = params.filter || "*";
      const recursive = params.recursive || false;

      // Use Tiled's File API to list directory contents
      const files = [];

      function listDir(path, depth) {
        try {
          const entries = File.directoryEntries(path);
          for (const entry of entries) {
            if (entry === "." || entry === "..") continue;
            const fullPath = path + "/" + entry;
            const isDir = File.exists(fullPath + "/.");

            if (isDir) {
              files.push({ path: fullPath, type: "directory" });
              if (recursive && depth < 10) {
                listDir(fullPath, depth + 1);
              }
            } else {
              // Apply filter
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

    // ── read_file ─────────────────────────────────────────────────────
    read_file(params) {
      if (!params.file_path) throw new Error("file_path is required");

      try {
        const file = new TextFile(params.file_path, TextFile.ReadOnly);
        const content = file.readAll();
        file.close();

        return {
          file_path: params.file_path,
          content: content,
          size: content.length,
        };
      } catch (e) {
        throw new Error("Failed to read file: " + params.file_path + " - " + e);
      }
    },

    // ── write_file ────────────────────────────────────────────────────
    write_file(params) {
      if (!params.file_path) throw new Error("file_path is required");
      if (params.content === undefined) throw new Error("content is required");

      try {
        const file = new TextFile(params.file_path, TextFile.WriteOnly);
        file.write(params.content);
        file.close();

        return {
          success: true,
          file_path: params.file_path,
          size: params.content.length,
        };
      } catch (e) {
        throw new Error("Failed to write file: " + params.file_path + " - " + e);
      }
    },

    // ── file_exists ───────────────────────────────────────────────────
    file_exists(params) {
      if (!params.file_path) throw new Error("file_path is required");
      return {
        file_path: params.file_path,
        exists: File.exists(params.file_path),
      };
    },

    // ── get_worlds ────────────────────────────────────────────────────
    get_worlds(_params) {
      const worlds = [];
      // Tiled 1.11+ has world support
      if (tiled.worlds) {
        for (const world of tiled.worlds) {
          const maps = [];
          if (world.maps) {
            for (const wm of world.maps) {
              maps.push({
                fileName: wm.fileName,
                rect: wm.rect ? {
                  x: wm.rect.x,
                  y: wm.rect.y,
                  width: wm.rect.width,
                  height: wm.rect.height,
                } : null,
              });
            }
          }
          worlds.push({
            fileName: world.fileName,
            maps: maps,
          });
        }
      }
      return { worlds: worlds };
    },

    // ── open_file ─────────────────────────────────────────────────────
    open_file(params) {
      if (!params.file_path) throw new Error("file_path is required");
      const asset = tiled.open(params.file_path);
      if (!asset) throw new Error("Failed to open: " + params.file_path);
      return {
        success: true,
        fileName: asset.fileName,
        isTileMap: asset.isTileMap,
      };
    },

    // ── close_file ────────────────────────────────────────────────────
    close_file(params) {
      if (!params.file_path) throw new Error("file_path is required");
      for (const asset of tiled.openAssets) {
        if (asset.fileName === params.file_path) {
          tiled.close(asset);
          return { success: true, fileName: params.file_path };
        }
      }
      throw new Error("Asset not open: " + params.file_path);
    },
  };
}
