<p align="center">
  <img src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/pngs/SecondBrain_SocialMedia.png" alt="SecondBrain" width="720"/>
</p>

<p align="center">
  <a href="https://github.com/SecretZauce/SecondBrain/wiki/Getting-Started">Documentation</a> &nbsp;|&nbsp;
  <a href="https://discord.gg/wzxhQS6eFc">Discord</a> &nbsp;|&nbsp;
  <a href="https://github.com/SecretZauce/SecondBrain/issues/new">Report a Bug</a> &nbsp;|&nbsp;
  <a href="https://github.com/SecretZauce/SecondBrain/blob/main/CHANGELOG.md">Changelog</a> &nbsp;|&nbsp;
  <a href="https://assetstore.unity.com/packages/slug/383598">Asset Store ↗</a>
</p>

---

## A Second Hierarchy for Anything

SecondBrain brings a powerful second hierarchy window to the Unity Editor, giving you a structured and searchable way to organize Assets, Prefabs, Scenes, GameObjects, Components, and Executable Actions.
Packed with productivity boosts to help you navigate complex projects with ease, and get more done.

> [!NOTE]
> This repository contains the free version of SecondBrain. Upgrade to [**PRO version**](https://assetstore.unity.com/packages/slug/383598) on the Unity Asset Store for more features, unlimited functionality, and long-term support.

<p>
<img src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/pngs/SecondBrain_Slideshow_1.png" width="500"/>
<img src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/pngs/SecondBrain_Slideshow_2.png" width="500"/>
</p>

## Table of Contents

- [Keep Whatever You Need](#keep-whatever-you-need)
- [Organize However You Like](#organize-however-you-like)
- [Move Faster](#move-faster)
- [Customize Your Experience](#customize-your-experience)
- [Free vs PRO](#free-vs-pro-at-a-glance)
- [Documentation](#in-this-guide)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Requirements](#requirements)
- [License](#license)

---

## Keep Whatever You Need

Drag or add anything from your project into the SecondBrain window to create a reference to it.

<img alt="Try Dragging any objects or assets into SecondBrain window" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/drag-items.gif" width="480" height="270"/>

| What you add                                            | Source                             | What is stored                                                                    | Productivity Features                                                                                                                                     |
|---------------------------------------------------------|------------------------------------|-----------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| GameObjects                                             | Unity's Hierarchy                  | Scene Object reference — persists even when scene is closed                       | - Navigate to object (Open scene if not already opened)<br/>- Quick Peek: Edit GameObject from a popup window (PRO)<br/>- Force Focus Camera on Selection |
| Components                                              | Inspector Window                   | Scene Component reference — persists even when scene is closed                    | - Navigate to object (Open scene if not already opened)<br/>- Quick Peek: Edit Component from a popup window (PRO)<br/>- Force Focus Camera on Selection  |
| Assets ( Scriptable Objects, Prefabs, Materials, etc. ) | Project Window                     | Direct reference to the asset file                                                | - Quick Peek: Edit an Asset from a popup window                                                                                                           |
| Scenes                                                  | Project Window / Unity's Hierarchy | Direct reference to the asset file                                                | - Option to load up the scene (if not already opened)<br/>- Option to Enter PlayMode (if scene is loaded)                                                 |
| Folders                                                 | Project Window                     | Direct reference to the folder                                                    | - Open folder in Project window automatically on selection                                                                                                |
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

- **Profiles** are swappable workspaces per device. They allow each team member to work and modify their own custom hierarchy independently — e.g. "Tech Artist's Workspace", "Dev's Workspace", "James's Workspace". The selected Profile is saved per device.
- **Bases** are the roots of each hierarchy. They let you have multiple hierarchy trees under one Profile.
- **Containers** are groups inside a Base — the actual parents of your references. You can add any supported asset from the table above or nest Containers inside each other freely.

> [!NOTE]
> **No duplicates.** SecondBrain prevents adding the same item twice — either within the same Container or anywhere else in the tree.

> [!NOTE]
> In the free tier, you have **one Profile** with **one Base**. Upgrade to PRO for unlimited Profiles, Bases, and windows.

See [Data Structure](https://github.com/SecretZauce/SecondBrain/wiki/Data-Structure) and [Building Your Hierarchy](https://github.com/SecretZauce/SecondBrain/wiki/Browsing#building-your-hierarchy) for more details.

---

## Move Faster

Navigate your project faster with these productivity boosts.

### Quick Peek *(PRO)*

<img alt="Take a glance at your item on hovering" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/quick-peek.gif" width="480" height="270"/>

Hover over the **left or right edge** of any row to see a floating inspector popup of each asset / object if available.
- GameObjects show their components in a tabbed or foldout layout.
- Containers show their children in a tabbed or foldout layout.
- Assets show their inspector / property editor with limitations for some types of assets such as materials and textures.

### Quick Browse *(PRO)*

<img alt="Quickly navigate to frequently used items" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/quick-browse.gif" width="480" height="270"/>

**Alt+W** *(Win/Linux)* / **Option+W** *(Mac)* opens a floating browser window centered on the editor — from anywhere, any time.
- Start typing and the search bar is already focused.
- Pressing Enter will navigate into the selected item (see Enter Actions below).
- Press **Alt+W** again or **Esc** twice to close.

### Scene Linking *(PRO)*

<img alt="Scene Linking — a Base auto-opens when its linked scene loads" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/scene-linking.gif" width="480" height="270"/>

Link a Base to a scene. When that scene opens in the editor, SecondBrain opens straight to that workspace automatically.

### Enter Actions

Every item type knows what to do when you press **Return** or double-click on it.

| Item            | What happens                                                                                                 |
|-----------------|--------------------------------------------------------------------------------------------------------------|
| Base            | Navigate in                                                                                                  |
| Container       | Toggle expand / collapse                                                                                     |
| Scene Object    | Open floating inspector of the GameObject OR open the scene and ping the object if the scene is not opened.  |
| Scene Component | Open floating inspector of the Component OR open the scene and ping the object if the scene is not opened.   |
| Scene           | Open the scene                                                                                               |
| Prefab          | Open in Prefab Stage                                                                                         |
| URLs            | Open in system's browser                                                                                     |
| Action Item     | Execute *(PRO)*                                                                                              |

### Keyboard Navigation

Full keyboard navigation support:
- **↑ / ↓** to move selection
- **← / →** to collapse/expand containers
- **Alt+← / Alt+→** to recursively collapse/expand
- **Ctrl+Z / Ctrl+Shift+Z** for undo/redo, **Ctrl+R** to rename
- **Ctrl+D** to duplicate
- **Ctrl+Left / Ctrl+Right** for back/forward history, and more.

See the full [Keyboard Shortcuts](https://github.com/SecretZauce/SecondBrain/wiki/Shortcuts) reference.

---

## Customize Your Experience

### Visual Styling

<img alt="Visual Styling — emoji icons and color styles on tree rows" src="https://github.com/SecretZauce/SecondBrainDocAssets/blob/main/gifs/visual-styling.gif" width="480" height="270"/>

Right-click any Container or Base to set an emoji (or any built-in Unity editor icon) and a label color. Four color styles let you decide how prominent the highlight is:

| Style | Effect |
|---|---|
| Gradient | Gradient wash across the row |
| Font Color | Tints the label text |
| Circle Dot | Small colored circle beside the name |
| Background | Fills the entire row |

Multi-select several nodes and style them all at once.

### Settings

Tune SecondBrain's behavior to your workflow — confirmation dialogs, row height, double-click action, force naming on creation, default expand state, and more.

See [Styling and Settings](https://github.com/SecretZauce/SecondBrain/wiki/Styling-And-Settings).

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
| [Getting Started](https://github.com/SecretZauce/SecondBrain/wiki/Getting-Started) | Opening the window, first-run orientation |
| [Data Structure](https://github.com/SecretZauce/SecondBrain/wiki/Data-Structure) | Profiles, Bases, Containers, asset types — the full hierarchy |
| [Browsing](https://github.com/SecretZauce/SecondBrain/wiki/Browsing) | Navigation, creating items, drag & drop, enter actions, undo |
| [Selection & Search](https://github.com/SecretZauce/SecondBrain/wiki/Selection-And-Search) | Multi-select, range select, real-time search |
| [Styling & Settings](https://github.com/SecretZauce/SecondBrain/wiki/Styling-And-Settings) | Emoji, colors, Inspectors, all settings |
| [PRO Features](https://github.com/SecretZauce/SecondBrain/wiki/Pro-Features) | Quick Peek, Quick Browse, Scene Linking, Action Items, Multiple Windows |
| [Keyboard Shortcuts](https://github.com/SecretZauce/SecondBrain/wiki/Shortcuts) | Full shortcut reference |
| [Advanced Topics](https://github.com/SecretZauce/SecondBrain/wiki/Advanced-Topics) | Extending TypedContainer and Action Items for custom tooling |
| [Upgrading to PRO](https://github.com/SecretZauce/SecondBrain/wiki/Upgrading-To-Pro) | Installing PRO and activating your license |

---

## Installation

### Free

Add via the Unity Package Manager using the Git URL:

```
https://github.com/SecretZauce/SecondBrain.git
```

On first launch, SecondBrain runs a one-time setup and automatically opens the **Installer Window** (Can be accessed later at `Tools → Second Brain → Installer`).

### PRO

1. Purchase and download SecondBrain PRO from the [Unity Asset Store](https://assetstore.unity.com/packages/slug/383598).
2. Import the PRO `.unitypackage` into your project. If the free package is not already present, a setup window offers to install it from GitHub for you.
3. Unity compiles and PRO activates — no license key required. On completion, the **Installer Window** opens automatically, same as the Free setup flow.

> [!NOTE]
> The two packages do not need identical version numbers. Each PRO release supports a range of free versions, so a newer free package keeps working with an existing PRO build. If they do fall out of range, SecondBrain tells you which side to update and PRO stays inactive until then.

---

## Quick Start

1. Open **Window → Second Brain Window** (PRO also adds **Second Brain (Home)** and **Second Brain (Default Base)**).
2. Double-click a Base to navigate into it, or press **Escape** to return to Home.
3. Drag assets from the Project window, Hierarchy, or Inspector onto a Container.
4. Right-click any Container to create children, rename, set an emoji, set a color, or delete.

See the [full documentation](https://github.com/SecretZauce/SecondBrain/wiki/Getting-Started) for browsing, styling, PRO features, and the extension API.

---

## Requirements

- Unity 6 LTS (6000.0.x) or later

---

## License

| Part                | License                        |
|---------------------|--------------------------------|
| Second Brain (Free) | MIT                            |
| Second Brain Pro    | Unity Asset Store EULA         |
| Material Icons      | Apache 2.0 — © 2014 Google LLC |

## Contact
- [Discord Server](https://discord.gg/wzxhQS6eFc)
- **Support Email:** sc.zauce.support@gmail.com