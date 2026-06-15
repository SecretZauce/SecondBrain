Everything in SecondBrain lives in a tree rooted at a Profile:

```
Profile  ( Per-Team Member Separation — e.g. "Tom's Profile", "Amy's Profile" )
  └── Base  ( Hierarchy Separation — e.g. "Frequently Used Stuff", "Level Design Tools" )
        └── Container  ( The actual parent of the references — nestable to any depth )
              ├── Container  (nested, any depth)
              ├── Any project asset  ( Prefabs, Materials, Scenes, etc. )
              ├── Scene Object reference
              ├── Scene Component reference
              └── Action Item  [PRO]
```

---

## Node Types

### Profile

The root of your SecondBrain hierarchy. Every Base, Container, and item lives inside a Profile. The free tier gives you one Profile; PRO gives you unlimited.

Profiles are stored as ScriptableObject assets — along with all their child nodes. You can have an **Editor-Only** profile (stored in `Assets/Resources/Editor/`, excluded from player builds) or an **In-Build** profile (stored in `Assets/Resources/`, included in player builds).

See [PRO: Profiles](pro-features.md#profiles) for managing multiple Profiles.

### Base

A workspace that groups related content. Common examples: `Level Design`, `Audio`, `UI`, `Environment Art`, `Character Assets`, `Scene: Forest` — but there's no strict rule. You can organize by discipline, by scene, by sprint, by feature, or by team member. Use whatever structure helps your team navigate fastest, and don't worry about getting it perfect from the start.

In the free tier you have one Base. PRO gives you unlimited Bases.

Bases can be linked to a Unity scene so SecondBrain opens to that workspace automatically when the scene loads. See [PRO: Scene Linking](pro-features.md#scene-linking).

### Container

A folder-like node that can hold other Containers and any kind of content. Nest them to any depth. Containers — and all their children — are child ScriptableObjects stored on the Profile asset. SecondBrain manages this automatically; do not move or rearrange these assets manually in the Project window.

The **Container Inspector** exposes per-Container options: emoji, color, preferred child view for [Quick Peek](pro-features.md#quick-peek), and default expand behavior.

### Any Project Asset

<img alt="Dragging assets and scene objects into SecondBrain" src="gifs/drag-assets.gif" width="600"/>

Drag any asset from Unity's **Project** window into a Container. You can also drag from other Unity windows:

- **Hierarchy** — drag a scene name or a GameObject; it's automatically wrapped in a Scene Object reference.
- **Inspector** — drag a component header; it's wrapped in a Scene Component reference.
- **Across tabs** — hover the SecondBrain window tab in the dock while dragging (the window doesn't need to be focused first), wait for it to become active, then drop at the target position inside the tree.

### Scene Object Reference

A link to a specific GameObject in a scene, stored by scene path. The reference shows the object's last known name even when the scene is closed. When the scene is open, pressing **Return** pings and selects the object.

### Scene Component Reference

A link to a specific component on a scene GameObject. Pressing **Return** selects the component in the Inspector (scene must be open).

### Action Item *(PRO)*

A ScriptableObject-based executable action you place in the hierarchy like any other item and run with **Return**. See [PRO: Action Items](pro-features.md#action-items).

---

## Duplicate Prevention

SecondBrain enforces two rules:

- **No same-parent duplicates** — adding an item that already exists in the same Container is silently rejected.
- **No tree-wide structural duplicates** — Containers and Bases cannot be added to a second location in the tree. Plain asset references and scene references can appear in multiple places.

---

> [!NOTE]
> For developer extension points (`TypedContainer<T>`, `ActionItem` subclassing, `[CreateChild]` attribute) and other internal details, see [Advanced Topics](advanced-topics.md).
