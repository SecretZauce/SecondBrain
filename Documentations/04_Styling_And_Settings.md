## Styling — Emoji and Colors

Styling options are available on Containers and Bases. Right-click a node to access them. When multiple nodes are selected, the style is applied to all of them simultaneously.
<br>
<img alt="Color styles on tree rows" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/visual-styling.gif" width="480" height="270"/>

### Emoji / Icon

Right-click → **Set Emoji Icon** opens the icon picker.

- **Emojis tab** — browse by category or search by name.
- **Editor Icons tab** — use any built-in Unity editor icon.
- **Last Used tab** — quick access to the 30 most recently used icons per mode (emojis and editor icons tracked separately).
- Clearing the icon reverts the node to its default editor icon.
- With no node selected, clicking an icon copies it to the clipboard instead of applying it.

The emoji/icon lists are loaded from JSON files at `Assets/SecretZauce/SecondBrain/Resources/Editor/Icons/`. You can edit `unity_editor_icons.json` and `unity_icons_emojis.json` to add, remove, or rename entries.

> **Emoji require Unity 6 (6000.0) or newer.**
> Unity 2022.3 and older generate Editor UI text through the legacy path, which has no OS font fallback: any glyph missing from the Editor font (Inter) is laid out with an advance of `0`, so emoji render as nothing at all — not even a placeholder box. Unity 6 renders IMGUI text through TextCore, which decodes surrogate pairs and falls back to the system emoji font.
>
> On unsupported editors SecondBrain detects this automatically and:
> - opens the icon picker on the **Editor Icons** tab and hides the emoji grid behind a **Pick anyway** toggle,
> - drops the emoji prefix from tree rows, foldouts and the window title so no blank gap is left,
> - keeps the per-type icon on rows whose emoji cannot be drawn, and explains the situation via tooltip.
>
> Emoji already assigned are never modified — they stay in the asset and display correctly once the project is opened in Unity 6. Use **Editor Icons** for icons that must be visible on every supported editor version.

### Label Color

Right-click → **Set Color** opens the color tray.

- Choose from **preset swatches** or create **custom colors** (up to 12, newest first, persisted across sessions).
- To add a custom color, click **Add Color** and use the HSV picker.
- Right-click any custom swatch to remove it. The **Clear** swatch cannot be removed.
- **Reset Colors** restores built-in defaults and removes all custom colors.

### Color Style

Controls how the color appears on each row:

| Style | Appearance |
|---|---|
| Gradient | Gradient wash across the row |
| Font Color | Tints the label text |
| Circle Dot | Small colored circle beside the label |
| Background | Fills the entire row background |


**Foldout Only** restricts the color to the Container's header row and does not propagate to child rows.

**Apply Styles to All** propagates the current style and Foldout Only setting to every Container in the active Base.

---

## Base & Container Configuration 

Selecting a Base or Container and opening the Unity **Inspector** shows a dedicated panel.

### Container Inspector

- Set emoji and color.
- Set **Preferred Child View** (Foldouts or Tabs) — controls how children appear in [Quick Peek](05_Pro_Features.md#quick-peek).
- Set **Container Expand** and **Child View Expand** — each uses the same three options:

| Option | Behavior |
|---|---|
| Expand | Starts expanded on first view |
| Collapse | Starts collapsed on first view |
| Always | Always expands, ignoring any saved foldout state |

**Container Expand** controls the container node's own expand state in the tree view. **Child View Expand** controls whether children start expanded or collapsed inside Quick Peek when no saved per-item foldout state exists.

- Reorder children directly in the inspector list.
- Enable **Disable Quick Peek** *(PRO)* to suppress hover previews for this Container and all its descendants.

### Base Inspector

- Set emoji and color.
- *(PRO)* Set the **Scene Link** — object picker or **Open Scenes** dropdown. See [PRO: Scene Linking](05_Pro_Features.md#scene-linking).

---

## Settings

Open via the **⚙** toolbar button. Settings are grouped into collapsible sections.

**Interaction**

| Setting | Default | Description |
|---|---|---|
| Double Click Action | Enter | What double-clicking a tree item does: **Enter** (triggers the item's enter action) or **Rename** |
| Enable Quick Peek on hover *(PRO)* | On | Toggle Quick Peek hover preview globally |

**General**

| Setting | Default | Description |
|---|---|---|
| Show icons per type | On | Display type-specific icons on tree rows |
| Force naming on create | On | Show inline name field before creating; disable to create with an auto-generated name instantly |
| Expand all on enter base | Off | When enabled, all containers in a Base expand automatically when you navigate into it. Does not affect foldout state on session reopen or domain reload. |
| Font Size | 12 | Controls TreeView item font size (9–15 pt). Row height scales with the value. |

**New Container Defaults**

| Setting | Default | Description |
|---|---|---|
| Color Style | Gradient | Color style applied to newly created nodes |
| Foldout only | Off | Restrict color to the Container's header row by default on new nodes |
| Container Expand | Expand | Initial expand/collapse state for the container node itself in the tree view |
| Preferred Child View *(PRO)* | Foldouts | Fallback layout for Quick Peek — Foldouts or Tabs — used when no per-container preference has been saved |

**Scene Linking** *(PRO)*

| Setting | Default | Description |
|---|---|---|
| Enable Scene Linking | On | Toggle all Scene Linking auto-open / close globally |
| Close on scene close | Off | Auto-close the browser window when its linked scene closes |

> [!NOTE]
> **Child View Expand** is set per-container in the [Container Inspector](#container-inspector), not in the Settings popup.

> [!NOTE]
> **Preferred Child View** is the fallback only. Switching the layout inside a Quick Peek panel saves the preference back to that container permanently, overriding this default for that container.
