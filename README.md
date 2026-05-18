# puzzle-platformer

A 2D puzzle-platformer game developed by **Ekalaivan Studio** using Unity.
This repository is publicly visible for portfolio and showcase purposes only.

© 2026 Ekalaivan Studio. All rights reserved. Unauthorized copying, modification, redistribution, or commercial use of this project is prohibited.

---

## Overview

The player is given a set of beat slots per turn. They program a sequence of actions (Left, Right, Jump, Interact) and press Play to watch the character execute them. The game supports a rhythmic, turn-based movement style where timing and planning determine success.

---

## Features

- Turn-based beat execution system
- Mouse/toggle UI grid for programming actions (original mode)
- Keyboard and gamepad input support (device mode)
- Runtime-switchable input mode — designed for a settings menu
- Collect keys and reach the door to advance levels
- Avoid spikes and hazards during execution
- Moving platforms (vertical and horizontal)

---

## Input Modes

| Mode | How to use |
|---|---|
| **Mouse** | Toggle beat slots on the UI grid, click Play |
| **Device** | Press arrow keys / gamepad buttons to queue actions, press Enter / Start to execute |

**Device mode keys (keyboard):**

| Key | Action |
|---|---|
| Left Arrow / A | Queue Left |
| Right Arrow / D | Queue Right |
| Up Arrow / W / Space | Queue Jump |
| Z / E | Queue Interact |
| Enter | Execute sequence |
| Backspace | Undo last action |
| Delete | Clear all |

---

## Architecture

### Core Systems

| Script | Role |
|---|---|
| `GameManager` | Singleton coordinator — owns turn start/end logic |
| `PlayerController` | Executes beat sequence; reads from `ISequenceSource` |
| `UIManager` | UI state — panel, buttons, timer slider, game-over screen |
| `AudioManager` | Singleton audio — walk, jump, button click, beat tunes |

### Input Architecture

| Script | Role |
|---|---|
| `ISequenceSource` | Interface PlayerController reads from — decoupled from input mode |
| `IInputProvider` | Interface for enabling/disabling an input provider |
| `SequenceSourceRouter` | Delegates reads to the active provider; assigned to PlayerController |
| `InputModeManager` | Switches between Mouse and Device mode at runtime |
| `MouseInputProvider` | Wraps toggle grid; bakes it to a flat sequence on Play |
| `DeviceInputProvider` | Binds New Input System actions to SequenceManager |
| `SequenceManager` | Manages the key-press action queue for device mode |
| `SequenceDisplay` | UI slots showing queued actions with icons |

### Toggle Grid (Mouse Mode)

| Script | Role |
|---|---|
| `ActionManager` | Aggregates all `ActionTimelineController` rows |
| `ActionTimelineController` | Manages one action row (e.g. Jump row); auto-attaches ToggleSound |
| `ToggleSound` | Plays beat audio when a toggle is switched on |
| `ActionState` | Struct — action type, active flag, clip, pitch |
| `ActionTypeEnum` | Enum — Left, Right, Jump, Interact |

### World

| Script | Role |
|---|---|
| `Key` | Interactable that notifies GameManager on collection |
| `MovingPlatform` | Generic ping-pong platform (vertical or horizontal) |
| `FloorMovement` | Vertical moving floor |
| `FloorMovementLvl4` | Horizontal moving floor |
| `IInteractable` | Interface implemented by interactable world objects |

---

## Unity Setup

1. Add all input components to one **InputManager** GameObject: `InputModeManager`, `MouseInputProvider`, `DeviceInputProvider`, `SequenceManager`, `SequenceSourceRouter`
2. Assign `SequenceSourceRouter` to **PlayerController → Sequence Source Object**
3. Assign `SequenceSourceRouter` and `DeviceInputProvider` to **GameManager**
4. Assign `SequenceSourceRouter` to **UIManager**
5. Assign `PlayerInputActions.inputactions` to **DeviceInputProvider → Input Action Asset**
6. Set **InputModeManager → Default Mode** to switch between Mouse and Device

---

## Project Info

- **Engine:** Unity 6 (URP)
- **Language:** C#
- **Input:** Unity New Input System
- **Platform:** PC (scalable for console/mobile via input bindings)

