## Selection

| Input | Result |
|---|---|
| Click | Select one item; deselect everything else |
| **Ctrl+Click** | Add to or remove from the current selection |
| **Shift+Click** | Range-select from the last-clicked item to this one |
| **Ctrl+A** | Select all currently visible rows |
| **Up / Down** arrow | Move selection one row |

Selecting items in SecondBrain updates Unity's own selection (Inspector, Project window) in sync.

Selection is cleared automatically when you navigate to a different node.

---

## Search

<img alt="Real-time search filtering the tree" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/search.gif" width="600"/>

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

---

## Search Filter

Click the **filter button** (funnel icon) beside the search bar to open the Search Filter popup. It lets you restrict which node types appear in search results.

Available filter flags:

| Flag | What it includes |
|---|---|
| Containers | Container nodes |
| Scene Objects | Scene Object references |
| Scenes | Scene asset references |
| Text Assets | Text file references |
| Scriptable Objects | ScriptableObject asset references |
| Prefabs | Prefab asset references |
| Assets | All other asset references |
| All | Everything (equivalent to all flags enabled) |

The default filter includes all types **except Containers**, keeping results focused on leaf content. Toggle individual flags to narrow the search to just the types you care about.

The filter button is **highlighted** when a non-default filter is active, so you can see at a glance that results are being restricted. Your filter choice is saved in EditorPrefs and persists between sessions.
