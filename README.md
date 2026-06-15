<p align="center">
  <img src="Assets/SecretZauce/SecondBrain/Resources/Editor/Branding/SecondBrain_Transparent.png" alt="SecondBrain" width="420"/>
</p>

<p align="center">
  <a href="https://secretzauce.gitbook.io/second-brain">Documentation</a> &nbsp;|&nbsp;
  <a href="https://discord.gg/wzxhQS6eFc">Discord</a> &nbsp;|&nbsp;
  <a href="https://github.com/SecretZauce/second-brain/issues">Report a Bug</a> &nbsp;|&nbsp;
  <a href="https://github.com/SecretZauce/second-brain/blob/main/CHANGELOG.md">Changelog</a> &nbsp;|&nbsp;
  <a href="https://assetstore.unity.com/">Asset Store ↗</a>
</p>

---

**SecondBrain** is a Unity Editor tool that gives you a second hierarchy window for organizing Assets, Prefabs, Scenes, GameObjects, Components, Notes, Folders, and executable Actions — all persisted as ScriptableObject data inside your project.

<img alt="SecondBrain's main browser window showing a hierarchy of Containers and Assets with custom emoji icons and label colors" src="gifs/browser-window.gif" width="600"/>

## Features

### Free
- **Drag anything in** — drop assets from the Project window, GameObjects or scene names from the Hierarchy, and components from the Inspector. References persist even when scenes are closed.
<br><img alt="Dragging assets from Project, Hierarchy, and Inspector" src="gifs/drag-drop.gif" width="600"/>
- **Structured hierarchy** — organize everything inside nestable Containers.
  <br><img alt="Creating a child item via right-click context menu" src="gifs/create-items.gif" width="600"/>
- **Real-time search** — case-insensitive substring filter with per-type filtering flags.
- **Enter actions** — every item type knows what to do on Return or double-click (open scene, ping object, open URL, open Prefab Stage, and more).
- **Visual styling** — assign emoji icons (or any built-in Unity editor icon) and label colors (Gradient, Font Color, Circle Dot, or Background) to Containers and Bases.
  <br><img alt="visual styling options" src="gifs/styling.gif" width="600"/>
- **Full undo / redo** — all structural operations integrate with Unity's undo system.
- **Keyboard navigation** — arrow keys, Ctrl+R rename, Ctrl+D duplicate, Ctrl+Left/Right back/forward history, and more.
- **Paste as note** — Ctrl+V pastes clipboard text as a new Text Asset child; URLs open in the system browser.
- **TypedContainer API** — subclass `TypedContainer<T>` to create strongly-typed containers for your own ScriptableObjects.
<br><img alt="Example of a custom typed container for CharacterConfig assets" src="gifs/typed-container.gif" width="600"/>

### PRO
Everything in Free, plus:

- **Quick Peek** — hover the edge of any row for a floating inspector popup. Containers show children in Tabs or Foldouts; Scene Objects show their components; Text Assets are editable inline.
<br><img alt="Quick Peek hover preview showing a floating inspector with tabs for child containers" src="gifs/quick-peek.gif" width="600"/>

- **Quick Browse** — `Option+W` / `Alt+W` opens a centered floating browser from anywhere in the editor. Search bar is focused immediately.
<br><img alt="Quick Browse floating popup opened with Alt+W" src="gifs/quick-browse.gif" width="600"/>

- **Scene Linking** — link a Base to a Unity scene so the browser auto-opens to that workspace whenever the scene loads.
- **Action Items** — ScriptableObject-based executable actions that live in your hierarchy.
<br><img alt="Action Item example showing a custom Log Message action in the Create Child menu and executed in the browser" src="gifs/action-items.gif" width="600"/>
- **Multiple Profiles & Bases** — unlimited profiles (per-team-member workspaces) and unlimited Bases per profile.
- **Multiple windows** — open and dock as many browser windows as you need; each tracks its own navigation, foldout state, and selection.
- **Drag out** — drag items from SecondBrain into the Scene View or another SecondBrain window.

## Free vs PRO

| Capability                                            | Free | PRO |
|-------------------------------------------------------|:---:|:---:|
| Browser windows                                       | 1 | Unlimited |
| Profiles                                              | 1 | Unlimited |
| Bases                                                 | 1 | Unlimited |
| Drag & drop (assets, scenes, GameObjects, components) | ✓ | ✓ |
| Real-time search                                      | ✓ | ✓ |
| Full undo / redo                                      | ✓ | ✓ |
| Keyboard navigation                                   | ✓ | ✓ |
| Emoji icons & label colors                            | ✓ | ✓ |
| TypedContainer API                                    | ✓ | ✓ |
| Quick Peek hover preview                              | — | ✓ |
| Quick Browse keyboard popup                           | — | ✓ |
| Scene Linking (auto-open / close)                     | — | ✓ |
| Action Items                                          | — | ✓ |
| Drag items out between Bases                          | — | ✓ |
| Multi-window support                                  | — | ✓ |

## Installation

### Free

Add via the Unity Package Manager using the Git URL:

```
https://github.com/SecretZauce/SecondBrain.git
```

Or download the latest release from the [GitHub releases page]() and import the `.unitypackage` into your project.

On first launch, SecondBrain runs a one-time setup and automatically opens the **Installer Window** (Can be accessed later at `Tools → Second Brain → Installer`).

### PRO

1. Purchase and download SecondBrain PRO from the [Unity Asset Store](https://assetstore.unity.com/).
2. Import the PRO `.unitypackage` into your project (The installer will ask you to download free package from the GitHub repository if not installed already).
3. Unity recompiles and PRO features activate automatically — no license key required.

> [!NOTE] Both packages must be at matching versions. A **Version Mismatch** dialog appears if they fall out of sync and links you to the correct update.

## Quick Start

1. Open **Window → Second Brain (Home)**.
2. Double-click a Base to navigate into it, or press **Escape** to return to Home.
3. Drag assets from the Project window, Hierarchy, or Inspector onto a Container.
4. Right-click any Container to create children, rename, set an emoji, set a color, or delete.

See the [full documentation](https://secretzauce.gitbook.io/second-brain) for browsing, styling, PRO features, and the extension API.

## Requirements

- Unity 6 LTS (6000.0.x) or later

## License

| Part                | License                        |
|---------------------|--------------------------------|
| Second Brain (Free) | MIT                            |
| Second Brain Pro    | Unity Asset Store EULA         |
| Material Icons      | Apache 2.0 — © 2014 Google LLC |
