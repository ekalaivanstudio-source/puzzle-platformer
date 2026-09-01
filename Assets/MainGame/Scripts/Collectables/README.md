# Robot Part Collectables

Four collectable robots — **ECHO, NOVA, PATCH, PIXEL** — each broken into **5 parts**
scattered one per level. Collecting a part is permanent progress: it is written to disk
immediately and the pickup never comes back, on level reset or on a new session.

The collection UI draws each robot as a dark silhouette that **fills in piece by piece**
as its parts are found. The same UI appears in two places: pinned to the right of the
screen during a level, and on the home screen's **Collection** tab.

This replaces the old Robot Part / Memory Shard system entirely.

## Identity

A robot's id is its lower-case name (`echo`); a part's id is that plus its 1-based number
(`echo_3`). `RobotIds` owns that format and the save file stores nothing else, so a part
keeps its identity when it moves to a different level. **Never renumber `RobotId`** — the
enum values are written into saves.

## Scripts

| File | Role |
|------|------|
| `RobotId.cs` | The `RobotId` enum plus `RobotIds`: parts-per-robot, robot order, and the id string format. |
| `RobotDefinition.cs` | **ScriptableObject, one per robot.** Display name, accent colour, silhouette, the 5 UI layer sprites (array order = draw order), the 5 world pickup sprites. |
| `RobotCollectionDatabase.cs` | **ScriptableObject listing all 4 robots.** Lives in `Resources/` so the service can load it with no scene reference. |
| `RobotPartSaveSystem.cs` | JSON save at `persistentDataPath/robotparts.json`. Stores collected part ids; provides counts; `ResetAll()`. |
| `RobotCollectionService.cs` | **The single access point.** Database lookup, `IsCollected` / `Collect` / counts, `OnPartCollected` + `OnProgressChanged` events, `ResetAll`. Static, so it works on the home screen too. |
| `RobotPartAssignment.cs` | Which part a level hides. Serialized into `LevelConfig.robotPart`. |
| `RobotPartPickup.cs` | The pickup in the level. Trigger-collect, permanent, self-hides if already collected. Reads its identity from the level's config. |
| `RobotCollectionSlot.cs` | One robot's silhouette + 5 stacked part layers + count label. Includes the collect pop animation. |
| `RobotCollectionView.cs` | Drives a set of slots and owns the event subscription. Used by both the level HUD and the home screen tab. |
| `Editor/RobotCollectionSetup.cs` | `Tools ▸ Robot Collection` — builds assets and prefabs, assigns parts, wires every scene. |

## How the fill effect works

Every sprite for a robot is the same 72x72 canvas:

- `<Robot>_Silhouette.png` — the whole robot dimmed, always visible.
- `<Robot>_Part1..5.png` — **only that part's pixels**, transparent everywhere else.

Because the layers share a canvas and hold disjoint pixels, stacking them lines up exactly:
a collected part simply enables its layer and lights up its region of the silhouette. The
slot's part layers are ordered so later parts draw in front (e.g. ECHO's eyes over its head).

These are **generated** from the artist's drops in `Sprites/ECHO|NOVA|PATCH|PIXEL/`, into
`Sprites/RobotParts/<Robot>/`. The originals are still used as-is for the **world pickups**,
where the full artwork (whole robot dark, one part lit) reads far better than a masked
layer — several of those are only a handful of pixels.

> Regenerating the layers is a one-off offline step. If the art changes, re-run the
> generator, then `Tools ▸ Robot Collection ▸ 1. Build Database And Definitions`.

## One part per level

The assignment lives on the level's **`LevelConfig.robotPart`**, not on the pickup object,
which is what enforces the one-part-per-scene rule and means moving the pickup around a
scene never changes what it is:

```
placePart   off when this level hides no part
robot       ECHO / NOVA / PATCH / PIXEL
partNumber  1-5
```

`RobotPartPickup` reads that on `Start`, dresses itself in the right sprite, and hides
itself when the part is already collected — or when the level has no part assigned.

A pickup can opt out with **Override Assignment** and name its own robot + part. That is
for test scenes; normal levels leave it off.

## Setup

`Tools ▸ Robot Collection ▸ Run Full Setup` does everything and is **idempotent** — re-run
it any time. Individual steps are on the same menu:

1. **Build Database And Definitions** — writes the 4 `RobotDefinition` assets from the
   sprite folders and the database into `Resources/`.
2. **Assign Parts To Levels** — walks the `LevelConfig` assets in `levelNumber` order and
   assigns 5 levels per robot (ECHO first). Levels past the 20th get none.
3. **Build Prefabs** — `RobotCollectionHUD.prefab` (own canvas, right-aligned column) and
   `RobotPartPickup.prefab`.
4. **Setup All Level Scenes** — per scene: strips the old collectable objects, then adds
   the HUD and the pickup. The **HUD is replaced** each run so prefab changes reach every
   level; the **pickup is only added when missing**, so hand-placed positions survive.
5. **Setup Home Screen Tab** — fills the home screen's Collection panel with the same
   four robots, drawn larger and with names.

Three more entries help while working:

- **Reset Progress** — deletes the save file.
- **Log Level Assignments** — prints the level → part table.
- **Purge Legacy From Remaining Scenes** — clears old collectable objects out of the
  unshipped duplicate scenes under `Scenes/`. Scoped to `Assets/MainGame/Scenes` and it
  does **not** strip missing scripts, so it cannot disturb unrelated or third-party scenes.

## Per-level content

The tool parks each pickup near the player spawn. Drag it somewhere worth finding — that
is the only hand work a level needs. Its collider is already a trigger and the player is
matched by the `Player` tag.

## Tuning

- **Which part a level hides** → that level's `LevelConfig ▸ Collectables`.
- **Robot names / accent colours / layer draw order** → the `RobotDefinition` assets, or
  the `RobotAuthoring` table in `RobotCollectionSetup.cs` which seeds them.
- **HUD size and placement** → `BuildHudPrefab` in `RobotCollectionSetup.cs`, then re-run
  steps 3 and 4.

## Reset

- `Tools ▸ Robot Collection ▸ Reset Progress`.
- **New Game** (`MenuManager.NewGame()` and the main menu button) also wipes part progress.
