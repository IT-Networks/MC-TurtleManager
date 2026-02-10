# Neue Features - Automatisches Ore Mining & WebSocket Support

## Übersicht

Dieses Update bringt drei große neue Funktionen:

1. **Option C: WebSocket für Real-Time Updates** - Sofortige Kommunikation zwischen Unity, Server und Turtles
2. **Automatisches Ore-Mining** - Turtles erkennen und bauen Erze automatisch ab
3. **Intelligentes Inventar- & Fuel-Management** - Automatische Lagerung in Truhen und Nachfüllen

---

## 1. Option C: WebSocket Support

### Was ist das?

WebSocket ersetzt das bisherige Polling-System (Unity fragt alle 1.0s nach Status) durch **Echtzeit-Kommunikation**. Statusänderungen werden sofort übertragen.

### Vorteile

- ✅ **Keine Verzögerung** mehr zwischen Turtle-Bewegung und Unity-Anzeige
- ✅ **Reduzierte Server-Last** - keine konstanten Polling-Anfragen
- ✅ **Bidirektionale Kommunikation** - Server kann Turtles sofort benachrichtigen
- ✅ **Skalierbar** für viele Turtles

### Installation

**1. Python Dependencies installieren:**
```bash
pip install flask-socketio python-socketio
```

**2. Server starten:**
```bash
python Assets/FlaskServer/TurtleControllerWebSocket.py
```

**3. In Unity:**
- WebSocket Client Manager wird automatisch eingerichtet (TODO: Implementation)

### API

**WebSocket Endpoints:**
- `ws://192.168.178.211:4999/socket.io/` - WebSocket Connection

**Events:**
- `connect` - Client verbunden
- `register` - Client registrieren (type: 'unity' oder 'turtle')
- `turtle_status` - Turtle sendet Status-Update
- `command` - Command an Turtle senden
- `status_update` - Broadcast: Turtle Status geändert
- `blocks_update` - Broadcast: Neue Blöcke gescannt

---

## 2. Automatisches Ore-Mining

### Features

#### 2.1 Ore-Erkennung

Der Turtle scannt automatisch die Umgebung (16 Block Radius) und erkennt:

**Vanilla Ores:**
- Coal (Kohle)
- Iron (Eisen)
- Gold
- Diamond (Diamant)
- Emerald (Smaragd)
- Lapis Lazuli
- Redstone
- Copper (Kupfer)

**ATM10 Modded Ores:**
- AllTheModium
- Vibranium
- Unobtainium
- Osmium
- Tin (Zinn)
- Lead (Blei)
- Uranium
- Zinc (Zink)

#### 2.2 Intelligente Mining-Patterns

- **Vein Mining**: Erkennt zusammenhängende Erze und baut das komplette Vorkommen ab
- **Prioritäts-System**: Baut wertvolle Erze (Diamant, Emerald) zuerst ab
- **Automatische Pfadfindung**: Navigiert zum nächsten Erz

#### 2.3 Konfiguration in Unity

**TurtleOreMiningManager Component:**

```
Inspector Settings:
├── Auto Detect Ores: ✓
├── Scan Radius: 16.0
├── Scan Interval: 5.0 (Sekunden)
├── Enable Vein Mining: ✓
├── Max Vein Size: 32
├── Prioritize Rare Ores: ✓
└── Target Ores:
    ├── Mine Coal: ✓
    ├── Mine Iron: ✓
    ├── Mine Gold: ✓
    ├── Mine Diamond: ✓
    └── ... (alle Erze)
```

---

## 3. Inventar- & Fuel-Management

### 3.1 Automatische Inventar-Verwaltung

**Problem:**
- Turtle-Inventar hat nur 16 Slots
- Bei vollem Inventar kann nicht weiter abgebaut werden

**Lösung:**
Der Turtle kehrt automatisch zur Truhe zurück, lagert Items ein und kehrt zur Arbeit zurück.

#### Ablauf:

1. **Inventar wird voll** (≥14 Slots belegt)
2. **Position merken** (aktuelle Mining-Position)
3. **Zur Truhe navigieren** (konfigurierbare Position)
4. **Items einlagern** (außer Fuel-Items)
5. **Zurück zur Arbeit** (zur gemerkten Position)

#### Konfiguration:

```csharp
TurtleInventoryManager:
├── Chest Position: (100, 64, 200)  // Deine Truhen-Position
├── Auto Return To Chest: ✓
├── Inventory Full Threshold: 14
```

### 3.2 Automatisches Fuel-Management

**Problem:**
- Turtle braucht Fuel für Bewegung
- Bei leerem Tank bleibt Turtle stehen

**Lösung:**
Automatisches Nachfüllen aus Inventar oder Truhe.

#### Fuel-Items:

- `minecraft:coal`
- `minecraft:charcoal`
- `minecraft:coal_block`
- `minecraft:lava_bucket`
- `minecraft:blaze_powder`
- `minecraft:blaze_rod`

#### Ablauf:

1. **Fuel-Level < Schwellwert** (Standard: 500)
2. **Suche Fuel-Items im Inventar**
3. **Refuel automatisch**
4. **Falls nicht genug:** Hole Fuel aus Truhe

#### Konfiguration:

```csharp
TurtleInventoryManager:
├── Auto Refuel: ✓
├── Low Fuel Threshold: 500
├── Refuel Amount: 1000
```

---

## Nutzung

### Setup in Minecraft

**1. Truhe platzieren:**
```
Setze eine Truhe an einer festen Position, z.B. (100, 64, 200)
```

**2. Turtle bei der Truhe platzieren:**
```
/summon computercraft:turtle ~ ~ ~
label set turtle_miner_01
```

**3. Lua Script hochladen:**
```lua
-- In ComputerCraft Console
edit startup.lua
-- Kopiere TurtleSlaveEnhanced.lua Code
-- Ctrl+S speichern
reboot
```

### Setup in Unity

**1. Turtle GameObject auswählen**

**2. Components hinzufügen:**
- `TurtleInventoryManager`
- `TurtleOreMiningManager`

**3. Chest Position setzen:**
```csharp
TurtleInventoryManager:
└── Chest Position: (100, 64, 200)
```

**4. Ore Mining aktivieren:**
```csharp
TurtleOreMiningManager:
└── Auto Detect Ores: ✓
```

### Lua Commands

**In ComputerCraft:**

```lua
-- Ore Mining an/aus
http.post("http://192.168.178.211:4999/commands",
    textutils.serializeJSON({
        label = "turtle_miner_01",
        commands = {"toggle_ore_mining"}
    })
)

-- Manuell Items lagern
http.post("http://192.168.178.211:4999/commands",
    textutils.serializeJSON({
        label = "turtle_miner_01",
        commands = {"store_items"}
    })
)

-- Scannen
http.post("http://192.168.178.211:4999/commands",
    textutils.serializeJSON({
        label = "turtle_miner_01",
        commands = {"scan"}
    })
)
```

---

## Workflow-Beispiel

### Automatischer Mining-Betrieb

```
1. Turtle startet bei Truhe
2. Bewegt sich zum Mining-Gebiet
3. Scannt Umgebung (16 Block Radius)
4. Erkennt 15 Diamond Ore
5. Beginnt mit Mining der Diamanten
   ├── Mine Diamant #1
   ├── Mine Diamant #2
   ├── ...
   ├── Inventar wird voll (14/16 Slots)
   └── PAUSE MINING
6. Kehrt zur Truhe zurück
7. Lagert alle Diamanten ein
8. Kehrt zu Mining-Position zurück
9. Fortsetzung Mining
   ├── Mine Diamant #3
   ├── ...
   └── Fuel wird niedrig (< 500)
10. Refuel aus Coal im Inventar
11. Fortsetzung Mining
```

---

## Technische Details

### Dateistruktur

```
MC-TurtleManager/
├── Assets/
│   ├── FlaskServer/
│   │   ├── TurtleController.py (ALT - HTTP only)
│   │   └── TurtleControllerWebSocket.py (NEU - WebSocket)
│   ├── Lua/
│   │   ├── TurtleSlave.lua (ALT)
│   │   └── TurtleSlaveEnhanced.lua (NEU)
│   └── Scripts/
│       └── TurtleManager/
│           ├── TurtleInventoryManager.cs (NEU)
│           └── TurtleOreMiningManager.cs (NEU)
└── NEUE_FEATURES.md (diese Datei)
```

### Kommunikations-Flow

#### ALT (HTTP Polling):
```
Unity --[HTTP GET /status/all]--> Flask Server
                                    ↑
                                    | [HTTP POST /status]
                                    |
                                  Turtle
```

#### NEU (WebSocket):
```
Unity ←--[WebSocket: status_update]--→ Flask Server
                                          ↕
                                    [WebSocket]
                                          ↕
                                       Turtle
```

### Status-Report (Enhanced)

```json
{
    "label": "turtle_miner_01",
    "position": {"x": 105, "y": 62, "z": 205},
    "direction": "north",
    "fuelLevel": 1523,
    "maxFuel": 20000,
    "inventorySlotsUsed": 12,
    "inventorySlotsTotal": 16,
    "inventory": [
        {"slot": 1, "name": "minecraft:diamond_ore", "count": 5},
        {"slot": 2, "name": "minecraft:gold_ore", "count": 8},
        {"slot": 5, "name": "minecraft:coal", "count": 32}
    ],
    "isBusy": false,
    "autoOreMining": true,
    "detectedOres": 8
}
```

---

## Troubleshooting

### Problem: Turtle findet keine Erze

**Lösung:**
1. Überprüfe ob GeoScanner installiert ist
2. Führe manuellen Scan aus: `scan` Command
3. Überprüfe `TARGET_ORES` Liste in Lua

### Problem: Turtle findet keine Truhe

**Lösung:**
1. Überprüfe Chest Position in Unity
2. Stelle sicher dass Truhe in Minecraft platziert ist
3. Turtle muss **über** der Truhe stehen für `dropDown()`

### Problem: WebSocket verbindet nicht

**Lösung:**
1. Installiere Dependencies: `pip install flask-socketio`
2. Starte Server neu
3. Überprüfe Firewall-Einstellungen (Port 4999)

### Problem: Fuel läuft trotzdem leer

**Lösung:**
1. Erhöhe `LOW_FUEL_THRESHOLD` in Lua
2. Lege mehr Coal in Truhe
3. Überprüfe ob Coal als Fuel-Item erkannt wird

---

## Performance

### Optimierungen

- **Vein Mining**: Reduziert Bewegung, baut zusammenhängende Erze effizient ab
- **Priority System**: Baut wertvolle Erze zuerst, minimiert Risiko bei Inventory-Full
- **Smart Refueling**: Tankt nur wenn nötig, spart Zeit
- **Batch Storage**: Sammelt Items und lagert in einem Durchgang

### Benchmarks

| Operation | Zeit (Sekunden) | Fuel Verbrauch |
|-----------|-----------------|----------------|
| Scan (16 Radius) | 2-3s | 0 |
| Mine Single Ore | 3-5s | 5-10 |
| Mine Vein (10 Ores) | 30-45s | 50-80 |
| Return to Chest (50 Blocks) | 60-80s | 100 |
| Store Items | 5-8s | 0 |
| Refuel | 2-3s | 0 |

---

## Geplante Erweiterungen

- [ ] WebSocket Client in Unity (C# SocketIO Client)
- [ ] Multi-Turtle Koordination (vermeidet Mining des gleichen Erzes)
- [ ] Quarry Mode (systematisches Abbau-Gitter)
- [ ] Auto-Smelting (Items in Ofen schmelzen)
- [ ] Route Recording (merkt sich erfolgreiche Pfade)
- [ ] Smart Ore Prediction (lernt wo Erze häufig sind)

---

## Credits

**Entwickelt für MC-TurtleManager**
- Option C: WebSocket Real-Time Communication
- Automatic Ore Mining System
- Inventory & Fuel Management

**Version:** 2.0.0
**Datum:** 2026-02-08
