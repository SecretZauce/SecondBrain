## Opening SecondBrain

Go to **Window → SecondBrainWindow** in the Unity menu bar.

The browser opens to the top-level view (Home). From here, double-click a Base workspace to enter it. Your last open workspace is remembered between editor sessions and survives domain reloads.

> [!NOTE]
> In the free tier only one browser window can be open at a time.

---

## First-Time Setup

On first launch, SecondBrain runs a one-time initialization flow that:

1. Creates a default **Profile** asset at `Assets/Resources/Editor/Default Profile.asset` — the root of your hierarchy.
2. Creates a default **"My Workspace"** Base so you have somewhere to start immediately.
3. Detects whether SecondBrain Pro is installed and activates it automatically if found.
4. Opens the **Installer Window** — your starting point for links to documentation, Discord, and the GitHub changelog.

> [!NOTE]
> *(PRO)* Use the **Editor / Build** toggle in the browser header to move a Profile between editor-only storage (`Assets/Resources/Editor/`) and in-build storage (`Assets/Resources/`). Do not move Profile assets manually in the Project window.

With **PRO**, you can create as many Bases as you need — one per scene, one per discipline, or however your project is organized.

### Installer Window

The Installer Window opens automatically after setup. You can also reopen it any time via **Window → Second Brain → Installer**. It shows:

- Your installed version, edition (Free / Pro), and status as capsule tags.
- Links to Documentation, Discord, and the GitHub changelog.
- A button to open the SecondBrain browser window.
- If you have only the free package installed, a prompt to upgrade to PRO.
- If you have only the Pro package installed (free package missing), a button to install the free package automatically via the Package Manager.

---

## What to Do Next

- Understand the hierarchy → [Data Structure](data-structure.md)
- Start adding content → [Browsing](browsing.md)
- Style your nodes → [Styling & Settings](styling-and-settings.md)
- Explore PRO features → [PRO Features](pro-features.md)
