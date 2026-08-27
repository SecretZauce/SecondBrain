## Getting PRO

SecondBrain PRO ships as a single `.unitypackage` from the Asset Store. It contains **everything** — the core SecondBrain window and hierarchy plus the PRO features — so it is the only package you need. There is no second download, no license key, and no version to match.

Purchase and download links are available through the Installer Window (**Tools → Second Brain → Installer**) or via the [Unity Asset Store](https://assetstore.unity.com/packages/slug/383598).

---

## Installation Flow

### If you have never installed SecondBrain

1. Import the SecondBrain PRO `.unitypackage`.
2. Unity compiles it. PRO is active as soon as that compile finishes.
3. The Installer Window opens and confirms the edition as **PRO**.

### If you already have the free version installed

> [!IMPORTANT]
> Remove the free version **before** importing PRO. PRO bundles the same core code, and leaving both in the project gives Unity two copies of the same assemblies — the console fills with duplicate-assembly errors and neither edition compiles.

1. Remove the free version:
   - **Installed from the Git URL (Package Manager):** open **Window → Package Manager → In Project**, select **SecondBrain**, and click **Remove**.
   - **Imported as files:** delete the `Assets/SecretZauce/SecondBrain` folder from the Project window.
2. Let Unity finish recompiling.
3. Import the SecondBrain PRO `.unitypackage`.
4. The Installer Window opens and confirms the edition as **PRO**.

Your content is safe across the swap. Profiles, Bases, and everything inside them are assets in your own project (`Assets/Resources/Editor/`, or `Assets/Resources/` for in-build Profiles) — they are not part of either package, so removing free and importing PRO leaves them untouched.

---

## Going Back to Free

Delete the `Assets/SecretZauce/SecondBrain` and `Assets/SecretZauce/SecondBrainPro` folders, then add the free package again through the Package Manager using the Git URL:

```
https://github.com/SecretZauce/SecondBrain.git
```

Your Profiles and Bases stay where they are. PRO-only content — Action Items, and Scene Links on Bases — stops resolving once the PRO assembly is gone, so export or note anything you want to keep first.

---

## Versions

There is only one package to keep up to date. Update PRO from the Asset Store as new versions land; nothing needs to line up with anything else.

> [!NOTE]
> PRO is either fully active or fully inactive — there is no partial state where some PRO features work and others silently do not.

---
