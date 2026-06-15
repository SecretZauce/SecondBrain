## A Second Hierarchy for Anything

SecondBrain brings a powerful second hierarchy window to the Unity Editor, giving you a structured and searchable way to organize Assets, Prefabs, Scenes, GameObjects, Components, and Executable Actions.
Packed with productivity boosts to help you navigate complex projects with ease, and get more done.

---

## Keep Whatever You Need

Drag or add anything from your project into the SecondBrain window to create a reference to it.

<img alt="Try Dragging items into SecondBrain window" src="gifs/drag-items.gif" width="600"/>

| What you add                                            | Source                             | What is stored                                                                    | Supported UX (Extra Magic)                                                                                                                                |
|---------------------------------------------------------|------------------------------------|-----------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| GameObjects                                             | Unity's Hierarchy                  | Scene Object reference — persists even when scene is closed                       | - Navigate to object (Open scene if not already opened)<br/>- Quick Peek: Edit GameObject from a popup window (PRO)<br/>- Force Focus Camera on Selection |
| Components                                              | Inspector Window                   | Scene Component reference — persists even when scene is closed                    | - Navigate to object (Open scene if not already opened)<br/>- Quick Peek: Edit Component from a popup window (PRO)<br/>- Force Focus Camera on Selection  |
| Assets ( Scriptable Objects, Prefabs, Materials, etc. ) | Project Window                     | Direct reference to the asset file                                                | - Quick Peek: Edit an Asset from a popup window                                                                                                           |
| Scenes                                                  | Project Window / Unity's Hierarchy | Direct reference to the asset file                                                | - Option to load up the scene (if not already opened)<br/>- Option to Enter PlayMode (if scene is loaded)                                                 |
| Folders                                                 | Project Window                     | Direct reference to the folder                                                    | - Open folder in Project window automatically on selection                                                                                                
| Typed Scriptable Object Container                       | SecondBrain Window's Context Menu  | A strongly-typed Scriptable Object container that holds children of specific type | - Add a new child Scriptable Object from a list of pre-defined templates<br/>- Quick Peek: Browse the container's contents from a popup window (PRO)      |
| Action Item (PRO)                                       | SecondBrain Window's Context Menu  | ScriptableObject-based executable action                                          | - Execute any custom actions right from the SecondBrain Window<br/>- Or execute from Quick Peek popup with parameter passing (PRO)                        |
| Copied Text or URLs                                     | Ctrl+V / Cmd+V                     | A newly created TextAsset containing your pasted content                          | - Opens URL on your browser<br/> - Quick Peek: Edit text content from a popup window (PRO)                                                                |

---

## Organize However You Like

The hierarchy is based on 3-level structure: Profiles > Bases > Containers

```
Profile ( Per-Team Member Separation - e.g. "Tom's Profile", "Amy's Profile" )
  └── Base  ( Hierarchy Separation - e.g. "Frequently Used Stuff", "Level Design Tools", "Game Systems" )
        └── Container  ( The actual parent of the references — nestable to any depth)
              ├── GameObjects / Components / Assets
              ├── Containers 
              └── Custom Actions [PRO]
```
- **Profiles** are Swappable workspace per device. it allows each team member to work and modify their own custom hierarchy independently from each other - e.g. "Tech Artist's Workspace", "Dev's Workspace", "James's Workspace" etc. Selected profile is saved per device.
- **Bases** are the roots of each hierarchy. Allow you to have multiple hierarchy trees under one profile. 
- **Containers** are groups inside a Base. A parent that contain your actual references dragged in from outside the SecondBrain window. You can add any supported assets from ahove table or nest Containers inside each other freely.

> [!NOTE]
**No duplicates.**  <br>
> SecondBrain prevents adding the same item twice — either within the same Container or anywhere else in the tree. 

See [Data Structure](data-structure.md) and [Building Your Hierarchy](browsing.md#building-your-hierarchy) for more details.

---

## Move Faster

Navigate your project faster with these productivity boosts.

### Quick Peek *(PRO)*

<img alt="Quick Peek hover preview" src="gifs/quick-peek.gif" width="600"/>

Hover over the **left or right edge** of any row to see a floating inspector popup of each asset / object if available. 
- GameObjects shows their components in a tabbed or foldout layout.
- Containers show their children in a tabbed or foldout layout.
- Assets show their inspctor / property editor with limitations for some types of assets such as materials and textures.

### Quick Browse *(PRO)*

<img alt="Quick Browse floating popup" src="gifs/quick-browse.gif" width="600"/>

**Alt+Q** *(Win/Linux)* / **Option+Q** *(Mac)* opens a floating browser window centered on the editor — from anywhere, any time. 
- Start typing and the search bar is already focused. 
- Pressing Enter will navigate into the selected item (See Enter Actions below)
- Press **Alt+Q** again or Press ESC twice to close.

### Scene Linking *(PRO)*

Link a Base to a scene. When that scene opens in the editor, SecondBrain opens straight to that workspace automatically.

### Enter Actions

Every item type knows what to do when you press **Return** or Double-Click on them.

| Item            | What happens                                                                                                |
|-----------------|-------------------------------------------------------------------------------------------------------------|
| Base            | Navigate in                                                                                                 |
| Container       | Toggle expand / collapse                                                                                    |
| Scene Object    | Open floating inspector of the GameObject OR open the scene and ping the object if the scene is not opened. |
| Scene Component | Open floating inspector of the Component OR open the scene and ping the object if the scene is not opened.  |
| Scene           | Open the scene                                                                                              |
| Prefab          | Open in Prefab Stage                                                                                        |
| URLs            | Open in system's browser                                                                                    |
| Action Item     | Execute *(PRO)*                                                                                             |

### Keyboard Navigation

Full keyboard navigation support: 
- **↑ / ↓** to move selection
- **← / →** to collapse/expand containers
- **Alt+← / Alt+→** to recursively collapse/expand
- **Ctrl+Z / Ctrl+Shift+Z** for undo/redo, **Ctrl+R** to rename
- **Ctrl+D** to duplicate
- **Ctrl+Left / Ctrl+Right** for back/forward history, and more.<br>

See the full [Keyboard Shortcuts](shortcuts.md) reference.

---

## Customize Your Experience

### Visual Styling

Right-click any Container or Base to set an emoji (or any built-in Unity editor icon) and a label color. Four color styles let you decide how prominent the highlight is:

| Style | Effect |
|---|---|
| Gradient | Gradient wash across the row |
| Font Color | Tints the label text |
| Circle Dot | Small colored circle beside the name |
| Background | Fills the entire row |

Multi-select several nodes and style them all at once.
### Settings

Tune SecondBrain's behavior to your workflow — confirmation dialogs, row height, What should happen when you double-click?, Should we force naming containers on creation?, default expand state, and more.

See [Styling and Settings](styling-and-settings.md).

---

## Free vs PRO at a Glance

| Capability                        | Free | PRO       |
|-----------------------------------|------|-----------|
| Browser windows                   | 1    | Unlimited |
| Profiles                          | 1    | Unlimited |
| Bases                             | 1    | Unlimited |
| Max Tabs / Windows                | 1    | Unlimited |
| Scenes and Assets (drag & drop)   | ✓    | ✓         |
| Scene Object references           | ✓    | ✓         |
| Component references              | ✓    | ✓         |
| Real-time search                  | ✓    | ✓         |
| Full undo / redo                  | ✓    | ✓         |
| Keyboard navigation               | ✓    | ✓         |
| Core browsing, styling            | ✓    | ✓         |
| Move items between Bases          | —    | ✓         |
| Quick Peek hover preview          | —    | ✓         |
| Quick Browse keyboard popup       | —    | ✓         |
| Scene Linking (auto-open / close) | —    | ✓         |
| Action Items                      | —    | ✓         |

---

## In This Guide

| Page | What it covers |
|---|---|
| [Getting Started](getting-started.md) | Opening the window, first-run orientation |
| [Data Structure](data-structure.md) | Profiles, Bases, Containers, asset types — the full hierarchy |
| [Browsing](browsing.md) | Navigation, creating items, drag & drop, enter actions, undo |
| [Selection & Search](selection-and-search.md) | Multi-select, range select, real-time search |
| [Styling & Settings](styling-and-settings.md) | Emoji, colors, Inspectors, all settings |
| [PRO Features](pro-features.md) | Quick Peek, Quick Browse, Scene Linking, Action Items, Multiple Windows |
| [Keyboard Shortcuts](shortcuts.md) | Full shortcut reference |
| [Upgrading to PRO](upgrading-to-pro.md) | Installing PRO and activating your license |
