using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Editor
{
    public partial class ColorTray
    {
        class ColorPickerPopup : EditorWindow
        {
            private Color _color = Color.white;
            private System.Action<Color> _onConfirm;

            // Inline picker textures and state
            private Texture2D _svTexture;
            private Texture2D _hueTexture;
            private int _svSize = 128;
            private float _h = 0f, _s = 0f, _v = 1f, _a = 1f;
            private bool _isClosing = false;

            public static void Show(Rect anchorRect, Color initial, System.Action<Color> onConfirm)
            {
                var w = CreateInstance<ColorPickerPopup>();
                w._color = initial;
                Color.RGBToHSV(initial, out w._h, out w._s, out w._v);
                w._a = initial.a;
                w._onConfirm = onConfirm;
                
                // Compute a minimum size that fits the SV box, hue strip and bottom controls.
                var minWidth = Mathf.Max(260, w._svSize + 154); // sv + hue + padding + buttons area
                var minHeight = Mathf.Max(180, w._svSize + 60);
                var size = new Vector2(minWidth, minHeight);
              
                // Position the popup below the anchor if space allows, otherwise above it
                float screenW = Screen.currentResolution.width;
                float screenH = Screen.currentResolution.height;
                float px = anchorRect.x;
                float py = anchorRect.yMax + 6f;
              
                // If popup would go off right edge, shift left
                if (px + size.x > screenW - 8f)
                    px = Mathf.Max(8f, screenW - size.x - 8f);
               
                // If popup would go off bottom, open above the anchor
                if (py + size.y > screenH - 8f)
                    py = anchorRect.y - size.y - 6f;
               
                // Clamp to left/top edges
                px = Mathf.Max(8f, px);
                py = Mathf.Max(8f, py);
                w.position = new Rect(px, py, size.x, size.y);
               
                // Suspend the parent's auto-close behavior so opening this popup doesn't close the tray
                SuspendAutoClose();
                w.ShowPopup();
                w.Focus();
                w.EnsureTextures();
            }

            void OnGUI()
            {
                GUILayout.Space(6);
                EnsureTextures();
                float pad = 8f;
                float sv = Mathf.Min(_svSize, position.width - 140f);
                Rect svRect = new Rect(pad, 8f, sv, sv);
                Rect hueRect = new Rect(svRect.xMax + 8f, svRect.y, 18, sv);

                // Draw SV texture and border
                if (_svTexture != null)
                    GUI.DrawTexture(svRect, _svTexture);
                DrawBorder(svRect, Color.black, 1f);

                // Draw hue texture
                if (_hueTexture != null)
                    GUI.DrawTexture(hueRect, _hueTexture);
                DrawBorder(hueRect, Color.black, 1f);

                // Handle mouse input for SV and Hue
                var e = Event.current;
                if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                {
                    if (e.button == 0)
                    {
                        if (svRect.Contains(e.mousePosition))
                        {
                            var local = e.mousePosition - new Vector2(svRect.x, svRect.y);
                            _s = Mathf.Clamp01(local.x / svRect.width);
                            // Because the texture is drawn with flipped UVs, invert the mouse V so
                            // top of the control corresponds to V=1 (white) and bottom to V=0.
                            _v = Mathf.Clamp01(1f - (local.y / svRect.height));
                            UpdateColorFromHSV();
                            e.Use();
                        }
                        else if (hueRect.Contains(e.mousePosition))
                        {
                            var local = e.mousePosition - new Vector2(hueRect.x, hueRect.y);
                            // Invert hue sampling to match flipped hue texture drawing
                            _h = Mathf.Clamp01(1f - (local.y / hueRect.height));
                            RebuildSVTexture();
                            UpdateColorFromHSV();
                            e.Use();
                        }
                    }
                }

                // Alpha slider
                Rect aLabel = new Rect(pad, svRect.yMax + 8f, 20, 18);
                GUI.Label(aLabel, "A");
                Rect aRect = new Rect(aLabel.xMax + 6f, aLabel.y, position.width - aLabel.xMax - SwatchPadding - 80f, 18);
                _a = EditorGUI.Slider(aRect, _a, 0f, 1f);

                // Explicitly position buttons and preview on the same bottom row to avoid overlap
                // Place buttons below the alpha slider to prevent overlap
                float btnY = aRect.yMax + 8f;
                float btnH = 20f;
                float btnW = 80f;
                float spacing = 8f;
                // Right-align buttons
                float right = position.width - SwatchPadding;
                Rect okRect = new Rect(right - btnW, btnY, btnW, btnH);
                Rect cancelRect = new Rect(okRect.x - spacing - btnW, btnY, btnW, btnH);

                // Preview occupies remaining space to the left of the Cancel button
                Rect previewRect = new Rect(SwatchPadding, btnY, Mathf.Max(40f, cancelRect.x - SwatchPadding - spacing), btnH);
                EditorGUI.DrawRect(previewRect, new Color(_color.r, _color.g, _color.b, _a));
                DrawBorder(previewRect, Color.black, 1f);

                if (GUI.Button(cancelRect, "Cancel"))
                {
                    Close();
                }

                if (GUI.Button(okRect, "OK"))
                {
                    try { _onConfirm?.Invoke(new Color(_color.r, _color.g, _color.b, _a)); } catch { }
                    Close();
                }
                // If parent tray was closed (user closed the ColorTray), close this popup too
                if (!IsAnyTrayOpen)
                {
                    Close();
                    return;
                }
            }

            private void EnsureTextures()
            {
                if (_hueTexture == null)
                    RebuildHueTexture();
                if (_svTexture == null)
                    RebuildSVTexture();
            }

            void OnLostFocus()
            {
                // Close the popup when it loses focus (click outside).
                // Use delayed call and a guard to avoid re-entrant Close/OnDestroy calls
                if (_isClosing) return;
                _isClosing = true;
                EditorApplication.delayCall += () => {
                    try { if (!this) return; Close(); } catch { }
                };
            }

            private void RebuildHueTexture()
            {
                int hHeight = _svSize;
                if (_hueTexture != null) Texture2D.DestroyImmediate(_hueTexture);
                _hueTexture = new Texture2D(1, hHeight, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < hHeight; y++)
                {
                    float hue = (float)y / (hHeight - 1);
                    Color c = Color.HSVToRGB(hue, 1f, 1f);
                    _hueTexture.SetPixel(0, y, c);
                }
                _hueTexture.Apply();
            }

            private void RebuildSVTexture()
            {
                int size = _svSize;
                if (_svTexture != null) Texture2D.DestroyImmediate(_svTexture);
                _svTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float s = (float)x / (size - 1);
                        // v should increase downward to match GUI texture display; use y/(size-1)
                        float v = (float)y / (size - 1);
                        Color c = Color.HSVToRGB(_h, s, v);
                        _svTexture.SetPixel(x, y, c);
                    }
                }
                _svTexture.Apply();
            }

            private void UpdateColorFromHSV()
            {
                var rgb = Color.HSVToRGB(_h, _s, _v);
                _color = new Color(rgb.r, rgb.g, rgb.b, _a);
                Repaint();
            }

            void OnDestroy()
            {
                // Re-enable parent's auto-close and attempt to refocus the tray if present
                ResumeAutoClose();
                try { if (_currentTray != null) _currentTray.Focus(); } catch { }
                try
                {
                    if (_svTexture != null)
                    {
                        DestroyImmediate(_svTexture);
                        _svTexture = null;
                    }
                }
                catch { _svTexture = null; }
                try
                {
                    if (_hueTexture != null)
                    {
                        DestroyImmediate(_hueTexture);
                        _hueTexture = null;
                    }
                }
                catch { _hueTexture = null; }
            }
        }
    }
}