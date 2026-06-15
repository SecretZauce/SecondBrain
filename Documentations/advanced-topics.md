## Advanced Topics

This page covers developer extension points — ways to add custom node types and executable actions to SecondBrain.

---

## Extending TypedContainer

`TypedContainer<T>` is an abstract `ScriptableObject` base class that holds a strongly-typed list of children. Subclass it to create a container that only accepts one specific `ScriptableObject` type. SecondBrain manages the asset lifecycle; you only need to define the type constraint and any create-child presets.

### Minimal example

```csharp
using SecretZauce.SecondBrain;
using UnityEngine;

[CreateAssetMenu(menuName = "My Game/Character Container")]
[CreateChild("Characters/Character Container")]
public class CharacterContainer : TypedContainer<CharacterConfig>
{
}
```

- `[CreateAssetMenu]` lets Unity create the asset from the Project window context menu.
- `[CreateChild("...")]` registers it in SecondBrain's **Create Child** menu. The string is the menu path; use `/` as a separator for sub-menus.

### Adding create-child presets

Implement `IHasCreateChildOption` to offer multiple named presets when users create a child from the context menu. Each `CreateChildOption` specifies the child type, a display name, and an optional initializer callback.

```csharp
using System.Collections.Generic;
using SecretZauce.SecondBrain;
using UnityEngine;

[CreateAssetMenu(menuName = "My Game/Character Container")]
[CreateChild("Characters/Character Container")]
public class CharacterContainer : TypedContainer<CharacterConfig>, IHasCreateChildOption
{
    public IReadOnlyList<CreateChildOption> GetCreateChildOptions()
    {
        return new List<CreateChildOption>
        {
            new CreateChildOption(
                typeof(CharacterConfig),
                "Warrior",
                (obj, t) =>
                {
                    if (obj is CharacterConfig c)
                    {
                        c.hp  = 100f;
                        c.atk = 15f;
                    }
                }),

            new CreateChildOption(
                typeof(CharacterConfig),
                "Mage",
                (obj, t) =>
                {
                    if (obj is CharacterConfig c)
                    {
                        c.hp  = 60f;
                        c.atk = 30f;
                    }
                })
        };
    }
}
```

Each option appears as a separate entry in the **Create Child** submenu.

### `[CreateChild]` attribute reference

```csharp
[CreateChild(menuPath: "Configs/Enemies", displayName: "Enemy Config")]
```

| Parameter | Purpose |
|---|---|
| `menuPath` | Slash-separated path in the Create Child menu. Omit to place the entry at the top level. |
| `displayName` | Override the label shown in the menu. Defaults to the class name. |

---

## Extending Action Items

`ActionItem` is an abstract `ScriptableObject` base class *(PRO)*. Subclass it to create any executable action that can live in the SecondBrain hierarchy and be triggered with **Return** or double-click.

### Minimal example

```csharp
using SecretZauce.SecondBrain;
using UnityEditor;

[CreateAssetMenu(menuName = "My Game/Actions/Log Message")]
public class LogMessageAction : ActionItem
{
    public string message = "Hello from SecondBrain!";

    public override string DefaultName => "Log Message";

    public override void Execute()
    {
        UnityEngine.Debug.Log(message);
    }
}
```

### Registering in the Create Child menu

Add `[CreateChild]` to make the action appear in SecondBrain's **Create Child → Action Items** menu:

```csharp
[CreateAssetMenu(menuName = "My Game/Actions/Log Message")]
[CreateChild("My Actions/Log Message")]
public class LogMessageAction : ActionItem
{
    // ...
}
```

### API reference

| Member | Purpose |
|---|---|
| `DefaultName` | Name used when the asset is first created. Override to set a meaningful default. |
| `ActionPath` | Slash-separated grouping path in the Action Item Selector popup (e.g. `"Editor/Play Mode"`). Leave empty for the top level. |
| `CustomIcon` | Resource name of the icon loaded from `Resources/Editor/Icons/`. Defaults to `"action"`. |
| `GetDetailDisplay()` | Optional single-line detail string shown next to the item label in the tree. |
| `Execute()` | Called when the user runs the action. Implement your logic here. |
