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

## Organize However You Want

The hierarchy is based on 3-level structure: Profiles > Bases > Containers

```
Profile ( Workspace Separation )
  └── Base  ( Topic Separation )
        └── Container  ( Parent of the references — nestable to any depth)
              ├── Container
              ├── Any asset or reference
              └── Action Item  [PRO]
```
- **Profiles** are Swappable workspace per device. it allows each team member to work and modify their own custom hierarchy independently from each other - e.g. "Tech Artist's Workspace", "Dev's Workspace", "James's Workspace" etc.
- **Bases** are the roots of each hierarchy. Think of it like a "Cabinet" or "Folder" that containing your custom hierarchy tree which help you separate concerns cleanly - e.g. "Level Design", "Audio", "UI"
- **Containers** are groups that contain your actual references dragged in from outside the SecondBrain window. You can add in amy supported asset types from ahove table or nest Containers inside each other freely. Custom emoji and color are supported.

> [!NOTE]
**No duplicates.**  <br>
> SecondBrain prevents adding the same item twice — either within the same Container or anywhere else in the tree. 

See [Data Structure](data-structure.md) and [Building Your Hierarchy](browsing.md#building-your-hierarchy) for more details.

---

## Move Faster with Ease 

### Quick Peek *(PRO)*

<img alt="Quick Peek hover preview" src="gifs/quick-peek.gif" width="600"/>

Hover over the **left or right edge** of any row to see a floating preview panel without navigating away. Containers show their children in a compact layout; scene objects show their component list; any other asset shows its Inspector inline if available.

### Quick Browse *(PRO)*

<img alt="Quick Browse floating popup" src="gifs/quick-browse.gif" width="600"/>

**Alt+Q** *(Win/Linux)* / **Option+Q** *(Mac)* opens a floating browser window centered on the editor — from anywhere, any time. Start typing and the search bar is already focused. Press **Alt+Q** again or Press ESC twice to close.

### Scene Linking *(PRO)*

Link a Base to a scene. When that scene opens in the editor, SecondBrain opens straight to that workspace automatically. No manual switching.

### Enter Actions

Every item type knows what to do when you press **Return** or Double-Click on them.

| Item | What happens |
|---|---|
| Base | Navigate in |
| Container | Toggle expand / collapse |
| Scene Object | Ping and select in the Hierarchy |
| Scene Component | Select in the Inspector |
| Scene | Open the scene |
| Prefab | Open in Prefab Stage |
| URL text file | Open in system browser |
| Action Item | Execute *(PRO)* |

### Keyboard Navigation

Full keyboard navigation: **↑ / ↓** to move selection, **← / →** to collapse/expand containers, **Alt+← / Alt+→** to recursively collapse/expand, **Ctrl+Z / Ctrl+Shift+Z** for undo/redo, **Ctrl+R** to rename, **Ctrl+D** to duplicate, **Ctrl+Left / Ctrl+Right** for back/forward history, and more. See the full [Keyboard Shortcuts](shortcuts.md) reference.

---

## Customization — Make It Yours

### Visual Styling

Right-click any Container or Base to set an emoji (or any built-in Unity editor icon) and a label color. Four color styles let you decide how prominent the highlight is:

| Style | Effect |
|---|---|
| Gradient | Gradient wash across the row |
| Font Color | Tints the label text |
| Circle Dot | Small colored circle beside the name |
| Background | Fills the entire row |

Multi-select several nodes and style them all at once.

### Profiles *(PRO)*

PRO gives you unlimited Profiles — each a completely separate hierarchy root. Use one Profile per team member for personalized workspaces, share a common Profile across the team while each person keeps their own private one alongside it, or separate entirely different project contexts. Profiles can be stored editor-only or included in player builds.

### Settings

Tune SecondBrain's behavior to your workflow — confirmation dialogs, row height, double-click action, naming-on-create, default expand state, and more. PRO adds Quick Peek layout, Scene Linking, and per-Base override controls.

See [Styling and Settings](styling-and-settings.md).

---

## Free vs PRO at a Glance

| Capability | Free | PRO |
|---|---|---|
| Browser windows | 1 | Unlimited |
| Profiles | 1 | Unlimited |
| Bases (workspaces) | 1 | Unlimited |
| Scenes and Assets (drag & drop) | ✓ | ✓ |
| Scene Object references | ✓ | ✓ |
| Component references | ✓ | ✓ |
| Real-time search | ✓ | ✓ |
| Full undo / redo | ✓ | ✓ |
| Keyboard navigation | ✓ | ✓ |
| Core browsing, styling | ✓ | ✓ |
| Move items between Bases | — | ✓ |
| Quick Peek hover preview | — | ✓ |
| Quick Browse keyboard popup | — | ✓ |
| Scene Linking (auto-open / close) | — | ✓ |
| Action Items | — | ✓ |

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
