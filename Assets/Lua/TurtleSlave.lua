local SERVER_COMMAND_URL = "http://192.168.178.211:4999/command"
local SERVER_REPORT_URL  = "http://192.168.178.211:4999/report"
local SERVER_STATUS_URL  = "http://192.168.178.211:4999/status"
local SLEEP_TIME         = 0.5
local GEOSCANNER_SLOT    = 1

local pos = { x = 0, y = 0, z = 0 }
local direction = nil
local isBusy = false

local directionOrder = { "north", "east", "south", "west" }

local validFuelItems = {
    ["minecraft:coal"] = true,
    ["minecraft:charcoal"] = true,
    ["minecraft:lava_bucket"] = true,
    ["minecraft:blaze_powder"] = true,
    ["minecraft:coal_block"] = true,
    -- hier kannst du weitere Brennstoffe hinzufügen
}

if not table.find then
    function table.find(tbl, val)
        for i, v in ipairs(tbl) do
            if v == val then return i end
        end
        return nil
    end
end


function getDirection()

    local x1, y1, z1 = gps.locate(2)
    if not x1 then
        print("Fehler: Kein GPS-Signal für initiale Position")
        direction = "unknown"
        return
    end

    -- Try to move forward
    local moved = turtle.forward()
    if not moved then
        print("Warnung: Turtle konnte sich nicht vorwärts bewegen, grabe Block")
        turtle.dig()
        moved = turtle.forward()
        if not moved then
            print("Fehler: Richtung konnte nicht bestimmt werden - Turtle blockiert")
            direction = "unknown"
            return
        end
    end

    local x2, y2, z2 = gps.locate(2)
    turtle.back()

    if not x2 then
        print("Fehler: Kein GPS-Signal nach Bewegung")
        direction = "unknown"
        return
    end

    local dx = x2 - x1
    local dz = z2 - z1

    if dx == 1 then direction = "east"
    elseif dx == -1 then direction = "west"
    elseif dz == 1 then direction = "south"
    elseif dz == -1 then direction = "north"
    else
        print("Warnung: Unerwartete Bewegung dx=" .. dx .. " dz=" .. dz)
        direction = "unknown"
    end

    print("Richtung: " .. direction)
end

function getPosition()
    local x1, y1, z1 = gps.locate(2)
    if not x1 then print("Kein GPS-Signal"); return end
    pos = { x = x1, y = y1, z = z1 }
    print("GPS: ", textutils.serialize(pos))
end

-- Inventarstatus erfassen
function getInventoryStatus()
    local inventory = {}
    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail then
            table.insert(inventory, {
                slot = slot,
                name = detail.name,
                count = detail.count
            })
        end
    end
    return inventory
end

-- Aktuell ausgerüstetes Tool (wenn periph.)
function getEquippedTool()
    local toolLeft = peripheral.getType("left") 
    local toolRight = peripheral.getType("right")
    return tool or "none"
end


function reportStatus()
    local status = {
        position = pos,
        direction = direction,
        label = os.getComputerLabel(),
        isBusy = isBusy,
        fuelLevel = turtle.getFuelLevel(),
        maxFuel = turtle.getFuelLimit(),
        inventorySlotsUsed = #getInventoryStatus(),
        inventorySlotsTotal = 16,
        inventory = getInventoryStatus(),
        equippedToolRight = peripheral.getType("right") or "none",
        equippedToolLeft = peripheral.getType("left") or "none",
    }
    local json = textutils.serializeJSON(status)
    http.post(SERVER_STATUS_URL, json, {["Content-Type"] = "application/json"})
end
function scanEnvironment()
    local scanner = peripheral.find("geoScanner")
    if not scanner then
        print("GeoScanner nicht gefunden")
        return
    end

    local ok, scanData = pcall(scanner.scan, 16)
    if ok and scanData then
        local cleaned = {}
        for _, block in ipairs(scanData) do
            local absX = pos.x + block.x
            local absY = pos.y + block.y
            local absZ = pos.z + block.z
            table.insert(cleaned, { name = block.name, x = absX, y = absY, z = absZ })
            if #cleaned % 200 == 0 then os.queueEvent(""); os.pullEvent() end
        end
        local json = textutils.serializeJSON(cleaned, { compact = true })
        http.post(SERVER_REPORT_URL, json, { ["Content-Type"] = "application/json" })
        print("Scan gesendet mit " .. #cleaned .. " Blöcken.")
    else
        print("Scan fehlgeschlagen.")
    end
end


function getNextCommand(label)
    local url = SERVER_COMMAND_URL .. "?label=" .. textutils.urlEncode(label)
    local ok, res = pcall(http.get, url)
    if ok and res then
        local body = res.readAll()
        res.close()
        local success, data = pcall(textutils.unserializeJSON, body)
        if success and data and data.command and data.command ~= "None" then
            return data
        end
    end
    return nil
end

-- Start
print("Initialisiere GPS...")
getDirection()
getPosition()
reportStatus()
print("Starte Steuerung...")

while true do
    local label = os.getComputerLabel()
    local cmdData = getNextCommand(label)

    if cmdData then
        local cmd = cmdData.command
        local result = false
        if cmd == "forward" then result = turtle.forward()
        elseif cmd == "back" then result = turtle.back()
        elseif cmd == "up" then result = turtle.up()
        elseif cmd == "down" then result = turtle.down()
        elseif cmd == "left" then
            turtle.turnLeft()
            local i = table.find(directionOrder, direction)
            if i then
                direction = directionOrder[(i - 2) % #directionOrder + 1]  -- links drehen
                result = true
            else
                print("Warnung: Unbekannte Richtung, versuche Richtung neu zu bestimmen")
                getDirection()
                result = false
            end
        elseif cmd == "right" then
            turtle.turnRight()
            local i = table.find(directionOrder, direction)
            if i then
                direction = directionOrder[i % #directionOrder + 1]  -- rechts drehen
                result = true
            else
                print("Warnung: Unbekannte Richtung, versuche Richtung neu zu bestimmen")
                getDirection()
                result = false
            end
        elseif cmd == "dig" then
            result = turtle.dig()
        elseif cmd == "digdown" then
            result = turtle.digDown()
        elseif cmd == "digup" then
            result = turtle.digUp()
        elseif cmd == "scan" then scanEnvironment(); result = true
        elseif cmd == "refuel" then executeRefuel(cmdData); result = true
        else print("Unbekannter Befehl:", cmd)
        end

        if result then            
            print("Befehl ausgeführt:", cmd)
        else
            print("Fehler bei Befehl:", cmd)
        end
    end
    getPosition()
    reportStatus()
    sleep(SLEEP_TIME)
end

function executeRefuel(cmdData)
    print("Starte Refuel-Vorgang...")
    isBusy = true
    local chest = peripheral.find("minecraft:chest")


    for slot, item in pairs(chest.list()) do       
        if item and validFuelItems[item.name] then
            chest.pushItems(os.getComputerLabel(), slot, 10)
        end           
    end
    turtle.refuel(64)
    isBusy = false
end

