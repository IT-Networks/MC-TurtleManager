# LM Studio Integration - AI-gesteuerter Turtle

## Übersicht

Mit der LM Studio Integration können Turtles durch ein lokales AI-Modell autonom gesteuert werden. Die AI analysiert das Inventar, die Umgebung und den Fuel-Status und trifft intelligente Entscheidungen.

---

## Setup

### 1. LM Studio installieren

**Download:** https://lmstudio.ai/

1. LM Studio herunterladen und installieren
2. LM Studio starten

### 2. Model herunterladen

**Empfohlene Models:**

**Für Anfänger (schnell, wenig RAM):**
- `TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF`
- RAM: ~2GB
- Geschwindigkeit: Sehr schnell
- Qualität: Gut für einfache Aufgaben

**Standard (ausgewogen):**
- `TheBloke/Mistral-7B-Instruct-v0.2-GGUF`
- RAM: ~8GB
- Geschwindigkeit: Mittel
- Qualität: Sehr gut

**Fortgeschritten (beste Qualität):**
- `TheBloke/neural-chat-7B-v3-3-GGUF`
- RAM: ~10GB
- Geschwindigkeit: Langsamer
- Qualität: Ausgezeichnet

**In LM Studio:**
1. Tab "Discover" öffnen
2. Model suchen (z.B. "Mistral 7B Instruct")
3. Download klicken
4. Auf Download-Abschluss warten

### 3. LM Studio Server starten

1. **Tab "Local Server"** öffnen
2. **Model auswählen** (das heruntergeladene Model)
3. **Server Settings:**
   ```
   Port: 1234
   CORS: Enabled
   ```
4. **"Start Server"** klicken

Server läuft jetzt auf: `http://localhost:1234`

### 4. Unity konfigurieren

**1. Turtle GameObject auswählen**

**2. LMStudioManager Component hinzufügen:**
```
Add Component > LMStudioManager
```

**3. Settings konfigurieren:**
```
Inspector:
LM Studio Configuration
├── LM Studio URL: http://localhost:1234/v1/chat/completions
├── Model Name: local-model
├── Temperature: 0.7
└── Max Tokens: 500

AI Behavior
├── Enable AI Control: ✓
├── AI Decision Interval: 5
├── Allow Mining: ✓
├── Allow Building: ✓
└── Allow Exploration: ✓

Debug
└── Debug Mode: ✓ (für Testing)
```

---

## Verwendung

### Automatischer Modus

Wenn **Enable AI Control** aktiviert ist:

1. AI analysiert alle 5 Sekunden (AI Decision Interval) den Turtle-Status
2. AI trifft Entscheidung basierend auf:
   - Fuel-Level
   - Inventar-Status
   - Erkannte Erze in Nähe
   - Aktuelle Aufgabe
3. AI sendet Befehl an Turtle
4. Turtle führt Befehl aus

**Beispiel-Ablauf:**
```
[Tick 1] AI: "Fuel bei 80%, Inventar leer, Diamond Ore erkannt"
         → COMMAND: mine
         → Turtle minet Diamant

[Tick 2] AI: "Fuel bei 78%, 1 Diamant im Inventar, kein Ore sichtbar"
         → COMMAND: forward
         → Turtle bewegt sich vorwärts

[Tick 3] AI: "Fuel bei 15%, Inventar 8/16"
         → COMMAND: refuel
         → Turtle tankt nach aus Inventar
```

### Manueller Modus

**1. AI Control deaktivieren:**
```csharp
enableAIControl = false
```

**2. Manuell AI-Entscheidung anfordern:**
```csharp
// In Unity Inspector oder per Script
lmStudioManager.RequestManualAIDecision();
```

**3. Custom Prompt senden:**
```csharp
lmStudioManager.SendCustomPrompt("Mine all iron ore in the area");
```

---

## AI Entscheidungs-Logik

### Prioritäten-System

Die AI folgt dieser Prioritäts-Hierarchie:

**1. Überlebenswichtig (CRITICAL):**
- Fuel < 20% → Refuel sofort
- Fuel < 5% → Notfall, zurück zur Basis
- Inventar 16/16 → Kann nichts aufheben, Items lagern

**2. Wichtig (HIGH):**
- Inventar > 14/16 → Items lagern
- Fuel < 50% → Bald nachfüllen
- Wertvolle Erze (Diamond, Emerald) erkannt → Mining

**3. Normal (MEDIUM):**
- Normale Erze (Gold, Iron) erkannt → Mining
- Aktuelle Aufgabe fortsetzen
- Exploration

**4. Niedrig (LOW):**
- Unwichtige Erze (Coal, Copper)
- Warten auf neue Aufgabe

### Beispiel-Entscheidungen

#### Szenario 1: Niedriger Fuel, Diamant erkannt

**Status:**
- Fuel: 15%
- Inventar: 3/16
- Nearby: Diamond Ore

**AI Analyse:**
```
CRITICAL: Fuel at 15% (3000/20000)
HIGH: Diamond ore detected 5 blocks away

Calculation:
- Distance to diamond: 5 blocks = ~10 fuel
- Mining cost: ~5 fuel
- Distance to chest: 50 blocks = ~100 fuel
- Total needed: ~115 fuel
- Current fuel: 3000 fuel

Decision: SAFE to mine diamond
Remaining fuel after: ~2885 (still > 2000 minimum)
```

**AI Response:**
```
COMMAND:forward
REASON:Diamond detected 5 blocks away. Current fuel (15%) sufficient for mining operation and safe return to chest. Will mine diamond then refuel.
```

#### Szenario 2: Volles Inventar, mehr Erze verfügbar

**Status:**
- Fuel: 60%
- Inventar: 15/16 (8x Diamond, 12x Gold, 20x Iron)
- Nearby: Diamond Ore

**AI Analyse:**
```
HIGH: Inventory 15/16 (nearly full)
HIGH: Diamond ore detected
HIGH: Already carrying 8 diamonds

Decision Tree:
1. Can I pick up more? → NO (inventory full)
2. Should I store items? → YES (valuable items)
3. Will I lose the diamond? → Maybe

Decision: Store items first, then return to mine
```

**AI Response:**
```
COMMAND:store_items
REASON:Inventory critically full (15/16) with valuable items: 8 diamonds, 12 gold, 20 iron. Must store these safely before mining more. Will mark position and return after storage.
```

---

## Erweiterte Features

### 1. Task-Specific Prompts

Du kannst spezifische System Prompts für verschiedene Aufgaben verwenden:

**Mining-Spezialist laden:**
```csharp
string miningPrompt = File.ReadAllText("AI_SYSTEM_PROMPT.md");
// Extract mining specialist prompt
lmStudioManager.conversationHistory[0] = miningPrompt;
```

**Builder AI laden:**
```csharp
// Load builder-specific prompt from AI_SYSTEM_PROMPT.md
```

### 2. Multi-Turtle Koordination

**Setup:**
```csharp
// Turtle 1: Mining Specialist
turtle1.lmStudioManager.SetSystemPrompt(miningSpecialistPrompt);

// Turtle 2: Resource Manager
turtle2.lmStudioManager.SetSystemPrompt(resourceManagerPrompt);

// Turtle 3: Builder
turtle3.lmStudioManager.SetSystemPrompt(builderPrompt);
```

**Koordination:**
```csharp
// Share information between turtles
string sharedContext = $"Turtle1 found diamond vein at {position}";
turtle2.lmStudioManager.SendCustomPrompt(sharedContext);
```

### 3. Aufgaben-Queue

**Aufgabe zuweisen:**
```csharp
lmStudioManager.SendCustomPrompt(@"
NEW TASK: Mine all iron ore in area 100,64,200 to 150,80,250
PRIORITY: High
DEADLINE: Complete before fuel drops below 30%
");
```

### 4. Lern-Modus (Future Feature)

Die AI könnte aus Fehlern lernen:

```csharp
// Log AI decisions and outcomes
aiLogger.LogDecision(decision, outcome, success);

// Feed back to AI for learning
if (!success) {
    lmStudioManager.SendCustomPrompt($"Previous action '{decision}' failed because: {reason}. Avoid this in future.");
}
```

---

## Debugging

### Console Output

**AI Requests:**
```
[AI miner_01] Requesting AI decision...
[AI] Sending request to LM Studio: Current Position: (105, 64, 205)...
```

**AI Responses:**
```
[AI] Received response: {"choices":[{"message":{"content":"COMMAND:mine\nREASON:..."}}]}
[AI] AI Response: COMMAND:mine
REASON:Diamond ore detected directly ahead. Fuel at 50%, inventory at 3/16. Safe to mine.
[AI] Executing command: mine
```

**Command Execution:**
```
[TurtleBase] Queuing command for miner_01: mine
[miner_01] Befehl ausgeführt: dig
```

### Inspector Monitoring

Im Unity Inspector (LMStudioManager Component):

```
Debug
├── Last AI Response: "COMMAND:mine\nREASON:Diamond detected..."
└── Last Executed Command: "mine"
```

### LM Studio Server Log

In LM Studio → Tab "Logs":

```
[INFO] Request received from 127.0.0.1
[INFO] Processing chat completion request
[INFO] Tokens used: 350 / 500
[INFO] Response sent (1.2s)
```

---

## Performance Optimierung

### Schnellere Entscheidungen

**1. Reduce Token Limit:**
```csharp
maxTokens = 200; // Statt 500
```

**2. Increase Decision Interval:**
```csharp
aiDecisionInterval = 10f; // Alle 10s statt 5s
```

**3. Use Smaller Model:**
- TinyLlama (1.1B) statt Mistral (7B)

### Intelligentere Entscheidungen

**1. More Context:**
```csharp
// Add more information to context
context.AppendLine($"Previous actions: {lastActions}");
context.AppendLine($"Failed attempts: {failedAttempts}");
```

**2. Larger Model:**
- Neural-Chat (7B) oder Mixtral (8x7B)

**3. Higher Temperature:**
```csharp
temperature = 0.9f; // Mehr Kreativität
```

---

## Troubleshooting

### Problem: AI antwortet nicht

**Lösung:**

1. **LM Studio Server läuft?**
   - Check LM Studio → Tab "Local Server"
   - Status sollte "Running" sein

2. **Richtiger Port?**
   ```csharp
   lmStudioUrl = "http://localhost:1234/v1/chat/completions"
   ```

3. **Firewall?**
   - Windows Firewall: LM Studio erlauben

4. **Test mit cURL:**
   ```bash
   curl http://localhost:1234/v1/models
   ```
   Sollte Model-Liste zurückgeben

### Problem: AI gibt unsinnige Befehle

**Lösung:**

1. **System Prompt verbessern:**
   - Mehr Kontext geben
   - Klarere Regeln definieren

2. **Temperature senken:**
   ```csharp
   temperature = 0.3f; // Weniger Kreativität, mehr Logik
   ```

3. **Besseres Model verwenden:**
   - Mistral 7B Instruct (sehr gut bei Befehlen)

### Problem: AI zu langsam

**Lösung:**

1. **Kleineres Model:**
   ```
   TinyLlama-1.1B (sehr schnell)
   ```

2. **Weniger Tokens:**
   ```csharp
   maxTokens = 150;
   ```

3. **GPU Acceleration in LM Studio aktivieren:**
   - Settings → GPU Acceleration → CUDA/Metal

### Problem: Hoher RAM-Verbrauch

**Lösung:**

1. **Quantized Model verwenden:**
   ```
   Mistral-7B-Instruct-v0.2-GGUF (Q4_K_M)
   ```
   Q4_K_M = 4-bit quantisiert, weniger RAM

2. **Model entladen wenn nicht genutzt:**
   ```csharp
   if (!enableAIControl) {
       // Unload model in LM Studio
   }
   ```

---

## Beispiel-Szenarien

### Szenario 1: Autonomous Mining Operation

**Setup:**
```
Turtle: miner_01
Position: (100, 64, 200) - bei Truhe
Fuel: 15000/20000 (75%)
Inventar: Leer
Aufgabe: Mine alle Diamanten im Umkreis
```

**AI Ablauf:**

```
[00:00] AI: Scan area for diamonds
        → COMMAND: scan

[00:05] AI: 5 diamond ores detected at various locations
        → COMMAND: forward (navigate to nearest)

[00:10] AI: Reached diamond ore
        → COMMAND: mine

[00:15] AI: Diamond mined, 4 more to go
        → COMMAND: forward (to next diamond)

[00:30] AI: All 5 diamonds mined, inventory 5/16, fuel 70%
        → COMMAND: wait (no more diamonds, mission complete)
```

### Szenario 2: Smart Resource Management

**Setup:**
```
Turtle: manager_01
Position: (150, 64, 180)
Fuel: 5000/20000 (25%)
Inventar: 14/16 (Mixed items)
```

**AI Ablauf:**

```
[00:00] AI: Critical assessment needed
        Fuel: 25% (LOW)
        Inventory: 14/16 (NEARLY FULL)
        Distance to chest: 60 blocks

[00:00] AI Analysis:
        - Fuel sufficient for chest trip (60 blocks = ~120 fuel)
        - Must store items before fuel drops further
        - After storage, immediately refuel

        → COMMAND: store_items

[00:45] AI: Items stored, at chest, inventory 1/16
        → COMMAND: suckdown (get fuel from chest)

[00:50] AI: Coal acquired
        → COMMAND: refuel

[00:55] AI: Refueled to 80%, ready for operations
        → COMMAND: scan (resume mining)
```

---

## Best Practices

### 1. Fuel Safety

```
FUEL RULES:
- Refuel at 50% for safety
- Never let fuel drop below 20%
- Always keep emergency coal in inventory
- Calculate fuel cost before long trips
```

### 2. Inventory Organization

```
INVENTORY LAYOUT:
Slots 1-12: Mining/Building materials
Slots 13-14: Tools
Slots 15-16: Fuel (Coal, Lava Bucket)
```

### 3. Task Prioritization

```
PRIORITY ORDER:
1. Survival (Fuel, Safety)
2. Critical tasks (Store valuable items)
3. Primary objective (Mining, Building)
4. Exploration
5. Idle/Wait
```

### 4. Error Recovery

```
ERROR HANDLING:
- If stuck: Try "up" command
- If lost: Scan and re-orient
- If low fuel: Emergency return to base
- If inventory full: Force storage
```

---

## Zukünftige Features

- [ ] Multi-Turtle Koordination via AI
- [ ] Lern-System (AI lernt aus Fehlern)
- [ ] Voice Commands über Whisper AI
- [ ] Computer Vision (Screenshot-Analyse)
- [ ] Task Planning (Mehrschrittige Aufgaben)
- [ ] Simulation Mode (AI plant in Unity, testet, dann ausführt)

---

**Viel Erfolg mit deinem AI-gesteuerten Turtle!** 🤖⛏️
