# Memory Shards

A second collectable, sitting alongside the [robot parts](README.md) and deliberately
answering a different question.

A robot part is about **which** piece you found — `echo_3` fills one slot of one robot, and
the art has to line up. A memory shard is only about **how many**. Reaching a total unlocks
a story cutscene: 5 shards unlocks one, 10 unlocks the next, and so on.

**No shard is tied to a story, and no level is tied to a story.** A shard is worth exactly
one point towards a running total, and the thresholds are read from one asset. That is the
whole design, and everything below follows from it.

## The two numbers that matter

| | Where it lives | Who changes it |
|---|---|---|
| Does *this level* hide a shard? | `LevelConfig ▸ Collectables ▸ Place Shard` | Level designer, per level |
| How many shards unlock *this story*? | `Resources/MemoryShardDatabase ▸ Stories ▸ Required Shards` | Narrative, in one asset |

Nothing else needs touching to re-tune the design.

## Thresholds are safe to change at any time

This is the part worth knowing, because it is what the system was built around: **unlock
state is never written to the save file.** The save holds only the shards picked up and the
stories already played. What is *unlocked* is recomputed from the live thresholds every time
it is asked for.

So with a player mid-save:

- drop a threshold from 10 to 8 and a player sitting on 9 shards has that story waiting at
  the end of their next level;
- raise it to 12 and it goes back to locked;
- reorder, insert or delete entries freely.

There is no migration step and nothing to keep in sync. The one thing that *is* stable is a
story's **`id`** — it is what records "already played", so renaming an id replays that story.

## Identity

A shard's save id is the level it sits in — `shard_7` — and nothing else. Since a level
hides at most one shard, the level number is a stable, collision-free id. Dragging the
pickup around the scene, changing its sprite, or re-tuning every story never affects it.

## The art is an animation, not five shards

The five sprites in `Sprites/Collectibles/memory shards/` are **one shard turning on the
spot** — front, three-quarter, edge-on (a thin sliver), three-quarter, front. Played in
order at 10fps they are a half-second revolution, which is how the pickup shows them:
a `SpriteSheetAnimator` on `MemoryShardPickup.prefab`, fed from the database at load.

They were first read as five interchangeable looks, and `Shard Variant` on the assignment
is what is left of that. It now only decides which frame a shard with **no** animator
stands still on — a spinning shard plays all five, so it changes nothing on a normal level.
The HUD icon uses frame 1, the front-facing one, because a still should face the player.

> Frames are **discovered** from the folder and sorted numerically, so re-drawing the spin
> or adding a frame is dropping files in and running `1. Build Database`. Nothing in the
> level scenes or the prefab has to be touched — the pickup asks the database at load.

## Why the story waits for the end of the level

Shards are picked up mid-puzzle, but a cutscene never plays there — being pulled out of a
puzzle you are halfway through solving is exactly what this avoids.

```
grab the 5th shard   →  unlock SAVED immediately
                     →  HUD toast: "New memory unlocked — Memory I"
                     →  story queued

finish the level     →  doctor reacts, level tears down
                     →  story cutscene plays
                     →  marked as played
```

Because the pending queue is *derived* (unlocked minus already-played) rather than stored,
an unlock cannot be lost — only deferred. Quit the game between the pickup and the end of
the level and the story is still waiting the next time any level is finished.

## Scripts

| File | Role |
|------|------|
| `MemoryShardIds.cs` | The id format (`shard_<level>`), the variant count, and the automatic variant spread. |
| `MemoryShardAssignment.cs` | Whether a level hides a shard. Serialized into `LevelConfig.memoryShard`. |
| `MemoryStoryEntry.cs` | One story: id, `requiredShards`, title, `VideoClip`. |
| `MemoryShardDatabase.cs` | **ScriptableObject in `Resources/`.** The shard sprites and every story threshold. The asset the design is tuned on. |
| `MemoryShardSaveSystem.cs` | JSON save at `persistentDataPath/memoryshards.json`. Stores collected shards + played stories. Never stores unlocks. |
| `MemoryShardService.cs` | **The single access point.** Counts, unlock evaluation, the pending queue, `OnShardCollected` / `OnProgressChanged` / `OnStoryUnlocked`, `ResetAll`. Static, so it works on the home screen too. |
| `MemoryShardPickup.cs` | The pickup in the level. Trigger-collect, permanent, self-hides if already collected or if the level hides no shard. Spawns the collect burst. |
| `MemoryShardCounterHUD.cs` | The in-level counter and the unlock toast. |
| `MemoryStoryPresenter.cs` | Full-screen video player that drains the pending queue at the end of a level. |
| `Editor/MemoryShardSetup.cs` | `Tools ▸ Memory Shards` — builds the database and prefabs, wires every scene, bulk assignment helpers. |
| `Editor/MemoryShardAnimationBuilder.cs` | Puts the spin on the pickup prefab. `Rebuild Shard Animation` patches the prefab in place, so the level instances stay linked. |

## Setup

`Tools ▸ Memory Shards ▸ Run Full Setup` is idempotent — re-run it any time. It does:

1. **Build Database** — writes `Resources/MemoryShardDatabase.asset` from the shard sprites.
   Seeds placeholder stories at 5/10/15/20 **only when there are none**; authored thresholds
   and clips are never overwritten by a re-run.
2. **Build Prefabs** — `MemoryShardCollectEffect`, `MemoryShardPickup`, `MemoryShardHUD`,
   `MemoryStoryPresenter`. The effect is built first because the pickup references it.
3. **Setup All Level Scenes** — per scene, replaces the HUD and the presenter (so prefab
   changes reach every level) and adds the pickup **only when missing**, so hand-placed
   shard positions survive.

> Step 3 opens and saves every level scene. It prompts to save modified scenes first, so
> **save or close whatever you are working on before running it**, or the prompt will sit
> there waiting.

Run Full Setup deliberately **does not** decide which levels hide a shard — that is a design
choice per level. Two bulk helpers sit on their own menu for a starting point:

- `Assignment ▸ Place A Shard In Every Level`
- `Assignment ▸ Remove Shards From Every Level`

Then untick the levels that should not have one, from their own `LevelConfig`.

Two more entries help while working:

- **Log Shard Assignments** — prints the level → shard table and the current thresholds.
- **Reset Progress** — deletes the save file. **New Game** also wipes shard progress.

## Per-level content

The tool parks each pickup near the player spawn. Drag it somewhere worth finding — that is
the only hand work a level needs. Its collider is already a trigger and the player is matched
by the `Player` tag.

## The collect effect

Picking a shard up spawns `MemoryShardCollectEffect.prefab` at its position: the five frames
of `Sprites/Effects/item_collection_effect/`, played once at 20fps and drawn at sorting order
20 so the burst reads over the shard rather than behind it.

It is a plain [`OneShotEffect`](../OneShotEffect.cs) — the project's existing component for
transient sprite VFX, which runs the frames on unscaled time and then destroys its own
GameObject. The pickup instantiates it and forgets about it; there is nothing to clean up.

Frames are **discovered** from the folder and sorted numerically, so adding or removing one is
just dropping a file in and re-running Build Prefabs. The numeric sort matters: an alphabetical
one would order a ten-frame effect `1, 10, 2, 3` and play it wrong.

> **Effect art must be imported as a sprite.** These PNGs arrived as plain textures
> (`Texture Type ▸ Default`), which makes them invisible to the builder — it warns and produces
> an effect with no frames. They are now set to `Sprite (2D and UI)`, Single, **72 pixels per
> unit**, matching the shard art so the burst is the same world size as the shard it replaces.
> Any new frame needs the same settings.

To change the effect, either drop different frames into that folder and re-run Build Prefabs,
or edit `MemoryShardCollectEffect.prefab` directly — the pickup only holds a reference, so
retiming it (`Frames Per Second`, `Linger After Last Frame`) needs no code change.

### The idle shine slot is still empty, and the spin is why

The pickup has a **Shine Effect** slot — an idle shimmer shown while the shard is
uncollected — and nothing is in it. The robot parts fill theirs with a three-sparkle twinkle
(`Editor/RobotPartShineBuilder.cs`), because a robot part is a still image lying in a room
and needs something to catch the eye.

A shard is already moving. It turns continuously, and the edge-on frame flashes past twice
a second on its own, so a sparkle on top of it is two idles competing. The slot stays
available: point it at any child object and the pickup will show and hide it with the shard.

## Authoring a story

On `Resources/MemoryShardDatabase.asset`, under **Stories**:

```
id              memory_01     ← stable; renaming it replays the story
requiredShards  5             ← re-tune freely
title           Memory I      ← shown in the unlock toast
clip            (VideoClip)   ← the cutscene
```

**A story with no clip is left pending rather than consumed.** That is intentional: author
the thresholds now, drop the video in later, and it plays the first time a level ends after
the clip exists. Nothing is silently skipped.

The presenter's VideoPlayer is configured exactly like `IntroCutsceneScreen` — direct audio
output, no wait-for-first-frame, surface re-bound every frame. Those three are not style
choices; each one is a bug that screen already hit. See the comments on `ConfigurePlayer`
before changing them.

## Tuning

- **Which levels hide a shard** → each level's `LevelConfig ▸ Collectables`.
- **Story thresholds, titles and clips** → `Resources/MemoryShardDatabase.asset`.
- **Counter position, toast wording and duration** → the `MemoryShardHUD` prefab, or
  `BuildHudPrefab` in `MemoryShardSetup.cs`, then re-run steps 2 and 3.
- **Collect burst frames and speed** → `Sprites/Effects/item_collection_effect/` and the
  `MemoryShardCollectEffect` prefab.
- **Spin art and speed** → `Sprites/Collectibles/memory shards/` then `1. Build Database`
  for the frames; `Frames Per Second` on the pickup prefab's `SpriteSheetAnimator` for the
  speed (`Rebuild Shard Animation` resets it to 10).
- **Skip, fades, video volume, music ducking** → the `MemoryStoryPresenter` prefab.

## Hooking up your own UI

Everything reads through `MemoryShardService`, so a Memories tab on the home screen needs no
new plumbing:

```csharp
int collected = MemoryShardService.TotalCollected;
foreach (var story in MemoryShardService.UnlockedStories())
    // ...draw an unlocked memory

MemoryShardService.OnProgressChanged += Repaint;   // repaint on pickup or reset
MemoryShardService.OnStoryUnlocked  += ShowToast;  // one-shot unlock feedback
```

These are **static** events — unsubscribe in `OnDisable`/`OnDestroy` or a destroyed object
keeps being called for the rest of the session.
