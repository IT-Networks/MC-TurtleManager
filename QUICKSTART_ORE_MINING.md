# Quick Start - Automatisches Ore Mining

## 5-Minuten Setup

### Schritt 1: Python Server starten

```bash
cd Assets/FlaskServer
pip install -r requirements.txt
python TurtleControllerWebSocket.py
```

Server läuft auf: `http://192.168.178.211:4999`

### Schritt 2: Turtle in Minecraft vorbereiten

**1. Truhe platzieren (Beispiel-Position):**
```
Position: X=100, Y=64, Z=200
```

**2. Turtle spawnen:**
```
/summon computercraft:turtle 100 65 200
```

**3. Turtle konfigurieren:**
```lua
-- Rechtsklick auf Turtle
label set miner_01

-- GeoScanner einbauen (falls noch nicht vorhanden)
-- In Crafting: Turtle + GeoScanner

-- Lua Script hochladen
edit startup.lua
```

**4. Enhanced Script kopieren:**
- Kopiere Inhalt von `Assets/Lua/TurtleSlaveEnhanced.lua`
- Füge in ComputerCraft `startup.lua` ein
- Speichern: `Ctrl + S`
- Beenden: `Ctrl + E`

**5. Server-IP anpassen (falls nötig):**
```lua
-- In startup.lua, Zeile 2-4 anpassen:
local SERVER_COMMAND_URL = "http://DEINE_IP:4999/command"
local SERVER_REPORT_URL  = "http://DEINE_IP:4999/report"
local SERVER_STATUS_URL  = "http://DEINE_IP:4999/status"
```

**6. Turtle starten:**
```
reboot
```

### Schritt 3: Unity konfigurieren

**1. Scene öffnen:**
```
Assets/Scenes/TurtleManager.unity
```

**2. Turtle GameObject finden:**
- Hierarchy: `Turtles/Turtle_miner_01`

**3. Components hinzufügen:**
```
Add Component > TurtleInventoryManager
Add Component > TurtleOreMiningManager
```

**4. TurtleInventoryManager konfigurieren:**
```
Inspector:
├── Chest Position: (100, 64, 200)
├── Auto Return To Chest: ✓
├── Inventory Full Threshold: 14
├── Auto Refuel: ✓
└── Low Fuel Threshold: 500
```

**5. TurtleOreMiningManager konfigurieren:**
```
Inspector:
├── Auto Detect Ores: ✓
├── Scan Radius: 16
├── Scan Interval: 5
├── Enable Vein Mining: ✓
└── Target Ores: (alle anhaken)
    ├── Mine Diamond: ✓
    ├── Mine Gold: ✓
    ├── Mine Iron: ✓
    └── ... (alle anderen)
```

### Schritt 4: Testen

**1. Play drücken in Unity**

**2. Turtle sollte:**
- GPS-Position ermitteln
- Status an Server senden
- Umgebung scannen
- Erze erkennen (falls vorhanden)

**3. Manueller Test - Scan auslösen:**
```lua
-- In ComputerCraft Console:
http.post("http://192.168.178.211:4999/commands",
    textutils.serializeJSON({
        label = "miner_01",
        commands = {"scan"}
    }),
    {["Content-Type"] = "application/json"}
)
```

**4. Erze platzieren (zum Testen):**
```
/setblock 105 64 205 minecraft:diamond_ore
/setblock 106 64 205 minecraft:diamond_ore
/setblock 107 64 205 minecraft:gold_ore
```

**5. Turtle sollte jetzt:**
- Erze im Scan erkennen
- Automatisch zum ersten Erz navigieren
- Erz abbauen
- Zum nächsten Erz weitergehen

---

## Wichtige Positionen einstellen

### Chest Position in Unity:

```csharp
// Finde deine Truhe in Minecraft
/execute in minecraft:overworld run tp @s ~ ~ ~
// Zeigt deine Position, z.B. (512, 64, -234)

// In Unity:
TurtleInventoryManager.chestPosition = new Vector3(512, 64, -234);
```

### Chest Position in Lua:

```lua
-- In TurtleSlaveEnhanced.lua, Zeile 10:
local CHEST_POSITION = {x = 512, y = 64, z = -234}
```

---

## Fehlerbehebung

### Turtle macht nichts:

**Check 1: GPS funktioniert?**
```lua
-- In Turtle Console:
gps.locate()
-- Sollte Koordinaten zurückgeben
```

**Check 2: Server erreichbar?**
```lua
http.get("http://192.168.178.211:4999/")
-- Sollte "Turtle Command Server..." zurückgeben
```

**Check 3: Script läuft?**
```lua
-- In Turtle Console sollte stehen:
-- "Initialisiere GPS..."
-- "Richtung: north"
-- "GPS: {x=..., y=..., z=...}"
-- "Starte Enhanced Turtle Control..."
```

### Turtle findet keine Erze:

**Check 1: GeoScanner installiert?**
```lua
peripheral.find("geoScanner")
-- Sollte nicht nil sein
```

**Check 2: Scan manuell ausführen:**
```lua
-- In TurtleSlaveEnhanced.lua, uncomment debug:
print("Detected ores: ", #detectedOres)
```

**Check 3: Erze in Reichweite?**
- Scan Radius: 16 Blöcke
- Platziere Erze näher am Turtle

### Inventar wird nicht geleert:

**Check: Truhe ist platziert?**
```
/execute at @e[type=computercraft:turtle] run setblock ~ ~-1 ~ minecraft:chest
```

**Check: Chest Position korrekt?**
- Turtle muss **direkt über** der Truhe stehen
- `dropDown()` funktioniert nur nach unten

---

## Nächste Schritte

### Mehrere Turtles:

```lua
-- Turtle 2:
label set miner_02

-- Turtle 3:
label set miner_03
```

In Unity für jeden Turtle:
- GameObject erstellen
- TurtleObject Component
- TurtleInventoryManager + TurtleOreMiningManager

### Erweiterte Konfiguration:

**Nur bestimmte Erze:**
```csharp
// In TurtleOreMiningManager:
mineDiamond = true;
mineGold = true;
mineCoal = false;  // Kohle ignorieren
```

**Größere Scan-Radius:**
```csharp
scanRadius = 32f;  // Achtung: Mehr Performance-Last
```

**Aggressiveres Refueling:**
```csharp
lowFuelThreshold = 1000;  // Früher nachfüllen
```

---

## Monitoring

### Server Output:

```
[STATUS] miner_01 @ {'x': 105, 'y': 64, 'z': 205} | Fuel: 1523 | Inv: 12/16
[SCAN] 8 neue Bloecke gespeichert. Gesamt: 1523
[WS STATUS] miner_01 @ {'x': 106, 'y': 64, 'z': 205} | Fuel: 1518 | Inv: 13/16
```

### Turtle Output:

```
Richtung: north
GPS: {x=105, y=64, z=205}
Starte Enhanced Turtle Control...
Auto Ore Mining: true
Ores erkannt: 3
Mine Ore: minecraft:diamond_ore @ (2,0,0)
Ore abgebaut!
Inventar voll! Lagere Items...
```

### Unity Console:

```
[miner_01] Inventory nearly full, returning to chest
[miner_01] Starting chest return from (105, 64, 205)
[miner_01] Storing items in chest at (100, 64, 200)
[miner_01] Returning to work position (105, 64, 205)
[miner_01] Chest return cycle complete
```

---

## Performance Tips

1. **Scan Interval erhöhen** (bei vielen Turtles):
   ```csharp
   scanInterval = 10f;  // Alle 10s statt 5s
   ```

2. **Vein Mining deaktivieren** (schnelleres Mining):
   ```csharp
   enableVeinMining = false;
   ```

3. **Inventory Threshold senken** (weniger Fahrten):
   ```csharp
   inventoryFullThreshold = 15;  // Nur bei fast voll
   ```

---

Viel Erfolg beim Mining! 💎⛏️
