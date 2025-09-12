using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles binary caching of chunk voxel data
/// </summary>
public class ChunkCacheSystem
{
    private const int CacheMagic = 0x43484E4B; // "CHNK"
    private const ushort CacheVersion = 1;
    
    private readonly string cacheDir;
    
    public ChunkCacheSystem()
    {
        cacheDir = Path.Combine(Application.persistentDataPath, "ChunkCache");
        EnsureCacheDir();
    }
    
    public class CacheData
    {
        public long lastUpdateTS;
        public int chunkX;
        public int chunkZ;
        public readonly List<string> blockNames = new();
        public readonly List<BlockRecord> blocks = new();
    }

    public struct BlockRecord
    {
        public byte id;
        public byte x, y, z;
        
        public BlockRecord(byte id, byte x, byte y, byte z)
        {
            this.id = id;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    private void EnsureCacheDir()
    {
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);
    }
    
    private string GetCachePath(Vector2Int coord)
    {
        return Path.Combine(cacheDir, $"chunk_{coord.x}_{coord.y}.bin");
    }

    public bool TryLoadCache(Vector2Int coord, out CacheData cacheData, out long cachedTS)
    {
        cacheData = null;
        cachedTS = 0L;

        string cachePath = GetCachePath(coord);
        if (!File.Exists(cachePath)) return false;

        try
        {
            using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            int magic = br.ReadInt32();
            if (magic != CacheMagic) return false;

            ushort ver = br.ReadUInt16();
            if (ver != CacheVersion) return false;

            int cx = br.ReadInt32();
            int cz = br.ReadInt32();
            if (cx != coord.x || cz != coord.y) return false;

            cachedTS = br.ReadInt64();

            cacheData = new CacheData
            {
                chunkX = cx,
                chunkZ = cz,
                lastUpdateTS = cachedTS
            };

            int paletteCount = br.ReadInt32();
            for (int i = 0; i < paletteCount; i++)
            {
                string name = br.ReadString();
                cacheData.blockNames.Add(name ?? "default");
            }

            int blockCount = br.ReadInt32();
            for (int i = 0; i < blockCount; i++)
            {
                byte id = br.ReadByte();
                byte lx = br.ReadByte();
                byte y = br.ReadByte();
                byte lz = br.ReadByte();

                cacheData.blocks.Add(new BlockRecord(id, lx, y, lz));
            }

            return blockCount > 0; // Return false if no blocks
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to read cache: {e.Message}");
            return false;
        }
    }

    public void SaveCache(Vector2Int coord, CacheData data)
    {
        string cachePath = GetCachePath(coord);
        
        try
        {
            using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            bw.Write(CacheMagic);
            bw.Write(CacheVersion);
            bw.Write(data.chunkX);
            bw.Write(data.chunkZ);
            bw.Write(data.lastUpdateTS);

            // palette
            bw.Write(data.blockNames.Count);
            foreach (string name in data.blockNames)
                bw.Write(name ?? "default");

            // voxels
            bw.Write(data.blocks.Count);
            foreach (var block in data.blocks)
            {
                bw.Write(block.id);
                bw.Write(block.x);
                bw.Write(block.y);
                bw.Write(block.z);
            }
            
            Debug.Log($"Chunk {coord}: Cache saved successfully with {data.blocks.Count} blocks.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to write cache: {e.Message}");
        }
    }
}