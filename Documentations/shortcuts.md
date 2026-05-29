## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **Up / Down** | Move selection one row |
| **Left** | Collapse Container foldout |
| **Right** | Expand Container foldout |
| **Alt+Left** | Recursively collapse all children |
| **Alt+Right** | Recursively expand all children |
| **Return** | Toggle Container / trigger Enter action on leaf |
| **Ctrl+A** | Select all visible rows |
| **Ctrl+D** | Duplicate selected item |
| **Ctrl+R** | Rename selected item |
| **Ctrl+V** | Paste clipboard text as new Text Asset child |
| **Ctrl+Left** | Navigate back |
| **Ctrl+Right** | Navigate forward |
| **Delete / Backspace** | Delete selected item |
| **Escape** | Clear search → go home → close popup *(priority order)* |
| **Down** *(in search bar)* | Move focus to first matching result |
| **Enter** *(in search bar)* | Exit search mode |
| **Ctrl+Space** *(Win/Linux)* | Toggle Quick Browse popup *(PRO)* |
| **Option+Space** *(Mac)* | Toggle Quick Browse popup *(PRO)* |

---

## Free vs PRO

| Capability | Free | PRO |
|---|---|---|
| Browser windows | 1 | Unlimited |
| Bases (workspaces) | 1 | Unlimited |
| Move items between Bases | — | ✓ |
| Quick Peek hover preview | — | ✓ |
| Quick Browse keyboard popup | — | ✓ |
| Scene Linking (auto-open / close) | — | ✓ |
| Action Items | — | ✓ |
| Container Children Inspector | — | ✓ |
| Core browsing, search, styling, undo | ✓ | ✓ |

---

## What This Guide Leaves Out

The following topics appear in the codebase or feature summary but are omitted because they are internal implementation details or developer extension points, not end-user concerns:

| Omitted topic | Why omitted |
|---|---|
| `TypedContainer<T>` API | Developer API for strongly-typed custom containers in code — not configurable from the editor UI |
| `[CreateChild]` attribute / `IHasCreateChildOption` | Developer APIs for registering custom child types in the Create Child menu |
| `ActionItem` subclassing (`ActionPath`, `GetDetailDisplay`, `DefaultName`, `EditorIcon`) | Developer extension points for building custom Action Items |
| Internal state files (`FocusHistorySO`, `SelectionStateSO`, `FoldoutState`) | Managed automatically under `Assets/Settings/`; delete to reset UI state if something goes wrong, not user-configurable |
| Motherbase `initializationProgress` bitmask | Internal first-run tracking — not user-configurable |
| Sub-asset file layout | Containers are embedded sub-assets inside their Base's `.asset` file — managed automatically; do not reorganize manually |
| `SceneObjectMap` rebuild | Internal cache rebuilt automatically on domain reload |
| DEV build PRO toggle on the Upgrade link | Relevant only during plugin development |
| Quick Peek hover delay and fade timing | Fixed internal values — not configurable |
| Quick Peek popup pixel dimensions | Implementation detail with no user-facing configuration |
| Detail Panel (toolbar Show/Hide Details button) | Exists in code but is unfinished and not exposed in any released build |
