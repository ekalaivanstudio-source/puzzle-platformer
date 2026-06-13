# Home Screen System (`HomeUI`)

A modular, data-driven, PC/mobile-friendly front-end: Home → Play (Level Select) / Collections /
Settings / Quit, plus a reusable confirmation popup and a screen-navigation manager. Built in the
`HomeUI` namespace so it drops into any project and never clashes with existing classes.

> **Naming note.** This project already has a global `AudioManager` (sound *playback*) and a static
> `SettingsManager` (frame-rate launcher). To avoid collisions and respect existing code, the audio
> *settings* module is named **`AudioSettingsManager`** and drives the existing `AudioManager`
> (extended with additive volume/mute/UI-channel API). There is one audio system, not two.

---

## Architecture

```
Core/        UIPanel (CanvasGroup show/hide + animation seam)
             ScreenManager (navigation + back-stack + controller focus)
             ConfirmationPopup (generic yes/no — reusable for quit/delete/reset/restart)
             JsonSaveUtility (shared JSON persistence)
Home/        HomeScreenController (4 buttons → navigation)
             LevelSelectionPanel (UIPanel wrapper hosting the existing LevelSelectionUI)
Settings/    SettingsData (serializable values) + SettingsDefaults (SO)
             ISettingsModule (extension seam)
             GraphicsManager, AudioSettingsManager, InputManager (category appliers)
             SettingsManager (load → apply → save coordinator)
   UI/       SettingsPanelUI (tabs) + GraphicsSettingsUI / AudioSettingsUI / ControlsSettingsUI
             RebindButtonUI (one remappable control row)
Collections/ RobotPartData, RoboData, CollectionDatabase (SOs)
             CollectionProgress (RoboProgress + save data)
             CollectionSaveManager (persistence) + CollectionManager (unlock rules)
   UI/       CollectionsPanelUI (tabs + parts grid) + RoboTabUI + PartSlotUI
```

**SOLID / design principles used**
- **SRP** — persistence (SaveManagers/JsonSaveUtility), rules (CollectionManager, category managers),
  and views (every `*UI`) are separate. Views are told what to show via `Setup`/`Refresh`.
- **Open/Closed** — settings categories implement `ISettingsModule`; `SettingsManager` discovers and
  applies them, so a new category = a new component, no edits to the coordinator.
- **Data-driven** — levels, robots, parts, and default settings are ScriptableObjects/serialized data.
  No hard-coded counts; designers add content in the Inspector.
- **Minimal singletons** — only `ScreenManager` and `SettingsManager` expose `Instance`, and only as
  convenience locators for genuinely single, navigation/settings state. Static *services*
  (SaveManagers, CollectionManager) are stateless I/O + rules, not scene singletons.
- **Localization-ready** — all visible strings come from data/Inspector fields, never literals in
  logic, so a future localization layer can swap them.

---

## UI hierarchy (one scene, one Canvas)

```
Canvas (Scale With Screen Size, 1920x1080)
├── ScreenManager            (ScreenManager; InitialPanelId = "Home")
│   ├── HomeScreenPanel      (UIPanel id="Home" + HomeScreenController)
│   │     Settings / Collections buttons, Play (green), Quit (red)
│   ├── LevelSelectionPanel  (UIPanel id="LevelSelection" + LevelSelectionPanel + Back button)
│   │   └── (the LevelSelectionSystem Scroll View — see that system's README)
│   ├── SettingsPanel        (UIPanel id="Settings" + SettingsPanelUI)
│   │   ├── Tabs: Graphics / Audio / Controls buttons
│   │   ├── GraphicsSection  (GraphicsSettingsUI) — dropdowns/toggle/slider
│   │   ├── AudioSection     (AudioSettingsUI)    — 4 sliders + mute toggle
│   │   └── ControlsSection  (ControlsSettingsUI) — sensitivity, vibration, rebinds, reset
│   └── CollectionsPanel     (UIPanel id="Collections" + CollectionsPanelUI)
│       ├── TabContainer  → RoboTab prefabs spawn here
│       └── PartContainer (GridLayoutGroup) → PartSlot prefabs spawn here
├── PopupLayer
│   └── ConfirmationPopup    (UIPanel + ConfirmationPopup)  ← put LAST so it draws on top
├── Managers
│   └── SettingsManager (+ child GraphicsManager, AudioSettingsManager, InputManager)
└── EventSystem (Input System UI Input Module)
```

Set each panel's **Panel Id** field (Home, LevelSelection, Settings, Collections). The
`ScreenManager` auto-collects panels from its children if its list is empty.

### Prefab structures
- **RoboTab**: Button + `RoboTabUI` → Name text, Progress text, Icon image, Lock icon, Selected highlight.
- **PartSlot**: `PartSlotUI` → Part image, Locked overlay, Part name text.
- **ConfirmationPopup**: dim background + dialog with Title, Message, Yes button, No button (labels are TMP).
- **RebindRow** (under Controls): Button + `RebindButtonUI` → Action label, Binding label; set Action
  Name (e.g. `Player/Jump`) and Binding Index in the Inspector.

All visual references are exposed on each component, so reskinning = swapping sprites/colors, no code.

---

## Setup steps

1. **Create the data assets**
   - Settings: `Create → Home UI → Settings Defaults` (tune default volumes/quality).
   - Collections: `Create → Collections → Robot Part` (one per part), `→ Robo` (list its parts),
     `→ Collection Database` (list Robo 1..4 in order). Robo 1 is unlocked by default.
   - Levels: use the existing **Level Selection System** (`Create → Level Selection → …`).
2. **Build the hierarchy** above; assign each component's references in the Inspector.
3. **SettingsManager**: assign the `SettingsDefaults` asset; put `GraphicsManager`,
   `AudioSettingsManager`, `InputManager` as children (auto-discovered). Assign the project's
   **Input Actions asset** to `InputManager`. Optionally assign a full-screen black Image's
   CanvasGroup to `GraphicsManager.BrightnessOverlay`.
4. **InputManager rebinds**: add `RebindRow` prefabs under Controls; set each row's Action Name +
   Binding Index to match your Input Actions.
5. **EventSystem**: use the *Input System UI Input Module* (already standard in this project) for
   keyboard/controller navigation. Set each panel's `First Selected` for gamepad focus.
6. Press Play. Settings load + apply on start; navigation, collections and quit-popup all work.

---

## Save system integration

Three independent JSON files under `Application.persistentDataPath` (via `JsonSaveUtility`):
`settings.json`, `collections.json`, and the level system's `level_progress.json`. All save
automatically — settings on any change, collections on collecting a part, levels on completion.

---

## Example usage

```csharp
// Quit confirmation is automatic on the Quit button. Reuse the SAME popup for anything:
confirmationPopup.Show("Delete Save", "Erase all progress?",
    onYes: () => { CollectionSaveManager.ResetProgress(); SaveManager.ResetProgress(); });

// Collect a robot part from gameplay (see Example_CollectPart.cs):
CollectionManager.CollectPart(collectionDatabase, "robo1", "head");   // completing robo1 unlocks robo2

// Read a control setting in gameplay:
float look = lookDelta * SettingsManager.Instance.Input.MouseSensitivity;
SettingsManager.Instance.Input.Rumble(0.5f, 0.5f, 0.2f);              // honours the vibration toggle

// Navigate from code:
ScreenManager.Instance.Show("Settings");
ScreenManager.Instance.Back();
```

## Extending without modifying existing code
- **New setting** → add a field to `SettingsData`, read it in the relevant manager. Pipeline unchanged.
- **New settings category** (e.g. Gameplay/Accessibility) → new MonoBehaviour implementing
  `ISettingsModule`, drop it under `SettingsManager`. Auto-applied.
- **New Robo / parts** → add ScriptableObjects to the `CollectionDatabase`. UI + unlock chain adapt.
- **Animations / scene transitions** → override `UIPanel.ApplyVisibility` (DOTween, Animator, fades).
- **Addressables** → override `LevelSelectionUI.LoadLevelScene` (see that system's README).

## Notes & limits
- For settings (esp. volumes) to persist into gameplay scenes, keep `SettingsManager.Persist = true`
  (it survives scene loads and re-applies on each load, since the project's `AudioManager` is
  per-scene). For a menu-only setup, set Persist = false.
- Brightness uses a black overlay (most platforms can't set hardware gamma); assign the overlay or
  leave it null to skip.
- Rebinding requires the Input System package (this project ships 1.19.0). Rebinds are stored as the
  action asset's override JSON inside `settings.json`.
```
