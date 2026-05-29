## Selection

| Input | Result |
|---|---|
| Click | Select one item; deselect everything else |
| **Ctrl+Click** | Add to or remove from the current selection |
| **Shift+Click** | Range-select from the last-clicked item to this one |
| **Ctrl+A** | Select all currently visible rows |
| **Up / Down** arrow | Move selection one row |

Selecting items in SecondBrain updates Unity's own selection (Inspector, Project window) in sync. Clicking outside the browser in Unity can clear the browser's selection — this is expected behavior.

Selection is cleared automatically when you navigate to a different node.

---

## Search

Type in the search bar at the top of the toolbar to filter the tree in real time. Search is:
- **Case-insensitive**
- **Substring match** — matches any part of a node's name
- **Universal** — searches Containers, asset references, and Action Items

| Input | Result |
|---|---|
| **Down arrow** (in search bar) | Move keyboard focus to the first matching result |
| **Enter** (in search bar) | Exit search mode; keep the current filter |
| **Escape** | Clear the search and return focus to the tree |

The search text is preserved through tree refreshes during the same editor session.
