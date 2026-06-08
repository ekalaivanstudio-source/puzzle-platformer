# puzzle-platformer

A 2D puzzle-platformer game developed by **Ekalaivan Studio** using Unity.
This repository is publicly visible for portfolio and showcase purposes only.

© 2026 Ekalaivan Studio. All rights reserved. Unauthorized copying, modification, redistribution, or commercial use of this project is prohibited.

---

## Overview

Each turn the player programs a sequence of actions (Left, Right, Jump, Interact) using the keyboard or a gamepad, then presses Submit to watch the character execute the whole sequence deterministically. Movement is fixed-distance and frame-rate independent — every command travels an exact number of units — so success comes down to planning the right sequence to navigate hazards and puzzles to the exit.

---

## Features

- Turn-based sequence programming and deterministic execution
- Keyboard and gamepad input via Unity's New Input System
- Directional jumps (Jump + Left/Right combine into JumpLeft / JumpRight)
- Optional per-level "correct sequence" validation and fixed slot count
- **Key & slot doors** — carry a key and drop it into a slot to open the door
- **Push bricks** — shove bricks one grid unit at a time; a laser-destructible variant
- **Laser puzzles** — shooters, and redirectors the player can move or rotate
- **Patrol enemies** that can be defeated, plus spikes and trap zones
- Moving platforms (vertical and horizontal)
- Camera shake feedback and screen fade transitions
- Main-menu level select with PlayerPrefs-based unlock progression

---

## Controls (keyboard / gamepad)

Bindings are defined in the Input Actions asset and routed through `DeviceInputProvider`. Defaults:

| Action | Keyboard | Gamepad |
|---|---|---|
| Queue Left | Left Arrow | — |
| Queue Right | Right Arrow | — |
| Queue Jump | Up Arrow | — |
| Queue Interact | Interact key | — |
| Submit / Execute | Enter | Start |
| Undo last action | Backspace | B |
| Clear queue | Delete | Select |
| Restart level | R | — |

> A Jump queued immediately before a Left or Right is interpreted as a directional jump (JumpLeft / JumpRight).

---

## Architecture

### Core Systems

| Script | Role |
|---|---|
| `GameManager` | Singleton coordinator — turn start/end, key state, win/lose, level loading. Broadcasts `OnTurnReset`, `OnKeyReset`, `OnFullReset`, `OnExecutionStarted` |
| `PlayerController` | Singleton. Coroutine-based deterministic command execution (Left/Right/Jump/JumpLeft/JumpRight/Interact); reads from an `ISequenceSource` |
| `UIManager` | Singleton UI — fade overlay, popups, Play/Clear hooks |
| `CameraController` | Singleton camera effects (shake) |
| `AudioManager` | Singleton that owns every sound — music, footsteps, jump, pickup, bricks, enemy, laser hum/move/rotate, and UI clicks. Other scripts call `AudioManager.Instance.PlayXxx()` |

### Input & Sequencing

| Script | Role |
|---|---|
| `ISequenceSource` | Abstraction the `PlayerController` executes against |
| `SequenceManager` | Singleton `ISequenceSource`. Builds the action queue, enforces max length / optional correct sequence; raises `OnSequenceChanged` |
| `DeviceInputProvider` | Singleton. Binds New Input System actions (queue / submit / undo / clear / restart) to `SequenceManager` and `GameManager` |
| `SequenceDisplay` | UI row of slots showing the queued actions with Unicode icons |
| `PlayerInputUIHelper` | On-screen input prompt / button-indication helper |
| `ActionTypeEnum` | Enum — Left, Right, Jump, Interact, Any, JumpRight, JumpLeft |
| `IInteractable` | Contract for objects the player can trigger with an Interact action |

### Puzzles & World

| Script | Role |
|---|---|
| `PlaceableKey` | Key auto-collected on proximity; carried until placed |
| `KeySlot` | Receives a carried key and opens its linked door |
| `PushBrick` | Pushable brick; sweeps its path so it stops at obstacles. Optional laser-destructible variant |
| `MovableBrick` | Brick + player follow waypoints when triggered (scripted set-piece) |
| `InvisibleLockPoint` | Hidden trigger that locks the player onto a scripted path, then ends the turn |
| `EnemyMovement` | Two-point patrol enemy; defeatable with a death effect |
| `MovingPlatform` | Generic ping-pong platform (vertical or horizontal) — preferred for new platforms |
| `FloorMovement` / `FloorMovementLvl4` | Legacy vertical / horizontal moving platforms |
| `SpriteSheetAnimator` | Frame-by-frame sprite animation for `SpriteRenderer` or UI `Image` |

### Laser System

| Script | Role |
|---|---|
| `LaserShooter` | Casts a beam each frame; terminates on blockers, redirectors, or bricks; kills the player on contact |
| `LaserRedirector` | Receives a beam and re-emits it (Cross / StraightThrough / LShape) |
| `LaserRedirectorMoverInputResetter` | Trigger zone letting the player slide a redirector along a grid |
| `LaserRedirectorRotatorInputResetter` | Trigger zone letting the player rotate a redirector in 90° steps |

### Home Scene / Progression

| Script | Role |
|---|---|
| `MainMenuManager` | Level-select menu; unlocks levels from saved `PlayerPrefs` progress |
| `LevelManager` | Per-level "complete" handling — unlocks the next level and saves progress |

---

## Unity Setup

1. Create a persistent **GameSystems** GameObject and add the singletons: `GameManager`, `SequenceManager`, `DeviceInputProvider`, `UIManager`, `CameraController`, and the `PlayerController` on the player object.
2. Assign `PlayerInputActions.inputactions` to **DeviceInputProvider → Input Action Asset** (expects a `Player` action map with Left / Right / Jump / Interact / Submit / Undo / Clear / Restart actions).
3. Assign the fade `CanvasGroup` to **UIManager → Fade Overlay**, and wire `SequenceDisplay → Sequence Manager`.
4. Add `LevelManager` to each level scene and `MainMenuManager` to the menu scene; keep the **Main Menu at build index 0** and levels in order after it.
5. Ensure the EventSystem uses the **Input System UI Input Module** so menu buttons work with the New Input System.

---

## Project Info

- **Engine:** Unity 6 (URP) — `6000.4.x`
- **Language:** C#
- **Input:** Unity New Input System (keyboard / gamepad)
- **Platform:** PC (scalable for console/mobile via input bindings)
