## Navigating the Browser

### Entering a Base

Double-click a Base (or select it and press **Return**) to navigate into it. The window title and toolbar update to show the Base name — including its emoji if one is set.

Press **Escape** to return to the Home view.

### Back and Forward

**Ctrl+Left** / **Ctrl+Right** (or the ◀ ▶ toolbar buttons) moves through your navigation history. The buttons are greyed out when there is no history in that direction.

### Escape Priority

| Context | What Escape does |
|---|---|
| Search bar is active | Clears the search field |
| Browsing inside a Base | Returns to Home |
| Quick Browse popup open | Closes the popup |

### The Tag Bar

When you are inside a Base, a tag bar appears at the bottom of the window:

- **Scene link tag** — shows the linked scene filename. Click **×** to unlink. See [PRO: Scene Linking](pro-features.md#scene-linking).
- **Default Base tag** *(PRO)* — shown when this Base is the Quick Browse default. Click **×** to clear it.

---

## Building Your Hierarchy

### Creating Items

Right-click any Container or Base and choose **Create Child**. A submenu lists all valid child types for that node.

When **Force Naming On Create** is on (the default), an inline ghost row appears:
- Type a name → press **Enter** to create.
- Press **Escape** to cancel without creating anything.
- Empty or duplicate names are rejected inline.

To create items with an auto-generated name instantly (no ghost row), disable **Force Naming On Create** in [Settings](styling-and-settings.md#settings).

### Pasting from the Clipboard

Press **Ctrl+V** inside the browser to paste the current clipboard text as a new Text Asset child of the selected Container. If the clipboard contains a URL, the asset name is set to the URL's hostname automatically.

### Adding Existing Assets

Drag any asset from Unity's **Project** window onto a Container or Base. Scene GameObjects are automatically wrapped in a Scene Object reference. See [Drag & Drop](#drag--drop) below for drop targeting details.

---

## Enter Actions

Pressing **Return** (or double-clicking when **Double-Click Action** is set to **Enter** in Settings) triggers the item's enter action:

| Item type | Enter action |
|---|---|
| Base | Navigate into it |
| Container | Expand / collapse |
| Scene Object ref | Ping and select the GameObject *(scene must be open)* |
| Scene Component ref | Select the component in the Inspector |
| Scene Asset | Open the scene; ▶ play button appears alongside |
| Prefab | Open in Prefab Stage |
| Text Asset (URL) | Open the URL in your system browser |
| Action Item | Execute the automation *(PRO)* |

---

## Working with Items

### Renaming

Right-click → **Rename** or press **Ctrl+R**. An inline edit field replaces the label.

Rename is not available for Scene Object or Scene Component references — their names reflect live scene content.

### Duplicating

Right-click → **Duplicate** or press **Ctrl+D**. A copy appears directly after the original with a unique auto-generated name.

Duplication is only available for leaf items (assets, references). Containers and Bases cannot be duplicated.

### Removing and Deleting

| Operation | What it does | Available on |
|---|---|---|
| **Delete Asset** | Permanently deletes the file from disk | All nodes except Scene refs |
| **Remove from List** | Removes the item from the hierarchy without deleting the file | Asset nodes that are not Containers or Bases |
| **Remove Link** | Removes the scene reference; does not touch the actual scene | Scene Object and Scene Component refs |

> [!WARNING]
> **Delete Asset** is permanent and cannot be undone via the asset file. Use **Remove from List** if you want to unlink an asset without destroying it.

If **Ask Before Deletion** or **Ask Before Remove** is enabled in [Settings](styling-and-settings.md#settings), a confirmation dialog appears.

### Properties

Right-click → **Properties** opens the Unity Properties window for the selected asset. For Scene Object / Component refs, it opens on the resolved live object if the scene is currently loaded.

### Undo and Redo

All operations — create, rename, delete, duplicate, reparent, move, and navigation — are integrated with Unity's undo system (**Ctrl+Z** / **Ctrl+Shift+Z**).

---

## Drag & Drop

### Reordering Within the Tree

Drag any row to reorder it within its parent or move it into a different Container. A drop indicator shows the exact insertion point. The drag only activates after you exceed a short distance threshold, so single-click selection is not affected.

Releasing outside the window or losing focus cancels the drag with no changes.

### Dropping from the Project Window

| Drop target | Result |
|---|---|
| Onto a Container | Asset added inside the Container |
| Between rows | Asset inserted at that position in the parent |
| Onto an empty Base | A default Container is auto-created; asset placed inside |

Scene GameObjects dragged from the Hierarchy are wrapped in a Scene Object reference. Scene components are wrapped in a Scene Component reference.

> [!WARNING]
> Assets from scenes that have never been saved to disk are rejected with a notification and not added.

### Move to a Different Base *(PRO)*

Right-click a Container → **Move to → `{BaseName}`** to move it and all its contents to another Base. This option appears only when you are inside a Base and at least one other Base exists.

The move is fully undoable.
