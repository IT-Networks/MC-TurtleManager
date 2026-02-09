package com.worldinfo.worldinfomod;

import com.google.gson.*;
import com.google.gson.reflect.TypeToken;
import net.minecraft.core.BlockPos;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.ResourceKey;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.level.chunk.LevelChunk;
import net.neoforged.fml.common.Mod;
import net.neoforged.bus.api.SubscribeEvent;
import net.neoforged.neoforge.event.server.ServerStartingEvent;
import net.neoforged.neoforge.event.server.ServerStoppingEvent;
import net.neoforged.neoforge.common.util.BlockSnapshot;
import net.neoforged.neoforge.event.level.BlockEvent;
import net.neoforged.bus.api.IEventBus;

import com.sun.net.httpserver.HttpServer;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpExchange;

import java.io.*;
import java.lang.reflect.Type;
import java.net.InetSocketAddress;
import java.net.URI;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.*;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;
import java.util.concurrent.ConcurrentHashMap;


@Mod(WorldInfo.MODID)
public class WorldInfo {
    public static final String MODID = "worldinfo";
    private static MinecraftServer server;
    private HttpServer httpServer;
    private ScheduledExecutorService autoSaveExecutor;

    // Store chunk update timestamps: Map<Dimension, Map<ChunkPos, Timestamp>>
    private final Map<ResourceLocation, Map<Long, Long>> chunkUpdateTimestamps = new ConcurrentHashMap<>();
    private final Object saveLock = new Object();
    private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();
    private Path dataDirectory;
    private static final String TIMESTAMPS_FILE = "chunk_timestamps.json";
    private static final long SAVE_INTERVAL_MINUTES = 1;

    // Cached JSON responses for blocks and buildables (performance optimization)
    private static String cachedBlocksJson = null;
    private static String cachedBuildablesJson = null;
    private static String cachedBlockCategoriesJson = null;

    // ===== BLOCK LIBRARY - Central Source of Truth =====
    // This replaces hardcoded lists in Lua scripts and Unity
    private static final Map<String, List<String>> BLOCK_CATEGORIES = initializeBlockCategories();

    private static Map<String, List<String>> initializeBlockCategories() {
        Map<String, List<String>> categories = new LinkedHashMap<>();

        // === BASIC BUILDING MATERIALS ===
        categories.put("Stone & Bricks", Arrays.asList(
            "minecraft:stone", "minecraft:cobblestone", "minecraft:stone_bricks",
            "minecraft:mossy_stone_bricks", "minecraft:cracked_stone_bricks", "minecraft:chiseled_stone_bricks",
            "minecraft:smooth_stone", "minecraft:andesite", "minecraft:polished_andesite",
            "minecraft:diorite", "minecraft:polished_diorite", "minecraft:granite", "minecraft:polished_granite",
            "minecraft:deepslate", "minecraft:deepslate_bricks", "minecraft:cracked_deepslate_bricks",
            "minecraft:deepslate_tiles", "minecraft:cracked_deepslate_tiles"
        ));

        categories.put("Wood & Planks", Arrays.asList(
            "minecraft:oak_planks", "minecraft:oak_log", "minecraft:stripped_oak_log",
            "minecraft:spruce_planks", "minecraft:spruce_log", "minecraft:stripped_spruce_log",
            "minecraft:birch_planks", "minecraft:birch_log", "minecraft:stripped_birch_log",
            "minecraft:jungle_planks", "minecraft:jungle_log", "minecraft:stripped_jungle_log",
            "minecraft:acacia_planks", "minecraft:acacia_log", "minecraft:stripped_acacia_log",
            "minecraft:dark_oak_planks", "minecraft:dark_oak_log", "minecraft:stripped_dark_oak_log",
            "minecraft:mangrove_planks", "minecraft:mangrove_log", "minecraft:stripped_mangrove_log",
            "minecraft:cherry_planks", "minecraft:cherry_log", "minecraft:stripped_cherry_log",
            "minecraft:crimson_planks", "minecraft:crimson_stem", "minecraft:stripped_crimson_stem",
            "minecraft:warped_planks", "minecraft:warped_stem", "minecraft:stripped_warped_stem"
        ));

        categories.put("Glass & Transparent", Arrays.asList(
            "minecraft:glass", "minecraft:white_stained_glass", "minecraft:light_gray_stained_glass",
            "minecraft:gray_stained_glass", "minecraft:black_stained_glass", "minecraft:brown_stained_glass",
            "minecraft:red_stained_glass", "minecraft:orange_stained_glass", "minecraft:yellow_stained_glass",
            "minecraft:lime_stained_glass", "minecraft:green_stained_glass", "minecraft:cyan_stained_glass",
            "minecraft:light_blue_stained_glass", "minecraft:blue_stained_glass", "minecraft:purple_stained_glass",
            "minecraft:magenta_stained_glass", "minecraft:pink_stained_glass", "minecraft:glass_pane", "minecraft:iron_bars"
        ));

        categories.put("Concrete & Terracotta", Arrays.asList(
            "minecraft:white_concrete", "minecraft:light_gray_concrete", "minecraft:gray_concrete",
            "minecraft:black_concrete", "minecraft:brown_concrete", "minecraft:red_concrete",
            "minecraft:orange_concrete", "minecraft:yellow_concrete", "minecraft:lime_concrete",
            "minecraft:green_concrete", "minecraft:cyan_concrete", "minecraft:light_blue_concrete",
            "minecraft:blue_concrete", "minecraft:purple_concrete", "minecraft:magenta_concrete",
            "minecraft:pink_concrete", "minecraft:white_terracotta", "minecraft:terracotta"
        ));

        categories.put("Wool & Carpet", Arrays.asList(
            "minecraft:white_wool", "minecraft:light_gray_wool", "minecraft:gray_wool",
            "minecraft:black_wool", "minecraft:brown_wool", "minecraft:red_wool",
            "minecraft:orange_wool", "minecraft:yellow_wool", "minecraft:lime_wool",
            "minecraft:green_wool", "minecraft:cyan_wool", "minecraft:light_blue_wool",
            "minecraft:blue_wool", "minecraft:purple_wool", "minecraft:magenta_wool", "minecraft:pink_wool"
        ));

        // === DECORATIVE BLOCKS ===
        categories.put("Slabs", Arrays.asList(
            "minecraft:stone_slab", "minecraft:stone_brick_slab", "minecraft:oak_slab", "minecraft:spruce_slab",
            "minecraft:birch_slab", "minecraft:jungle_slab", "minecraft:acacia_slab", "minecraft:dark_oak_slab",
            "minecraft:smooth_stone_slab", "minecraft:andesite_slab", "minecraft:diorite_slab", "minecraft:granite_slab"
        ));

        categories.put("Stairs", Arrays.asList(
            "minecraft:stone_stairs", "minecraft:stone_brick_stairs", "minecraft:oak_stairs", "minecraft:spruce_stairs",
            "minecraft:birch_stairs", "minecraft:jungle_stairs", "minecraft:acacia_stairs", "minecraft:dark_oak_stairs",
            "minecraft:andesite_stairs", "minecraft:diorite_stairs", "minecraft:granite_stairs"
        ));

        categories.put("Fences & Walls", Arrays.asList(
            "minecraft:oak_fence", "minecraft:spruce_fence", "minecraft:birch_fence", "minecraft:jungle_fence",
            "minecraft:acacia_fence", "minecraft:dark_oak_fence", "minecraft:nether_brick_fence",
            "minecraft:cobblestone_wall", "minecraft:mossy_cobblestone_wall", "minecraft:stone_brick_wall",
            "minecraft:andesite_wall", "minecraft:diorite_wall", "minecraft:granite_wall"
        ));

        categories.put("Doors & Gates", Arrays.asList(
            "minecraft:oak_door", "minecraft:spruce_door", "minecraft:birch_door", "minecraft:jungle_door",
            "minecraft:acacia_door", "minecraft:dark_oak_door", "minecraft:iron_door",
            "minecraft:oak_fence_gate", "minecraft:spruce_fence_gate", "minecraft:birch_fence_gate",
            "minecraft:jungle_fence_gate", "minecraft:acacia_fence_gate", "minecraft:dark_oak_fence_gate",
            "minecraft:oak_trapdoor", "minecraft:spruce_trapdoor", "minecraft:birch_trapdoor", "minecraft:iron_trapdoor"
        ));

        // === FUNCTIONAL BLOCKS ===
        categories.put("Storage & Crafting", Arrays.asList(
            "minecraft:chest", "minecraft:barrel", "minecraft:crafting_table", "minecraft:furnace",
            "minecraft:smoker", "minecraft:blast_furnace", "minecraft:anvil", "minecraft:enchanting_table",
            "minecraft:bookshelf", "minecraft:lectern", "minecraft:ladder", "minecraft:bed"
        ));

        categories.put("Lighting", Arrays.asList(
            "minecraft:torch", "minecraft:soul_torch", "minecraft:lantern", "minecraft:soul_lantern",
            "minecraft:glowstone", "minecraft:sea_lantern", "minecraft:redstone_lamp",
            "minecraft:shroomlight", "minecraft:end_rod"
        ));

        // === CREATE MOD - MECHANICAL COMPONENTS ===
        categories.put("Create: Kinetic Power", Arrays.asList(
            "create:cogwheel", "create:large_cogwheel", "create:shaft", "create:gearbox",
            "create:gearshift", "create:clutch", "create:encased_chain_drive",
            "create:adjustable_chain_gearshift", "create:rotation_speed_controller"
        ));

        categories.put("Create: Generators & Motors", Arrays.asList(
            "create:water_wheel", "create:windmill_bearing", "create:mechanical_bearing",
            "create:steam_engine", "create:motor", "create:creative_motor"
        ));

        categories.put("Create: Logistics", Arrays.asList(
            "create:mechanical_arm", "create:deployer", "create:mechanical_drill", "create:mechanical_saw",
            "create:mechanical_harvester", "create:mechanical_plough", "create:portable_storage_interface",
            "create:andesite_funnel", "create:brass_funnel", "create:andesite_tunnel", "create:brass_tunnel"
        ));

        categories.put("Create: Conveyor Belts", Arrays.asList(
            "create:belt_connector", "create:mechanical_belt", "create:belt_support"
        ));

        categories.put("Create: Processing", Arrays.asList(
            "create:millstone", "create:crushing_wheel", "create:mechanical_press",
            "create:mechanical_mixer", "create:blaze_burner", "create:basin",
            "create:item_drain", "create:spout", "create:encased_fan"
        ));

        categories.put("Create: Storage & Containers", Arrays.asList(
            "create:item_vault", "create:fluid_tank", "create:creative_fluid_tank",
            "create:hose_pulley", "create:chute", "create:smart_chute"
        ));

        // === ATM10 MODS - PIPES & CABLES ===
        categories.put("Pipez: Item Transport", Arrays.asList(
            "pipez:item_pipe", "pipez:gas_pipe", "pipez:fluid_pipe",
            "pipez:energy_pipe", "pipez:universal_pipe"
        ));

        categories.put("Mekanism: Logistics", Arrays.asList(
            "mekanism:logistical_transporter", "mekanism:mechanical_pipe",
            "mekanism:pressurized_tube", "mekanism:universal_cable", "mekanism:thermodynamic_conductor"
        ));

        categories.put("Thermal: Ducts", Arrays.asList(
            "thermal:fluid_duct", "thermal:energy_duct", "thermal:item_duct"
        ));

        return categories;
    }

    // Get all blocks as flat list (performance-optimized with lazy caching)
    private static List<String> getAllBlocks() {
        List<String> allBlocks = new ArrayList<>();
        for (List<String> categoryBlocks : BLOCK_CATEGORIES.values()) {
            allBlocks.addAll(categoryBlocks);
        }
        return allBlocks;
    }

    // Get buildable structures/components metadata
    private static Map<String, Object> getBuildables() {
        Map<String, Object> buildables = new LinkedHashMap<>();

        // Functional buildables
        List<Map<String, String>> functionalBuildables = new ArrayList<>();
        functionalBuildables.add(createBuildable("Chest Storage", "minecraft:chest", "Storage unit for items"));
        functionalBuildables.add(createBuildable("Furnace", "minecraft:furnace", "Smelting and cooking"));
        functionalBuildables.add(createBuildable("Crafting Table", "minecraft:crafting_table", "3x3 crafting grid"));
        functionalBuildables.add(createBuildable("Enchanting Table", "minecraft:enchanting_table", "Enchant items"));
        buildables.put("functional", functionalBuildables);

        // Create Mod buildables
        List<Map<String, String>> createBuildables = new ArrayList<>();
        createBuildables.add(createBuildable("Water Wheel Generator", "create:water_wheel", "Generates rotational power from water"));
        createBuildables.add(createBuildable("Windmill", "create:windmill_bearing", "Generates power from wind"));
        createBuildables.add(createBuildable("Mechanical Press", "create:mechanical_press", "Processes items"));
        createBuildables.add(createBuildable("Mechanical Mixer", "create:mechanical_mixer", "Mixes ingredients"));
        createBuildables.add(createBuildable("Crushing Wheel", "create:crushing_wheel", "Crushes ores and items"));
        createBuildables.add(createBuildable("Item Vault", "create:item_vault", "Large storage container"));
        buildables.put("create_mod", createBuildables);

        // Mekanism buildables
        List<Map<String, String>> mekanismBuildables = new ArrayList<>();
        mekanismBuildables.add(createBuildable("Logistical Transporter", "mekanism:logistical_transporter", "Item transport pipe"));
        mekanismBuildables.add(createBuildable("Mechanical Pipe", "mekanism:mechanical_pipe", "Fluid transport"));
        mekanismBuildables.add(createBuildable("Universal Cable", "mekanism:universal_cable", "Energy transfer"));
        buildables.put("mekanism", mekanismBuildables);

        return buildables;
    }

    private static Map<String, String> createBuildable(String name, String blockId, String description) {
        Map<String, String> buildable = new LinkedHashMap<>();
        buildable.put("name", name);
        buildable.put("blockId", blockId);
        buildable.put("description", description);
        return buildable;
    }

    public WorldInfo() {
        IEventBus bus = net.neoforged.neoforge.common.NeoForge.EVENT_BUS;
        bus.register(this);
        bus.addListener(this::onBlockBreak);
    bus.addListener(this::onBlockPlace);
    }
    // Handle block breaks
public void onBlockBreak(BlockEvent.BreakEvent event) {
    updateChunkForBlockEvent(event);
}

// Handle block placements
public void onBlockPlace(BlockEvent.EntityPlaceEvent event) {
    updateChunkForBlockEvent(event);
}
private void updateChunkForBlockEvent(BlockEvent event) {
    if (event.getLevel() instanceof ServerLevel level) {
        BlockPos pos = event.getPos();
        int chunkX = pos.getX() >> 4;
        int chunkZ = pos.getZ() >> 4;
        updateChunkTimestamp(level.dimension().location(), chunkX, chunkZ);
    }
}

    @SubscribeEvent
    private void onServerStarting(ServerStartingEvent event) {
        if (event.getServer().isDedicatedServer()) {
            server = event.getServer();
            dataDirectory = server.getServerDirectory().toAbsolutePath().resolve("worldinfo_data");
            loadChunkTimestamps();
            startAutoSave();
            startHttpServer();
        }
    }

    @SubscribeEvent
    private void onServerStopping(ServerStoppingEvent event) {
        stopAutoSave();
        saveChunkTimestamps(); // Save one last time on server shutdown
        if (httpServer != null) {
            httpServer.stop(1);
        }
    }
  
    // Update timestamp for a chunk
    private void updateChunkTimestamp(ResourceLocation dimension, int chunkX, int chunkZ) {
        long chunkKey = ((long) chunkX << 32) | (chunkZ & 0xFFFFFFFFL);
        long currentTime = System.currentTimeMillis();
        
        chunkUpdateTimestamps.computeIfAbsent(dimension, k -> new ConcurrentHashMap<>())
                           .put(chunkKey, currentTime);
    }

    // Start periodic autosave
    private void startAutoSave() {
        autoSaveExecutor = Executors.newSingleThreadScheduledExecutor();
        autoSaveExecutor.scheduleAtFixedRate(() -> {
            try {
                if (!chunkUpdateTimestamps.isEmpty()) { // Only save if there's data
                    saveChunkTimestamps();
                }
            } catch (Exception e) {
                System.err.println("Failed to auto-save chunk timestamps: " + e.getMessage());
            }
        }, SAVE_INTERVAL_MINUTES, SAVE_INTERVAL_MINUTES, TimeUnit.MINUTES);
    }

    // Stop periodic autosave
    private void stopAutoSave() {
        if (autoSaveExecutor != null) {
            autoSaveExecutor.shutdown();
            try {
                if (!autoSaveExecutor.awaitTermination(5, TimeUnit.SECONDS)) {
                    autoSaveExecutor.shutdownNow();
                }
            } catch (InterruptedException e) {
                autoSaveExecutor.shutdownNow();
                Thread.currentThread().interrupt();
            }
        }
    }

    // Load chunk timestamps from disk
    private void loadChunkTimestamps() {
        Path filePath = dataDirectory.resolve(TIMESTAMPS_FILE);
        if (!Files.exists(filePath)) {
            return;
        }

        try (Reader reader = Files.newBufferedReader(filePath)) {
            Type type = new TypeToken<Map<String, Map<Long, Long>>>() {}.getType();
            Map<String, Map<Long, Long>> loadedData = GSON.fromJson(reader, type);

            synchronized (saveLock) {
                chunkUpdateTimestamps.clear();
                loadedData.forEach((dimensionStr, chunkMap) -> {
                    ResourceLocation dimension = ResourceLocation.parse(dimensionStr);
                    chunkUpdateTimestamps.put(dimension, new ConcurrentHashMap<>(chunkMap));
                });
            }
            System.out.println("Loaded chunk timestamps for " + loadedData.size() + " dimensions");
        } catch (Exception e) {
            System.err.println("Failed to load chunk timestamps: " + e.getMessage());
        }
    }

    // Save chunk timestamps to disk
    private void saveChunkTimestamps() {
        if (dataDirectory == null) return;

        try {
            if (!Files.exists(dataDirectory)) {
                Files.createDirectories(dataDirectory);
            }

            Path tempFile = dataDirectory.resolve(TIMESTAMPS_FILE + ".tmp");
            Path finalFile = dataDirectory.resolve(TIMESTAMPS_FILE);

            // Convert ResourceLocation keys to strings for serialization
            Map<String, Map<Long, Long>> saveData = new HashMap<>();
            synchronized (saveLock) {
                chunkUpdateTimestamps.forEach((dimension, chunkMap) -> {
                    saveData.put(dimension.toString(), new HashMap<>(chunkMap));
                });
            }

            // Write to temp file first
            try (Writer writer = Files.newBufferedWriter(tempFile)) {
                GSON.toJson(saveData, writer);
            }

            // Atomically replace the old file
            Files.move(tempFile, finalFile, java.nio.file.StandardCopyOption.REPLACE_EXISTING);
            
            System.out.println("Saved chunk timestamps for " + saveData.size() + " dimensions at " + finalFile.toAbsolutePath());
        } catch (Exception e) {
            System.err.println("Failed to save chunk timestamps: " + e.getMessage());
        }
    }


    private void startHttpServer() {
        try {
            int threads = Math.max(2, Runtime.getRuntime().availableProcessors() / 2);
            httpServer = HttpServer.create(new InetSocketAddress(4567), 0);
            httpServer.createContext("/chunkdata", new ChunkDataHandler());
            httpServer.createContext("/dimensions", new DimensionsHandler());
            httpServer.createContext("/chunkupdate", new ChunkUpdateHandler());
            httpServer.createContext("/triggerupdate", new TriggerUpdateHandler());

            // NEW: Block library endpoints
            httpServer.createContext("/blocks", new BlocksHandler());
            httpServer.createContext("/blocks/categories", new BlockCategoriesHandler());
            httpServer.createContext("/buildables", new BuildablesHandler());

            httpServer.setExecutor(Executors.newFixedThreadPool(threads));
            httpServer.start();
            System.out.println("WorldInfo HTTP Server started on port 4567");
            System.out.println("Available endpoints: /chunkdata, /dimensions, /chunkupdate, /triggerupdate, /blocks, /blocks/categories, /buildables");
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    // New handler for chunk update checks
    private class ChunkUpdateHandler implements HttpHandler {
    @Override
    public void handle(HttpExchange exchange) throws IOException {
        if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
            sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
            return;
        }

        URI requestURI = exchange.getRequestURI();
        Map<String, String> queryParams = queryToMap(requestURI.getQuery());

        try {
            int chunkX = Integer.parseInt(queryParams.getOrDefault("chunkX", "0"));
            int chunkZ = Integer.parseInt(queryParams.getOrDefault("chunkZ", "0"));
            String levelParam = queryParams.get("level");
            String sinceParam = queryParams.get("since");
            boolean reset = Boolean.parseBoolean(queryParams.getOrDefault("reset", "false"));

            ServerLevel level = getLevelFromParam(levelParam);
            if (level == null) {
                sendResponse(exchange, 400, "{\"error\":\"Invalid dimension\"}");
                return;
            }

            ResourceLocation dimension = level.dimension().location();
            long chunkKey = ((long) chunkX << 32) | (chunkZ & 0xFFFFFFFFL);
            Long lastUpdate = chunkUpdateTimestamps.getOrDefault(dimension, Collections.emptyMap())
                                                .get(chunkKey);

            JsonObject response = new JsonObject();
            response.addProperty("chunkX", chunkX);
            response.addProperty("chunkZ", chunkZ);
            response.addProperty("dimension", dimension.toString());
            
            if (lastUpdate != null) {
                response.addProperty("lastUpdate", lastUpdate);
                response.addProperty("hasUpdates", true);
                
                if (sinceParam != null) {
                    try {
                        long sinceTime = Long.parseLong(sinceParam);
                        response.addProperty("isUpdated", lastUpdate > sinceTime);
                    } catch (NumberFormatException e) {
                        response.addProperty("isUpdated", true);
                    }
                }

                // Reset the update status if requested
                if (reset) {
                    resetChunkUpdateStatus(dimension, chunkX, chunkZ);
                    response.addProperty("reset", true);
                }
            } else {
                response.addProperty("lastUpdate", 0);
                response.addProperty("hasUpdates", false);
                if (sinceParam != null) {
                    response.addProperty("isUpdated", false);
                }
            }

            sendResponse(exchange, 200, response.toString());
        } catch (NumberFormatException e) {
            sendResponse(exchange, 400, "{\"error\":\"Invalid number format in parameters.\"}");
        }
    }
}

    private class ChunkDataHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
                sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
                return;
            }

            URI requestURI = exchange.getRequestURI();
            Map<String, String> queryParams = queryToMap(requestURI.getQuery());

            try {
                int chunkX = Integer.parseInt(queryParams.getOrDefault("chunkX", "0"));
                int chunkZ = Integer.parseInt(queryParams.getOrDefault("chunkZ", "0"));
                int radius = Integer.parseInt(queryParams.getOrDefault("radius", "0"));
                String levelParam = queryParams.get("level");
                boolean reset = Boolean.parseBoolean(queryParams.getOrDefault("reset", "false"));

                ServerLevel level = getLevelFromParam(levelParam);
                if (level == null) {
                    sendResponse(exchange, 400, "{\"error\":\"Invalid dimension\"}");
                    return;
                }
                
                ResourceLocation dimension = level.dimension().location();
                JsonArray chunksJson = new JsonArray();

                for (int dx = -radius; dx <= radius; dx++) {
                    for (int dz = -radius; dz <= radius; dz++) {
                        int currentChunkX = chunkX + dx;
                        int currentChunkZ = chunkZ + dz;
                        LevelChunk chunk = level.getChunk(currentChunkX, currentChunkZ);

                        JsonObject chunkData = processChunk(chunk, level, currentChunkX, currentChunkZ);
                         if (reset) {
                            resetChunkUpdateStatus(dimension, chunkX + dx, chunkZ + dz);
                         }
                        chunksJson.add(chunkData);
                    }
                }

                JsonObject response = new JsonObject();
                response.addProperty("level", level.dimension().location().toString());
                response.add("chunks", chunksJson);

                sendResponse(exchange, 200, response.toString());
            } catch (NumberFormatException e) {
                sendResponse(exchange, 400, "{\"error\":\"Invalid number format in parameters.\"}");
            }
        }

        private JsonObject processChunk(LevelChunk chunk, ServerLevel level, int chunkX, int chunkZ) {
            Map<String, Integer> paletteMap = new HashMap<>();
            List<String> paletteList = new ArrayList<>();
            Map<Integer, List<Integer>> blocksById = new HashMap<>();
            int minBuildHeight = level.getMinBuildHeight();

            for (int bx = 0; bx < 16; bx++) {
                for (int by = level.getMinBuildHeight(); by < level.getMaxBuildHeight(); by++) {
                    for (int bz = 0; bz < 16; bz++) {
                        BlockPos pos = new BlockPos(chunkX * 16 + bx, by, chunkZ * 16 + bz);
                        BlockState state = chunk.getBlockState(pos);

                        if (!state.isAir()) {
                            String blockName = state.getBlock().builtInRegistryHolder().key().location().toString();
                            int id = paletteMap.computeIfAbsent(blockName, name -> {
                                paletteList.add(name);
                                return paletteList.size() - 1;
                            });

                            int localY = by - level.getMinBuildHeight();
                            int posId = (bx << 12) | (localY << 4) | bz;
                            blocksById.computeIfAbsent(id, k -> new ArrayList<>()).add(posId);
                        }
                    }
                }
            }

            JsonObject chunkData = new JsonObject();
            chunkData.addProperty("chunkX", chunkX);
            chunkData.addProperty("chunkZ", chunkZ);
            chunkData.addProperty("minBuildHeight", minBuildHeight);
            chunkData.addProperty("level", level.dimension().location().toString());

            JsonArray paletteJson = new JsonArray();
            paletteList.forEach(paletteJson::add);
            chunkData.add("palette", paletteJson);

            JsonObject blocksJson = new JsonObject();
            for (Map.Entry<Integer, List<Integer>> entry : blocksById.entrySet()) {
                JsonArray posArray = new JsonArray();
                entry.getValue().forEach(posArray::add);
                blocksJson.add(entry.getKey().toString(), posArray);
            }
            chunkData.add("blocks", blocksJson);

            return chunkData;
        }
    }
    private void resetChunkUpdateStatus(ResourceLocation dimension, int chunkX, int chunkZ) {
    long chunkKey = ((long) chunkX << 32) | (chunkZ & 0xFFFFFFFFL);
    Map<Long, Long> dimensionChunks = chunkUpdateTimestamps.get(dimension);
    if (dimensionChunks != null) {
        dimensionChunks.remove(chunkKey);
        // Remove dimension entry if empty
        if (dimensionChunks.isEmpty()) {
            chunkUpdateTimestamps.remove(dimension);
        }
    }
}

    private class TriggerUpdateHandler implements HttpHandler {
    @Override
    public void handle(HttpExchange exchange) throws IOException {
        if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
            sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
            return;
        }

        URI requestURI = exchange.getRequestURI();
        Map<String, String> queryParams = queryToMap(requestURI.getQuery());

        try {
            int chunkX = Integer.parseInt(queryParams.getOrDefault("chunkX", "0"));
            int chunkZ = Integer.parseInt(queryParams.getOrDefault("chunkZ", "0"));
            String levelParam = queryParams.get("level");
            String action = queryParams.getOrDefault("action", "break"); // "break" or "place"

            ServerLevel level = getLevelFromParam(levelParam);
            if (level == null) {
                sendResponse(exchange, 400, "{\"error\":\"Invalid dimension\"}");
                return;
            }

            // Calculate a position within the chunk (center at y=64)
            BlockPos pos = new BlockPos(
                chunkX * 16 + 8,  // Center of chunk X
                5,               // Middle of build height
                chunkZ * 16 + 8   // Center of chunk Z
            );

            // Get the block state at this position
            BlockState state = level.getBlockState(pos);

            // Simulate either a break or place event
            if ("place".equalsIgnoreCase(action)) {
                        // Create a BlockSnapshot for the placement event
                BlockSnapshot blockSnapshot = BlockSnapshot.create(
                    level.dimension(),
                    level,
                    pos                    
                );
                BlockEvent.EntityPlaceEvent event = new BlockEvent.EntityPlaceEvent(
                    blockSnapshot,
                    level.getBlockState(pos.below()), // placed against block below
                    null // no specific entity
                );
                onBlockPlace(event); // Trigger the place event handler
                
            } else {
                BlockEvent.BreakEvent event = new BlockEvent.BreakEvent(
                    level, pos, state, null
                );
                onBlockBreak(event); // Trigger the break event handler                
            }

            // Get the updated timestamp
            ResourceLocation dimension = level.dimension().location();
            long chunkKey = ((long) chunkX << 32) | (chunkZ & 0xFFFFFFFFL);
            Long lastUpdate = chunkUpdateTimestamps.getOrDefault(dimension, Collections.emptyMap())
                                                .get(chunkKey);

            JsonObject response = new JsonObject();
            response.addProperty("chunkX", chunkX);
            response.addProperty("chunkZ", chunkZ);
            response.addProperty("dimension", dimension.toString());
            response.addProperty("action", action);
            response.addProperty("block", state.getBlock().getDescriptionId());
            response.addProperty("lastUpdate", lastUpdate != null ? lastUpdate : 0);
            response.addProperty("success", true);

            sendResponse(exchange, 200, response.toString());
        } catch (NumberFormatException e) {
            sendResponse(exchange, 400, "{\"error\":\"Invalid number format in parameters.\"}");
        }
    }
}


    private class DimensionsHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
                sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
                return;
            }

            JsonArray dims = new JsonArray();
            for (ServerLevel level : server.getAllLevels()) {
                dims.add(level.dimension().location().toString());
            }
            sendResponse(exchange, 200, dims.toString());
        }
    }

    private ServerLevel getLevelFromParam(String levelParam) {
        if (levelParam == null || levelParam.isEmpty()) {
            return server.getLevel(Level.OVERWORLD);
        }
        ResourceKey<Level> dimensionKey;
        if (levelParam.contains(":")) {
            dimensionKey = ResourceKey.create(Registries.DIMENSION, ResourceLocation.parse(levelParam));
        } else {
            dimensionKey = switch (levelParam.toLowerCase()) {
                case "overworld" -> Level.OVERWORLD;
                case "the_nether", "nether" -> Level.NETHER;
                case "the_end", "end" -> Level.END;
                default -> ResourceKey.create(Registries.DIMENSION, ResourceLocation.fromNamespaceAndPath("minecraft", levelParam));
            };
        }
        return server.getLevel(dimensionKey);
    }

    private static Map<String, String> queryToMap(String query) {
        return query == null ? Map.of() : Arrays.stream(query.split("&"))
                .map(s -> s.split("=", 2))
                .filter(pair -> pair.length == 2)
                .collect(Collectors.toMap(
                        pair -> decodeURIComponent(pair[0]),
                        pair -> decodeURIComponent(pair[1])
                ));
    }

    private static String decodeURIComponent(String s) {
        try {
            return java.net.URLDecoder.decode(s, "UTF-8");
        } catch (Exception e) {
            return s;
        }
    }

    private void sendResponse(HttpExchange exchange, int statusCode, String responseText) throws IOException {
        exchange.getResponseHeaders().add("Content-Type", "application/json; charset=utf-8");
        byte[] bytes = responseText.getBytes("UTF-8");
        exchange.sendResponseHeaders(statusCode, bytes.length);
        try (OutputStream os = exchange.getResponseBody()) {
            os.write(bytes);
        }
    }

    // ===== NEW HANDLERS FOR BLOCK LIBRARY =====

    /**
     * Handler for /blocks endpoint
     * Returns a flat list of all available blocks
     * Performance: Uses cached JSON response
     */
    private class BlocksHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
                sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
                return;
            }

            // Use cached response if available (performance optimization)
            if (cachedBlocksJson == null) {
                synchronized (WorldInfo.class) {
                    if (cachedBlocksJson == null) {
                        JsonObject response = new JsonObject();
                        JsonArray blocksArray = new JsonArray();

                        List<String> allBlocks = getAllBlocks();
                        for (String block : allBlocks) {
                            blocksArray.add(block);
                        }

                        response.addProperty("count", allBlocks.size());
                        response.add("blocks", blocksArray);
                        response.addProperty("source", "WorldInfo Mod");
                        response.addProperty("version", "1.0");

                        cachedBlocksJson = response.toString();
                        System.out.println("Generated cached blocks JSON (" + allBlocks.size() + " blocks)");
                    }
                }
            }

            sendResponse(exchange, 200, cachedBlocksJson);
        }
    }

    /**
     * Handler for /blocks/categories endpoint
     * Returns blocks organized by category
     * Performance: Uses cached JSON response
     */
    private class BlockCategoriesHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
                sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
                return;
            }

            // Use cached response if available (performance optimization)
            if (cachedBlockCategoriesJson == null) {
                synchronized (WorldInfo.class) {
                    if (cachedBlockCategoriesJson == null) {
                        JsonObject response = new JsonObject();
                        JsonObject categoriesObj = new JsonObject();

                        int totalBlocks = 0;
                        for (Map.Entry<String, List<String>> entry : BLOCK_CATEGORIES.entrySet()) {
                            JsonArray blocksArray = new JsonArray();
                            for (String block : entry.getValue()) {
                                blocksArray.add(block);
                            }
                            categoriesObj.add(entry.getKey(), blocksArray);
                            totalBlocks += entry.getValue().size();
                        }

                        response.add("categories", categoriesObj);
                        response.addProperty("totalCategories", BLOCK_CATEGORIES.size());
                        response.addProperty("totalBlocks", totalBlocks);
                        response.addProperty("source", "WorldInfo Mod");
                        response.addProperty("version", "1.0");

                        cachedBlockCategoriesJson = response.toString();
                        System.out.println("Generated cached block categories JSON (" + BLOCK_CATEGORIES.size() + " categories, " + totalBlocks + " blocks)");
                    }
                }
            }

            sendResponse(exchange, 200, cachedBlockCategoriesJson);
        }
    }

    /**
     * Handler for /buildables endpoint
     * Returns buildable structures and components with metadata
     * Performance: Uses cached JSON response
     */
    private class BuildablesHandler implements HttpHandler {
        @Override
        public void handle(HttpExchange exchange) throws IOException {
            if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
                sendResponse(exchange, 405, "{\"error\":\"Method Not Allowed\"}");
                return;
            }

            // Use cached response if available (performance optimization)
            if (cachedBuildablesJson == null) {
                synchronized (WorldInfo.class) {
                    if (cachedBuildablesJson == null) {
                        Map<String, Object> buildables = getBuildables();

                        JsonObject response = new JsonObject();

                        // Convert buildables map to JSON
                        for (Map.Entry<String, Object> entry : buildables.entrySet()) {
                            @SuppressWarnings("unchecked")
                            List<Map<String, String>> items = (List<Map<String, String>>) entry.getValue();

                            JsonArray itemsArray = new JsonArray();
                            for (Map<String, String> item : items) {
                                JsonObject itemObj = new JsonObject();
                                itemObj.addProperty("name", item.get("name"));
                                itemObj.addProperty("blockId", item.get("blockId"));
                                itemObj.addProperty("description", item.get("description"));
                                itemsArray.add(itemObj);
                            }
                            response.add(entry.getKey(), itemsArray);
                        }

                        response.addProperty("source", "WorldInfo Mod");
                        response.addProperty("version", "1.0");

                        cachedBuildablesJson = response.toString();
                        System.out.println("Generated cached buildables JSON");
                    }
                }
            }

            sendResponse(exchange, 200, cachedBuildablesJson);
        }
    }
}