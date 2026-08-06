## Getting PRO

SecondBrain PRO builds on top of the free package — both must be installed. PRO ships as a `.unitypackage` from the Asset Store; the free package is installed through the Unity Package Manager from its Git URL.

Purchase and download links are available through the Installer Window (**Tools → Second Brain → Installer**) or via the [Unity Asset Store](https://assetstore.unity.com/packages/slug/383598).

---

## Installation Flow

### If you have the free package and are adding PRO

1. Import the SecondBrain PRO `.unitypackage`.
2. Unity compiles it. PRO activates during that same compile — there is no license key to enter and no second reload to wait for.
3. The Installer Window opens and confirms the edition as **PRO**.

### If you have PRO but the free package is missing

A **SecondBrain Pro — Setup Required** window opens automatically. Click **Install / Update Free Package** to add the free package from its Git URL. Unity recompiles once it lands, and PRO activates.

Until then PRO simply stays inactive — nothing is in a broken state, and no errors are produced.

> [!NOTE]
> If you dismiss the window, reopen it any time via **Tools → Second Brain → Pro Installer**. That menu item appears only when PRO is installed.

---

## Version Compatibility

Free and PRO do **not** need identical version numbers. Each PRO release supports a range of free versions — in practice, any free release sharing the same major version.

That means the common case needs no attention at all: a free package that is newer than your PRO build keeps working, because the two are designed to stay compatible across minor updates.

Two situations do need action, and SecondBrain tells you which:

| Situation | What you see | What to do |
|-----------|--------------|------------|
| Free is **older** than your PRO build requires | A window naming your installed free version and the required range | Click **Install / Update Free Package** to pull the current free release |
| Free is **newer** than your PRO build supports (a new major version) | A window explaining that PRO is the side that must move | Update SecondBrain PRO from the Asset Store |

In both cases PRO stays inactive rather than running against a version it does not match, and the free package continues to work normally in the meantime.

> [!NOTE]
> PRO is either fully active or fully inactive — there is no partial state where some PRO features work and others silently do not.

---
