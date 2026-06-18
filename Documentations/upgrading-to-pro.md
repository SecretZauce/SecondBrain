## Getting PRO

SecondBrain PRO is a separate Unity package that builds on top of the free package. Both must be installed and at matching versions to work correctly.

Purchase and download links are available through the Installer Window (**Tools → Second Brain → Installer**) or via the Unity Asset Store.

---

## Installation Flow

### If you have the free package and are adding PRO

1. Import the SecondBrain PRO `.unitypackage` (or add it via the Package Manager).
2. Unity recompiles. On the next editor reload, SecondBrain detects the PRO assembly and activates PRO features automatically — no license key entry required.
3. The Installer Window confirms the edition as **PRO**.

<img alt="SecondBrain Installer Window showing PRO edition" src="gifs/installer-window-pro.png" width="600"/>

### If you have the PRO package but the free package is missing

The **Pro Installer** window opens automatically and shows a warning. Click **Accept — Add Package** to install the free base package directly from the Package Manager. Unity recompiles and both packages become active.

<img alt="Pro Installer Window showing missing free package warning" src="gifs/pro-installer-missing-free.png" width="600"/>

> [!NOTE] You can also reopen this window via **Tools → Second Brain → Pro Installer**.

---

## Version Compatibility

Free and PRO are versioned together — their version numbers must match. If you update only one package (for example, updating PRO without updating free), a **Version Mismatch** dialog appears on the next editor reload.

The dialog tells you which package is ahead of the other and provides a link to update the lagging package. Bring both packages to the same version to dismiss the dialog and restore full functionality.

> [!WARNING]
> While versions are mismatched, PRO features may not activate correctly. Always update both packages together.

---