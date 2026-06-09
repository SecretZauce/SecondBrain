## Opening SecondBrain

Go to **Window → Second Brain (Home)** in the Unity menu bar.

The browser opens to the top-level view (Home). From here, double-click a Base workspace (or click the **▶** arrow beside it) to navigate in. Your last open workspace is remembered between editor sessions and survives domain reloads.

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
> *(PRO)* Use the **Editor / Build** toggle in the browser header to move a Profile between editor-only storage (`Assets/Resources/Editor/`) and in-build storage (`Assets/Resources/`). Do not move Profile assets manually in the Project window.

With **PRO**, you can create unlimited Profiles, Bases, and browser windows — ideal for team projects where each person can maintain a personalized Profile alongside a shared team Profile.

### Installer Window

The Installer Window opens automatically after setup. You can also reopen it any time via **Tools → Second Brain → Installer**. It shows:

- Your installed version, edition (Free / Pro), and status as capsule tags.
- Links to Documentation, Discord, and the GitHub changelog.
- A button to open the SecondBrain browser window.
- If you have only the free package installed, a prompt to upgrade to PRO.
- If you have only the Pro package installed (free package missing), a button to install the free package automatically via the Package Manager.

---

## Getting Around

<img alt="Dragging items into SecondBrain from various sources" src="gifs/drag-items.gif" width="600"/>

Once the window is open, you can start filling it immediately:

- **Drag anything in** — drop assets from the Project window, GameObjects or scene names from the Hierarchy, or components from the Inspector directly onto a Container or Base. You can even drag while the SecondBrain tab is not focused: hover over the tab in the dock during a drag, wait for the window to appear, then drop at the target position.
- **Make groups** — right-click a Container or Base and choose **Create Child → Container** to create a nested folder.
- **Drag to reorder** — drag any row up or down to reorder it within its parent, or drag it onto a different Container to move it there.
- **Keyboard shortcuts** — use **↑ / ↓** to navigate rows, **Return** to enter a Base or execute an action, **Ctrl+R** to rename, **Ctrl+Z** to undo. See [Keyboard Shortcuts](shortcuts.md) for the full list.

---

## What to Do Next

- Understand the hierarchy → [Data Structure](data-structure.md)
- Start adding content → [Browsing](browsing.md)
- Style your nodes → [Styling & Settings](styling-and-settings.md)
- Explore PRO features → [PRO Features](pro-features.md)

---

## Version Compatibility and Licensing

SecondBrain free and PRO are versioned together. When both packages are installed, their versions must match. If they fall out of sync — for example, after updating only one package — a **Version Mismatch** dialog appears on the next editor reload. It identifies which package is ahead and links you to the correct update so you can bring them back into alignment.

**Free** — full browsing, drag & drop, search, styling, undo/redo, and keyboard navigation, with one Profile, one Base, and one window.

**PRO** — everything in Free, plus unlimited Profiles, Bases, and windows; Quick Peek, Quick Browse, Scene Linking, Action Items, cross-Base moves, and multi-user workflow support.
