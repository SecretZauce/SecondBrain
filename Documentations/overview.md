SecondBrain is a Unity **editor tool** that gives you a second hierarchy window for your project — organize assets, prefabs, scenes, GameObjects, and custom actions in a structured, searchable window built for faster access and better productivity. Nothing is shipped to your built game.

---

## Flexibility — Store Anything That Matters

SecondBrain is a universal project organizer built around Unity's asset system.

**Drag in anything from your project:**

| What you drag | What SecondBrain stores |
|---|---|
| Prefab, material, texture, audio clip | Direct reference to the asset file |
| Scene file | Reference + a ▶ play button to open it |
| Text file | Reference; URLs open in your system browser on Enter |
| GameObject from the Hierarchy | Scene Object reference — retains name even when scene is closed |
| Component from the Inspector | Scene Component reference — opens in the Inspector on Enter |
| Action Item *(PRO)* | ScriptableObject-based executable action — run with a single keypress |

**Developers can go further:**

`TypedContainer<T>` lets you create strongly-typed containers that only accept a specific asset type, and `ActionItem` is fully subclassable for custom executable actions.

---

## Organization — Structure Work Exactly How You Think

The hierarchy is simple and infinitely flexible:

```
Profile  (project root)
  └── Base  (workspace — e.g. "Level Design", "Audio", "UI")
        └── Container  (folder — nestable to any depth)
              ├── Container
              ├── Any asset or reference
              └── Action Item  [PRO]
```

- **Bases** are top-level workspaces. Separate concerns cleanly — one Base per feature, discipline, or scene. Link a Base to a scene so it opens automatically when that scene loads (PRO).
- **Containers** are folders you nest freely. Give each one a name, an emoji, and a color so your team can scan the tree at a glance.
- **No duplicates.** SecondBrain prevents adding the same item twice — either within the same Container or anywhere in the tree — so the hierarchy stays a reliable single source of truth.

See [Data Structure](data-structure.md) and [Building Your Hierarchy](browsing.md#building-your-hierarchy).

---

## Productivity — Stay in Flow

### Quick Peek *(PRO)*

<img alt="Quick Peek hover preview" src="gifs/quick-peek.gif" width="600"/>

Hover over the **left or right edge** of any row to see a floating preview panel without navigating away. Containers show their children in a compact layout; scene objects show their component list; any other asset shows its Inspector inline.

### Quick Browse *(PRO)*

<img alt="Quick Browse floating popup" src="gifs/quick-browse.gif" width="600"/>

**Shift+Q** opens a floating browser window centered on the editor — from anywhere, any time. Start typing and the search bar is already focused. Press **Shift+Q** again or click away to dismiss.

### Scene Linking *(PRO)*

Link a Base to a scene. When that scene opens in the editor, SecondBrain opens straight to that workspace automatically. No manual switching.

### Enter Actions

Every item type knows what to do when you press **Return**:

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
