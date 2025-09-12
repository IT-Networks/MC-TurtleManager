using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Parses chunk JSON data and converts it to mesh data and cache data.
/// </summary>
public class ChunkJsonParser
{
    private readonly Vector2Int coord;
    private readonly int chunkSize;

    public ChunkJsonParser(Vector2Int coord, int chunkSize)
    {
        this.coord = coord;
        this.chunkSize = chunkSize;
    }

    public ChunkMeshData ParseChunkJson(string json, out CacheWriteData cacheWrite)
    {
        cacheWrite = new CacheWriteData
        {
            chunkX = coord.x,
            chunkZ = coord.y,
            lastUpdateTS = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        try
        {
            JObject root = JObject.Parse(json);
            List<string> palette = null;
            JToken blocksObj = null;
            int minBuildHeight = 0;

            // Parse timestamp
            if (root["lastUpdateTS"] != null && long.TryParse(root["lastUpdateTS"]?.ToString(), out long ts))
                cacheWrite.lastUpdateTS = ts;
            else if (root["ts"] != null && long.TryParse(root["ts"]?.ToString(), out long ts2))
                cacheWrite.lastUpdateTS = ts2;

            // Parse palette and blocks
            if (root["palette"] != null)
            {
                palette = root["palette"].ToObject<List<string>>();
                blocksObj = root["blocks"];
            }
            else if (root["chunks"] != null && root["chunks"].HasValues)
            {
                var first = root["chunks"].First;
                palette = first["palette"]?.ToObject<List<string>>();
                blocksObj = first["blocks"];
                if (first["minBuildHeight"] != null)
                    minBuildHeight = first["minBuildHeight"].Value<int>();

                if (first["lastUpdateTS"] != null && long.TryParse(first["lastUpdateTS"]?.ToString(), out long ts3))
                    cacheWrite.lastUpdateTS = ts3;
            }

            if (palette == null || palette.Count == 0)
                return new ChunkMeshData(new string[chunkSize, 400, chunkSize], coord, chunkSize);

            // Create name to ID mapping
            var nameToId = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < palette.Count; i++)
            {
                string name = palette[i] ?? "default";
                if (!nameToId.ContainsKey(name))
                {
                    byte id = (byte)cacheWrite.idToBlockName.Count;
                    cacheWrite.idToBlockName.Add(name);
                    nameToId[name] = id;
                }
            }

            // Create 3D grid and process blocks
            var blockGrid = new string[chunkSize, 400, chunkSize];
            ProcessBlocksObject(blocksObj, palette, blockGrid, minBuildHeight, cacheWrite, nameToId);

            return new ChunkMeshData(blockGrid, coord, chunkSize);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Chunk {coord} Parse error: {ex.Message}");
            return new ChunkMeshData(new string[chunkSize, 400, chunkSize], coord, chunkSize);
        }
    }

    private void ProcessBlocksObject(
        JToken obj,
        List<string> palette,
        string[,,] blockGrid,
        int minBuildHeight,
        CacheWriteData cacheWrite,
        Dictionary<string, byte> nameToId)
    {
        if (obj == null || obj.Type != JTokenType.Object) return;

        foreach (var prop in (JObject)obj)
        {
            if (!int.TryParse(prop.Key, out int paletteIndex)) continue;
            if (paletteIndex < 0 || paletteIndex >= palette.Count) continue;

            string blockName = palette[paletteIndex] ?? "default";
            var arr = prop.Value.ToObject<List<int>>();
            if (arr == null) continue;

            byte id = nameToId[blockName];

            foreach (int packed in arr)
            {
                int x = (packed >> 12) & 0xFFF;
                int y = (packed >> 4) & 0xFF;
                int z = packed & 0xF;

                int wy = y + Math.Abs(minBuildHeight);

                if (x >= 0 && x < chunkSize && 
                    wy >= 0 && wy < 400 && 
                    z >= 0 && z < chunkSize)
                {
                    blockGrid[x, wy, z] = blockName;

                    // Store for cache (check byte range)
                    if (x >= 0 && x <= 255 &&
                        wy >= 0 && wy <= 255 &&
                        z >= 0 && z <= 255)
                    {
                        cacheWrite.blocks.Add(new BlockRecord
                        {
                            id = id,
                            x = (byte)x,
                            y = (byte)wy,
                            z = (byte)z
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"Block coordinates out of byte range: x={x}, wy={wy}, z={z}");
                    }
                }
            }
        }
    }
}