Everything in SecondBrain lives in a tree rooted at a Profile:

```
Profile  (project root)
  └── Base  (workspace)
        └── Container  (folder)
              ├── Container  (nested, any depth)
              ├── Any project asset
              ├── Scene Object reference
              ├── Scene Component reference
              └── Action Item  [PRO]
```

---

## Node Types

### Base

A top-level workspace. Think of it as a project or a discipline: `Level Design`, `Audio`, `UI`, `Sprint Board`. In the free tier you have one Base. PRO gives you unlimited Bases.

Bases can be linked to a Unity scene so SecondBrain opens to that workspace automatically when the scene loads. See [PRO: Scene Linking](pro-features.md#scene-linking).

### Container

A folder-like node that can hold other Containers and any kind of content. Nest them to any depth. Containers live as sub-assets embedded inside their Base's `.asset` file — do not manually rearrange these files.

The **Container Inspector** exposes per-Container options: emoji, color, preferred child view for [Quick Peek](pro-features.md#quick-peek), and default expand behavior.

### Any Project Asset

Drag any asset from Unity's **Project** window into a Container:

| Asset type | Enter action |
|---|---|
| Prefab | Opens in Prefab Stage |
| Scene file | Opens the scene; ▶ play button appears |
| Text file (URL) | Opens the URL in your system browser |
| Any other asset | Selects in Project window / Inspector |

### Scene Object Reference

A link to a specific GameObject in a scene, stored by scene path. The reference shows the object's last known name even when the scene is closed. When the scene is open, pressing **Return** pings and selects the object.

### Scene Component Reference

A link to a specific component on a scene GameObject. Pressing **Return** selects the component in the Inspector (scene must be open).

### Action Item *(PRO)*

A ScriptableObject-based automation you place in the hierarchy like any other item and run with **Return**. See [PRO: Action Items](pro-features.md#action-items).

---

## Duplicate Prevention

SecondBrain enforces two rules:

- **No same-parent duplicates** — adding an item that already exists in the same Container is silently rejected.
- **No tree-wide structural duplicates** — Containers and Bases cannot be added to a second location in the tree. Plain asset references and scene references can appear in multiple places.

---

## What's Not Here

A few things visible in the codebase are intentionally left out of this guide:

| Topic | Why omitted |
|---|---|
| `TypedContainer<T>` | Developer API — create strongly-typed containers in code. Not configurable from the editor UI. |
| `[CreateChild]` attribute / `IHasCreateChildOption` | Developer APIs for registering custom child types in the Create Child menu. |
| Sub-asset file layout | Containers are embedded sub-assets. Fully managed by SecondBrain — do not reorganize manually. |
| `SceneObjectMap` rebuild | Internal cache rebuilt automatically on domain reload. |
| `SecondBrainCore` initialization state | Internal first-run tracking stored in `SecondBrainCore.asset`. Not user-configurable. |
