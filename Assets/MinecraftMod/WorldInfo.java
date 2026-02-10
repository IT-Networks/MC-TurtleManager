package com.worldinfo.worldinfomod;

import com.google.gson.*;
import com.google.gson.reflect.TypeToken;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Registry;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.ResourceKey;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.Block;
import net.minecraft.world.level.block.Blocks;
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

    // ===== DYNAMIC BLOCK LIBRARY - Central Source of Truth =====
    // Blocks are now read dynamically from Minecraft's Block Registry
    // This automatically includes all blocks from Minecraft, Create, and installed mods

    // Cached block data (invalidated on server restart)
    private static Map<String, List<String>> cachedBlockCategories = null;
    private static List<String> cachedAllBlocks = null;

    /**
     * Dynamically loads all blocks from Minecraft's Block Registry
     * Categorizes blocks based on their namespace and naming patterns
     * @return Map of category names to lists of block IDs
     */
    private static Map<String, List<String>> getBlockCategories() {
        if (cachedBlockCategories != null) {
            return cachedBlockCategories;
        }

        Map<String, List<String>> categories = new LinkedHashMap<>();

        // Lists for dynamic categorization
        List<String> stoneBlocks = new ArrayList<>();
        List<String> woodBlocks = new ArrayList<>();
        List<String> glassBlocks = new ArrayList<>();
        List<String> concreteBlocks = new ArrayList<>();
        List<String> woolBlocks = new ArrayList<>();
        List<String> slabs = new ArrayList<>();
        List<String> stairs = new ArrayList<>();
        List<String> fences = new ArrayList<>();
        List<String> doors = new ArrayList<>();
        List<String> storage = new ArrayList<>();
        List<String> lighting = new ArrayList<>();
        List<String> createKinetic = new ArrayList<>();
        List<String> createGenerators = new ArrayList<>();
        List<String> createLogistics = new ArrayList<>();
        List<String> createProcessing = new ArrayList<>();
        List<String> createStorage = new ArrayList<>();
        List<String> pipez = new ArrayList<>();
        List<String> mekanism = new ArrayList<>();
        List<String> thermal = new ArrayList<>();
        List<String> decorative = new ArrayList<>();
        List<String> other = new ArrayList<>();

        // Iterate through all registered blocks
        for (Block block : BuiltInRegistries.BLOCK) {
            ResourceLocation blockId = BuiltInRegistries.BLOCK.getKey(block);
            if (blockId == null) continue;

            String fullId = blockId.toString();
            String namespace = blockId.getNamespace();
            String path = blockId.getPath();

            // Skip air and technical blocks
            if (block == Blocks.AIR || block == Blocks.CAVE_AIR || block == Blocks.VOID_AIR) {
                continue;
            }
            if (path.contains("potted_") || path.contains("wall_") && path.contains("_banner")) {
                continue;
            }

            // Categorize by namespace and name patterns
            if (namespace.equals("minecraft")) {
                // Stone & Bricks
                if (path.contains("stone") && !path.contains("_slab") && !path.contains("_stairs")
                    || path.contains("brick") && !path.contains("_slab") && !path.contains("_stairs")
                    || path.contains("andesite") || path.contains("diorite") || path.contains("granite")
                    || path.contains("deepslate") || path.equals("cobblestone")) {
                    stoneBlocks.add(fullId);
                }
                // Wood & Planks
                else if (path.contains("planks") || path.contains("_log") || path.contains("_stem")
                         || path.contains("stripped_")) {
                    woodBlocks.add(fullId);
                }
                // Glass
                else if (path.contains("glass") || path.contains("_pane")) {
                    glassBlocks.add(fullId);
                }
                // Concrete & Terracotta
                else if (path.contains("concrete") || path.contains("terracotta")) {
                    concreteBlocks.add(fullId);
                }
                // Wool
                else if (path.contains("wool") || path.contains("carpet")) {
                    woolBlocks.add(fullId);
                }
                // Slabs
                else if (path.contains("_slab")) {
                    slabs.add(fullId);
                }
                // Stairs
                else if (path.contains("_stairs")) {
                    stairs.add(fullId);
                }
                // Fences & Walls
                else if (path.contains("fence") || path.contains("_wall") && !path.contains("banner")) {
                    fences.add(fullId);
                }
                // Doors & Gates
                else if (path.contains("door") || path.contains("gate") || path.contains("trapdoor")) {
                    doors.add(fullId);
                }
                // Storage & Crafting
                else if (path.contains("chest") || path.contains("barrel") || path.contains("crafting")
                         || path.contains("furnace") || path.contains("anvil") || path.contains("enchanting")
                         || path.contains("bookshelf") || path.contains("lectern")) {
                    storage.add(fullId);
                }
                // Lighting
                else if (path.contains("torch") || path.contains("lantern") || path.contains("glowstone")
                         || path.contains("sea_lantern") || path.contains("shroomlight") || path.contains("end_rod")
                         || path.contains("redstone_lamp")) {
                    lighting.add(fullId);
                }
                // Decorative
                else if (path.contains("_banner") || path.contains("carpet") || path.contains("bed")) {
                    decorative.add(fullId);
                }
                else {
                    other.add(fullId);
                }
            }
            // Create Mod
            else if (namespace.equals("create")) {
                if (path.contains("cogwheel") || path.contains("shaft") || path.contains("gearbox")
                    || path.contains("gearshift") || path.contains("clutch") || path.contains("chain_drive")
                    || path.contains("speed_controller")) {
                    createKinetic.add(fullId);
                }
                else if (path.contains("water_wheel") || path.contains("windmill") || path.contains("bearing")
                         || path.contains("motor") || path.contains("engine")) {
                    createGenerators.add(fullId);
                }
                else if (path.contains("arm") || path.contains("deployer") || path.contains("drill")
                         || path.contains("saw") || path.contains("harvester") || path.contains("plough")
                         || path.contains("funnel") || path.contains("tunnel")) {
                    createLogistics.add(fullId);
                }
                else if (path.contains("millstone") || path.contains("crushing") || path.contains("press")
                         || path.contains("mixer") || path.contains("blaze_burner") || path.contains("basin")
                         || path.contains("drain") || path.contains("spout") || path.contains("fan")) {
                    createProcessing.add(fullId);
                }
                else if (path.contains("vault") || path.contains("tank") || path.contains("chute")
                         || path.contains("pulley")) {
                    createStorage.add(fullId);
                }
                else {
                    other.add(fullId);
                }
            }
            // Pipez Mod
            else if (namespace.equals("pipez")) {
                pipez.add(fullId);
            }
            // Mekanism Mod
            else if (namespace.equals("mekanism")) {
                mekanism.add(fullId);
            }
            // Thermal Mod
            else if (namespace.equals("thermal")) {
                thermal.add(fullId);
            }
            // Other mods
            else {
                other.add(fullId);
            }
        }

        // Only add non-empty categories
        if (!stoneBlocks.isEmpty()) categories.put("Stone & Bricks", stoneBlocks);
        if (!woodBlocks.isEmpty()) categories.put("Wood & Planks", woodBlocks);
        if (!glassBlocks.isEmpty()) categories.put("Glass & Transparent", glassBlocks);
        if (!concreteBlocks.isEmpty()) categories.put("Concrete & Terracotta", concreteBlocks);
        if (!woolBlocks.isEmpty()) categories.put("Wool & Carpet", woolBlocks);
        if (!slabs.isEmpty()) categories.put("Slabs", slabs);
        if (!stairs.isEmpty()) categories.put("Stairs", stairs);
        if (!fences.isEmpty()) categories.put("Fences & Walls", fences);
        if (!doors.isEmpty()) categories.put("Doors & Gates", doors);
        if (!storage.isEmpty()) categories.put("Storage & Crafting", storage);
        if (!lighting.isEmpty()) categories.put("Lighting", lighting);
        if (!createKinetic.isEmpty()) categories.put("Create: Kinetic Power", createKinetic);
        if (!createGenerators.isEmpty()) categories.put("Create: Generators & Motors", createGenerators);
        if (!createLogistics.isEmpty()) categories.put("Create: Logistics", createLogistics);
        if (!createProcessing.isEmpty()) categories.put("Create: Processing", createProcessing);
        if (!createStorage.isEmpty()) categories.put("Create: Storage & Containers", createStorage);
        if (!pipez.isEmpty()) categories.put("Pipez: Transport", pipez);
        if (!mekanism.isEmpty()) categories.put("Mekanism: Machines & Pipes", mekanism);
        if (!thermal.isEmpty()) categories.put("Thermal: Ducts & Machines", thermal);
        if (!decorative.isEmpty()) categories.put("Decorative", decorative);
        if (!other.isEmpty()) categories.put("Other", other);

        // Cache the result
        cachedBlockCategories = categories;

        // Log statistics
        int totalBlocks = categories.values().stream().mapToInt(List::size).sum();
        System.out.println("Loaded " + totalBlocks + " blocks from registry into " + categories.size() + " categories");

        return categories;
    }

    /**
     * Get all blocks as flat list (performance-optimized with lazy caching)
     * @return List of all block IDs from the registry
     */
    private static List<String> getAllBlocks() {
        if (cachedAllBlocks != null) {
            return cachedAllBlocks;
        }

        List<String> allBlocks = new ArrayList<>();
        Map<String, List<String>> categories = getBlockCategories();
        for (List<String> categoryBlocks : categories.values()) {
            allBlocks.addAll(categoryBlocks);
        }

        // Cache the result
        cachedAllBlocks = allBlocks;
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

                        Map<String, List<String>> blockCategories = getBlockCategories();
                        int totalBlocks = 0;
                        for (Map.Entry<String, List<String>> entry : blockCategories.entrySet()) {
                            JsonArray blocksArray = new JsonArray();
                            for (String block : entry.getValue()) {
                                blocksArray.add(block);
                            }
                            categoriesObj.add(entry.getKey(), blocksArray);
                            totalBlocks += entry.getValue().size();
                        }

                        response.add("categories", categoriesObj);
                        response.addProperty("totalCategories", blockCategories.size());
                        response.addProperty("totalBlocks", totalBlocks);
                        response.addProperty("source", "WorldInfo Mod - Dynamic Registry");
                        response.addProperty("version", "2.0");

                        cachedBlockCategoriesJson = response.toString();
                        System.out.println("Generated cached block categories JSON (" + blockCategories.size() + " categories, " + totalBlocks + " blocks)");
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