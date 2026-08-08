using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  YerbaWidgets — C# port of Yerba's custom widgets
    //  (settings_widgets.cpp + color_picker.cpp)
    // ────────────────────────────────────────────────────────────────────────
    public static class YerbaWidgets
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        // ── Helpers ─────────────────────────────────────────────────────────
        public static void EaseAnim(ref float value, float target, float dt, float speed)
        {
            if (value < target) value = Math.Min(value + speed * dt, target);
            else if (value > target) value = Math.Max(value - speed * dt, target);
        }

        public static Vector2 RectMin(Vector2 a, Vector2 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        public static Vector2 RectMax(Vector2 a, Vector2 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

        public static void DrawFieldOutline(ImDrawListPtr dl, Vector2 min, Vector2 max, uint col, float round, float thickness = 0.5f)
        {
            float inset = thickness * 0.5f;
            dl.AddRect(
                new Vector2(min.X + inset, min.Y + inset),
                new Vector2(max.X - inset, max.Y - inset),
                col, Math.Max(round - inset, 0f), ImDrawFlags.None, thickness);
        }

        public static void DrawRoundedVerticalGradient(ImDrawListPtr dl, Vector2 min, Vector2 max, float round,
            uint top, uint bottom, float gradientSpan = 1f)
        {
            float height = max.Y - min.Y;
            if (height <= 0f) return;

            // Draw base rounded rect with the bottom color (used as the fill at full extent)
            dl.AddRectFilled(min, max, bottom, round);

            // Cover with vertical gradient strips from top -> bottom over the gradient span.
            float span = Math.Max(gradientSpan, 0.05f);
            float gradH = height * span;
            const int strips = 24;
            float clipTop = min.Y;
            float clipBottom = Math.Min(max.Y, min.Y + gradH);
            if (clipBottom - clipTop <= 0f) return;

            for (int i = 0; i < strips; ++i)
            {
                float t0 = (float)i / strips;
                float t1 = (float)(i + 1) / strips;
                uint col = YerbaColors.LerpColor(top, bottom, t0);

                float y0 = clipTop + (clipBottom - clipTop) * t0;
                float y1 = clipTop + (clipBottom - clipTop) * t1;
                float y0F = (float)Math.Floor(y0);
                float y1F = (float)Math.Ceiling(y1);
                dl.AddRectFilled(new Vector2(min.X, y0F), new Vector2(max.X, y1F), col);
            }
        }

        public static bool IsMouseHoveringRect(Vector2 min, Vector2 max)
        {
            var mouse = ImGui.GetIO().MousePos;
            return mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        }

        // True when the mouse is over any active ImGui widget (so we don't
        // start a window drag/resize while clicking a control).
        public static bool IsMouseHoveringAnyControl()
        {
            return ImGui.IsAnyItemHovered() || ImGui.IsWindowHovered(ImGuiHoveredFlags.None);
        }

        // ── Gradient checkbox ────────────────────────────────────────────────
        public static bool GradientCheckbox(Vector2 min, Vector2 max, ref bool value)
        {
            using var _ = new ImGuiIDScope("chk_" + min.X + "_" + min.Y);
            uint id = ImGui.GetID("chk_" + min.X + "_" + min.Y);
            var storage = ImGui.GetStateStorage();
            float animT = storage.GetFloat(id, value ? 1f : 0f);

            float target = value ? 1f : 0f;
            EaseAnim(ref animT, target, ImGui.GetIO().DeltaTime, 12f);
            storage.SetFloat(id, animT);

            float scale = 1f + 0.08f * (float)Math.Sin(animT * Math.PI);
            var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
            var half = new Vector2((max.X - min.X) * 0.5f * scale, (max.Y - min.Y) * 0.5f * scale);
            var drawMin = center - half;
            var drawMax = center + half;

            var dl = ImGui.GetWindowDrawList();
            if (animT > 0.01f)
            {
                uint top = YerbaColors.LerpColor(YerbaColors.PanelInnerBg, YerbaColors.ToggleOnTop, animT);
                uint bottom = YerbaColors.LerpColor(YerbaColors.PanelInnerBg, YerbaColors.ToggleOnBottom, animT);
                DrawRoundedVerticalGradient(dl, drawMin, drawMax, YerbaLayout.CheckboxRound, top, bottom);
            }
            else
            {
                dl.AddRectFilled(drawMin, drawMax, YerbaColors.PanelInnerBg, YerbaLayout.CheckboxRound);
            }

            if (animT < 1f)
            {
                uint outline = YerbaColors.WithAlpha(YerbaColors.ToggleOutline, 1f - animT);
                dl.AddRect(drawMin, drawMax, outline, YerbaLayout.CheckboxRound, ImDrawFlags.None, YerbaLayout.ToggleOutlineW);
            }

            if (animT > 0.2f)
            {
                float checkT = Math.Clamp((animT - 0.2f) / 0.8f, 0f, 1f);
                uint checkCol = YerbaColors.WithAlpha(YerbaColors.TextActive, checkT);
                var textPos = new Vector2(
                    center.X - ImGui.CalcTextSize("✓").X * 0.5f,
                    center.Y - ImGui.CalcTextSize("✓").Y * 0.5f);
                dl.AddText(textPos, checkCol, "✓");
            }

            bool clicked = IsMouseHoveringRect(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            if (clicked) value = !value;
            return clicked;
        }

        // ── Action button ────────────────────────────────────────────────────
        public static bool ActionButton(Vector2 min, Vector2 max, float round, string label, float fontSize)
        {
            uint id = ImGui.GetID("ab_" + label);
            var storage = ImGui.GetStateStorage();
            float hoverT = storage.GetFloat(id, 0f);

            bool hovered = IsMouseHoveringRect(min, max);
            EaseAnim(ref hoverT, hovered ? 1f : 0f, ImGui.GetIO().DeltaTime, YerbaLayout.ActionBtnAnimSpeed);
            storage.SetFloat(id, hoverT);

            bool held = hoverT > 0.01f && hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            float scale = 1f + YerbaLayout.ActionBtnHoverScale * hoverT;
            if (held) scale -= YerbaLayout.ActionBtnPressScale;

            var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
            var half = new Vector2((max.X - min.X) * 0.5f * scale, (max.Y - min.Y) * 0.5f * scale);
            var drawMin = center - half;
            var drawMax = center + half;

            uint top = YerbaColors.LerpColor(YerbaColors.ActionBtnTop, YerbaColors.ActionBtnHoverTop, hoverT);
            uint bottom = YerbaColors.LerpColor(YerbaColors.ActionBtnBottom, YerbaColors.ActionBtnHoverBottom, hoverT);

            var dl = ImGui.GetWindowDrawList();
            DrawRoundedVerticalGradient(dl, drawMin, drawMax, round, top, bottom, YerbaLayout.ActionBtnGradientSpan);

            float inset = Math.Min(round, (drawMax.X - drawMin.X) * 0.5f);
            dl.AddLine(
                new Vector2(drawMin.X + inset, drawMin.Y + 0.5f),
                new Vector2(drawMax.X - inset, drawMin.Y + 0.5f),
                YerbaColors.ActionBtnHighlight, 1f);

            if (hoverT > 0.01f)
            {
                uint outline = YerbaColors.WithAlpha(YerbaColors.ActionBtnHoverOutline, hoverT * 0.85f);
                dl.AddRect(drawMin, drawMax, outline, round, ImDrawFlags.None, 1f);
            }

            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(
                (drawMin.X + drawMax.X) * 0.5f - textSize.X * 0.5f,
                (drawMin.Y + drawMax.Y) * 0.5f - textSize.Y * 0.5f);
            dl.AddText(textPos, YerbaColors.TextActive, label);

            return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        }

        // ── Rounded slider ───────────────────────────────────────────────────
        public static bool RoundedSlider(Vector2 min, Vector2 max, ref float value, float minVal, float maxVal, string? label = null)
        {
            value = Math.Clamp(value, minVal, maxVal);
            float t = (value - minVal) / (maxVal - minVal);

            uint id = ImGui.GetID("sl_" + (label ?? min.X.ToString()));
            var storage = ImGui.GetStateStorage();
            float hoverT = storage.GetFloat(id, 0f);

            bool hovered = IsMouseHoveringRect(min, max);
            EaseAnim(ref hoverT, hovered ? 1f : 0f, ImGui.GetIO().DeltaTime, 10f);
            storage.SetFloat(id, hoverT);

            float sliderRound = (max.Y - min.Y) * 0.5f;
            uint trackBg = YerbaColors.LerpColor(YerbaColors.PanelInnerBg, YerbaColors.WithAlpha(YerbaColors.PanelInnerBg, 0.7f), hoverT);
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(min, max, trackBg, sliderRound);

            uint trackOutline = YerbaColors.LerpColor(YerbaColors.ToggleOutline, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.5f), hoverT);
            dl.AddRect(min, max, trackOutline, sliderRound, ImDrawFlags.None, YerbaLayout.ToggleOutlineW);

            if (t > 0.01f)
            {
                var fillMax = new Vector2(min.X + t * (max.X - min.X), max.Y);
                DrawRoundedVerticalGradient(dl, min, fillMax, sliderRound, YerbaColors.ToggleOnTop, YerbaColors.ToggleOnBottom);
            }

            float thumbSize = (max.Y - min.Y) + 8f;
            var thumbCenter = new Vector2(min.X + t * (max.X - min.X), (min.Y + max.Y) * 0.5f);
            float thumbScale = 1f + 0.15f * hoverT;
            float thumbR = (thumbSize * 0.5f) * thumbScale;

            dl.AddCircleFilled(new Vector2(thumbCenter.X + 1f, thumbCenter.Y + 1f), thumbR, 0x28000000, 16);
            uint thumbCol = YerbaColors.LerpColor(YerbaColors.TextActive, YerbaColors.IceBlue, hoverT);
            dl.AddCircleFilled(thumbCenter, thumbR, thumbCol, 16);
            dl.AddCircle(thumbCenter, thumbR, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.5f + 0.3f * hoverT), 16, 1.5f);

            bool changed = false;
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetIO().MousePos;
                float newT = Math.Clamp((mouse.X - min.X) / (max.X - min.X), 0f, 1f);
                value = minVal + newT * (maxVal - minVal);
                changed = true;
            }
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && hovered)
            {
                var mouse = ImGui.GetIO().MousePos;
                float newT = Math.Clamp((mouse.X - min.X) / (max.X - min.X), 0f, 1f);
                value = minVal + newT * (maxVal - minVal);
                changed = true;
            }

            if (label != null)
            {
                string valueText = value.ToString("0.0");
                var textSize = ImGui.CalcTextSize(valueText);
                var textPos = new Vector2(
                    (min.X + max.X) * 0.5f - textSize.X * 0.5f,
                    (min.Y + max.Y) * 0.5f - textSize.Y * 0.5f);
                dl.AddText(textPos, YerbaColors.WithAlpha(YerbaColors.TextActive, 0.8f), valueText);
            }

            return changed;
        }

        // ── Keybind field ────────────────────────────────────────────────────
        public static bool KeybindField(Vector2 min, Vector2 max, ref int key, ref bool listening)
        {
            var dl = ImGui.GetWindowDrawList();
            uint bg = listening ? YerbaColors.KeybindBgActive : YerbaColors.KeybindBg;
            uint outline = listening ? YerbaColors.KeybindOutlineActive : YerbaColors.KeybindOutline;

            dl.AddRectFilled(min, max, bg, YerbaLayout.KeybindRound);
            DrawFieldOutline(dl, min, max, outline, YerbaLayout.KeybindRound, YerbaLayout.KeybindOutlineW);

            float iconW = YerbaLayout.KeybindIconW;
            var divMin = new Vector2(min.X + iconW, min.Y + 4f);
            var divMax = new Vector2(min.X + iconW + YerbaLayout.KeybindDivW, max.Y - 4f);
            dl.AddRectFilled(divMin, divMax, YerbaColors.KeybindDivider);

            // keyboard icon (simple glyph)
            var iconCenter = new Vector2(min.X + iconW * 0.5f, (min.Y + max.Y) * 0.5f);
            uint iconCol = listening
                ? YerbaColors.WithAlpha(YerbaColors.KeybindIcon, 0.45f + 0.55f * (0.5f + 0.5f * (float)Math.Sin(ImGui.GetTime() * 6f)))
                : YerbaColors.KeybindIcon;
            dl.AddText(new Vector2(iconCenter.X - 5f, iconCenter.Y - 7f), iconCol, "⌨");

            string text = listening ? "..." : KeyName(key);
            uint textCol = listening ? YerbaColors.KeybindWaitingText : YerbaColors.TextActive;
            var textSize = ImGui.CalcTextSize(text);
            var textPos = new Vector2(
                divMax.X + (max.X - divMax.X - textSize.X) * 0.5f,
                (min.Y + max.Y) * 0.5f - textSize.Y * 0.5f);
            dl.AddText(textPos, textCol, text);

            bool clicked = IsMouseHoveringRect(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            if (clicked) listening = true;

            if (listening)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    listening = false;
                    return true;
                }

                // Mouse buttons via GetAsyncKeyState:
                // M3 (Middle) = VK 0x04, M4 (XButton1) = VK 0x05, M5 (XButton2) = VK 0x06
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) { key = 0x04; listening = false; return true; }
                if ((GetAsyncKeyState(0x05) & 0x8000) != 0) { key = 0x05; listening = false; return true; }
                if ((GetAsyncKeyState(0x06) & 0x8000) != 0) { key = 0x06; listening = false; return true; }

                for (int k = (int)ImGuiKey.NamedKey_BEGIN; k < (int)ImGuiKey.NamedKey_END; ++k)
                {
                    if (ImGui.IsKeyPressed((ImGuiKey)k))
                    {
                        int vk = ImGuiKeyToVk((ImGuiKey)k);
                        if (vk != 0)
                        {
                            key = vk;
                            listening = false;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int ImGuiKeyToVk(ImGuiKey key)
        {
            if (key >= ImGuiKey.A && key <= ImGuiKey.Z) return (int)key - (int)ImGuiKey.A + 0x41;
            if (key >= ImGuiKey._0 && key <= ImGuiKey._9) return (int)key - (int)ImGuiKey._0 + 0x30;
            if (key >= ImGuiKey.F1 && key <= ImGuiKey.F12) return (int)key - (int)ImGuiKey.F1 + 0x70;
            switch (key)
            {
                case ImGuiKey.Space: return 0x20;
                case ImGuiKey.LeftShift: case ImGuiKey.RightShift: return 0x10;
                case ImGuiKey.LeftCtrl: case ImGuiKey.RightCtrl: return 0x11;
                case ImGuiKey.LeftAlt: case ImGuiKey.RightAlt: return 0x12;
                case ImGuiKey.Tab: return 0x09;
                default: return 0;
            }
        }

        private static string KeyName(int vk)
        {
            if (vk == 0) return "none";
            if (vk == 0x01) return "mouse1";
            if (vk == 0x02) return "mouse2";
            if (vk == 0x04) return "mouse3";
            if (vk == 0x05) return "mouse4";
            if (vk == 0x06) return "mouse5";
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x70 + 1);
            switch (vk)
            {
                case 0x20: return "space";
                case 0x10: return "shift";
                case 0x11: return "ctrl";
                case 0x12: return "alt";
                case 0x09: return "tab";
                case 0x1B: return "esc";
                default: return "key" + vk;
            }
        }

        // ── Color picker ─────────────────────────────────────────────────────
        public static bool ColorPicker(Vector2 min, Vector2 max, ref Vector4 color, ref bool pickerOpen, ref bool rainbowMode, string label)
        {
            bool changed = false;
            var dl = ImGui.GetWindowDrawList();

            if (rainbowMode)
            {
                float time = (float)ImGui.GetTime();
                float hue = time * 0.3f % 1f;
                HsvToRgb(hue, 0.8f, 0.95f, out float r, out float g, out float b);
                color = new Vector4(r, g, b, color.W);
            }

            uint previewCol = ImGui.ColorConvertFloat4ToU32(color);

            // checkerboard
            const float checkerSize = 4f;
            uint c1 = 0x282828FF;
            uint c2 = 0x3C3C3CFF;
            for (float y = min.Y; y < max.Y; y += checkerSize)
            {
                for (float x = min.X; x < max.X; x += checkerSize)
                {
                    int ix = (int)((x - min.X) / checkerSize);
                    int iy = (int)((y - min.Y) / checkerSize);
                    uint col = ((ix + iy) % 2 == 0) ? c1 : c2;
                    dl.AddRectFilled(
                        new Vector2(x, y),
                        new Vector2(Math.Min(x + checkerSize, max.X), Math.Min(y + checkerSize, max.Y)),
                        col);
                }
            }

            dl.AddRectFilled(min, max, previewCol, 4f);
            DrawFieldOutline(dl, min, max, YerbaColors.KeybindOutline, 4f, 1f);

            if (IsMouseHoveringRect(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                pickerOpen = !pickerOpen;

            if (pickerOpen)
            {
                const float popupW = 220f;
                const float popupH = 290f;
                var popupPos = new Vector2(max.X + 30f, min.Y - 40f);
                var display = ImGui.GetIO().DisplaySize;
                if (popupPos.X + popupW > display.X - 20f) popupPos.X = min.X - popupW - 30f;
                if (popupPos.Y + popupH > display.Y - 10f) popupPos.Y = display.Y - popupH - 10f;
                if (popupPos.Y < 10f) popupPos.Y = 10f;

                ImGui.SetNextWindowPos(popupPos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(popupW, popupH), ImGuiCond.Always);

                var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove;

                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, YerbaLayout.PanelRound);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
                ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.ColorConvertU32ToFloat4(YerbaColors.PanelInnerBg));
                ImGui.PushStyleColor(ImGuiCol.Border, ImGui.ColorConvertU32ToFloat4(YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.5f)));

                if (ImGui.Begin("##ColorPickerPopup", flags))
                {
                    var pdl = ImGui.GetWindowDrawList();
                    var winPos = ImGui.GetWindowPos();
                    var winSize = ImGui.GetWindowSize();

                    const float titlebarH = 24f;
                    var titlebarMin = winPos;
                    var titlebarMax = new Vector2(winPos.X + winSize.X, winPos.Y + titlebarH);

                    pdl.AddText(new Vector2(winPos.X + 10f, winPos.Y + 5f), YerbaColors.TextActive, label);
                    pdl.AddLine(new Vector2(winPos.X, winPos.Y + titlebarH), new Vector2(winPos.X + winSize.X, winPos.Y + titlebarH),
                        YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.2f), 1f);

                    float contentTop = winPos.Y + titlebarH + 8f;
                    const float pad = 10f;

                    // rainbow checkbox
                    const float rbSize = 13f;
                    var rbMin = new Vector2(winPos.X + pad, contentTop);
                    var rbMax = new Vector2(winPos.X + pad + rbSize, contentTop + rbSize);
                    pdl.AddText(new Vector2(rbMax.X + 5f, contentTop - 1f), YerbaColors.TextActive, "Rainbow");

                    if (rainbowMode)
                    {
                        float time = (float)ImGui.GetTime();
                        float h1 = time * 0.3f % 1f;
                        float h2 = (h1 + 0.33f) % 1f;
                        HsvToRgb(h1, 0.8f, 0.95f, out float r1, out float g1, out float b1);
                        HsvToRgb(h2, 0.8f, 0.95f, out float r2, out float g2, out float b2);
                        pdl.AddRectFilledMultiColor(rbMin, rbMax,
                            ImGui.ColorConvertFloat4ToU32(new Vector4(r1, g1, b1, 1f)),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(r2, g2, b2, 1f)),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(r2, g2, b2, 1f)),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(r1, g1, b1, 1f)));
                    }
                    else
                    {
                        pdl.AddRectFilled(rbMin, rbMax, YerbaColors.KeybindBg, 3f);
                    }
                    DrawFieldOutline(pdl, rbMin, rbMax, YerbaColors.KeybindOutline, 3f, 1f);

                    if (IsMouseHoveringRect(rbMin, rbMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        rainbowMode = !rainbowMode;
                        changed = true;
                    }

                    // SV square + hue bar
                    float pickerTop = contentTop + 20f;
                    float squareSize = popupW - pad * 2f - 20f;
                    var svMin = new Vector2(winPos.X + pad, pickerTop);
                    var svMax = new Vector2(winPos.X + pad + squareSize, pickerTop + squareSize);
                    var hueMin = new Vector2(svMax.X + 5f, svMin.Y);
                    var hueMax = new Vector2(svMax.X + 5f + 15f, svMax.Y);

                    if (!rainbowMode)
                    {
                        RgbToHsv(color.X, color.Y, color.Z, out float h, out float s, out float v);
                        DrawSvSquare(pdl, svMin, svMax, h, ref s, ref v);
                        DrawHueBar(pdl, hueMin, hueMax, ref h);
                        HsvToRgb(h, s, v, out float r, out float g, out float b);
                        color = new Vector4(r, g, b, color.W);
                    }
                    else
                    {
                        uint overlay = YerbaColors.WithAlpha(YerbaColors.PanelInnerBg, 0.7f);
                        pdl.AddRectFilled(svMin, svMax, overlay, 4f);
                        pdl.AddRectFilled(hueMin, hueMax, overlay, 4f);
                        DrawFieldOutline(pdl, svMin, svMax, YerbaColors.WithAlpha(YerbaColors.KeybindOutline, 0.3f), 4f, 1f);
                        DrawFieldOutline(pdl, hueMin, hueMax, YerbaColors.WithAlpha(YerbaColors.KeybindOutline, 0.3f), 4f, 1f);
                    }

                    // alpha bar
                    float alphaTop = svMax.Y + 8f;
                    var alphaMin = new Vector2(winPos.X + pad, alphaTop);
                    var alphaMax = new Vector2(winPos.X + popupW - pad, alphaTop + 14f);
                    if (!rainbowMode)
                    {
                        DrawAlphaBar(pdl, alphaMin, alphaMax, ref color.W, color);
                    }
                    else
                    {
                        uint overlay = YerbaColors.WithAlpha(YerbaColors.PanelInnerBg, 0.7f);
                        pdl.AddRectFilled(alphaMin, alphaMax, overlay, 4f);
                        DrawFieldOutline(pdl, alphaMin, alphaMax, YerbaColors.WithAlpha(YerbaColors.KeybindOutline, 0.3f), 4f, 1f);
                    }

                    pdl.AddText(new Vector2(winPos.X + pad, alphaMax.Y + 4f), YerbaColors.TextActive,
                        $"Opacity: {(int)(color.W * 100f)}%");

                    // close button
                    float closeTop = winPos.Y + popupH - 28f;
                    var closeMin = new Vector2(winPos.X + pad, closeTop);
                    var closeMax = new Vector2(winPos.X + popupW - pad, closeTop + 20f);
                    bool closeHovered = IsMouseHoveringRect(closeMin, closeMax);
                    uint closeBg = closeHovered ? YerbaColors.LerpColor(YerbaColors.PanelInnerBg, YerbaColors.TextActive, 0.1f) : YerbaColors.PanelInnerBg;
                    pdl.AddRectFilled(closeMin, closeMax, closeBg, 3f);
                    DrawFieldOutline(pdl, closeMin, closeMax, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.3f), 3f, 1f);
                    var closeTextSize = ImGui.CalcTextSize("Close");
                    pdl.AddText(new Vector2(
                        (closeMin.X + closeMax.X) * 0.5f - closeTextSize.X * 0.5f,
                        (closeMin.Y + closeMax.Y) * 0.5f - closeTextSize.Y * 0.5f),
                        YerbaColors.TextActive, "Close");

                    if (closeHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        pickerOpen = false;
                        changed = true;
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        var mouse = ImGui.GetIO().MousePos;
                        var winMin = winPos;
                        var winMax = new Vector2(winPos.X + winSize.X, winPos.Y + winSize.Y);
                        if (!(mouse.X >= winMin.X && mouse.X <= winMax.X && mouse.Y >= winMin.Y && mouse.Y <= winMax.Y)
                            && !(mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y))
                        {
                            pickerOpen = false;
                            changed = true;
                        }
                    }
                }
                ImGui.End();
                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar(3);
            }

            return changed;
        }

        private static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
        {
            if (s == 0f) { r = g = b = v; return; }
            h = h % 1f / (60f / 360f);
            int i = (int)h;
            float f = h - i;
            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }

        private static void RgbToHsv(float r, float g, float b, out float h, out float s, out float v)
        {
            float K = 0f;
            if (g < b) { (g, b) = (b, g); K = -1f; }
            if (r < g) { (r, g) = (g, r); K = -2f / 6f - K; }
            float chroma = r - Math.Min(g, b);
            h = Math.Abs(K + (g - b) / (6f * chroma + 1e-20f));
            s = chroma / (r + 1e-20f);
            v = r;
        }

        private static void DrawSvSquare(ImDrawListPtr dl, Vector2 min, Vector2 max, float hue, ref float sat, ref float val)
        {
            const int steps = 32;
            float stepX = (max.X - min.X) / steps;
            float stepY = (max.Y - min.Y) / steps;

            for (int y = 0; y < steps; ++y)
            {
                float v0 = 1f - (float)y / steps;
                float v1 = 1f - (float)(y + 1) / steps;
                for (int x = 0; x < steps; ++x)
                {
                    float s0 = (float)x / steps;
                    float s1 = (float)(x + 1) / steps;
                    HsvToRgb(hue, s0, v0, out float r0, out float g0, out float b0);
                    HsvToRgb(hue, s1, v0, out float r1, out float g1, out float b1);
                    HsvToRgb(hue, s1, v1, out float r2, out float g2, out float b2);
                    HsvToRgb(hue, s0, v1, out float r3, out float g3, out float b3);
                    var p0 = new Vector2(min.X + x * stepX, min.Y + y * stepY);
                    var p2 = new Vector2(p0.X + stepX, p0.Y + stepY);
                    dl.AddRectFilledMultiColor(p0, p2,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(r0, g0, b0, 1f)),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(r1, g1, b1, 1f)),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(r2, g2, b2, 1f)),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(r3, g3, b3, 1f)));
                }
            }

            dl.AddRect(min, max, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.3f), 0f, ImDrawFlags.None, 1f);

            if (IsMouseHoveringRect(min, max) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetIO().MousePos;
                sat = Math.Clamp((mouse.X - min.X) / (max.X - min.X), 0f, 1f);
                val = 1f - Math.Clamp((mouse.Y - min.Y) / (max.Y - min.Y), 0f, 1f);
            }

            var cursor = new Vector2(min.X + sat * (max.X - min.X), min.Y + (1f - val) * (max.Y - min.Y));
            dl.AddCircleFilled(cursor, 6f, 0xFFFFFFFF, 12);
            dl.AddCircle(cursor, 6f, 0xC8000000, 12, 2f);
        }

        private static void DrawHueBar(ImDrawListPtr dl, Vector2 min, Vector2 max, ref float hue)
        {
            const int steps = 64;
            float stepH = (max.Y - min.Y) / steps;
            for (int i = 0; i < steps; ++i)
            {
                float h0 = (float)i / steps;
                float h1 = (float)(i + 1) / steps;
                HsvToRgb(h0, 1f, 1f, out float r0, out float g0, out float b0);
                HsvToRgb(h1, 1f, 1f, out float r1, out float g1, out float b1);
                var p0 = new Vector2(min.X, min.Y + i * stepH);
                var p1 = new Vector2(max.X, min.Y + (i + 1) * stepH);
                dl.AddRectFilledMultiColor(p0, p1,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(r0, g0, b0, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(r0, g0, b0, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(r1, g1, b1, 1f)),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(r1, g1, b1, 1f)));
            }

            dl.AddRect(min, max, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.3f), 0f, ImDrawFlags.None, 1f);

            if (IsMouseHoveringRect(min, max) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetIO().MousePos;
                hue = Math.Clamp((mouse.Y - min.Y) / (max.Y - min.Y), 0f, 1f);
            }

            var cursor = new Vector2((min.X + max.X) * 0.5f, min.Y + hue * (max.Y - min.Y));
            var cMin = new Vector2(min.X - 2f, cursor.Y - 3f);
            var cMax = new Vector2(max.X + 2f, cursor.Y + 3f);
            dl.AddRectFilled(cMin, cMax, 0xFFFFFFFF);
            dl.AddRect(cMin, cMax, 0xC8000000, 0f, ImDrawFlags.None, 2f);
        }

        private static void DrawAlphaBar(ImDrawListPtr dl, Vector2 min, Vector2 max, ref float alpha, Vector4 color)
        {
            const float checkerSize = 6f;
            uint c1 = 0x282828FF;
            uint c2 = 0x3C3C3CFF;
            for (float y = min.Y; y < max.Y; y += checkerSize)
            {
                for (float x = min.X; x < max.X; x += checkerSize)
                {
                    int ix = (int)((x - min.X) / checkerSize);
                    int iy = (int)((y - min.Y) / checkerSize);
                    uint col = ((ix + iy) % 2 == 0) ? c1 : c2;
                    dl.AddRectFilled(
                        new Vector2(x, y),
                        new Vector2(Math.Min(x + checkerSize, max.X), Math.Min(y + checkerSize, max.Y)),
                        col);
                }
            }

            uint colFull = ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 1f));
            uint colZero = ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0f));
            dl.AddRectFilledMultiColor(min, max, colFull, colFull, colZero, colZero);
            dl.AddRect(min, max, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.3f), 0f, ImDrawFlags.None, 1f);

            if (IsMouseHoveringRect(min, max) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetIO().MousePos;
                alpha = 1f - Math.Clamp((mouse.X - min.X) / (max.X - min.X), 0f, 1f);
            }

            var cursor = new Vector2(min.X + (1f - alpha) * (max.X - min.X), (min.Y + max.Y) * 0.5f);
            var cMin = new Vector2(cursor.X - 3f, min.Y - 2f);
            var cMax = new Vector2(cursor.X + 3f, max.Y + 2f);
            dl.AddRectFilled(cMin, cMax, 0xFFFFFFFF);
            dl.AddRect(cMin, cMax, 0xC8000000, 0f, ImDrawFlags.None, 2f);
        }

        private struct ImGuiIDScope : IDisposable
        {
            public ImGuiIDScope(string id) { }
            public void Dispose() { }
        }
    }
}