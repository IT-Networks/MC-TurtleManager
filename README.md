# MC-TurtleManager

Eine Unity-Anwendung zur Visualisierung und Steuerung von Minecraft-Welten mit ComputerCraft-Turtles in Echtzeit.

## Überblick

MC-TurtleManager ermöglicht es, Minecraft-Chunks in Unity als Top-Down-Strategieansicht zu laden und ComputerCraft-Turtles präzise zu steuern. Die Anwendung bietet:

- **Echtzeit-Visualisierung** der Minecraft-Welt in Unity
- **Präzise Turtle-Steuerung** über Wegpunkte und Pathfinding
- **Chunk-Verwaltung** mit automatischem Laden und Caching
- **3D-Navigation** für Turtles mit A*-Pathfinding
- **Kommando-Queue-System** für zuverlässige Befehlsausführung

## System-Architektur

Das Projekt besteht aus vier Hauptkomponenten, die über HTTP kommunizieren:

```
┌─────────────────────┐
│  Minecraft + Mod    │
│  (WorldInfo Server) │
└──────────┬──────────┘
           │ HTTP (Port 8000)
           │ Chunk-Daten
           ↓
┌─────────────────────┐      ┌──────────────────┐
│   Flask Server      │◄────►│  Unity Client    │
│   (Port 4999)       │      │  (Visualisierung │
│                     │      │   + Steuerung)   │
│ - Kommando-Queue    │      └──────────────────┘
│ - Block-Datenbank   │
│ - Status-Tracking   │
└──────────┬──────────┘
           │ HTTP Polling (1s)
           │ Kommandos + Status
           ↓
┌─────────────────────┐
│  Turtle (Lua)       │
│  - TurtleSlave.lua  │
│  - GPS-basiert      │
└─────────────────────┘
```

### Komponenten im Detail

#### 1. **Minecraft Mod (WorldInfo Server)**
- **Pfad:** `MinecraftMod/`
- **Funktion:** Stellt Chunk-Daten über HTTP-API bereit
- **Port:** 8000
- **Endpoints:**
  - `/chunks` - Liefert alle geladenen Chunks mit Blockdaten

#### 2. **Flask Server (Kommando-Zentrale)**
- **Pfad:** `Assets/FlaskServer/TurtleController.py`
- **Port:** 4999
- **Hauptaufgaben:**
  - Verwaltung der Kommando-Queues pro Turtle
  - Speicherung und Caching von Block-Informationen
  - Status-Tracking aller aktiven Turtles

**Wichtige Endpoints:**
- `POST /commands` - Kommandos für Turtle in Queue einreihen
- `GET /command?label=X` - Nächstes Kommando für Turtle abrufen
- `POST /status` - Status-Update vom Turtle empfangen
- `GET /status/<label>` - Aktuellen Status eines Turtles abrufen
- `POST /report` - Block-Scan-Daten vom Turtle empfangen
- `GET /report` - Alle bekannten Blöcke abrufen

#### 3. **Unity Client (Visualisierung & Steuerung)**
- **Pfad:** `Assets/`
- **Hauptkomponenten:**

  **TurtleManager System:**
  - `TurtleBaseManager.cs` - Basis-Kommunikation und Kommando-Verarbeitung
  - `TurtleMovementManager.cs` - Wegfindung und Bewegungssteuerung
  - `TurtleWorldManager.cs` - Chunk-Synchronisation

  **Pathfinding & Navigation:**
  - `Pathfinding3D.cs` - A*-Algorithmus für 3D-Navigation
  - `CommandConverter3D.cs` - Konvertierung von Pfaden zu Turtle-Befehlen
  - `DirectionUtils.cs` - Richtungsberechnungen und Drehungen

  **Chunk-Verwaltung:**
  - `ChunkCache.cs` - Caching-System für Chunk-Daten
  - `ChunkMeshBuilder.cs` - Generierung der 3D-Meshes
  - `ChunkJsonParser.cs` - Parsing von Minecraft-Daten

#### 4. **Turtle Script (Ausführung)**
- **Pfad:** `Assets/Lua/TurtleSlave.lua`
- **Funktion:** Läuft auf dem ComputerCraft-Turtle in Minecraft
- **Hauptaufgaben:**
  - Polling neuer Kommandos vom Flask-Server
  - Ausführung von Bewegungs- und Aktionsbefehlen
  - GPS-basierte Positions- und Richtungserkennung
  - Status-Updates an Server senden

**Unterstützte Kommandos:**
- Bewegung: `forward`, `back`, `up`, `down`
- Drehung: `left`, `right`
- Aktionen: `dig`, `digup`, `digdown`, `place`, etc.
- Spezial: `scan`, `refuel`

## Technische Details

### Koordinatensystem-Transformation

Unity und Minecraft verwenden unterschiedliche Koordinatensysteme. Die Transformation erfolgt in `TurtleBaseManager.cs`:

```csharp
// Minecraft → Unity (beim Status-Empfang)
x = -(int)minecraftX - 1
y = (int)minecraftY + 128
z = (int)minecraftZ

// Unity → Minecraft (für GetTurtlePosition)
unityX = currentTurtleStatus.position.x + 1
```

**Richtungszuordnung:**
- Minecraft **EAST** (+X) → Unity **WEST** (negativer X-Wert)
- Minecraft **WEST** (-X) → Unity **EAST** (positiver X-Wert)
- Minecraft **NORTH** (-Z) → Unity **NORTH**
- Minecraft **SOUTH** (+Z) → Unity **SOUTH**

### Kommando-Verarbeitung & Synchronisation

**Problem:** Turtle-Befehle brauchen Zeit zur Ausführung, aber HTTP-Requests sind sofort.

**Lösung - Multi-Layer Queue System:**

1. **Unity Queue** (`TurtleBaseManager.cs`):
   - Lokale Queue für Befehle
   - Verzögerung: 2.1s zwischen Befehlen
   - Sendet Befehle als Batch an Flask-Server

2. **Flask Server Queue** (`TurtleController.py`):
   - Pro-Turtle-Queue (Label-basiert)
   - FIFO-Verarbeitung
   - Persistenz während Server-Laufzeit

3. **Turtle Polling Loop** (`TurtleSlave.lua`):
   - Fragt alle 0.5s nach neuem Kommando
   - Führt aus und sendet Status-Update
   - Wartet dann erneut

**Richtungs-Caching:**
Um veraltete Richtungsinformationen zu vermeiden, cached `TurtleMovementManager.cs` die Turtle-Richtung lokal nach jeder Drehung. Dies verhindert falsche Berechnungen bei schnell aufeinanderfolgenden Bewegungen.

```csharp
// Cached direction wird verwendet statt Server-Status
string currentDirection = !string.IsNullOrEmpty(cachedDirection)
    ? cachedDirection
    : status.direction;
```

### Pathfinding-Algorithmus

`Pathfinding3D.cs` implementiert A* mit folgenden Besonderheiten:

- **Nur kardinale Richtungen** (keine Diagonalen)
- **Bewegungskosten:**
  - Horizontal: 10
  - Vertikal (Hoch): 20
  - Diagonal: 14
- **Unterstützungsprüfung:** Turtle braucht festen Boden unter sich
- **Pfad-Vereinfachung:** Entfernt redundante Wegpunkte

### GPS-System für Turtles

Turtles verwenden das ComputerCraft-GPS-System zur Positionsbestimmung:

```lua
-- Richtungserkennung bei Startup
function getDirection()
    local x1, y1, z1 = gps.locate(2)
    turtle.forward()
    local x2, y2, z2 = gps.locate(2)
    turtle.back()

    -- Berechne Richtung aus Positionsänderung
    local dx = x2 - x1
    local dz = z2 - z1
end
```

**Wichtig:** Es müssen mindestens 4 GPS-Hosts in der Minecraft-Welt aktiv sein!

## Setup & Installation

### Voraussetzungen

- **Minecraft** mit Forge/Fabric
- **ComputerCraft** oder **CC: Tweaked** Mod
- **Python 3.8+** mit Flask und flask-cors
- **Unity 2021.3+** (oder kompatible Version)
- **GPS-System** in Minecraft (mindestens 4 GPS-Hosts)

### Installation

1. **Minecraft Mod installieren:**
   ```bash
   # Mod aus MinecraftMod/ in den Minecraft mods-Ordner kopieren
   ```

2. **Flask-Server starten:**
   ```bash
   cd Assets/FlaskServer
   pip install flask flask-cors
   python TurtleController.py
   ```
   Server läuft auf `http://0.0.0.0:4999`

3. **Turtle-Script hochladen:**
   - In Minecraft einen Turtle platzieren
   - Script mit `edit startup` öffnen
   - Inhalt von `Assets/Lua/TurtleSlave.lua` einfügen
   - Mit Ctrl+S speichern und mit `reboot` neustarten

4. **Unity-Projekt öffnen:**
   - Unity Hub → Add → MC-TurtleManager Ordner auswählen
   - Projekt öffnen
   - Scene laden und Play drücken

5. **Server-URLs konfigurieren:**
   In Unity TurtleBaseManager-Komponente:
   ```
   Turtle Command URL: http://<FLASK_IP>:4999/command
   Turtle Status URL: http://<FLASK_IP>:4999/status
   ```

## Verwendung

### Turtle einen Weg laufen lassen

1. In Unity die Welt wird automatisch aus Minecraft geladen
2. Rechtsklick auf gewünschte Zielposition
3. System berechnet Pfad und zeigt ihn als Linie an
4. Turtle beginnt automatisch, dem Pfad zu folgen
5. Fortschritt wird in Echtzeit visualisiert

### Debugging

**Unity Logs:**
- Kommando-Ausführung: `[INFO] Neuer Befehl empfangen`
- Turtle-Status: Position, Richtung, Fuel-Level

**Flask-Server Logs:**
```
[QUEUE] Für Turtle 'TurtleSlave' 5 Kommandos hinzugefügt
[COMMAND] Turtle 'TurtleSlave' bekommt Kommando: forward
[STATUS] TurtleSlave @ {x, y, z} | Richtung: east
```

**Turtle Console:**
```
Richtung: east
Position: (100, 64, 200)
Befehl ausgeführt: forward
```

## Bekannte Probleme & Lösungen

### Turtle bewegt sich in falsche Richtung
- **Ursache:** Richtungs-Cache nicht synchron
- **Lösung:** Bereits implementiert mit lokalem Caching in `TurtleMovementManager.cs`

### GPS-Signal fehlt
- **Prüfen:** Mindestens 4 GPS-Hosts aktiv in Minecraft
- **Lösung:** Mehr GPS-Hosts platzieren oder näher zum Turtle

### Kommandos werden übersprungen
- **Ursache:** Zu kurze `commandDelay`
- **Lösung:** In Unity `TurtleBaseManager` → `commandDelay` auf 2.5s erhöhen

## Projektstruktur

```
MC-TurtleManager/
├── Assets/
│   ├── FlaskServer/
│   │   └── TurtleController.py      # Flask HTTP-Server
│   ├── Lua/
│   │   └── TurtleSlave.lua          # Turtle-Script
│   ├── Scripts/
│   │   ├── TurtleManager/           # Turtle-Steuerung
│   │   │   ├── TurtleBaseManager.cs
│   │   │   ├── TurtleMovementManager.cs
│   │   │   └── TurtleWorldManager.cs
│   │   ├── Pathfinding3D.cs         # A*-Algorithmus
│   │   ├── CommandConverter3D.cs     # Pfad → Kommandos
│   │   ├── ChunkMeshBuilder.cs      # Mesh-Generierung
│   │   └── ...
│   └── Scenes/                       # Unity Scenes
├── MinecraftMod/                     # Minecraft Mod (WorldInfo)
└── README.md
```

## Lizenz

[Lizenz hier einfügen]

## Kontakt & Beiträge

[Kontaktinformationen hier einfügen] 
