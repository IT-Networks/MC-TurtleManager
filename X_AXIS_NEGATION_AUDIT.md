# X-Achsen-Negierung Audit

## Konsistenz-Überprüfung aller wichtigen Stellen

### ✅ KORREKT - Turtle Position Handling

**TurtleBaseManager.cs:**
- Line 77: Speichert RAW Minecraft coords: `x = (int)status.position.x`
- Line 218: Konvertiert zu Unity: `-currentTurtleBaseStatus.position.x`
- **Status:** KONSISTENT ✓

**TurtleObject.cs:**
- Line 209: `new Vector3(-status.position.x, status.position.y + offset, status.position.z)`
- **Status:** KONSISTENT mit TurtleBaseManager ✓

**MultiTurtleManager.cs:**
- Line 135: Speichert RAW: `new Vector3Int(statusData.position.x, ...)`
- Line 177: Spawn mit Negierung: `new Vector3(-status.position.x, ...)`
- **Status:** KONSISTENT ✓

### ✅ KORREKT - Chunk Coordinate Handling

**TurtleWorldManager.cs:**
- Line 182: Camera to chunk: `Mathf.FloorToInt(-camPos.x / chunkSize)`
- Line 301: Same formula konsistent verwendet
- Line 1032: Chunk to world: `float worldX = -chunkCoord.x * chunkSize`
- **Status:** KONSISTENT ✓

**ChunkMeshBuilder.cs:**
- Line 42: Block world pos: `-(data.coord.x * data.chunkSize + x)`
- **Status:** KONSISTENT ✓

**ChunkManager.cs:**
- Line 545: `int localX = Mathf.FloorToInt(-worldPosition.x) - (coord.x * chunkSize)`
- **Status:** KONSISTENT ✓

## Koordinatensystem-Konvention

**Minecraft → Unity Konvertierung:**
```
Minecraft X = 1051  →  Unity X = -1051
Minecraft Y = 147   →  Unity Y = 147 + 64 = 211
Minecraft Z = -504  →  Unity Z = -504
```

**Regel:**
- X-Achse wird IMMER negiert (Minecraft +X = Unity -X)
- Y-Achse bekommt Offset +64 (Minecraft Y=-64 → Unity Y=0)
- Z-Achse bleibt gleich

## ✅ FAZIT: Alle X-Achsen-Negierungen sind KONSISTENT

Es gibt KEINE Probleme mit der X-Achsen-Negierung.
Alle Stellen folgen der gleichen Konvention korrekt.
