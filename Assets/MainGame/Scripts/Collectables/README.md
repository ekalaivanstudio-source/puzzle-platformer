# Collectables System

Persistent pickups that survive across sessions: **Robot Parts** (single game‑wide
total, e.g. `0/56`) and **Memory Shards** (tiered — each level range needs N shards to
unlock a story). Separate from the Key/Door system.

## Scripts

| File | Role |
|------|------|
| `CollectableType.cs` | `RobotPart` / `MemoryShard` enum. |
| `CollectableConstants.cs` | **Hand‑edited dials**: Robot Parts grand total + Memory‑Shard story tiers (level range → required count → story id). Edit here to change/extend tiers. |
| `CollectableDatabase.cs` | ScriptableObject: per‑level placement counts. One shared asset. |
| `CollectableSaveData.cs` / `CollectableSaveSystem.cs` | JSON save (`persistentDataPath/collectables.json`). Records every picked‑up id so it never respawns; provides counts; `ResetAll()`. |
| `CollectableLevelManager.cs` | Per‑scene singleton. Holds the database, resolves the current level, is the single access point for collect + queries. |
| `Collectable.cs` | Component on the pickup prefab. Trigger‑collect, permanent, self‑hides if already collected. |
| `CollectableHUD.cs` | Two TMP labels: Robot Parts `total/grandTotal`, Memory Shards `tierCollected/tierTarget`. |
| `Editor/CollectableToolsWindow.cs` | `Tools ▸ Collectables ▸ Collectable Tools`: reset data, create prefabs, create DB asset, assign missing ids. |
| `Editor/CollectableUISetupWindow.cs` | `Tools ▸ Collectables ▸ Setup Collectable UI`: build the self‑contained HUD prefab and inject HUD + manager into all level scenes. |
| `Editor/CollectableDatabaseEditor.cs` | Summary + tier validation in the database inspector. |

## One‑time setup

1. **Create the database**: `Tools ▸ Collectables ▸ Collectable Tools ▸ Create Collectable Database Asset`
   (or `Assets ▸ Create ▸ Collectables ▸ Collectable Database`). Fill in a row per
   level with how many Robot Parts / Memory Shards it contains.
2. **Create the prefabs**: same window → `Create Robot Part Prefab` / `Create Memory Shard Prefab`.
   Assign a sprite to each. Leave the prefab's `Unique Id` **empty** — every dropped
   instance auto‑assigns its own id.

## HUD + manager setup (automated)

Level scenes have no shared Canvas, so the HUD is a **self‑contained prefab** carrying
its own Screen‑Space‑Overlay canvas. Use `Tools ▸ Collectables ▸ Setup Collectable UI`:

1. (Optional) assign the Robot Part / Memory Shard **icons**, then
   **Build / Update HUD Prefab** → creates `Prefabs/Collectables/CollectableHUD.prefab`.
2. Assign the **Database**, keep *Ensure Level Manager* on, then
   **Setup In All Build Scenes** (or *Setup Current Scene*). This:
   - adds the HUD prefab to each scene (if missing),
   - adds a `CollectableLevelManager` under `Managers` (if missing) and assigns the DB,
   - saves each scene.

Re‑run any time — it's idempotent (skips scenes already set up). *Only 'Level\*' Scenes*
filters out Home/menu.

## Per‑level content

1. Drop the Robot Part / Memory Shard prefabs into the level. Each instance gets a
   unique id automatically; if any is blank use *Collectable Tools ▸ Assign Missing Ids*.
2. Make sure the pickup's collider is a **trigger** and the player is tagged `Player`.
3. The `CollectableLevelManager`'s *Level Number Override* stays `0` to use the scene
   build index (Home = 0, Level1 = 1, …); set it explicitly for non‑standard scenes.

## Tuning

- **Change how many shards a story needs / add a new tier** → edit
  `CollectableConstants.MemoryShardTiers` (ranges must not overlap).
- **Change the Robot Parts HUD total** → `CollectableConstants.RobotPartsGrandTotal`
  (the HUD prefers the database's placed sum when non‑zero; keep them in sync — the
  database inspector warns on mismatch).

## Reset

`Tools ▸ Collectables ▸ Collectable Tools ▸ Reset Collectable Data` deletes the save
file and clears the in‑memory cache.

## Store (future)

`CollectableLevelManager.IsStoryUnlockedForLevel(level)` /
`IsCurrentStoryUnlocked` report tier completion — hook the not‑yet‑built store screen
into these.
