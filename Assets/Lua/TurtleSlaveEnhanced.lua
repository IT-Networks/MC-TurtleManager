-- Enhanced TurtleSlave with Ore Mining, Inventory Management, and Fuel Management
local SERVER_COMMAND_URL = "http://192.168.178.211:4999/command"
local SERVER_REPORT_URL  = "http://192.168.178.211:4999/report"
local SERVER_STATUS_URL  = "http://192.168.178.211:4999/status"
local SLEEP_TIME         = 0.5
local GEOSCANNER_SLOT    = 1

-- Configuration
local AUTO_ORE_MINING = true
local AUTO_INVENTORY_MANAGEMENT = true
local AUTO_FUEL_MANAGEMENT = true
local INVENTORY_FULL_THRESHOLD = 14
local LOW_FUEL_THRESHOLD = 500
local CHEST_POSITION = {x = 0, y = 0, z = 0}  -- Set this to your chest location

local pos = { x = 0, y = 0, z = 0 }
local direction = nil
local isBusy = false

local directionOrder = { "north", "east", "south", "west" }

-- Ore types to detect and mine
local TARGET_ORES = {
    -- Vanilla ores
    "minecraft:coal_ore",
    "minecraft:deepslate_coal_ore",
    "minecraft:iron_ore",
    "minecraft:deepslate_iron_ore",
    "minecraft:gold_ore",
    "minecraft:deepslate_gold_ore",
    "minecraft:diamond_ore",
    "minecraft:deepslate_diamond_ore",
    "minecraft:emerald_ore",
    "minecraft:deepslate_emerald_ore",
    "minecraft:lapis_ore",
    "minecraft:deepslate_lapis_ore",
    "minecraft:redstone_ore",
    "minecraft:deepslate_redstone_ore",
    "minecraft:copper_ore",
    "minecraft:deepslate_copper_ore",

    -- ATM10 modded ores
    "allthemodium:allthemodium_ore",
    "allthemodium:vibranium_ore",
    "allthemodium:unobtainium_ore",
    "mekanism:osmium_ore",
    "mekanism:deepslate_osmium_ore",
    "thermal:tin_ore",
    "thermal:deepslate_tin_ore",
    "thermal:lead_ore",
    "thermal:deepslate_lead_ore",
    "mekanism:uranium_ore",
    "mekanism:deepslate_uranium_ore",
    "create:zinc_ore",
    "create:deepslate_zinc_ore",
}

local validFuelItems = {
    ["minecraft:coal"] = true,
    ["minecraft:charcoal"] = true,
    ["minecraft:lava_bucket"] = true,
    ["minecraft:blaze_powder"] = true,
    ["minecraft:coal_block"] = true,
    ["minecraft:blaze_rod"] = true,
}

-- All available blocks for construction (synced with Unity AIBlockLibrary)
local AVAILABLE_BLOCKS = {
    -- Stone & Bricks
    "minecraft:stone", "minecraft:cobblestone", "minecraft:stone_bricks",
    "minecraft:mossy_stone_bricks", "minecraft:smooth_stone", "minecraft:andesite",
    "minecraft:polished_andesite", "minecraft:diorite", "minecraft:polished_diorite",
    "minecraft:granite", "minecraft:polished_granite", "minecraft:deepslate",
    "minecraft:deepslate_bricks", "minecraft:deepslate_tiles",

    -- Wood & Planks
    "minecraft:oak_planks", "minecraft:oak_log", "minecraft:stripped_oak_log",
    "minecraft:spruce_planks", "minecraft:spruce_log", "minecraft:birch_planks",
    "minecraft:birch_log", "minecraft:jungle_planks", "minecraft:jungle_log",
    "minecraft:acacia_planks", "minecraft:acacia_log", "minecraft:dark_oak_planks",
    "minecraft:dark_oak_log", "minecraft:mangrove_planks", "minecraft:mangrove_log",
    "minecraft:cherry_planks", "minecraft:cherry_log", "minecraft:crimson_planks",
    "minecraft:crimson_stem", "minecraft:warped_planks", "minecraft:warped_stem",

    -- Glass
    "minecraft:glass", "minecraft:white_stained_glass", "minecraft:light_gray_stained_glass",
    "minecraft:gray_stained_glass", "minecraft:black_stained_glass", "minecraft:brown_stained_glass",
    "minecraft:red_stained_glass", "minecraft:orange_stained_glass", "minecraft:yellow_stained_glass",
    "minecraft:lime_stained_glass", "minecraft:green_stained_glass", "minecraft:cyan_stained_glass",
    "minecraft:light_blue_stained_glass", "minecraft:blue_stained_glass", "minecraft:purple_stained_glass",
    "minecraft:magenta_stained_glass", "minecraft:pink_stained_glass", "minecraft:glass_pane",

    -- Concrete
    "minecraft:white_concrete", "minecraft:light_gray_concrete", "minecraft:gray_concrete",
    "minecraft:black_concrete", "minecraft:brown_concrete", "minecraft:red_concrete",
    "minecraft:orange_concrete", "minecraft:yellow_concrete", "minecraft:lime_concrete",
    "minecraft:green_concrete", "minecraft:cyan_concrete", "minecraft:light_blue_concrete",
    "minecraft:blue_concrete", "minecraft:purple_concrete", "minecraft:magenta_concrete",
    "minecraft:pink_concrete",

    -- Slabs & Stairs
    "minecraft:oak_slab", "minecraft:spruce_slab", "minecraft:stone_slab", "minecraft:stone_brick_slab",
    "minecraft:oak_stairs", "minecraft:spruce_stairs", "minecraft:stone_stairs", "minecraft:stone_brick_stairs",

    -- Doors & Gates
    "minecraft:oak_door", "minecraft:spruce_door", "minecraft:oak_fence", "minecraft:spruce_fence",
    "minecraft:oak_fence_gate", "minecraft:iron_door", "minecraft:iron_bars",

    -- Lighting
    "minecraft:torch", "minecraft:lantern", "minecraft:glowstone", "minecraft:sea_lantern",
    "minecraft:redstone_lamp", "minecraft:shroomlight",

    -- Functional
    "minecraft:chest", "minecraft:barrel", "minecraft:crafting_table", "minecraft:furnace",
    "minecraft:bookshelf", "minecraft:ladder", "minecraft:bed",

    -- Create Mod - Kinetic
    "create:cogwheel", "create:large_cogwheel", "create:shaft", "create:gearbox",
    "create:clutch", "create:gearshift", "create:encased_chain_drive", "create:adjustable_chain_gearshift",

    -- Create Mod - Generators
    "create:water_wheel", "create:windmill_bearing", "create:steam_engine", "create:motor",

    -- Create Mod - Logistics
    "create:mechanical_arm", "create:deployer", "create:mechanical_drill", "create:mechanical_saw",
    "create:mechanical_harvester", "create:mechanical_plough", "create:portable_storage_interface",

    -- Create Mod - Processing
    "create:millstone", "create:crushing_wheel", "create:mechanical_press", "create:mechanical_mixer",
    "create:encased_fan", "create:item_drain",

    -- Create Mod - Storage
    "create:item_vault", "create:belt_connector", "create:chute", "create:smart_chute",

    -- ATM10 Mods
    "pipez:item_pipe", "pipez:gas_pipe", "pipez:fluid_pipe", "pipez:energy_pipe",
    "mekanism:logistical_transporter", "mekanism:pressurized_tube", "mekanism:mechanical_pipe",
}

-- Ore mining state
local detectedOres = {}
local currentMiningTarget = nil

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

function getEquippedTool()
    local toolLeft = peripheral.getType("left") or "none"
    local toolRight = peripheral.getType("right") or "none"
    return {left = toolLeft, right = toolRight}
end

-- Get all available blocks (comprehensive list)
function getAllAvailableBlocks()
    return AVAILABLE_BLOCKS
end

-- Get unique block types currently in turtle's inventory
function getInventoryBlockTypes()
    local blockTypes = {}
    local seen = {}

    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail and not seen[detail.name] then
            -- Count total of this block type across all slots
            local totalCount = 0
            for s = 1, 16 do
                local d = turtle.getItemDetail(s)
                if d and d.name == detail.name then
                    totalCount = totalCount + d.count
                end
            end

            table.insert(blockTypes, {
                name = detail.name,
                totalCount = totalCount
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
        inventorySlotsUsed = #getInventoryStatus(),
        inventorySlotsTotal = 16,
        inventory = getInventoryStatus(),
        equippedToolRight = peripheral.getType("right") or "none",
        equippedToolLeft = peripheral.getType("left") or "none",
        autoOreMining = AUTO_ORE_MINING,
        detectedOres = #detectedOres,
        -- NEW: Available blocks information
        availableBlocks = getAllAvailableBlocks(),  -- Complete list of known blocks
        inventoryBlocks = getInventoryBlockTypes(), -- Blocks currently in inventory
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
        detectedOres = {}  -- Reset detected ores

        for _, block in ipairs(scanData) do
            local absX = pos.x + block.x
            local absY = pos.y + block.y
            local absZ = pos.z + block.z

            table.insert(cleaned, { name = block.name, x = absX, y = absY, z = absZ })

            -- Check if this is an ore block
            if isOreBlock(block.name) then
                table.insert(detectedOres, {
                    name = block.name,
                    x = absX,
                    y = absY,
                    z = absZ,
                    relX = block.x,
                    relY = block.y,
                    relZ = block.z
                })
            end

            if #cleaned % 200 == 0 then os.queueEvent(""); os.pullEvent() end
        end

        local json = textutils.serializeJSON(cleaned, { compact = true })
        http.post(SERVER_REPORT_URL, json, { ["Content-Type"] = "application/json" })

        if #detectedOres > 0 then
            print("Ores erkannt: " .. #detectedOres)
        end
        print("Scan gesendet mit " .. #cleaned .. " Blöcken.")
    else
        print("Scan fehlgeschlagen.")
    end
end

function isOreBlock(blockName)
    for _, oreName in ipairs(TARGET_ORES) do
        if blockName == oreName then
            return true
        end
    end
    return false
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

-- ========== INVENTORY MANAGEMENT ==========

function isInventoryFull()
    return #getInventoryStatus() >= INVENTORY_FULL_THRESHOLD
end

function isFuelItem(itemName)
    return validFuelItems[itemName] == true
end

function storeItemsInChest()
    print("Lagere Items in Truhe...")
    isBusy = true

    -- Try to find chest below
    local hasChest, chestData = turtle.inspectDown()
    if hasChest and chestData.name:find("chest") then
        -- Drop items into chest (keep fuel items)
        for slot = 1, 16 do
            turtle.select(slot)
            local detail = turtle.getItemDetail()
            if detail and not isFuelItem(detail.name) then
                turtle.dropDown()
            end
        end
        print("Items in Truhe gelagert")
    else
        print("Keine Truhe gefunden! Leere Inventar auf den Boden")
        -- Drop non-fuel items on ground if no chest
        for slot = 1, 16 do
            turtle.select(slot)
            local detail = turtle.getItemDetail()
            if detail and not isFuelItem(detail.name) then
                turtle.drop()
            end
        end
    end

    isBusy = false
end

function autoRefuel()
    print("Auto-Refuel: Suche Fuel Items...")
    isBusy = true

    -- Try to refuel from inventory
    for slot = 1, 16 do
        local detail = turtle.getItemDetail(slot)
        if detail and isFuelItem(detail.name) then
            turtle.select(slot)
            turtle.refuel(1)
            print("Refueled mit " .. detail.name)

            if turtle.getFuelLevel() > LOW_FUEL_THRESHOLD * 2 then
                print("Fuel ausreichend: " .. turtle.getFuelLevel())
                isBusy = false
                return true
            end
        end
    end

    -- Try to get fuel from chest below
    local hasChest, chestData = turtle.inspectDown()
    if hasChest and chestData.name:find("chest") then
        print("Hole Fuel aus Truhe...")
        turtle.suckDown()

        -- Try to refuel again
        for slot = 1, 16 do
            local detail = turtle.getItemDetail(slot)
            if detail and isFuelItem(detail.name) then
                turtle.select(slot)
                turtle.refuel(64)
                print("Refueled mit " .. detail.name .. " aus Truhe")
            end
        end
    end

    isBusy = false
    return turtle.getFuelLevel() > LOW_FUEL_THRESHOLD
end

-- ========== ORE MINING ==========

function mineOre(oreData)
    print("Mine Ore: " .. oreData.name .. " @ (" .. oreData.relX .. "," .. oreData.relY .. "," .. oreData.relZ .. ")")
    isBusy = true

    local relX = oreData.relX
    local relY = oreData.relY
    local relZ = oreData.relZ

    -- Simple navigation to ore (move on Y axis first, then X, then Z)
    -- Move up/down
    if relY > 0 then
        for i = 1, math.abs(relY) do
            turtle.digUp()
            turtle.up()
        end
    elseif relY < 0 then
        for i = 1, math.abs(relY) do
            turtle.digDown()
            turtle.down()
        end
    end

    -- Move forward/back/left/right to X and Z
    -- This is simplified - in production you'd want proper pathfinding
    navigateToRelativePosition(relX, relZ)

    -- Mine the ore
    turtle.dig()
    turtle.forward()

    print("Ore abgebaut!")
    getPosition()
    isBusy = false
end

function navigateToRelativePosition(relX, relZ)
    -- Simplified navigation - move X first, then Z
    -- Note: This needs proper direction handling in production

    if relX > 0 then
        for i = 1, math.abs(relX) do
            turtle.dig()
            turtle.forward()
        end
    elseif relX < 0 then
        turnAround()
        for i = 1, math.abs(relX) do
            turtle.dig()
            turtle.forward()
        end
        turnAround()
    end

    if relZ > 0 then
        for i = 1, math.abs(relZ) do
            turtle.dig()
            turtle.forward()
        end
    elseif relZ < 0 then
        turnAround()
        for i = 1, math.abs(relZ) do
            turtle.dig()
            turtle.forward()
        end
        turnAround()
    end
end

function turnAround()
    turtle.turnRight()
    turtle.turnRight()
end

function autoOreMining()
    if not AUTO_ORE_MINING or isBusy then
        return
    end

    -- Check inventory
    if AUTO_INVENTORY_MANAGEMENT and isInventoryFull() then
        print("Inventar voll! Lagere Items...")
        storeItemsInChest()
        return
    end

    -- Check fuel
    if AUTO_FUEL_MANAGEMENT and turtle.getFuelLevel() < LOW_FUEL_THRESHOLD then
        print("Fuel niedrig! Tanke nach...")
        autoRefuel()
        return
    end

    -- Mine detected ores
    if #detectedOres > 0 then
        local ore = table.remove(detectedOres, 1)  -- Get first ore
        mineOre(ore)
    end
end

function executeRefuel(cmdData)
    print("Starte Refuel-Vorgang...")
    autoRefuel()
end

-- ========== COMMAND EXECUTION ==========

function executeCommand(cmdData)
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
    elseif cmd == "dig" then
        result = turtle.dig()
    elseif cmd == "digdown" then
        result = turtle.digDown()
    elseif cmd == "digup" then
        result = turtle.digUp()
    elseif cmd == "scan" then
        scanEnvironment()
        result = true
    elseif cmd == "refuel" then
        executeRefuel(cmdData)
        result = true
    elseif cmd == "store_items" then
        storeItemsInChest()
        result = true
    elseif cmd == "toggle_ore_mining" then
        AUTO_ORE_MINING = not AUTO_ORE_MINING
        print("Auto Ore Mining: " .. tostring(AUTO_ORE_MINING))
        result = true
    else
        print("Unbekannter Befehl:", cmd)
    end

    if result then
        print("Befehl ausgeführt:", cmd)
        if cmd == "forward" or cmd == "back" or cmd == "up" or cmd == "down" then
            getPosition()
        end
    else
        print("Fehler bei Befehl:", cmd)
    end
end

-- ========== MAIN LOOP ==========

print("Initialisiere GPS...")
getDirection()
getPosition()
reportStatus()
print("Starte Enhanced Turtle Control...")
print("Auto Ore Mining: " .. tostring(AUTO_ORE_MINING))
print("Auto Inventory Management: " .. tostring(AUTO_INVENTORY_MANAGEMENT))
print("Auto Fuel Management: " .. tostring(AUTO_FUEL_MANAGEMENT))

while true do
    local label = os.getComputerLabel()
    local cmdData = getNextCommand(label)

    if cmdData then
        executeCommand(cmdData)
    end

    -- Auto ore mining check
    autoOreMining()

    reportStatus()
    sleep(SLEEP_TIME)
end
