using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using FoulzExternal.logging;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  DebugConsole — Yerba-styled ImGui debug console rendered inside the
    //  overlay. Shows the same log history as the old WPF console but in the
    //  new UI style (ice-blue border, dot-pattern body, dark header).
    // ────────────────────────────────────────────────────────────────────────
    public static class DebugConsole
    {
        public static bool Open;

        private static bool posInited;
        private static Vector2 pos = new(120f, 120f);
        private static Vector2 size = new(520f, 360f);

        private static bool dragActive;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartPos;

        private static string filter = "";
        private static bool autoScroll = true;
        private static int lastCount = -1;

        public static void Render()
        {
            if (!Open) return;

            var io = ImGui.GetIO();

            if (!posInited)
            {
                posInited = true;
                pos = new Vector2(
                    (io.DisplaySize.X - size.X) * 0.5f + 40f,
                    (io.DisplaySize.Y - size.Y) * 0.5f - 20f);
            }

            // Drag via header
            bool overHeader = !YerbaWidgets.IsMouseHoveringAnyControl() &&
                io.MousePos.X >= pos.X && io.MousePos.X <= pos.X + size.X &&
                io.MousePos.Y >= pos.Y && io.MousePos.Y <= pos.Y + 34f;

            if (!dragActive && io.MouseClicked[0] && overHeader)
            {
                dragActive = true;
                dragStartMouse = io.MousePos;
                dragStartPos = pos;
            }

            if (dragActive)
            {
                if (io.MouseDown[0])
                    pos = Vector2.Max(dragStartPos + (io.MousePos - dragStartMouse), Vector2.Zero);
                else
                    dragActive = false;
            }

            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);

            var flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoBackground;

            ImGui.Begin("##debug_console", flags);
            var dl = ImGui.GetWindowDrawList();

            var min = pos;
            var max = pos + size;

            // Shell background
            dl.AddRectFilled(min, max, YerbaColors.BodyBg, YerbaLayout.CornerR);

            // Header
            var headerMin = min;
            var headerMax = new Vector2(max.X, min.Y + 34f);
            MenuUI.DrawHeaderBackground(dl, headerMin, headerMax, YerbaLayout.CornerR);

            // Header title
            var titleSize = ImGui.CalcTextSize("debug console");
            dl.AddText(new Vector2(headerMin.X + 14f, (headerMin.Y + headerMax.Y) * 0.5f - titleSize.Y * 0.5f),
                YerbaColors.TextActive, "debug console");

            // Close button
            var closeMin = new Vector2(headerMax.X - 34f, headerMin.Y + 4f);
            var closeMax = new Vector2(headerMax.X - 6f, headerMax.Y - 4f);
            bool closeHover = YerbaWidgets.IsMouseHoveringRect(closeMin, closeMax);
            if (closeHover)
                dl.AddRectFilled(closeMin, closeMax, YerbaColors.KeybindBgActive, 4f);
            var xSize = ImGui.CalcTextSize("X");
            dl.AddText(new Vector2((closeMin.X + closeMax.X) * 0.5f - xSize.X * 0.5f,
                (closeMin.Y + closeMax.Y) * 0.5f - xSize.Y * 0.5f),
                closeHover ? YerbaColors.TextActive : YerbaColors.TextIdle, "X");
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && closeHover)
                Open = false;

            // Separator strip
            var sepMin = new Vector2(min.X, headerMax.Y);
            var sepMax = new Vector2(max.X, headerMax.Y + YerbaLayout.SeparatorH);
            MenuUI.DrawGradientSeparator(dl, sepMin, sepMax, true);

            // Body
            var bodyMin = new Vector2(min.X, sepMax.Y);
            var bodyMax = max;
            dl.AddRectFilled(bodyMin, bodyMax, YerbaColors.BodyBg, YerbaLayout.CornerR, ImDrawFlags.RoundCornersBottom);

            // Dot grid
            MenuUI.DrawDotGrid(dl, bodyMin, bodyMax);

            // Filter input
            float pad = 10f;
            float filterH = 26f;
            var filterMin = new Vector2(bodyMin.X + pad, bodyMin.Y + pad);
            var filterMax = new Vector2(bodyMax.X - pad, filterMin.Y + filterH);

            dl.AddRectFilled(filterMin, filterMax, YerbaColors.SearchBg, YerbaLayout.SearchRound);
            YerbaWidgets.DrawFieldOutline(dl, filterMin, filterMax, YerbaColors.SearchBorder, YerbaLayout.SearchRound, 1f);

            ImGui.SetCursorScreenPos(filterMin);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Vector4.Zero);
            ImGui.SetNextItemWidth(filterMax.X - filterMin.X - 8f);
            string filterBuf = filter;
            if (ImGui.InputText("##console_filter", ref filterBuf, 128))
                filter = filterBuf;
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar();

            var filterTextSize = ImGui.CalcTextSize(filter);
            dl.AddText(new Vector2(filterMin.X + 10f, (filterMin.Y + filterMax.Y) * 0.5f - filterTextSize.Y * 0.5f),
                YerbaColors.TextActive, filter);

            // Log lines
            var listMin = new Vector2(bodyMin.X + pad, filterMax.Y + pad);
            var listMax = new Vector2(bodyMax.X - pad, bodyMax.Y - pad);

            dl.PushClipRect(listMin, listMax, true);

            var history = LogsWindow.History;
            var lines = new List<string>();
            if (string.IsNullOrEmpty(filter))
            {
                lines.AddRange(history);
            }
            else
            {
                foreach (var h in history)
                {
                    if (h.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        lines.Add(h);
                }
            }

            float lineH = 18f;
            float y = listMax.Y - lineH;
            for (int i = lines.Count - 1; i >= 0; --i)
            {
                if (y < listMin.Y) break;
                string msg = lines[i];

                string ts = "", txt = msg;
                if (msg.StartsWith("[") && msg.Contains("]"))
                {
                    int idx = msg.IndexOf(']');
                    ts = msg.Substring(0, idx + 1);
                    txt = msg.Substring(idx + 2);
                }

                var tsSize = ImGui.CalcTextSize(ts);
                dl.AddText(new Vector2(listMin.X, y), YerbaColors.TextIdle, ts);
                dl.AddText(new Vector2(listMin.X + tsSize.X + 8f, y), YerbaColors.TextActive, txt);

                y -= lineH;
            }

            dl.PopClipRect();

            // Outline
            dl.AddRect(min, max, YerbaColors.WithAlpha(YerbaColors.IceBlue, YerbaLayout.OutlineOpacity),
                YerbaLayout.CornerR, ImDrawFlags.None, YerbaLayout.IceBorder);

            ImGui.End();
        }
    }
}