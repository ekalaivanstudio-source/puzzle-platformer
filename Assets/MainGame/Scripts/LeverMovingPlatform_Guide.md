# Lever-Controlled Moving Platform — Usage Guide

A patrol platform that travels one tile at a time between two endpoints and carries
whatever is standing on it. It is switched on/off by a **lever** the player operates
with **E**, exactly like the laser mover/rotator controllers.

Two scripts make up the system:

| Script | Goes on | Role |
| --- | --- | --- |
| `LeverMovingPlatform` | the platform | Moves the platform in tile steps and carries riders. |
| `PlatformLever` | the lever | Detects the player, handles the **E** press, and controls the platform. |

---

## Quick setup

### 1. The platform
1. Create a GameObject for the platform and give it a **Sprite Renderer**.
2. Add a **non-trigger `Collider2D`** (usually a `BoxCollider2D`) sized to the visible platform. The player stands on this.
3. Add a **Kinematic `Rigidbody2D`** (recommended — a moving collider without a body is treated as static and is expensive to move). Set *Body Type = Kinematic*, *Interpolate = Interpolate*.
4. Put the platform on a layer your **player treats as ground** (so the player's ground check lands on it).
5. Add the **`LeverMovingPlatform`** component and set the path (see field reference below).

### 2. The lever
1. Create a GameObject for the lever (with its own sprite if you like).
2. Add a **`Collider2D` with `Is Trigger = true`** — this is the *interaction range*. Make it a bit larger than the lever art.
3. Add the **`PlatformLever`** component.
4. Drag the platform into **Target Platform**.
5. (Optional) Assign **Prompt UI**, **Control UI**, and On/Off **Visuals**.

### 3. Wiring notes
- The **player** GameObject's **Tag** must be `Player` (matches the *Player Tag* field on both components).
- To let the lever **ride the platform**, make the lever a **child of the platform** — it is then carried automatically.
- Save it all as a **prefab** so you can reuse it across levels.

---

## How it behaves in game

The lever is a **toggle** operated with **E** while the player is within the trigger zone. It works just like the laser controllers:

**Press E (engage):**
- The current run is **aborted** and any remaining queued inputs are **dropped** (`ResetAtCheckpoint`). The player stops where they stand — they are **not** teleported to the lever.
- Normal gameplay input is **locked**.
- The platform switches **ON** and starts patrolling. The player rides along.

**Press E again (disengage):**
- The platform switches **OFF** — it **finishes the current tile, then stops** (never mid-tile).
- Normal input is **restored**.

**While ON:**
- The platform advances **one tile at a time** until it reaches an endpoint.
- Reaching an endpoint **reverses direction** automatically and it heads to the opposite endpoint.
- Re-engaging after a stop **resumes from the current tile in the same direction** it was travelling.

There is **no cooldown** — E can be pressed at any time, in any motion state.

---

## What gets carried

Anything resting on top of the platform is carried by the exact distance the platform moves each physics step:

- ✅ **Player** — always (matched by the *Player* tag).
- ✅ **Movable bodies** — anything with a non-static `Rigidbody2D` (pushable **bricks**, crates, etc.).
- ✅ **The lever** — if parented to the platform (moves with its parent automatically).
- ❌ **Static geometry** — ground, walls, other platforms (no `Rigidbody2D`) are **never** moved.

> Bricks share the **Ground** layer with the static ground, so the platform tells them
> apart by their **Rigidbody2D**, not by layer. A brick rides only if it has its kinematic
> body (which `PushBrick` adds automatically).

---

## Field reference — `LeverMovingPlatform`

**Path**
| Field | Meaning |
| --- | --- |
| **Axis** | `Horizontal` or `Vertical` — the line the platform patrols along. |
| **Tile Size** | World size of one movement unit. Match your level grid (usually `1`). |
| **Tiles Forward** | How many tiles it can travel in the **positive** axis direction from its start. |
| **Tiles Backward** | How many tiles it can travel in the **negative** axis direction from its start. |
| **Start Direction** | Which endpoint it heads toward first (`Forward` / `Backward`). |

**Movement**
| Field | Meaning |
| --- | --- |
| **Speed** | Travel speed in units per second. |

**Riders**
| Field | Meaning |
| --- | --- |
| **Player Tag** | Tag of the player (default `Player`). |
| **Rider Probe Height** | Thickness of the detection strip across the platform's top edge. Increase if a rider isn't picked up. |

> The patrol path runs from `start − Tiles Backward` to `start + Tiles Forward`.
> Place the platform at one end and set only *Tiles Forward* for a simple one-way-and-back patrol,
> or split the tiles both ways to patrol around the start point. The path and its tile
> stops are drawn as **gizmos** in the Scene view.

## Field reference — `PlatformLever`

| Field | Meaning |
| --- | --- |
| **Target Platform** | The `LeverMovingPlatform` this lever controls. **Required.** |
| **Prompt UI** | Shown when the player is in range (e.g. *"Press E to control platform"*). Optional. |
| **Control UI** | Shown while engaged (e.g. *"Platform moving — E to stop"*). Optional. |
| **Player Tag** | Tag used to detect the player (default `Player`). |
| **On Visual** / **Off Visual** | Optional objects toggled to show the lever's pulled/raised state. |

---

## Scripting API

`LeverMovingPlatform` exposes:

```csharp
bool IsOn         // is the platform currently switched on (moving / will move)?
void Toggle()     // flip ON/OFF
void SetOn(bool)  // ON resumes from the current tile; OFF finishes the tile then stops
```

Use these if you want another trigger (a pressure plate, a timer, a cutscene) to drive
the platform instead of — or in addition to — the lever.

---

## Reset behaviour

The platform returns to its **start position** and switches **OFF** whenever the player is back at its spawn:

- On a **full restart** (R key / UI Restart button) — via `OnFullReset`.
- When the player **respawns at spawn after a turn finishes** (all inputs executed) or on a **soft reset / death** — via the `OnPlayerRespawn` event.

It does **NOT** reset when a lever aborts a run *in place* mid-turn (that keeps the platform where it is so it isn't yanked out from under a riding player). The lever also leaves control mode and restores input on reset.

### Bricks that ride the platform
A pushed brick (`PushBrick`) normally **persists across turns** (it only resets on a full restart) — that's the intended incremental-pushing mechanic. If a brick sits on a moving platform and you want it to return home when the player respawns, tick **Reset On Respawn** on that brick's `PushBrick` component. Leave it off for bricks that should stay put across turns.

> `MovableBrick` (the waypoint-carry brick) already returns to its start each turn, so it needs no change.

---

## Common recipes

- **Horizontal ferry (4 tiles, back and forth):** Axis `Horizontal`, Tiles Forward `4`, Tiles Backward `0`, Start Direction `Forward`.
- **Vertical elevator (3 tiles up):** Axis `Vertical`, Tiles Forward `3`, Tiles Backward `0`.
- **Patrol around the start (2 tiles each way):** Tiles Forward `2`, Tiles Backward `2`.
- **Faster/slower platform:** adjust **Speed** (units/second).

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Player doesn't ride the platform | Player's tag must be `Player`; the platform needs a solid (non-trigger) collider; try increasing **Rider Probe Height**. |
| Ground/walls move with the platform | They shouldn't — static geometry has no `Rigidbody2D`. If something moves that shouldn't, remove its `Rigidbody2D` (or set it to *Static*). |
| A brick won't ride | The brick needs a non-static `Rigidbody2D` (kinematic). `PushBrick` adds this automatically. |
| Pressing E does nothing | Check **Target Platform** is assigned, the lever's collider is **Is Trigger**, and the player is inside the trigger zone. |
| Input stays locked after using the lever | The lever restores input on exit and on full reset; make sure the lever object isn't destroyed while engaged. |
| Platform stops mid-tile | It shouldn't — OFF always completes the current tile. Verify nothing else is calling `SetOn`. |
