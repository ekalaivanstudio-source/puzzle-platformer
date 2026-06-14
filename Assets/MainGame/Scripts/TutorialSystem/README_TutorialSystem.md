# Tutorial System

A reusable, data-driven onboarding/tutorial framework in the style of Candy Crush, Toon Blast and
Royal Match: a character + speech-bubble popup, a bouncing arrow that points at a target, a dimmed
screen with a spotlight cut-out, and a powerful Editor tool so designers build whole tutorials with
**zero code**.

It works for **UI elements** (buttons, cards, shop icons) *and* **world objects** (NPCs, buildings,
collectibles) through one framework, and the indicators stay correct whether the target lives in a
Screen Space Overlay / Screen Space Camera / World Space canvas, or in front of an Orthographic or
Perspective camera.

---

## 60-second quick start

1. Open the scene you want the tutorial in.
2. **Tools ▸ Tutorial System ▸ Setup Tutorial System** — builds the whole rig + a sample tutorial.
3. Select any object in the scene ▸ **GameObject ▸ Tutorial ▸ Convert To Tutorial Target** (or use
   the *Make Selection A Target* button in the creator). It gets a unique id automatically.
4. **Tools ▸ Tutorial System ▸ Tutorial Creator** — add steps, type messages, pick targets from the
   *Pick ▾* dropdown, drag to reorder.
5. From gameplay, start it:
   ```csharp
   TutorialManager.Instance.PlaySequence(myTutorialAsset);
   ```
6. Tell the tutorial when the player did something (for interaction/custom steps):
   ```csharp
   TutorialEventBus.Fire("collected_first_key");
   ```

That's the whole API surface for day-to-day use: `PlaySequence(...)` and `TutorialEventBus.Fire(...)`.

---

## Architecture

The system has a strict separation of concerns. Each box is one C# file; arrows are "talks to".

```
                         ┌─────────────────────────┐
        game code  ───▶  │     TutorialManager     │  ◀── auto-plays sequences on Start
                         │  (orchestrator, single  │
                         │   place the game talks   │
                         │   to; runs step coroutine)│
                         └───────────┬─────────────┘
            resolves id│             │ drives                 │ saves
                       ▼             ▼                        ▼
        ┌──────────────────────┐  ┌──────────────────────┐  ┌────────────────────┐
        │ TutorialTargetRegistry│  │  UI views:           │  │ TutorialSaveSystem │
        │  id → live target     │  │  • TutorialPopupUI   │  │  PlayerPrefs-backed│
        └───────────┬──────────┘  │  • TutorialArrow…    │  └────────────────────┘
                    │             │  • TutorialHighlight… │
            registers│             └──────────┬───────────┘
                    ▼                        │ asks "where on screen?"
        ┌──────────────────────┐             ▼
        │    TutorialTarget     │   ┌───────────────────────────┐
        │ (on UI or world obj)  │──▶│ TutorialScreenPositioner  │  ← the camera/canvas-agnostic core
        └──────────────────────┘   └───────────────────────────┘

        ┌──────────────────────┐         completes event-driven steps
        │   TutorialEventBus    │ ◀──────────────────────────────────  game code / TutorialEventTrigger
        └──────────────────────┘
```

### Files & responsibilities

| Layer | File | Responsibility |
|-------|------|----------------|
| **Data** | `Data/TutorialActionType.cs` | Enums: step completion condition + popup anchor. |
| | `Data/TutorialStepData.cs` | One step (ScriptableObject): target id, action, message, flags. |
| | `Data/TutorialSequenceData.cs` | Ordered list of steps + identity & playback rules. |
| **Targets** | `Core/TutorialTarget.cs` | Tags a UI/world object and registers it under a string id. |
| | `Core/TutorialTargetRegistry.cs` | Global id → target lookup (static). |
| | `Core/TutorialScreenPositioner.cs` | Converts any target to **screen pixels** — the camera/canvas-agnostic core. |
| **Orchestration** | `Core/TutorialManager.cs` | Runs sequences, drives views, waits for completion, saves. |
| | `Core/TutorialSaveSystem.cs` | Persists "completed" + resume index. |
| **Events** | `Events/TutorialEventBus.cs` | Decoupled pub/sub so gameplay needn't know about tutorials. |
| | `Events/TutorialEventTrigger.cs` | No-code component to fire an event (OnClick/UnityEvent). |
| **UI** | `UI/TutorialPopupUI.cs` | Character + speech bubble + Next; springy pop-in. |
| | `UI/TutorialArrowController.cs` | Arrow that follows + points at the target, bouncing. |
| | `UI/TutorialHighlightSystem.cs` | Dim + spotlight (4-strip cut-out) + pulsing ring. |
| | `UI/TutorialClickCatcher.cs` | Forwards dim taps to "tap to continue". |
| **Examples** | `Example/TutorialDragHandle.cs` | Drag-and-drop helper that fires the completion event. |
| | `Example/Example_TutorialStarter.cs` | Shows how the game starts a tutorial + fires events. |
| **Editor** | `Editor/TutorialSystemSetup.cs` | One-click build & wire of the whole rig + sample asset. |
| | `Editor/TutorialCreatorWindow.cs` | Visual authoring window. |
| | `Editor/TutorialTargetTagger.cs` | Scene "convert to target" + target inspector. |
| | `Editor/TutorialDataEditors.cs` | Action-aware step inspector + reorderable sequence inspector. |
| | `Editor/TutorialCreatorUtility.cs` | Shared add/remove/reorder of step sub-assets. |

---

## How "works with any camera / canvas" actually works

Everything is reduced to **screen pixels** by `TutorialScreenPositioner`, then mapped onto the
Screen Space Overlay tutorial canvas with `RectTransformUtility.ScreenPointToLocalPointInRectangle`
(null camera). The only per-target difference is which camera projects the target to screen:

| Target kind | Camera passed to `WorldToScreenPoint` |
|-------------|----------------------------------------|
| UI, Screen Space **Overlay** | `null` |
| UI, Screen Space **Camera** / **World Space** | `canvas.worldCamera` |
| World object, **Orthographic** or **Perspective** | the world camera (or `Camera.main`) |

Because the arrow and highlight re-read the screen position **every `LateUpdate`**, they track moving
or animating targets for free, and world targets behind the camera are hidden automatically.

### The spotlight needs no shader

Rather than masking a hole out of a full-screen image (stencil shader, pipeline-specific), the
highlight draws **four opaque strips** that frame the target. The rectangular gap they leave *is* the
spotlight. Strips block input where they cover; the gap lets taps fall through to the real target —
which is exactly what `WaitForButtonClick` needs. Swap the strip/ring sprites to reskin.

---

## Action types (step completion conditions)

| Action | Completes when… | Needs |
|--------|-----------------|-------|
| `PopupOnly` | player taps Next / anywhere, or auto-delay elapses | nothing |
| `Highlight` | same as above, but target is spotlit + arrowed | a target |
| `WaitForButtonClick` | the target's UI `Button` is clicked | UI target with a `Button` |
| `WaitForObjectInteraction` | `TutorialEventBus.Fire(id)` is called | the event fired from gameplay |
| `DragAndDrop` | `TutorialEventBus.Fire(id)` on a successful drop | e.g. `TutorialDragHandle` |
| `CustomEvent` | `TutorialEventBus.Fire(id)` is called | the event fired from gameplay |

For the event-driven actions the id is the step's **Completion Event Id**, or its **Target Id** if
that's left blank — so a step that targets `play_button` is completed by `Fire("play_button")` with
no extra typing.

---

## Integrating with *this* project

- **Wait for the player to interact with a Key / Door / Switch**: those already implement
  `IInteractable`. Add a `TutorialEventTrigger` to the object (or call `TutorialEventBus.Fire(id)`
  inside `Interact()`), set the step to `WaitForObjectInteraction`.
- **Home screen buttons** (`HomeScreenController`): set a step to `WaitForButtonClick` and target the
  Play / Settings / Collections button.
- **The boost-shop popup in the reference screenshot**: target the purchase button card, action
  `Highlight` for "Here's 70 free coins…", then a `WaitForButtonClick` step on the buy button.

Start tutorials from your existing managers, e.g. in `GameManager`/`MainMenuManager.Start()`:
```csharp
TutorialManager.Instance?.PlaySequence(onboardingTutorial);
```

---

## Extending

- **New action type**: add to `TutorialActionType`, then a `case` in
  `TutorialManager.WaitForCompletion`. Nothing else changes.
- **New indicator** (e.g. a hand sprite, confetti): add a view component that takes a
  `TutorialTarget`, calls `TutorialScreenPositioner` in `LateUpdate`, and have the manager drive it.
- **Cloud save**: replace the four `PlayerPrefs` calls in `TutorialSaveSystem` with your backend.
- **Animations/skin**: every animation constant is a serialized field on the popup/arrow/highlight;
  swap the placeholder built-in sprites on the prefab to reskin.

---

## Save data

`PlayerPrefs` keys, namespaced so they never collide:
- `Tutorial.Completed.<sequenceId>` → `1` once finished (skips `PlayOnce` tutorials).
- `Tutorial.Resume.<sequenceId>` → next step index (only for `Resumable` tutorials).

QA reset: the *Reset Saved Progress* button in the creator window, or
`TutorialManager.Instance.ResetProgress(sequence)`.
```
