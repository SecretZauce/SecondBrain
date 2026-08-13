# CHANGELOG

All notable changes to SecondBrain will be documented in this file.

## 1.1.1 (13-08-2026)

### Fixed
- The editor no longer crashes while dragging items in a browser window. Dragging an item out of the window kept updating what Unity hands to the operating system from a background editor tick, where that data is not valid to touch. The drag data could end up corrupt, and the next drag brought the whole editor down with no error message first — most often after a run of adding and deleting folders, which is what invalidated the items being carried.
- A drag that ends outside Unity — released over another application or over the toolbar, or cancelled with Esc — now finishes cleanly. Such a drag previously left its per-frame tracking running for the rest of the session, which is what set up the crash above.
- Items that were destroyed by a recent add or delete are now left out of a drag instead of being carried into it.

## 1.1.0 (07-08-2026)

### Fixed
- Importing Pro into a project where SecondBrain had already finished its first-time setup left the activation half-done: the log stopped at the activation notice and the Installer never opened to confirm the edition. Pro now announces itself as soon as it becomes active, whichever order the two packages were installed in.
- Action Items are no longer stripped out of their Containers while Pro is installed but not compiled — during an update, for instance. The entries were previously removed for good even though the assets themselves survived, so they did not come back when Pro returned.
- A leftover Pro activation entry in Player Settings, written by earlier versions, is now cleaned up. It could otherwise let Pro run against a free version it was not built for.

### Changed
- Pro activates during the same compile that imports it, instead of writing a scripting define and waiting for a second recompile.
- Free and Pro no longer need identical version numbers. Each Pro release supports a range of free versions, so updating the free package on its own keeps working with the Pro build you already have.
- When the two genuinely are out of range, Pro stays fully inactive and a window names which side to update. This replaces the old mismatch dialog, and removes the in-between state where some Pro features worked and others quietly did not.
- The Pro setup window can be reopened at any time from **Tools → Second Brain → Pro Installer**.
- Pro's **Window → Second Brain (Home)** and **Window → Second Brain (Default Base)** entries now sit alongside the standard **Window → Second Brain Window** item instead of replacing it.

## 1.0.5 (31-07-2026)

### Fixed
- Scene references no longer report as "Missing" when the linked object's parents are removed at runtime — as hierarchy-folder assets do when they flatten their folder objects on entering Play mode. The object is now found at its new place in the hierarchy, as long as only one object fits.
- Quick Peek now recovers references the same way the browser and Inspector already did, instead of refusing to open them once their id stops resolving in Play mode.

### Changed
- Browser windows holding many scene object references are substantially faster to hover and scroll. A reference whose target cannot be found by id no longer re-searches every loaded scene on each repaint — the result is remembered until a scene or the hierarchy actually changes.
- Browser settings such as row height, font size, and the icon and Quick Peek toggles are now read from memory. They were previously fetched from EditorPrefs several times per row on every repaint.
- Tree rows no longer allocate while drawing. Click handling, label layout, row renderers, and selection lookups all reused throwaway objects on every row on every repaint, which built up garbage and caused periodic hitches in large trees.
- Moving the mouse over the tree now redraws only when the hovered item changes, instead of on every mouse movement.

## 1.0.4 (31-07-2026)

### Fixed
- Scene references no longer report as "Missing" when the linked object is moved to the DontDestroyOnLoad scene during Play mode.
- Scene and component references shown in the Inspector now recover the same way as the ones in the browser window. Previously they still reported "Missing" for prefab instances after entering Play mode.

## 1.0.3 (31-07-2026)

### Changed
- Browser window rows render faster in large trees. Row styles, icons, and toolbar labels are now built once and reused instead of being rebuilt every repaint, and rows no longer allocate a string each to track their foldout state.
- Searching a large tree no longer gets slower as the tree grows. Matches are worked out once per search instead of being recalculated for every visible row on every repaint.
- Large selections stay responsive. Checking whether a row is selected is now a direct lookup instead of a scan through the whole selection, both while drawing rows and while changing the selection.
- Installing SecondBrain alongside SecondBrain Pro does less redundant work. The Pro editor code is skipped entirely until Pro is actually activated, and setup no longer writes the Pro activation flag from two places in the same pass.

## 1.0.2 (31-07-2026)

### Added
- Warning when the installed Unity version cannot render an emoji icon, so unsupported glyphs are obvious instead of silently blank.

### Changed
- Large performance improvement for the browser window in scenes with many objects. Open-window lookups no longer scan every loaded object in the project, and row rendering no longer allocates per row.
- Styling and undo now save only SecondBrain's own assets instead of flushing every dirty asset in the project.

### Fixed
- Prefab-instance scene references no longer report as "Missing" after entering Play mode.
- Assigning an emoji or a label colour no longer triggers an asset re-import on every click.
- Undoing a selection no longer causes unrelated assets in the project to be written and re-imported.
- Undo/redo no longer overwrites unrelated Selection changes.
- Default Profile could lose its objects after restarting following a Pro install — Profile resolution and orphaned sub-asset cleanup are both hardened against acting on a mis-resolved Profile.
- Dragging containers and items between two SecondBrain windows works again.
- Dropping onto a Base that has no containers now transfers the dragged items instead of creating duplicate entries, and dragging a container there no longer leaves an empty container behind.
- Dragging a scene name into a Base with no container.
- Crash when an asset re-import destroyed a dragged item mid-drag (Windows drag loop race).
- Moving a scene file whose scene is currently open is now refused instead of corrupting Unity's scene bookkeeping.
- Compile error on older Unity versions.

## 1.0.1 (13-07-2026)
- Improved UX. Show Installer Window on finishing installation.

## 1.0.0 (13-07-2026) 
- Initial Release

