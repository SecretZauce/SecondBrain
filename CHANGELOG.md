# CHANGELOG

All notable changes to SecondBrain will be documented in this file.

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

