# Collectables & Level Config System

Persistent pickups that survive across sessions: **Robot Parts** (single game‑wide
total, e.g. `0/56`) and **Memory Shards** (tiered — each level range needs N shards to
unlock a story). Separate from the Key/Door system.

All **per‑level data** — collectable counts, camera dead‑zone, sequence settings, and
anything added later — lives in one **`LevelConfig`** asset per level, assigned to that
scene's **`LevelContext`**. Components read their values from there instead of exposing
their own Inspector fields.

## Scripts

| File | Role |
|------|------|
| `Level/LevelConfig.cs` | **ScriptableObject, one per level.** Collectable counts + camera dead‑zone + sequence settings (extend as needed). |
| `Level/LevelContext.cs` | Per‑scene singleton (runs first). Holds the level's `LevelConfig`; everything reads `LevelContext.Instance.Config`. Lives on the same `LevelManager` object as `CollectableLevelManager` (which `[RequireComponent]`s it). |
| `CollectableType.cs` | `RobotPart` / `MemoryShard` enum. |
| `CollectableConstants.cs` | **Hand‑edited dials**: Robot Parts grand total + Memory‑Shard story tiers (level range → required count → story id). |
| `CollectableSaveData.cs` / `CollectableSaveSystem.cs` | JSON save (`persistentDataPath/collectables.json`). Records every picked‑up id so it never respawns; provides counts; `ResetAll()`. |
| `CollectableLevelManager.cs` | Per‑scene singleton. Single access point for collect + queries; reads level number from `LevelContext`. |
| `Collectable.cs` | Component on the pickup prefab. Trigger‑collect, permanent, self‑hides if already collected. |
| `CollectableHUD.cs` | Two TMP labels: Robot Parts `total/grandTotal`, Memory Shards `tierCollected/tierTarget`. |
| `Editor/CollectableToolsWindow.cs` | `Tools ▸ Collectables ▸ Collectable Tools`: reset data, create prefabs, create a `LevelConfig`, assign missing ids. |
| `Editor/CollectableUISetupWindow.cs` | `Tools ▸ Collectables ▸ Setup Collectable UI`: build the HUD prefab and inject HUD + managers + per‑level `LevelConfig` into scenes. |

## Components that now read from `LevelConfig`

| Component | Fields moved into `LevelConfig` |
|-----------|--------------------------------|
| `CameraFollowDeadZone` | dead zone, offset, smooth time, bounds (min/max X/Y), follow X/Y. `Target`/`Camera` still auto‑resolve at runtime. |
| `SequenceManager` | Max Sequence Length, Require Full Sequence. (A level's correct‑sequence still overrides max length at runtime via `PlayerInputUIHelper`.) |
| `CollectableLevelManager` | `robotPartCount` / `memoryShardCount` (authoring info). |

These fields are now **hidden in the Inspector** (kept serialized only as a fallback);
the asset is the source of truth.

## One‑time setup

1. **Prefabs**: `Tools ▸ Collectables ▸ Collectable Tools` → `Create Robot Part Prefab`
   / `Create Memory Shard Prefab`. Assign a sprite to each. Leave the prefab's
   `Unique Id` **empty** — every dropped instance auto‑assigns its own id.
2. **HUD**: `Tools ▸ Collectables ▸ Setup Collectable UI` → (optional) assign icons →
   `Build / Update HUD Prefab`.

## Per‑level setup (automated)

`Tools ▸ Collectables ▸ Setup Collectable UI` → keep *Ensure Level Manager* and
*Create + assign LevelConfig* on → **Setup In All Build Scenes** (or *Setup Current
Scene*). Per scene it:

- adds the self‑contained HUD prefab (if missing),
- adds one **`LevelManager`** object under `Managers` holding both `CollectableLevelManager`
  and `LevelContext` (consolidating any separate objects an earlier setup created),
- creates `ScriptableObjects/LevelConfigs/Level{n}Config.asset` and **captures that
  scene's current camera dead‑zone + sequence values into it**, then assigns it to the
  `LevelContext`,
- saves the scene.

Idempotent — safe to re‑run. *Only 'Level\*' Scenes* filters out Home/menu. The level
number used everywhere comes from the config's `Level Number` field (via `LevelContext`).

> Because the migration captures the **existing** per‑scene camera bounds/sequence
> values, nothing is lost when those fields move into the asset.

## Per‑level content

1. Drop the Robot Part / Memory Shard prefabs into the level. Each instance gets a
   unique id automatically; if any is blank use *Collectable Tools ▸ Assign Missing Ids*.
2. Set that level's `LevelConfig` **Robot Part Count** / **Memory Shard Count** to match
   how many you placed (authoring info).
3. Make sure the pickup's collider is a **trigger** and the player is tagged `Player`.

## Tuning

- **A level's camera / sequence / collectable counts** → that level's `LevelConfig` asset.
- **Shard story tiers (count + level range + story id)** → `CollectableConstants.MemoryShardTiers`
  (ranges must not overlap).
- **Robot Parts HUD total (0/56)** → `CollectableConstants.RobotPartsGrandTotal`.

## Reset

- `Tools ▸ Collectables ▸ Collectable Tools ▸ Reset Collectable Data` — deletes the save file.
- **New Game** (`MenuManager.NewGame()`) also wipes collectable progress for now.

## Store (future)

`CollectableLevelManager.IsStoryUnlockedForLevel(level)` / `IsCurrentStoryUnlocked`
report tier completion — hook the not‑yet‑built store screen into these.
