using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public static class ItemContextMenu
    {
        static Vector2 mouseScreenPos;
        /// <summary>
        /// Shows context menu for the given path
        /// </summary>
        public static void ShowContextMenu(BrowserWindow window, int[] path, Object obj, bool isIStructure)
        {
            if (path == null || obj == null)
                return;

            SampleMousePosition();

#if SECOND_BRAIN_PRO
            // When opening the context menu, close the QuickPeek popup so it doesn't
            // overlap or remain visible while the user interacts with the menu.
            window.DisposeQuickPeek();
#endif

            GenericMenu menu = new GenericMenu();

            // ── Group 1: Structural creation ─────────────────────────────────
            if (isIStructure)
            {
                // Merge multiple create-child sources under a single "Create Child" parent.
                // 1) Types annotated with [CreateChild] (discovered via CreateChildAttributeSelector)
                // 2) Per-instance options from IHasCreateChildOption (CreateChildMenuUtils)
                bool addedAttributeEntries = CreateChildAttributeSelector.AddToMenu(menu, (selectedType) =>
                {
                    window.CreateChildOfType(obj, selectedType);
                }, "Create Child", obj as IStructure);

                bool addedHasCreateOptions = CreateChildMenuUtils.AddOptionsToMenu(menu, obj, window);

                // If neither source provided items, fall back to the legacy direct Create Child action
                if (!addedAttributeEntries && !addedHasCreateOptions)
                {
                    menu.AddItem(new GUIContent("Create Child"), false, () =>
                    {
                        window.CreateChild(obj);
                    });
                }

#if SECOND_BRAIN_PRO
                var handler = ProFeature.Provider.CreateActionItemHandler();
                // Embed action-type entries as a submenu so they appear in the same GenericMenu
                handler.AddToMenu(menu, (selectedType) =>
                {
                    window.CreateChildOfType(obj, selectedType);
                });
#endif
            }

            // ── Group 2: Item operations ──────────────────────────────────────
            if (isIStructure)
                menu.AddSeparator("");

#if SECOND_BRAIN_PRO
            if (obj is ActionItem actionItem)
            {
                menu.AddItem(new GUIContent("Execute"), false, () => actionItem.Execute());
                menu.AddSeparator("");
            }
#endif

            if (!isIStructure)
            {
                bool canDuplicate = true;
                string objAssetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(objAssetPath) && AssetDatabase.IsValidFolder(objAssetPath))
                    canDuplicate = false;
                if (canDuplicate && path.Length > 1)
                {
                    var parentObj = window.Controller?.GetObjectAtPath(path[..^1]);
                    if (AssetUtils.IsInDifferentAsset(obj, parentObj))
                        canDuplicate = false;
                }
                if (canDuplicate)
                    menu.AddItem(new GUIContent("Duplicate"), false, window.DuplicateSelectedItems);
            }
            if (obj is not SceneObjectRef && obj is not SceneComponentRef)
                menu.AddItem(new GUIContent("Rename"), false, window.BeginRenamingSelectedItem);

            menu.AddItem(new GUIContent("Remove from Base"), false, window.DeleteSelectedItems);

            // ── Group 3: Properties ───────────────────────────────────────────
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Properties"), false, window.ShowPropertiesForSelectedItem);

#if SECOND_BRAIN_PRO
            // ── Group 4: Move to Base submenu ─────────────────────────────────
            // Only visible when inside a Base, the item is a container (IStructure),
            // and other Bases exist to move to
            if (isIStructure && window.Root is Base currentBase)
            {
                var profile = Profile.Active;
                if (profile?.Children != null)
                {
                    var otherBases = profile.Children
                        .Where(b => b != null && b != currentBase)
                        .ToList();

                    if (otherBases.Count > 0)
                    {
                        menu.AddSeparator("");
                        foreach (var targetBase in otherBases)
                        {
                            var capturedBase = targetBase;
                            menu.AddItem(new GUIContent("Move to/" + targetBase.name), false, () =>
                            {
                                window.MoveSelectedItemsToBase(capturedBase);
                            });
                        }
                    }
                }
            }

            // ── Group 4b: Move Base to Profile submenu ─────────────────────────
            // Only visible at the home (Profile) view when the right-clicked item is
            // a Base and there are other Profiles available to move it to.
            // Operates on the whole selection when several Bases are selected.
            if (obj is Base baseToMove && window.IsAtHome())
            {
                var allProfiles = ProfileManager.GetAllProfiles();
                var currentProfile = Profile.Active;
                var otherProfiles = allProfiles
                    .Where(p => p != null && !ReferenceEquals(p, currentProfile))
                    .ToList();

                if (otherProfiles.Count > 0)
                {
                    var basesToMove = CollectSelectedBases(window, baseToMove);
                    string submenu = basesToMove.Count > 1
                        ? $"Move {basesToMove.Count} Bases to Profile/"
                        : "Move to Profile/";

                    menu.AddSeparator("");
                    foreach (var targetProfile in otherProfiles)
                    {
                        var capturedProfile = targetProfile;
                        var capturedBases   = basesToMove;
                        menu.AddItem(new GUIContent(submenu + targetProfile.name), false, () =>
                        {
                            if (ProfileManager.MoveBasesToProfile(capturedBases, capturedProfile))
                            {
                                // Selection paths are row indices into the home view; the moved
                                // rows are gone, so keeping them would select unrelated Bases.
                                window.Controller?.SelectionState?.ClearSelection(window);
                            }
                        });
                    }
                }
            }
#endif

            // ── Group 5: Emoji icon ───────────────────────────────────────────
            // Multi-selection: get all selected objects from the window's selection state
            var selectedPaths = window.Controller?.SelectionState?.GetAllPaths();
            var selectedObjs = new System.Collections.Generic.List<IHasEmoji>();
            if (selectedPaths != null)
            {
                foreach (var p in selectedPaths)
                {
                    var o = window.Controller?.GetObjectAtPath(p);
                    if (o is IHasEmoji emojiObj)
                        selectedObjs.Add(emojiObj);
                }
            }
            // Only show if all selected are IHasEmoji and at least one is selected
            if (selectedObjs.Count > 0 && selectedPaths != null && selectedObjs.Count == selectedPaths.Count)
            {
                menu.AddSeparator("");
                // Use the cached mouseScreenPos instead of Event.current
                menu.AddItem(new GUIContent("Set Emoji Icon..."), false, () =>
                {
                    EmojiTray.ShowForObjects(selectedObjs, new Vector2(mouseScreenPos.x, mouseScreenPos.y + 4));
                });
            }

            // ── Group 6: Label color ──────────────────────────────────────────
            // Only show when all selected items support color (IHasColor)
            var selectedColorObjs = new System.Collections.Generic.List<IHasColor>();
            if (selectedPaths != null)
            {
                foreach (var p in selectedPaths)
                {
                    var o = window.Controller?.GetObjectAtPath(p);
                    if (o is IHasColor colorObj)
                        selectedColorObjs.Add(colorObj);
                }
            }
            if (selectedColorObjs.Count > 0 && selectedColorObjs.Count == selectedPaths.Count)
            {
                // Add a separator before the color option only when the emoji option was not shown
                // (which would have added its own separator already).
                if (selectedObjs.Count == 0)
                    menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set Color..."), false, () =>
                {
                    ColorTray.ShowForObjects(selectedColorObjs, new Vector2(mouseScreenPos.x, mouseScreenPos.y + 4));
                });
            }

            // ── Group 7: Clean up missing children ───────────────────────────────
            if (isIStructure && obj is IStructure structForClean)
            {
                var childObjects = structForClean.ChildrenObjects;
                if (childObjects != null && childObjects.Any(c => c == null))
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Clean Up Missing Items"), false, () =>
                    {
                        window.CleanMissingChildren(obj);
                    });
                }
            }

            if (menu.GetItemCount() > 0)
            {
                menu.ShowAsContext();
            }
        }

#if SECOND_BRAIN_PRO
        /// <summary>
        /// Returns every Base in the current selection, keeping selection order.
        /// Falls back to just <paramref name="rightClickedBase"/> when it is not part of the
        /// selection (right-clicking an unselected row) or when the selection holds no Bases.
        /// </summary>
        static System.Collections.Generic.List<Base> CollectSelectedBases(BrowserWindow window, Base rightClickedBase)
        {
            var bases = new System.Collections.Generic.List<Base>();
            var paths = window.Controller?.SelectionState?.GetAllPaths();

            if (paths != null)
            {
                foreach (var p in paths)
                {
                    if (window.Controller?.GetObjectAtPath(p) is Base b && !bases.Contains(b))
                        bases.Add(b);
                }
            }

            if (!bases.Contains(rightClickedBase))
            {
                bases.Clear();
                bases.Add(rightClickedBase);
            }

            return bases;
        }
#endif

        static void SampleMousePosition()
        {
            // Cache the mouse position in screen space before showing the menu
            mouseScreenPos = Vector2.zero;
            if (Event.current != null)
            {
                mouseScreenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
        }
    }
}
