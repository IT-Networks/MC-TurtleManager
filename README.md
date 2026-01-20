# MinecraftTurtleMaster

Ein unity Projekt welches eine Welt aus blocken erzeugt. Die Position der Blöcke kommt vom Minecraft selbst. Es ist ein top down Strategie Ansicht und man kann die turtles des mods computercaft steuern und Strukturen bauen und abbauen lassen


Zusätzliche Infos zur Struktur:
MinecraftMod: Hier liegt der Minecraft Mod welcher die Daten über Chunks etc. über einen API liefert
Lua: Hier liegt das Script für den Turtle in Minecraft welcher die Aufgaben ausführt aus Unity
FlaskServer: Der Flask Server dient als Schnittstelle zwischen Unity3d und dem Turtle. Außerdem speichert er Block Informationen und cached diese 
