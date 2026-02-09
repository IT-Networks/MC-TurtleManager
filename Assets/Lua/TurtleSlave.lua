-- TurtleSlave.lua - Simple Command Executor
-- All intelligence is in Unity, this script just executes commands

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
    ["minecraft:blaze_rod"] = true,
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

    local moved = turtle.forward()
    if not moved then
        print("Fehler: Richtung konnte nicht bestimmt werden - Turtle blockiert")
        direction = "unknown"
        return
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
end

-- Get detailed inventory status
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

-- Count total inventory slots used
function countInventorySlotsUsed()
    local count = 0
    for slot = 1, 16 do
        if turtle.getItemCount(slot) > 0 then
            count = count + 1
        end
    end
    return count
end

function getEquippedTool()
    local toolLeft = peripheral.getType("left") or "none"
    local toolRight = peripheral.getType("right") or "none"
    return {left = toolLeft, right = toolRight}
end

-- Get unique block types currently in turtle's inventory
function getInventoryBlockTypes()
    local blockTypes = {}
    local seen = {}

    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail and not seen[detail.name] then
            table.insert(blockTypes, {
                name = detail.name,
                totalCount = detail.count
            })
            seen[detail.name] = true
        end
    end

    return blockTypes
end

-- Count total blocks of a specific type in inventory
function countBlocksInInventory(blockName)
    local total = 0
    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail and detail.name == blockName then
            total = total + detail.count
        end
    end
    return total
end

function reportStatus()
    local status = {
        position = pos,
        direction = direction,
        label = os.getComputerLabel(),
        isBusy = isBusy,
        fuelLevel = turtle.getFuelLevel(),
        maxFuel = turtle.getFuelLimit(),
        inventorySlotsUsed = countInventorySlotsUsed(),
        inventorySlotsTotal = 16,
        inventory = getInventoryStatus(),
        equippedToolRight = peripheral.getType("right") or "none",
        equippedToolLeft = peripheral.getType("left") or "none",
        inventoryBlocks = getInventoryBlockTypes(), -- Blocks currently in inventory (for AI context)
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

-- Parse commands with parameters (e.g., "select:5", "refuel:10", "drop:3")
function parseCommand(cmdString)
    local parts = {}
    for part in string.gmatch(cmdString, "[^:]+") do
        table.insert(parts, part)
    end
    return parts[1], parts[2]
end

-- Refuel from inventory or chest
function executeRefuel(amount)
    print("Starte Refuel-Vorgang...")
    isBusy = true

    amount = tonumber(amount) or 64

    -- Try to refuel from inventory first
    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail and validFuelItems[detail.name] then
            turtle.select(slot)
            turtle.refuel(amount)
            print("Refueled mit " .. detail.name)
            if turtle.getFuelLevel() > 1000 then
                isBusy = false
                return true
            end
        end
    end

    -- Try to get fuel from chest below
    local hasChest, chestData = turtle.inspectDown()
    if hasChest and chestData.name:find("chest") then
        turtle.suckDown()
        -- Try again
        for slot = 1, 16 do
            local detail = turtle.getItemDetail(slot)
            if detail and validFuelItems[detail.name] then
                turtle.select(slot)
                turtle.refuel(amount)
            end
        end
    end

    isBusy = false
    return true
end

-- Drop items (to chest or ground)
function executeDrop(slot)
    isBusy = true
    slot = tonumber(slot)

    if slot then
        turtle.select(slot)
        turtle.drop()
    else
        -- Drop all non-fuel items
        for s = 1, 16 do
            local detail = turtle.getItemDetail(s)
            if detail and not validFuelItems[detail.name] then
                turtle.select(s)
                turtle.drop()
            end
        end
    end

    isBusy = false
    return true
end

-- Drop items down (to chest below)
function executeDropDown(slot)
    isBusy = true
    slot = tonumber(slot)

    if slot then
        turtle.select(slot)
        turtle.dropDown()
    else
        -- Drop all non-fuel items
        for s = 1, 16 do
            local detail = turtle.getItemDetail(s)
            if detail and not validFuelItems[detail.name] then
                turtle.select(s)
                turtle.dropDown()
            end
        end
    end

    isBusy = false
    return true
end

-- Select inventory slot
function executeSelect(slot)
    slot = tonumber(slot) or 1
    turtle.select(slot)
    return true
end

-- Place block
function executePlace(direction)
    if direction == "up" then
        return turtle.placeUp()
    elseif direction == "down" then
        return turtle.placeDown()
    else
        return turtle.place()
    end
end

-- Suck items from chest
function executeSuck(direction)
    if direction == "up" then
        return turtle.suckUp()
    elseif direction == "down" then
        return turtle.suckDown()
    else
        return turtle.suck()
    end
end

-- ========== MAIN LOOP ==========

print("Initialisiere GPS...")
getDirection()
getPosition()

reportStatus()
print("Starte Turtle Control...")
print("Waiting for commands from Unity...")

while true do
    local label = os.getComputerLabel()
    local cmdData = getNextCommand(label)

    if cmdData then
        local cmdString = cmdData.command
        local cmd, param = parseCommand(cmdString)
        local result = false

        -- Movement commands
        if cmd == "forward" then result = turtle.forward()
        elseif cmd == "back" then result = turtle.back()
        elseif cmd == "up" then result = turtle.up()
        elseif cmd == "down" then result = turtle.down()

        -- Rotation commands
        elseif cmd == "left" then
            turtle.turnLeft()
            local i = table.find(directionOrder, direction)
            if i then
                direction = directionOrder[(i - 2) % #directionOrder + 1]
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
                direction = directionOrder[i % #directionOrder + 1]
                result = true
            else
                print("Warnung: Unbekannte Richtung, versuche Richtung neu zu bestimmen")
                getDirection()
                result = false
            end

        -- Digging commands
        elseif cmd == "dig" then result = turtle.dig()
        elseif cmd == "digdown" then result = turtle.digDown()
        elseif cmd == "digup" then result = turtle.digUp()

        -- Placing commands
        elseif cmd == "place" then result = executePlace(param)
        elseif cmd == "placeup" then result = turtle.placeUp()
        elseif cmd == "placedown" then result = turtle.placeDown()

        -- Inventory commands
        elseif cmd == "select" then result = executeSelect(param)
        elseif cmd == "drop" then result = executeDrop(param)
        elseif cmd == "dropup" then
            if param then turtle.select(tonumber(param)) end
            result = turtle.dropUp()
        elseif cmd == "dropdown" then result = executeDropDown(param)
        elseif cmd == "suck" then result = executeSuck(param)
        elseif cmd == "suckup" then result = turtle.suckUp()
        elseif cmd == "suckdown" then result = turtle.suckDown()

        -- Utility commands
        elseif cmd == "scan" then scanEnvironment(); result = true
        elseif cmd == "refuel" then result = executeRefuel(param)

        else
            print("Unbekannter Befehl:", cmdString)
        end

        if result then
            print("Befehl ausgeführt:", cmdString)
            -- Update position after successful movement
            if cmd == "forward" or cmd == "back" or cmd == "up" or cmd == "down" then
                getPosition()
            end
        else
            print("Fehler bei Befehl:", cmdString)
        end
    end

    reportStatus()
    sleep(SLEEP_TIME)
end
