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
            httpServer.setExecutor(Executors.newFixedThreadPool(threads));
            httpServer.start();
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
}