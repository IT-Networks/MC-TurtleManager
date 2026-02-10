using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles binary caching of voxel data (block IDs + positions + palette).
/// </summary>
public class ChunkCache
{
    private const int CacheMagic = 0x43484E4B; // "CHNK"
    private const ushort CacheVersion = 1;
    
    private readonly string cacheDir;
    private readonly Vector2Int coord;
    private readonly int chunkSize;

    public ChunkCache(Vector2Int coord, int chunkSize)
    {
        this.coord = coord;
        this.chunkSize = chunkSize;
        this.cacheDir = Path.Combine(Application.persistentDataPath, "ChunkCache");
    }

    private string CachePath => Path.Combine(cacheDir, $"chunk_{coord.x}_{coord.y}.bin");

    public bool TryLoadCache(out ChunkMeshData meshData, out long cachedTS)
    {
        meshData = null;
        cachedTS = 0L;

        if (!File.Exists(CachePath)) return false;

        try
        {
            using var fs = new FileStream(CachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            int magic = br.ReadInt32();
            if (magic != CacheMagic) return false;

            ushort ver = br.ReadUInt16();
            if (ver != CacheVersion) return false;

            int cx = br.ReadInt32();
            int cz = br.ReadInt32();
            if (cx != coord.x || cz != coord.y) return false;

            cachedTS = br.ReadInt64();

            // Load palette
            int paletteCount = br.ReadInt32();
            var idToName = new List<string>(paletteCount);
            for (int i = 0; i < paletteCount; i++)
            {
                string name = br.ReadString();
                idToName.Add(name ?? "default");
            }

            // Load blocks
            int blockCount = br.ReadInt32();
            var blockGrid = new string[chunkSize, 400, chunkSize]; // maxHeight = 400

            for (int i = 0; i < blockCount; i++)
            {
                byte id = br.ReadByte();
                byte lx = br.ReadByte();
                short y = br.ReadInt16(); // Read as short (was byte, causing data loss)
                byte lz = br.ReadByte();

                string blockName = (id < idToName.Count) ? idToName[id] : "default";

                if (lx < chunkSize && y >= 0 && y < 400 && lz < chunkSize)
                {
                    blockGrid[lx, y, lz] = blockName;
                }
            }

            meshData = new ChunkMeshData(blockGrid, coord, chunkSize);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to read cache: {e.Message}");
            return false;
        }
    }

    public void SaveCache(CacheWriteData data)
    {
        EnsureCacheDir();
        try
        {
            SaveCacheBinary(CachePath, data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to write cache: {e.Message}");
        }
    }

    private void EnsureCacheDir()
    {
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);
    }

    private void SaveCacheBinary(string path, CacheWriteData data)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs);

        bw.Write(CacheMagic);
        bw.Write(CacheVersion);
        bw.Write(data.chunkX);
        bw.Write(data.chunkZ);
        bw.Write(data.lastUpdateTS);

        // Write palette
        bw.Write(data.idToBlockName.Count);
        for (int i = 0; i < data.idToBlockName.Count; i++)
            bw.Write(data.idToBlockName[i] ?? "default");

        // Write voxels
        bw.Write(data.blocks.Count);
        foreach (var b in data.blocks)
        {
            bw.Write(b.id);
            bw.Write(b.x);
            bw.Write(b.y); // Write as short (matches BlockRecord.y type)
            bw.Write(b.z);
        }
    }
}

// Data structures for caching
public class CacheWriteData
{
    public long lastUpdateTS;
    public int chunkX;
    public int chunkZ;
    public readonly List<string> idToBlockName = new();
    public readonly List<BlockRecord> blocks = new();
}

public struct BlockRecord
{
    public byte id;
    public byte x;   // 0-15 within chunk (X coordinate)
    public short y;  // -64 to 320 in Minecraft 1.18+ (changed from byte to fix overflow)
    public byte z;   // 0-15 within chunk (Z coordinate)
}