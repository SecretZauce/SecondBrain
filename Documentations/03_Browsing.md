## Navigating the Browser

### Entering a Base

<img alt="Entering a Base by double-clicking or clicking the arrow" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/enter-base.gif" width="600"/>

Double-click a Base, click the **>** arrow on the right side of its row, or select it and press **Return** to navigate into it. The window title and toolbar update to show the Base name — including its emoji if one is set.

> [!NOTE]
> By default, double-clicking a leaf item triggers its **Enter** action. If you prefer double-click to rename instead, change **Double-Click Action** to **Rename** in [Settings](04_Styling_And_Settings.md).

Press **Escape** to return to the Home view.

### Back and Forward

**Ctrl+Left** / **Ctrl+Right** (or the < > toolbar buttons) moves through your navigation history. The buttons are greyed out when there is no history in that direction.

### Escape Priority

| Context | What Escape does |
|---|---|
| Search bar is active | Clears the search field |
| Browsing inside a Base | Returns to Home |
| Quick Browse popup open | Closes the popup |

### The Tag Bar

When you are inside a Base, a tag bar appears at the bottom of the window:

- **Scene link tag** — shows the linked scene filename. Click **×** to unlink. 
- **Default Base tag** *(PRO)* — shown when this Base is the Quick Browse default. When set, Quick Browse navigates straight to this Base on open instead of showing the Home view. Click **×** to clear it.

---

## Building Your Hierarchy

### Adding Existing Assets

<img alt="Dragging assets from Project, Hierarchy, and Inspector" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/drag-drop.gif" width="600"/>

Drag any asset from Unity's **Project** window onto a Container or Base. You can also drag from:

- **Hierarchy** — GameObjects are automatically wrapped in Scene Object references; scene names create scene references.
- **Inspector** — drag a component header to create a Scene Component reference.
- **Across tabs** — hover the SecondBrain tab in the dock while dragging (without needing to focus it first) and drop at the target position inside the tree.

### Creating Items

<img alt="Creating a child item via right-click context menu" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/create-items.gif" width="600"/>

Right-click any Container or Base and choose **Create Child**. A submenu lists all valid child types for that node. You can also click the **+** button that appears at the right side of a selected row to create a new child Container directly.

When **Force naming on create** is on (the default), an inline ghost row appears:
- Type a name → press **Enter** to create.
- Press **Escape** to cancel without creating anything.
- Empty or duplicate names are rejected inline.

To create items with an auto-generated name instantly (no ghost row), disable **Force naming on create** in [Settings](04_Styling_And_Settings.md).

### Creating a New Container

Press **Ctrl+N** to create a new Container:

- If a **Container** is selected → a child Container is created inside it.
- Otherwise (no selection, or a leaf item is selected) → a new Container is created at the root level of the current view.

### Grouping Items

Select one or more items and press **Ctrl+G** to wrap them in a new Container:

- If all selected items share the **same parent** → the new Container is created under that parent, and the items are moved into it.
- If items come from **different parents** → the new Container is created at root level, and all items are moved into it.

The new Container is automatically selected and expanded after the operation. The action is fully undoable.

### Pasting from the Clipboard

Press **Ctrl+V** inside the browser to paste the current clipboard text as a new Text Asset child of the selected Container. If the clipboard contains a URL, the asset name is set to the URL's hostname automatically.

---

## Enter Actions

Pressing **Return** (or double-clicking when **Double-Click Action** is set to **Enter** in Settings) triggers the item's enter action:

| Item type | Enter action |
|---|---|
| Base | Navigate into it |
| Container | Toggle expand / collapse |
| Scene Object ref | If scene is open: open a floating inspector for the GameObject. If scene is not open: open the scene and ping the object. |
| Scene Component ref | If scene is open: open a floating inspector for the Component. If scene is not open: open the scene and ping the object. |
| Scene | Open the scene |
| Prefab | Open in Prefab Stage |
| URLs | Open in system's browser |
| Action Item | Execute *(PRO)* |

### Focus Toggles

<img alt="Focus toggle" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/focus-toggle.gif" width="600"/>

Several item types display a small focus icon on the right side of their row. Clicking it toggles focus-on-select behaviour for that specific item. The icon turns blue when enabled.

**Scene Object refs and Scene Component refs** (only visible when the scene is open):

- **Off (dim)** — selecting the ref updates Unity's selection but leaves the Scene View camera where it is.
- **On (blue)** — selecting the ref also frames the Scene View camera on the referenced object, the same as pressing **F** in the Scene View.

The toggle is saved on the asset itself and persists across sessions.

**Folder assets**:

- **Off (dim)** — selecting the folder highlights it in the Project window but does not navigate into it.
- **On (blue)** — selecting the folder automatically navigates the Project window into it, the same as pressing **Return**.

The toggle is per-folder and stored in EditorPrefs.

---

## Working with Items

### Renaming

Right-click → **Rename** or press **Ctrl+R**. An inline edit field replaces the label.

Rename is not available for Scene Object or Scene Component references — their names reflect live scene content.

### Duplicating

Right-click → **Duplicate** or press **Ctrl+D**. A copy appears directly after the original with a unique auto-generated name.

Duplication is only available for leaf items (assets, references). Containers and Bases cannot be duplicated.

### Removing Items

Right-click any item and choose **Remove**, or select it and press **Delete** / **Backspace**.

What happens depends on how the item is stored:

| Item type | What Remove does |
|---|---|
| Container, Action Item, or any asset embedded directly in the profile | Destroyed and removed from the profile file |
| A linked asset with its own file (e.g. a Scene Object ref saved as a separate `.asset`) | Unlinked only — the file stays on disk |

Both cases are **fully undoable** with **Ctrl+Z**. For anything that can't be undone in-editor, use **git discard** to restore disk state.

### Properties

Right-click → **Properties** opens the Unity Properties window for the selected asset. For Scene Object / Component refs, it opens on the resolved live object if the scene is currently loaded.

### Undo and Redo

All operations — create, rename, delete, duplicate, reparent, move, and navigation — are integrated with Unity's undo system (**Ctrl+Z** / **Ctrl+Shift+Z**).

---

## Drag & Drop

### Reordering Within the Tree

<img alt="Reordering items with drag and drop" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/reorder.gif" width="600"/>

Drag any row to reorder it within its parent or move it into a different Container. A drop indicator shows the exact insertion point. The drag only activates after you exceed a short distance threshold, so single-click selection is not affected.

### Dropping from the Project Window

| Drop target | Result |
|---|---|
| Onto a Container | Asset added inside the Container |
| Between rows | Asset inserted at that position in the parent |
| Onto an empty Base | A default Container is auto-created; asset placed inside |

Scene GameObjects dragged from the Hierarchy are wrapped in a Scene Object reference. Scene components are wrapped in a Scene Component reference.

> [!WARNING]
> Assets from scenes that have never been saved to disk are rejected with a notification and not added.

### Dragging Out *(PRO)*

<img alt="Dragging an item from SecondBrain into the Scene View" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/drag-out.gif" width="600"/>

Items can be dragged back out of SecondBrain to other Unity windows:

- **Another SecondBrain window** — drop onto a Container or between rows to move the item across windows.
- **Scene View** — places the asset at the cursor position in the scene. Only asset types Unity supports dropping into the Scene View work here (e.g. Prefabs, Materials).

### Move to a Different Base *(PRO)*

Right-click a Container → **Move to → `{BaseName}`** to move it and all its contents to another Base. This option appears only when you are inside a Base and at least one other Base exists.

The move is fully undoable.
