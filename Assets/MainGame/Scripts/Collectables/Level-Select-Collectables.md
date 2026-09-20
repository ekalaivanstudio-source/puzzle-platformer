# Collectables On The Level Selection Map

Every level's collectables are drawn **on the route line** leaving that level, and the level
number sits inside the marker. Level 3 hides PIXEL's third part and a memory shard, so both
show on the line out of node 3, each lit once it is found.

```
       ( 3 )────[robot] [shard]────( 4 )────[shard]────( 5 )
                  unlit = still out there
                  lit   = in the collection
```

They sit on the line rather than on the marker because the marker is already full: a circle
50 units across holding either a level number or a padlock. The line between two levels is
the only empty space on the map, which is exactly why it is where the icons go.

Nothing new is authored for this. A level already says what it hides on its `LevelConfig`,
and both collectable systems already know what has been picked up — this only joins the two
and draws the result.

## The level number

`LevelNodeUI` writes the level number into a label inside the marker, on **unlocked levels
only**. A locked level draws the padlock in that same circle and the two cannot share it, so
the padlock wins — it is the more urgent thing to know about a level you cannot play.

The label is auto-sized between 14 and 28, so a one-digit level is drawn at full size and a
two-digit one shrinks to fit the circle rather than spilling out of it.

## The part that was actually missing

`LevelConfig` assets live in `ScriptableObjects/LevelConfigs/`, not in `Resources/`, and a
level's config normally reaches the game through the `LevelContext` in that level's own
scene. The map breaks that assumption: it draws twenty levels while standing in the home
screen, so there is no `LevelContext` for any of them and no way to reach their configs.

`LevelConfigDatabase` is the answer — a `Resources/` asset holding a **reference** to every
config, so the map can ask about a level it is not in. References, not copied values, so it
can never disagree with the configs; the only thing that can go stale is the list itself,
and that is rebuilt automatically (see [Setup](#setup)).

## Reading it

Everything goes through `LevelCollectableService`, which is the single access point:

```csharp
var held = LevelCollectableService.For(levelNumber);

held.Any                  // false → the level hides nothing, draw no indicator
held.HasRobotPart         // and .Robot / .PartIndex / .RobotPartCollected
held.HasMemoryShard       // and .MemoryShardCollected
held.Collected / .Total   // e.g. "1 of 2"

LevelCollectableService.RobotPartIcon(held.Robot, held.PartIndex);
LevelCollectableService.MemoryShardIcon();

LevelCollectableService.OnProgressChanged += Repaint;  // both systems, one subscription
```

`OnProgressChanged` is **static** — unsubscribe in `OnDisable`/`OnDestroy` or a destroyed
object keeps being called for the rest of the session.

Nothing is cached. Every call reads the live assignment and the live save, so re-assigning a
part or running **Reset Progress** shows up the next time the map is drawn.

## What the icons are

| Icon | Sprite | Why |
|---|---|---|
| Robot part | `Collect sprite/Pixel-Shilloute.png`, the light PIXEL silhouette | See below. |
| Memory shard | shard **frame 1** | The five shard sprites are one shard turning; frame 1 is the front-facing one, and a still should face the player. |

The part is drawn first, so on a level hiding both, the part is on the left and the shard on
the right. A level hiding one draws it **centred** on the line instead — with no panel behind
them there is no empty socket for a missing icon to leave, so nothing is gained by holding
its place.

### Why the part does not use the part's own art

It was built that way first and it does not read. Every robot-part sprite the project has is
dark:

- the **pickup** art is the whole robot near-black with one part lit — at icon size that is a
  black blob, and the lit part is a few pixels;
- the **UI layer** sprites hold only that part's pixels, several of which are literally four
  pixels;
- the generated **silhouette** is a dark grey chassis.

An `Image` tint *multiplies*, so it can only ever darken. Against the map's dark background
every one of those stays a black blob whether it is found or not — the found/not-found
distinction, which is the whole point of the icon, disappears.

A light silhouette tints exactly the way the shard does: grey-blue while it is still out
there, bright white once it is found. The cost is that it says "a part of PIXEL is here"
rather than *which* part — which the art could not have shown at this size anyway.

The mapping is `Robot Icons` on `LevelNodeCollectables`, one entry per robot. **Clear the
entry and that robot falls straight back to its real part sprite**, so this is one field to
revert if the art ever changes.

## Locked levels show their icons too

A locked level draws the same icons, unlit. The map is a teaser: the player can look ahead
and see that level 12 is worth coming back to. Locked and unlocked-but-uncollected look
identical, so the only distinction the icons ever draw is **lit vs unlit** — one thing to
read instead of three.

## Which stretch of line an icon sits on

The midpoint of the segment **leaving** that level — the step towards the next one. Each
segment therefore carries the icons of exactly one level, and no two nodes fight over the
same stretch of line. The last node in an arc has no next, so it borrows the segment it
arrived on.

`ArcLevelGenerator.AnchorCollectablesToPath` does this after the nodes are spawned, because
only the generator knows where the neighbouring node landed. It calls
`LevelNodeCollectables.SetLineAnchor`; the offset serialized on the prefab is only a fallback
for a node nothing positioned.

One consequence worth knowing: the last column of a row steps *down* to the next row, so its
icons sit on the vertical connector rather than beside the marker. That is correct — it is
still the segment leaving that level.

The unlit tint is deliberately mid-toned. An unlit icon has to read both against the dark
background and against the white or yellow route line it is sitting on, so it can be neither
dark navy nor near-white.

## Scripts

| File | Role |
|------|------|
| `Level/LevelConfigDatabase.cs` | **ScriptableObject in `Resources/`.** Every LevelConfig, reachable without its scene. `Get(levelNumber)`. |
| `Collectables/LevelCollectableService.cs` | **The single access point.** Joins the configs to both save systems; `For`, the two icon lookups, `OnProgressChanged`. |
| `Level Selection/LevelNodeCollectables.cs` | The icons for one level. Lays them out, tints them, hides itself when the level hides nothing. Holds no state but the level it is bound to. |
| `Level Selection/ArcLevelGenerator.cs` | `AnchorCollectablesToPath` puts each node's icons on the segment leaving it. |
| `Level Selection/LevelNodeUI.cs` | Writes the level number, and calls `Bind` from `SetupNode` — so first generation, arc paging and the unlock animation all refresh both. |
| `Level Selection/Editor/LevelCollectableIndicatorSetup.cs` | `Tools ▸ Level Collectables` — builds the database, and the icons and number on the node prefab. Keeps the database current by itself. |

## Setup

`Tools ▸ Level Collectables ▸ Run Full Setup` is idempotent — re-run it any time. It:

1. **Rebuilds the level config database** from `ScriptableObjects/LevelConfigs/`, ordered by
   level number, warning about two configs claiming one number.
2. **Builds the icons and the level number** into
   `Prefabs/UI/Menu Level Creation/Level Node.prefab`. Patched in place rather than
   re-authored, so the node objects already sitting in HomeScreen keep their links.

**Adding a level needs neither step.** An `AssetPostprocessor` rebuilds the database whenever
a LevelConfig is added, deleted or moved. Only the *list* can go stale — the database stores
references, so changing what an existing level hides needs no rebuild at all.

**Log Level Collectables** prints the level → part/shard table the map will draw, which is
the quick way to check an assignment without opening twenty assets.

## Tuning

- **Which levels hide what** → each level's `LevelConfig ▸ Collectables`. Unchanged by this;
  it is the same switch the pickups already read.
- **Icon size and spacing** → `Icon Size` / `Icon Gap` on `LevelNodeCollectables`, in UI
  units. The icons are square; the gap only applies when a level hides both.
- **Lit and unlit colours** → `Collected Tint` / `Uncollected Tint` on the same component.
  Remember the tint multiplies: a dark sprite cannot be made to look lit.
- **The part's icon** → `Robot Icons`, one entry per robot; empty falls back to the part's
  own sprite.
- **Level number size and colour** → the `LevelNumber` label on the node prefab. It is
  auto-sized, so the two bounds are what to change, not the size itself.

Re-running **2. Build Node Indicator** rewrites all of those back to the tool's defaults, so
tune on the prefab and only re-run that step after changing the art.

## Related

- [Robot parts](README.md) — which piece, per robot.
- [Memory shards](Memory-Shards.md) — how many, against story thresholds.
