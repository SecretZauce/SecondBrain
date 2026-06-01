All features on this page require the **PRO** edition of SecondBrain.

---

## Multiple Windows

Click the **+** toolbar button to open additional browser windows. Each window tracks its own navigation history, foldout state, and selection independently.

A **Container Children Inspector** window is also available: it opens a full standalone editor view for a Container's children that shares foldout state with the main browser and [Quick Peek](#quick-peek).

---

## Quick Peek

Hover over any tree row to see a floating preview panel alongside it — without navigating away.

| Node hovered | Panel shows |
|---|---|
| Container | Children in a Tabs or Foldouts layout |
| Base | The Base's own inspector inline |
| Scene Object ref | The referenced GameObject's components in a Tabs or Foldouts layout |
| Any other asset | The Unity Inspector inline |

### Tabs and Foldouts mode

The panel header contains a layout toggle (tab icon / foldout icon) to switch between **Tabs** and **Foldouts** mode.

**Tabs mode** — each child or component is a selectable tab. Long names are ellipsized; hovering a tab shows the full name as a tooltip. The selected tab index is remembered per item between sessions.

**Foldouts mode** — children are shown as expandable foldout rows. An **Expand / Collapse All** button appears in the panel header.

- Switching the layout for a **Container** saves the new preference back to that container's **Preferred Child View** field (undo-supported). The main browser tree and Container Children Inspector immediately reflect the change.
- Switching the layout for a **Scene Object ref** persists the choice per scene object in editor preferences.
- Foldout expand/collapse states are persisted per item and shared with the [Container Children Inspector](pro-features.md#multiple-windows) and the main browser window.

> [!NOTE]
> In Foldouts mode, nested Containers and Scene Assets appear as non-expandable rows with a **▶** button. Clicking it opens the Container Children Inspector or opens the scene.

**Other behavior:**
- Moving the cursor away dismisses the panel immediately.
- Quick Peek is suppressed while a drag is in progress.
- Double-clicking the panel's header opens the full Unity editor window for that asset.

**Disabling Quick Peek:**

| Method | Scope |
|---|---|
| Turn off **Enable Quick Peek** in [Settings](styling-and-settings.md#settings) | Globally — all Containers |
| Enable **Disable Quick Peek** in the Container Inspector | Per-Container and all its descendants |

**Layout defaults** are set under **New Container Defaults** in [Settings](styling-and-settings.md#settings):
- **Preferred Child View** — the fallback layout (Tabs or Foldouts) when no per-container preference has been saved.
- **Child View Expand** — whether foldout items start expanded or collapsed when no saved per-item state exists.

---

## Quick Browse

**Ctrl+Space** (Windows / Linux) or **Option+Space** (Mac) opens a floating browser window centered on the Unity editor — from anywhere, any time.

- The search bar is focused automatically on open. Start typing to filter immediately.
- Press the shortcut again, click outside the popup, or press **Escape** to close it.
- If a **Default Base** is set, Quick Browse navigates straight to it on open.

**Setting a Default Base:**

Select the Motherbase asset in the Project window and assign a Base in the **Default Base** field in the Inspector.

Alternatively, navigate into the Base you want to set as default, open the [Tag Bar](browsing.md#the-tag-bar), and use the Default Base controls there.

---

## Scene Linking

Link a Base to a Unity scene so that the browser opens automatically to that workspace when the scene loads.

**To link a scene:**
1. Select any node inside the Base (or select the Base itself) and open the Inspector.
2. In the **Base Inspector**, use the **Scene Link** object picker to choose a scene asset, or pick from the **Open Scenes** dropdown.

**To unlink:** Click **×** beside the scene tag at the bottom of the browser, or use **Clear** in the Base Inspector.

**Behavior:**

| Event | Result |
|---|---|
| Linked scene opens | Browser auto-opens to that Base |
| Linked scene closes | Browser closes *(only if **Close on Scene Close** is enabled in Settings)* |
| Multiple Bases linked to the same scene | All open as tabs in the same dock area |
| Same Base already open | Existing window is reused, not duplicated |

> [!WARNING]
> If **Enable Scene Linking** is off in [Settings](styling-and-settings.md#settings), auto-open and auto-close do not fire even if a link is set. A warning banner appears in the Base Inspector when this is the case.

---

## Action Items

Action Items are ScriptableObject-based automations that live in your hierarchy like any other item. Developers create them by subclassing `ActionItem`; users simply run them.

**To run an Action Item:** Select it and press **Return**, double-click it (when **Double-Click Action** is set to **Enter**), or right-click → **Execute**.

**Built-in Action Items:**

| Action | Location in Create Child menu | What it does |
|---|---|---|
| Change Unity Layout | Editor / Layout | Switches the editor window layout |
| Enter Play Mode | Editor / Play Mode | Enters Unity Play Mode |
| Open Editor Window | Editor / Windows | Opens a chosen Unity editor window |

Action Items appear in the **Create Child** submenu grouped under their category path.

> [!NOTE]
> The `ActionItem` class is subclassable. Teams can ship custom automations (CI triggers, asset validators, build scripts) directly inside the SecondBrain hierarchy alongside the content they operate on.
