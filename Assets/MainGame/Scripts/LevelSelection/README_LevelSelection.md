# Level Selection System

A scalable, data-driven, reusable level-select screen for Unity. No hard-coded level count —
the same screen serves 10 or 500+ levels. Drop the scripts in, author ScriptableObjects, build
one prefab + one Scroll View, and you're done.

## Architecture (separation of concerns)

| Script | Responsibility | Type |
|---|---|---|
| `LevelData` | Authored info for ONE level: id, name, thumbnail, scene name. | ScriptableObject |
| `LevelDatabase` | The ordered list of all levels. Single source of truth, no hard-coded count. | ScriptableObject |
| `LevelProgress` | Runtime per-level state: id, completed, best star count. | `[Serializable]` data |
| `SaveManager` | ONLY load/save progress (JSON in `persistentDataPath`). Raises `OnProgressChanged`. | static service |
| `LevelManager` | Unlock + completion *rules*: complete a level, keep best stars, unlock next. | static service |
| `LevelButtonUI` | Pure view of one tile. Paints itself from data via `Setup(...)`. | MonoBehaviour |
| `LevelSelectionUI` | Controller: reads DB, spawns buttons into the grid, handles clicks, auto-scroll. | MonoBehaviour |

Design notes:
- **No singleton abuse.** `SaveManager`/`LevelManager` are stateless static *services* (pure I/O
  and rules), not scene-resident singletons. The view is a plain MonoBehaviour.
- **Data-driven.** Everything reads from `LevelDatabase`. Add a level = add an asset to the list.
- **Best-result stars.** Stars only ever increase (`LevelProgress.TrySetStars`).
- **Event-driven UI.** `SaveManager.OnProgressChanged` → `LevelSelectionUI.Refresh()`.
- **Reskinnable.** Every visual is an Inspector reference; change sprites/colors/layout, not code.
- **Namespace `LevelSelectionSystem`** keeps it self-contained and avoids clashing with the
  project's existing global `LevelManager`. Copy the folder into any project to reuse — the only
  project-specific line is the optional `AudioManager.Instance?.PlayButton()` click sound in
  `LevelSelectionUI.OnLevelClicked` (delete it if the target project has no `AudioManager`).

---

## 1. Author the data

1. **Per level:** `Create → Level Selection → Level Data`. Set:
   - `Level Id` — stable unique number (the save key + unlock order key; never reuse/change it).
   - `Level Name`, `Thumbnail` (sprite), `Scene Name` (must be in **Build Settings → Scenes In Build**).
2. **Once:** `Create → Level Selection → Level Database`. Drag every `LevelData` into `Levels`
   **in unlock order**. The first entry is the level that's unlocked by default.

> Unlock order = list order. Completing the level at index *i* unlocks index *i+1*.
> Ids do NOT need to equal scene indices and need not be contiguous.

---

## 2. Build the Level Button prefab

Hierarchy (names are suggestions; only the component references matter):

```
LevelButton                (RectTransform + Button + Image[background] + LevelButtonUI)
├── Thumbnail              (Image)            ← shown when unlocked
├── LevelNumberText        (TextMeshPro - UGUI) ← shown when unlocked
├── LockIcon               (Image/GameObject) ← shown when locked
└── StarContainer          (GameObject, e.g. Horizontal Layout Group)
    ├── Star0  (Image)
    ├── Star1  (Image)
    └── Star2  (Image)
```

On the root, in the **LevelButtonUI** component, assign:
- `Button` (the Button on the root), `Background` (root Image).
- `Thumbnail`, `Level Number Text`.
- `Lock Icon` (the lock GameObject).
- `Star Images` → size 3, drag Star0/1/2.
- `Star Filled` / `Star Empty` sprites, `Star Container`.
- Optional: `Highlight Color`, click-animation settings.

Save it as a prefab. **You never place these by hand** — the controller instantiates them.

---

## 3. Build the Scroll View hierarchy

Use Unity's **UI → Scroll View**, then:

```
LevelSelectScreen            (LevelSelectionUI)
└── Scroll View              (ScrollRect)        ← assign to LevelSelectionUI.ScrollRect
    └── Viewport             (Mask + Image)
        └── Content          (RectTransform)     ← assign to LevelSelectionUI.Content
            • add GridLayoutGroup
            • add ContentSizeFitter (Vertical Fit = Preferred Size)
```

- **Content** needs a **GridLayoutGroup** (the system writes columns/cell/spacing/padding onto it
  from the Inspector) and a **ContentSizeFitter** (Vertical = *Preferred Size*) so it grows to fit
  hundreds of rows and scrolls.
- Set the ScrollRect to **Vertical** only (typical), Movement Type *Elastic* or *Clamped*.

On **LevelSelectionUI** assign: `Database`, `Level Button Prefab`, `Content`, `Scroll Rect`,
and tune `Columns`, `Cell Size`, `Spacing`, `Padding`, plus the `Highlight`/`Scroll-to-latest`
toggles.

Make sure your **EventSystem** uses the *Input System UI Input Module* (this project uses the new
Input System), so button clicks work.

Press Play: buttons generate automatically, Level 1 is unlocked, the rest are locked.

---

## 4. Call it from gameplay (level completion)

The only thing gameplay must do when the player wins:

```csharp
// stars = whatever your scoring decides (0..3). The system keeps only the best result.
LevelSelectionSystem.LevelManager.CompleteLevel(database, thisLevelId, stars);

// Shorter form if you called LevelManager.Configure(database) at startup:
LevelSelectionSystem.LevelManager.CompleteLevel(thisLevelId, stars);
```

That single call: marks the level completed, updates stars (only if higher), unlocks the next
level, and saves. When the player returns to the select screen, `OnProgressChanged` makes it
refresh automatically. See `Example_LevelCompletion.cs` for a copy-paste starting point.

Other handy calls:
```csharp
LevelManager.IsLevelUnlocked(id);   // gate logic
LevelManager.GetStars(id);          // best stars
SaveManager.ResetProgress();        // wipe save (settings / QA)
```

---

## 5. Optional enhancements (already included / easy seams)

- **Highlight latest unlocked** — `Highlight Latest Level` toggle on the controller.
- **Auto-scroll to latest** — `Scroll To Latest Level` toggle.
- **Click animation** — toggle on `LevelButtonUI` (scale punch, no tween dependency).
- **Addressables / async** — subclass `LevelSelectionUI` and override `LoadLevelScene(LevelData)`.
  Add an Addressable key field to `LevelData` and load it there; nothing else changes.
- **Paging for very large counts** — the GridLayout + ContentSizeFitter handles hundreds fine.
  For thousands, add UI object pooling (recycle off-screen buttons) behind the same `Refresh()`
  contract, or split `Database.Levels` into pages and rebuild the grid per page. The view/save
  layers don't change.

## Migration note

This supersedes the older `Assets/MainGame/Scripts/HomeScene/MainMenuManager.cs` +
`HomeScene/LevelManager.cs` approach (manual button arrays + a single `"UnlockedLevel"`
PlayerPrefs int). Those still work and are untouched; switch a scene over to the new
`LevelSelectionUI` when ready. The two save systems are independent (PlayerPrefs key vs JSON file).
```
