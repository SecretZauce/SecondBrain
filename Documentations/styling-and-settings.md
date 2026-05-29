## Styling — Emoji and Colors

Styling options are available on Containers and Bases. Right-click a node to access them. When multiple nodes are selected, the style is applied to all of them simultaneously.

### Emoji / Icon

Right-click → **Set Emoji Icon** opens the icon picker.

- **Emojis tab** — browse by category or search by name.
- **Editor Icons tab** — use any built-in Unity editor icon.
- **Last Used tab** — quick access to the 30 most recently used icons per mode (emojis and editor icons tracked separately).
- Clearing the icon reverts the node to its default editor icon.
- With no node selected, clicking an icon copies it to the clipboard instead of applying it.

### Label Color

Right-click → **Set Color** opens the color tray.

- Choose from **preset swatches** or create **custom colors** (up to 12, newest first, persisted across sessions).
- To add a custom color, click **Add Color** and use the HSV picker.
- Right-click any custom swatch to remove it. The **Clear** swatch cannot be removed.
- **Reset Colors** restores built-in defaults and removes all custom colors (asks for confirmation).

### Color Style

Controls how the color appears on each row:

| Style | Appearance |
|---|---|
| Font Color | Tints the label text |
| Circle Dot | Small colored circle beside the label |
| Background | Fills the entire row background |
| Gradient | Gradient wash across the row |

**Foldout Only** restricts the color to the Container's header row and does not propagate to child rows.

**Apply Styles to All** propagates the current style and Foldout Only setting to every Container in the active Base (asks for confirmation).

---

## Inspectors

Selecting a Base or Container and opening the Unity **Inspector** shows a dedicated panel.

### Container Inspector

- Set emoji and color.
- Set **Preferred Child View** (Tabs or Foldouts) — controls how children appear in [Quick Peek](pro-features.md#quick-peek) and the Container Children Inspector.
- Set **Default Expand** behavior:

| Option | Behavior |
|---|---|
| Collapsed | Starts collapsed on first view |
| Expand as Default | Starts expanded on first view |
| Always Expand | Always expands, ignoring any saved foldout state |

- Reorder children directly in the inspector list.
- Enable **Disable Quick Peek** to suppress hover previews for this Container and all its descendants.

### Base Inspector

- Set emoji and color.
- *(PRO)* Set the **Scene Link** — object picker or **Open Scenes** dropdown. See [PRO: Scene Linking](pro-features.md#scene-linking).

---

## Settings

Open via the **⚙** toolbar button.

| Setting | Default | Description |
|---|---|---|
| Ask Before Deletion | On | Confirmation dialog before permanently deleting an asset |
| Ask Before Remove | On | Confirmation dialog before removing an item from its parent |
| Show Icons Per Type | On | Display type-specific icons on tree rows |
| Force Naming On Create | On | Show inline name field before creating; disable to create with auto-name instantly |
| Default Color Style | Font Color | Color style applied to newly created nodes |
| Default Color Foldout Only | Off | Restrict color to foldout header by default on new nodes |
| Default Expand Option | Collapsed | Initial expand state for newly created Containers |
| Double-Click Action | Rename | What double-clicking a leaf item does: **Rename** or **Enter** |
| Item Size | Medium | Row height — Tiny / Small / Medium / Large / Extra Large |
| Expand All on Enter Base | Off | When enabled, all containers in a Base expand automatically when you navigate into it. Does not affect foldout state on session reopen or domain reload. |
| Storage Location | Resources | Where `Motherbase.asset` (and all Base sub-assets) are stored. **Resources** = `Assets/Resources/` (included in player builds). **EditorResources** = `Assets/Resources/Editor/` (excluded from player builds). A **Move to …** button appears when the on-disk location does not match this setting. |
| Enable Quick Peek *(PRO)* | On | Toggle Quick Peek hover preview globally |
| Enable Scene Linking *(PRO)* | On | Toggle all Scene Linking auto-open / close globally |
| Close on Scene Close *(PRO)* | Off | Auto-close the browser window when its linked scene closes |
| Default Quick Peek Layout *(PRO)* | Foldouts | Fallback layout inside Quick Peek panels — Tabs or Foldouts — used when no per-container preference has been saved |
| Default Quick Peek Foldout State *(PRO)* | Expand as Default | Initial foldout expand state in Quick Peek and Container Children Inspector when no saved per-item state exists |

> [!NOTE]
> **Default Expand Option** sets the initial state only for *newly created* Containers. Override it per-Container in the [Container Inspector](#container-inspector).

> [!NOTE]
> **Default Quick Peek Layout** is the fallback only. Switching the layout inside a Quick Peek panel saves the preference back to that container permanently, overriding this default for that container.
