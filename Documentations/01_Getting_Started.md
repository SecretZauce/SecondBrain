## Opening SecondBrain

Go to **Window → Second Brain (Home)** in the Unity menu bar.

The browser opens to the top-level view (Home). From here, double-click a Base workspace (or click the **▶** arrow beside it) to navigate in. The window remembers your last open location between editor sessions and survives domain reloads.

> [!NOTE]
> In the free tier, only one browser window can be open at a time, and you have one Profile with one Base.

---

## First-Time Setup

On first launch, SecondBrain runs a one-time initialization flow that:

1. Creates a default **Profile** asset at `Assets/Resources/Editor/Default Profile.asset` — the root of your hierarchy.
2. Creates a default **"My Workspace"** Base so you have somewhere to start immediately.
3. Detects whether SecondBrain Pro is installed and activates it automatically if found.
4. Opens the **Installer Window** — your starting point for links to documentation, Discord, and the GitHub changelog.

> [!NOTE]
> If you have only the PRO package and have not yet imported the free package into this project, the Installer Window will prompt you to install the free package first via the Package Manager before PRO can activate.

> [!NOTE]
> *(PRO)* Use the **Editor / Build** toggle in the browser header to move a Profile between editor-only storage (`Assets/Resources/Editor/`) and in-build storage (`Assets/Resources/`). Do not move Profile assets manually in the Project window.

With **PRO**, you can create unlimited Profiles, Bases, and browser windows — ideal for team projects where each person can maintain a personalized Profile alongside a shared team Profile.

### Installer Window

<img alt="SecondBrain Installer Window" src="gifs/installer-window.png" width="600"/>

The Installer Window opens automatically after setup. You can also reopen it any time via **Tools → Second Brain → Installer**. It shows:

- Your installed version, edition (Free / Pro), and status as capsule tags.
- A button to open the SecondBrain browser window.
- If you have only the free package installed, a prompt to upgrade to PRO.
- If you have only the Pro package installed (free package missing), a button to install the free package automatically via the Package Manager.

---

## Getting Around

<img alt="Dragging items into SecondBrain from various sources" src="gifs/drag-items.gif" width="600"/>

Once the window is open, you can start filling it immediately:

- **Drag anything in** — drop assets from the Project window, GameObjects or scene names from the Hierarchy, or components from the Inspector directly onto a Container or Base. You can even drag while the SecondBrain tab is not focused: hover over the tab in the dock during a drag, wait for the window to appear, then drop at the target position.
- **Make groups** — You can click the **+** button that appears on the toolbar of the SecondBrainWindow to create a new Container at depth zero. Or press **+** at the right side of the selected row to created a Child Container.
- **Drag to reorder** — drag any row up or down to reorder it within its parent, or drag it onto a different Container to move it there.
- **Keyboard shortcuts** — use **↑ / ↓** to navigate rows, **Return** to enter a Base or execute an action, **Ctrl+R** to rename, **Ctrl+Z** to undo. See [Keyboard Shortcuts](07_Shortcuts.md) for the full list.

---

## What to Do Next

- Understand the hierarchy → [Data Structure](02_Data_Structure.md)
- Start adding content → [Browsing](03_Browsing.md)
- Style your nodes → [Styling & Settings](04_Styling_And_Settings.md)
- Explore PRO features → [PRO Features](05_Pro_Features.md)
