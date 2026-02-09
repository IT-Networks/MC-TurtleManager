# Architektur-Update: Unity-basierte Intelligenz

## Änderungen Übersicht

### Vorher (Smart Lua)
```
┌─────────────┐
│   Unity     │ ← Poll Status
│  (Display)  │
└─────────────┘
       ↓
┌─────────────┐
│ Flask Server│
└─────────────┘
       ↓
┌─────────────┐
│    Lua      │ ← Intelligenz hier (Auto-Mining, Auto-Inventory)
│  (Smart)    │
└─────────────┘
```

**Problem:**
- Lua schwer zu debuggen
- Keine Visualisierung der Entscheidungen
- Schwierig Unity-Features zu nutzen
- Code-Duplikation

---

### Nachher (Unity Brain)
```
┌──────────────────────┐
│       Unity          │ ← INTELLIGENZ HIER
│  ┌────────────────┐  │
│  │ Mining Manager │  │ ← Entscheidet wo abbauen
│  │Inventory Mgr   │  │ ← Entscheidet wann lagern
│  │ LM Studio AI   │  │ ← AI-Steuerung (optional)
│  └────────────────┘  │
│         ↓            │
│   Send Commands      │
└──────────────────────┘
          ↓
┌─────────────────────┐
│   Flask Server      │
│   (WebSocket)       │
└─────────────────────┘
          ↓
┌─────────────────────┐
│       Lua           │ ← NUR Befehle ausführen
│    (Executor)       │    forward, dig, etc.
└─────────────────────┘
```

**Vorteile:**
- ✅ Unity hat volle Kontrolle
- ✅ Einfaches Debugging (Unity Console)
- ✅ Visualisierung (Gizmos, Inspector)
- ✅ Flexibel erweiterbar
- ✅ Lua bleibt einfach und stabil

---

## Komponenten-Details

### 1. TurtleSlave.lua (Vereinfacht)

**Aufgaben:**
- GPS Position ermitteln
- Status an Server senden (Position, Fuel, Inventar)
- Befehle vom Server abholen
- Befehle ausführen

**KEINE Intelligenz:**
- ❌ Kein Auto-Mining
- ❌ Keine Entscheidungsfindung
- ❌ Keine Pfadfindung

**Befehle:**
```lua
Movement: forward, back, up, down, left, right
Mining: dig, digup, digdown
Inventory: select:N, drop:N, dropdown, suck, suckdown, refuel:N
Utility: scan
```

---

### 2. TurtleOreMiningManager.cs (Unity)

**Aufgaben:**
- Erze in WorldManager finden
- Pfad zum Erz berechnen
- **Befehle generieren** und an Turtle senden
- Vein Mining koordinieren

**Beispiel:**
```csharp
// Unity findet Diamant bei (105, 64, 205)
// Turtle ist bei (100, 64, 200)

// Unity berechnet Pfad:
Path: (100,64,200) → (100,64,205) → (105,64,205)

// Unity sendet Befehle:
1. forward (5x für Z-Achse)
2. right (drehen)
3. forward (5x für X-Achse)
4. dig (Diamant abbauen)

// Lua führt aus (dumm):
turtle.forward()
turtle.forward()
...
turtle.dig()
```

---

### 3. TurtleInventoryManager.cs (Unity)

**Aufgaben:**
- Inventar-Status überwachen
- Entscheiden wann zur Truhe fahren
- **Navigation-Befehle generieren**
- **Lagerungs-Befehle senden**

**Beispiel:**
```csharp
// Unity detektiert: Inventar 14/16 voll
// Unity entscheidet: Items lagern

// Unity sendet Befehle:
1. Navigation zur Truhe: up, up, forward, forward, ...
2. Lagerung: dropdown (alle non-fuel items)
3. Navigation zurück: forward, forward, down, down, ...

// Lua führt aus:
turtle.up()
turtle.forward()
...
turtle.dropDown()
```

---

### 4. LMStudioManager.cs (Unity)

**Aufgaben:**
- Verbindung zu LM Studio (lokales AI-Model)
- Turtle-Status als Context an AI senden
- AI-Entscheidungen empfangen
- **Befehle aus AI-Response generieren**

**Beispiel:**
```csharp
// Unity sammelt Context:
"Fuel: 50%, Inventar: 3/16, Diamond Ore erkannt bei +5 Blöcke"

// Unity sendet an AI (LM Studio)
Request → LM Studio

// AI antwortet:
"COMMAND:mine\nREASON:Diamond detected, good fuel, empty inventory"

// Unity parst Response:
command = "mine"

// Unity sendet an Turtle:
baseManager.QueueCommand("dig")

// Lua führt aus:
turtle.dig()
```

---

## Datenfluss

### Status-Updates (Real-time via WebSocket)

```
Turtle (Lua)
  └─→ reportStatus() alle 0.5s
       └─→ POST /status → Flask Server
            └─→ WebSocket Broadcast
                 └─→ Unity empfängt Update
                      └─→ turtle.position, turtle.fuelLevel, etc. aktualisiert
```

### Command-Ausführung

```
Unity (Intelligenz)
  └─→ Entscheidung: "Mine diamond at (105,64,205)"
       └─→ TurtleOreMiningManager.MineSingleOre()
            └─→ Pfad berechnen: [(100,64,205), (105,64,205)]
                 └─→ Befehle generieren: ["forward", "right", "forward", "dig"]
                      └─→ baseManager.QueueCommand()
                           └─→ POST /commands → Flask Server
                                └─→ Command Queue
                                     └─→ Turtle (Lua) pollt:
                                          └─→ GET /command
                                               └─→ {"command": "forward"}
                                                    └─→ turtle.forward()
```

### AI-Steuerung (Optional)

```
Unity (LMStudioManager)
  └─→ Alle 5s: AI Decision Interval
       └─→ Context sammeln:
            - Position, Fuel, Inventar, Nearby Blocks
       └─→ POST zu LM Studio API
            └─→ LM Studio (lokales AI-Model)
                 └─→ AI analysiert Context
                      └─→ AI Response: "COMMAND:mine\nREASON:..."
                           └─→ Unity parst Response
                                └─→ executeAICommand("mine")
                                     └─→ baseManager.QueueCommand("dig")
                                          └─→ ... (wie oben)
```

---

## Befehlssatz

### Movement
```
forward   - Bewege vorwärts
back      - Bewege rückwärts
up        - Bewege nach oben
down      - Bewege nach unten
left      - Drehe links
right     - Drehe rechts
```

### Mining
```
dig       - Grabe Block vor Turtle
digup     - Grabe Block über Turtle
digdown   - Grabe Block unter Turtle
```

### Building
```
place         - Platziere Block vorwärts
placeup       - Platziere Block oben
placedown     - Platziere Block unten
```

### Inventory
```
select:N      - Wähle Slot N (1-16)
drop          - Werfe alle non-fuel items weg (vorwärts)
drop:N        - Werfe Item aus Slot N weg
dropdown      - Werfe alle non-fuel items nach unten (in Truhe)
dropdown:N    - Werfe Item aus Slot N nach unten
dropup:N      - Werfe Item aus Slot N nach oben
suck          - Sauge Items von vorne ein
suckdown      - Sauge Items von unten ein (aus Truhe)
suckup        - Sauge Items von oben ein
refuel:N      - Tanke N items aus Inventar
```

### Utility
```
scan          - Scanne Umgebung (16 Block Radius)
```

---

## Vorteile der neuen Architektur

### 1. Separation of Concerns

**Lua:** Nur Hardware-Schnittstelle
- GPS
- Turtle Movement API
- Inventory API
- Peripheral API (GeoScanner)

**Unity:** Gesamte Logik
- Weltdaten (WorldManager)
- Entscheidungsfindung
- Pfadplanung
- AI-Integration

### 2. Debugging

**Vorher:**
```
Problem: Turtle macht komisches Verhalten
Debug: Lua print() statements, schwer nachzuvollziehen
```

**Nachher:**
```
Problem: Turtle macht komisches Verhalten
Debug: Unity Console, Breakpoints, Inspector, Gizmos
       → Sehe genau welche Befehle gesendet wurden
       → Visualisiere Pfad im Scene View
       → Inspect AI Response im Inspector
```

### 3. Visualisierung

**Unity Scene View:**
```
- Turtle Position (Echtzeit)
- Geplanter Pfad (grüne Linie)
- Erkannte Erze (gelbe Würfel)
- Truhen-Position (gelber Wireframe)
- Scan-Radius (grüne Sphere)
- Geminte Blöcke (graue Wireframes)
```

**Unity Inspector:**
```
TurtleOreMiningManager
├── Detected Ores: 5
├── Is Mining: true
└── Debug Mode: ✓

TurtleInventoryManager
├── Inventory Full: 14/16
├── Low Fuel: false
└── Is Returning To Chest: false

LMStudioManager
├── Enable AI Control: ✓
├── Last AI Response: "COMMAND:mine..."
└── Last Executed Command: "dig"
```

### 4. Flexibilität

**Verschiedene Steuerungs-Modi:**

1. **Manuell:** Spieler klickt in Unity → Befehle
2. **Automatisch:** Unity Manager entscheiden → Befehle
3. **AI-gesteuert:** LM Studio AI entscheidet → Befehle
4. **Hybrid:** Mix aus allem

**Beispiel:**
```csharp
// Modus 1: Manuell
if (Input.GetKeyDown(KeyCode.M)) {
    baseManager.QueueCommand("dig");
}

// Modus 2: Automatisch
if (autoMining && oreDetected) {
    oreMiningManager.MineSingleOre(orePosition);
}

// Modus 3: AI
if (aiControlEnabled) {
    lmStudioManager.RequestAIDecision();
}
```

---

## Migration Guide

### Von Smart Lua zu Unity Brain

**1. TurtleSlave.lua aktualisieren:**
```lua
-- Alt (Smart):
if isInventoryFull() then
    returnToChest()
    storeItems()
    returnToPosition()
end

-- Neu (Executor):
-- Unity entscheidet, sendet Befehle:
-- "up", "forward", ..., "dropdown", ..., "down", "back"
```

**2. Unity Manager hinzufügen:**
```csharp
// Turtle GameObject
gameObject.AddComponent<TurtleInventoryManager>();
gameObject.AddComponent<TurtleOreMiningManager>();
gameObject.AddComponent<LMStudioManager>(); // Optional
```

**3. Settings konfigurieren:**
```csharp
inventoryManager.chestPosition = new Vector3(100, 64, 200);
inventoryManager.autoReturnToChest = true;

oreMiningManager.autoDetectOres = true;
oreMiningManager.mineDiamond = true;
```

**4. Testen:**
```
1. Play in Unity
2. Turtle sollte scannen
3. Bei Erz-Erkennung: Unity sendet Befehle
4. Lua führt aus
5. Visualisierung in Unity Scene
```

---

## Performance

### Latenz-Analyse

**Alte Architektur (Smart Lua):**
```
Erz erkannt → Lua entscheidet → Lua bewegt → Unity aktualisiert (1.0s delay)
Total: ~1.2s
```

**Neue Architektur (Unity Brain):**
```
Erz erkannt (Unity) → Unity sendet Befehl → Lua führt aus → Unity aktualisiert (WebSocket instant)
Total: ~0.3s
```

**Mit AI:**
```
Erz erkannt → Unity fragt AI → AI antwortet (0.5-2s) → Unity sendet Befehl → Lua führt aus
Total: ~1.0-2.5s (abhängig von AI-Model)
```

### Optimierungen

**1. Command Batching:**
```csharp
// Statt einzelne Befehle:
QueueCommand("forward");
QueueCommand("forward");
QueueCommand("dig");

// Batch senden:
QueueCommands(new[] {"forward", "forward", "dig"});
```

**2. WebSocket statt HTTP:**
```
HTTP Polling: Delay ~1.0s
WebSocket: Delay ~0.05s (instant)
```

**3. AI Caching:**
```csharp
// Cache AI decisions für ähnliche Situationen
if (SimilarContext(currentContext, lastContext)) {
    ExecuteCommand(cachedDecision);
    return;
}
```

---

## Zusammenfassung

| Aspekt | Vorher (Smart Lua) | Nachher (Unity Brain) |
|--------|-------------------|---------------------|
| **Intelligenz** | Lua | Unity |
| **Debugging** | Print Statements | Unity Debugger + Console |
| **Visualisierung** | Keine | Scene Gizmos + Inspector |
| **Flexibilität** | Fest kodiert | Modulare Manager |
| **AI-Integration** | Nicht möglich | LM Studio Support |
| **Performance** | Gut | Besser (WebSocket) |
| **Wartbarkeit** | Schwierig | Einfach (C#) |
| **Testing** | Nur in Minecraft | Unity + Minecraft |

---

**Die neue Architektur macht das System wartbarer, erweiterbarer und bietet KI-Integration!** 🎯
