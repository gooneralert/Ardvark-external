using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  MenuUI — C# port of the Yerba menu (made by maybach_gh from dopamina)
    //  Fixed 579x455 shell drawn inside a real ImGui window so mouse input is
    //  captured and every widget is interactive. Header with logo + nav tabs +
    //  search, gradient separator, dot-pattern body. All Ardvark features are
    //  wired into the Yerba-style tabs in MenuUITabs.cs (partial class).
    // ────────────────────────────────────────────────────────────────────────
    public static partial class MenuUI
    {
        // The menu is a normal window that is always open by default.
        public static bool Open { get; private set; } = true;

        // F7 menu toggle (optional -> closes/reopens the always-on window)
        private const int VK_F7 = 0x76;
        private static bool kbPrev;

        // ── Nav tabs (matching Yerba header) ────────────────────────────────
        public enum NavTab { Aimbot, Visuals, Character, World, Misc, Scripts, Settings, Count }
        public static NavTab activeTab = NavTab.Settings;
        private static string SearchQuery = "";

        // ── State — settings panel (mirrors Yerba settings_state.h) ────────
        public static bool vsync;
        public static bool showWatermark = true;
        public static bool streamproof;
        public static bool dexExplorer;
        public static bool keybindList;
        public static bool accentColorEnabled = true;
        public static int menuKey = 0x5A; // 'Z' default like Yerba
        public static bool menuKeyListening;
        public static bool debugConsole; // default off — shows the debug console

        // ── State — configs panel (mirrors Yerba configs_state.h) ──────────
        public static string newConfigName = "new config";
        public static readonly List<string> configNames = new();
        public static int selectedConfig = -1;

        // ── Accent color + color picker state ───────────────────────────────
        public static Vector4 globalAccent = new(0.19f, 0.44f, 0.61f, 1.0f); // Yerba default blue
        public static bool colorPickerOpen;
        public static bool rainbowMode;

        // ── Animation speed (test slider in Yerba) ──────────────────────────
        public static float testSliderValue = 50f;

        // ── Menu open animation ─────────────────────────────────────────────
        private static float menuOpenT = 1f;

        // ── Window geometry (draggable + resizable) ─────────────────────────
        public static Vector2 MenuPos { get; private set; } = new(100f, 100f);
        public static Vector2 MenuSize { get; private set; } = new(
            YerbaLayout.WindowW + YerbaLayout.OuterBorder * 2f,
            YerbaLayout.WindowH + YerbaLayout.OuterBorder * 2f);
        private static bool menuPosInited;

        // drag state
        private static bool dragActive;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartPos;

        // resize state: bit0=right, bit1=bottom, bit2=corner
        private static int resizeActive;
        private static Vector2 resizeStartMouse;
        private static Vector2 resizeStartPos;
        private static Vector2 resizeStartSize;

        // auto-attach
        private static bool autoAttachRequested;
        private static bool gameWatcherStarted;

        // ── Native helpers ──────────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        // ── State storage for tab transition animations ─────────────────────
        private static readonly Dictionary<string, float> tabAnim = new();

        // ── Theme application (matches Yerba apply_style) ───────────────────
        private static void ApplyStyle()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = YerbaLayout.CornerR;
            style.WindowBorderSize = 0f;
            style.WindowPadding = Vector2.Zero;
            style.FrameRounding = 6f;
            style.ScrollbarSize = 8f;
            style.AntiAliasedLines = true;
            style.AntiAliasedFill = true;

            var c = style.Colors;
            c[(int)ImGuiCol.WindowBg] = new Vector4(0f, 0f, 0f, 0f);
            c[(int)ImGuiCol.Text] = new Vector4(1f, 1f, 1f, 1f);
            c[(int)ImGuiCol.Border] = new Vector4(0.1f, 0.1f, 0.12f, 1f);
        }

        // ── Optional toggle (F7 or configured menu key) ─────────────────────
        public static void UpdateToggle()
        {
            int toggleKey = menuKey;
            if (toggleKey <= 0) toggleKey = VK_F7;
            bool down = (GetAsyncKeyState(toggleKey) & 0x8000) != 0;
            if (down && !kbPrev) Open = !Open;
            kbPrev = down;
        }

        // ── Render ──────────────────────────────────────────────────────────
        public static void Render()
        {
            var io = ImGui.GetIO();

            UpdateToggle();
            ApplyStyle();
            EnsureCentered(io.DisplaySize);
            // Auto-attach on startup — runs on a background thread so it never
            // blocks the render loop.
            if (!autoAttachRequested) RequestAutoAttach();
            if (!gameWatcherStarted)
            {
                gameWatcherStarted = true;
                StartGameWatcher();
            }

            if (!Open)
            {
                if (menuOpenT > 0f)
                    menuOpenT = Math.Max(menuOpenT - io.DeltaTime * 8f, 0f);
                if (menuOpenT <= 0.01f) return;
            }
            else
            {
                if (menuOpenT < 1f)
                    menuOpenT = Math.Min(menuOpenT + io.DeltaTime * 5f, 1f);
            }

            float fadeT = Math.Clamp(menuOpenT / 0.3f, 0f, 1f);
            float scaleT = 0.95f + 0.05f * menuOpenT;

            // ── Create a real ImGui window so all input is captured ────────
            var winPos = MenuPos;
            var winSize = MenuSize;

            HandleWindowInteraction(winPos, winSize);

            ImGui.SetNextWindowPos(MenuPos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(MenuSize, ImGuiCond.Always);

            var flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground;

            ImGui.Begin("##menu_shell", flags);

            var dl = ImGui.GetWindowDrawList();

            // Panel bounds (inside the outer border)
            var panelMin = winPos + new Vector2(YerbaLayout.OuterBorder, YerbaLayout.OuterBorder);
            var panelMax = panelMin + new Vector2(winSize.X - YerbaLayout.OuterBorder * 2f, winSize.Y - YerbaLayout.OuterBorder * 2f);

            // Scale animation for visual flare (window stays static for input)
            var panelCenter = (panelMin + panelMax) * 0.5f;
            var panelHalf = new Vector2(panelMax.X - panelMin.X, panelMax.Y - panelMin.Y) * 0.5f * scaleT;
            var animMin = panelCenter - panelHalf;
            var animMax = panelCenter + panelHalf;

            var headerMin = animMin;
            var headerMax = new Vector2(animMax.X, animMin.Y + YerbaLayout.HeaderH);

            var separatorMin = new Vector2(animMin.X, headerMax.Y);
            var separatorMax = new Vector2(animMax.X, headerMax.Y + YerbaLayout.SeparatorH);

            var bodyMin = new Vector2(animMin.X, separatorMax.Y);
            var bodyMax = animMax;

            // Fade colors
            uint bodyBg = YerbaColors.WithAlpha(YerbaColors.BodyBg, fadeT);
            uint headerBottom = YerbaColors.WithAlpha(YerbaColors.HeaderBgBottom, fadeT);

            // Shell background (rounded panel)
            dl.AddRectFilled(animMin, animMax, bodyBg, YerbaLayout.CornerR);

            // Header background
            DrawHeaderBackground(dl, headerMin, headerMax, YerbaLayout.CornerR);

            // Header bottom fill (the seam between header and separator)
            dl.AddRectFilled(new Vector2(animMin.X, headerMax.Y - 1f), separatorMax, headerBottom);

            // Separator strip
            dl.AddRectFilled(separatorMin, separatorMax, headerBottom);

            // Body background (bottom rounded corners only)
            dl.AddRectFilled(bodyMin, bodyMax, bodyBg, YerbaLayout.CornerR, ImDrawFlags.RoundCornersBottom);

            // Dot grid
            DrawDotGrid(dl, bodyMin, bodyMax);

            // Content (fade in)
            if (fadeT > 0.1f)
            {
                DrawHeaderBar(dl, headerMin, headerMax);
                DrawHeaderSeparator(dl, separatorMin, separatorMax);
                DrawContentPanels(dl, headerMax.Y);
            }

            // Shell outlines (ice blue + outer black)
            DrawShellOutlines(dl, animMin, animMax);

            ImGui.End();
        }

        // ── Center the menu once ────────────────────────────────────────────
        private static void EnsureCentered(Vector2 displaySize)
        {
            if (menuPosInited) return;
            menuPosInited = true;
            MenuPos = new Vector2(
                (displaySize.X - MenuSize.X) * 0.5f,
                (displaySize.Y - MenuSize.Y) * 0.5f);
        }

        // ── Auto-attach to Roblox on startup ────────────────────────────────
        private static void RequestAutoAttach()
        {
            autoAttachRequested = true;
            try { AutoAttach(); } catch { }
        }

        // Performs a single attach attempt (no one-shot guard) so the
        // background loop can retry until Roblox is found.
        private static void TryAttachOnce()
        {
            try
            {
                var m = new FoulzExternal.Memory();
                bool ok = m.Attach("RobloxPlayerBeta") || m.Attach("RobloxPlayer");
                if (ok)
                {
                    FoulzExternal.storage.Storage.Initialize(m);
                    if (FoulzExternal.storage.Storage.IsInitialized)
                        StartFeatureSystems();
                }
            }
            catch { }
        }

        // Force a fresh attach (used by the ATTACH button). Unlike AutoAttach
        // this is never gated by a one-shot flag, so after switching games
        // you can re-attach and refresh Storage's cached instances.
        public static void Reattach()
        {
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    var m = new FoulzExternal.Memory();
                    bool ok = m.Attach("RobloxPlayerBeta") || m.Attach("RobloxPlayer");
                    if (ok)
                    {
                        FoulzExternal.storage.Storage.Initialize(m);
                        attachStatus = FoulzExternal.storage.Storage.IsInitialized ? "ACTIVE" : "ACTIVE (partial)";
                        if (FoulzExternal.storage.Storage.IsInitialized)
                            StartFeatureSystems();
                    }
                    else
                    {
                        attachStatus = "WAITING";
                    }
                }
                catch { }
            }) { IsBackground = true };
            t.Start();
        }

        // Watchdog: when the game changes (teleport / rejoin / process restart)
        // the DataModel address changes. Detect that cheaply once a second and
        // auto re-attach so features + explorer come back without pressing the
        // ATTACH button. Uses a slow, guarded read (not per-frame) so it can't
        // destabilize the game mid-transition.
        private static void StartGameWatcher()
        {
            var t = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (FoulzExternal.storage.Storage.IsInitialized &&
                            FoulzExternal.SDK.Instance.Mem != null)
                        {
                            long cur = 0;
                            try { cur = FoulzExternal.SDK.Instance.GetDataModel().Address; }
                            catch { cur = 0; }

                            if (cur != 0 &&
                                cur != FoulzExternal.storage.Storage.DataModelInstance.Address)
                            {
                                Reattach();
                                System.Threading.Thread.Sleep(3000); // cooldown after switch
                            }
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(1200);
                }
            }) { IsBackground = true };
            t.Start();
        }

        // ── Custom drag + resize on the no-decoration window ───────────────
        private const float ResizeGrip = 10f;

        private static void HandleWindowInteraction(Vector2 pos, Vector2 size)
        {
            var io = ImGui.GetIO();
            var m = io.MousePos;
            bool uiLive = Open && menuOpenT > 0.99f;

            bool overHeader = uiLive && !YerbaWidgets.IsMouseHoveringAnyControl() &&
                m.X >= pos.X && m.X <= pos.X + size.X &&
                m.Y >= pos.Y && m.Y <= pos.Y + YerbaLayout.HeaderH;

            bool overRight = uiLive &&
                m.X >= pos.X + size.X - ResizeGrip && m.X <= pos.X + size.X &&
                m.Y >= pos.Y + YerbaLayout.HeaderH && m.Y <= pos.Y + size.Y;
            bool overBottom = uiLive &&
                m.Y >= pos.Y + size.Y - ResizeGrip && m.Y <= pos.Y + size.Y &&
                m.X >= pos.X && m.X <= pos.X + size.X;
            bool overCorner = uiLive && overRight && overBottom;

            // start drag
            if (uiLive && resizeActive == 0 && !dragActive && io.MouseClicked[0] && overHeader)
            {
                dragActive = true;
                dragStartMouse = m;
                dragStartPos = pos;
            }

            // start resize
            if (uiLive && !dragActive && resizeActive == 0 && io.MouseClicked[0] && (overCorner || overRight || overBottom))
            {
                resizeActive = (overCorner ? 4 : 0) | (overRight ? 1 : 0) | (overBottom ? 2 : 0);
                resizeStartMouse = m;
                resizeStartPos = pos;
                resizeStartSize = size;
            }

            // drag
            if (dragActive)
            {
                if (io.MouseDown[0])
                {
                    MenuPos = Vector2.Max(dragStartPos + (m - dragStartMouse), Vector2.Zero);
                }
                else
                {
                    dragActive = false;
                }
            }

            // resize
            if (resizeActive != 0)
            {
                if (io.MouseDown[0])
                {
                    var delta = m - resizeStartMouse;
                    var newSize = resizeStartSize;
                    if ((resizeActive & 1) != 0) newSize.X = resizeStartSize.X + delta.X;
                    if ((resizeActive & 2) != 0) newSize.Y = resizeStartSize.Y + delta.Y;

                    const float minW = 430f, minH = 360f;
                    newSize.X = Math.Max(minW, newSize.X);
                    newSize.Y = Math.Max(minH, newSize.Y);
                    MenuSize = newSize;
                }
                else
                {
                    resizeActive = 0;
                }
            }

            // cursor feedback
            if (uiLive)
            {
                if (overCorner) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
                else if (overRight) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                else if (overBottom) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
                else if (overHeader) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

        private static void DrawShellOutlines(ImDrawListPtr dl, Vector2 min, Vector2 max)
        {
            uint outline = YerbaColors.WithAlpha(YerbaColors.IceBlue, YerbaLayout.OutlineOpacity);
            dl.AddRect(min, max, outline, YerbaLayout.CornerR, ImDrawFlags.None, YerbaLayout.IceBorder);

            var outerMin = new Vector2(min.X - YerbaLayout.OuterBorder, min.Y - YerbaLayout.OuterBorder);
            var outerMax = new Vector2(max.X + YerbaLayout.OuterBorder, max.Y + YerbaLayout.OuterBorder);
            dl.AddRect(outerMin, outerMax, YerbaColors.BorderBlack, YerbaLayout.CornerR + YerbaLayout.OuterBorder,
                ImDrawFlags.None, YerbaLayout.OuterBorder);
        }

        // ── Header background (vertical gradient, stripped like Yerba) ─────
        public static void DrawHeaderBackground(ImDrawListPtr dl, Vector2 min, Vector2 max, float cornerR)
        {
            float height = max.Y - min.Y;
            if (height <= 0f) return;

            const int strips = 28;
            float roundSpan = (cornerR > 0f) ? Math.Min(cornerR + 2f, height) : 0f;

            if (roundSpan > 0f)
            {
                float gradT = (roundSpan * 0.5f) / height;
                uint col = YerbaColors.LerpColor(YerbaColors.HeaderBgTop, YerbaColors.HeaderBgBottom, gradT);
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + roundSpan), col, cornerR, ImDrawFlags.RoundCornersTop);
            }

            float restH = height - roundSpan;
            if (restH <= 0f) return;

            for (int i = 0; i < strips; ++i)
            {
                float t0 = (float)i / strips;
                float t1 = (float)(i + 1) / strips;
                float tm = (t0 + t1) * 0.5f;
                float gradT = (roundSpan + restH * tm) / height;
                uint col = YerbaColors.LerpColor(YerbaColors.HeaderBgTop, YerbaColors.HeaderBgBottom, gradT);

                var stripMin = new Vector2(min.X, min.Y + roundSpan + restH * t0);
                var stripMax = new Vector2(max.X, min.Y + roundSpan + restH * t1);
                dl.AddRectFilled(stripMin, stripMax, col);
            }
        }

        // ── Dot grid in body ────────────────────────────────────────────────
        public static void DrawDotGrid(ImDrawListPtr dl, Vector2 min, Vector2 max)
        {
            dl.PushClipRect(min, max, true);
            for (float y = min.Y + YerbaLayout.DotSpacing * 0.5f; y < max.Y; y += YerbaLayout.DotSpacing)
            {
                for (float x = min.X + YerbaLayout.DotSpacing * 0.5f; x < max.X; x += YerbaLayout.DotSpacing)
                    dl.AddCircleFilled(new Vector2(x, y), YerbaLayout.DotRadius, YerbaColors.BodyDot);
            }
            dl.PopClipRect();
        }

        // ── Header bar (logo + nav tabs + search) ───────────────────────────
        private static readonly string[] TabLabels = { "aimbot", "visuals", "character", "world", "misc", "scripts", "settings" };

        private static void DrawHeaderBar(ImDrawListPtr dl, Vector2 min, Vector2 max)
        {
            float x = min.X + YerbaLayout.PadX;
            float centerY = (min.Y + max.Y) * 0.5f;

            // Logo ("A" for Aardvark)
            float logoSize = YerbaLayout.LogoSize;
            var logoPos = new Vector2(x, centerY - logoSize * 0.5f);
            DrawLogo(dl, logoPos, logoSize);
            x += logoSize + YerbaLayout.LogoSepGap;

            // Logo separator
            var sepMin = new Vector2(x, centerY - YerbaLayout.LogoSepH * 0.5f);
            var sepMax = new Vector2(x + YerbaLayout.LogoSepW, centerY + YerbaLayout.LogoSepH * 0.5f);
            dl.AddRectFilled(sepMin, sepMax, YerbaColors.Divider);
            x += YerbaLayout.LogoSepW + YerbaLayout.LogoSepGap;

            // Nav tabs
            var io = ImGui.GetIO();
            for (int i = 0; i < TabLabels.Length; ++i)
            {
                string label = TabLabels[i];
                bool isActive = (int)activeTab == i;

                if (!tabAnim.TryGetValue(label, out float animT))
                    animT = isActive ? 1f : 0f;
                float target = isActive ? 1f : 0f;
                if (animT < target) animT = Math.Min(animT + io.DeltaTime * 8f, target);
                else if (animT > target) animT = Math.Max(animT - io.DeltaTime * 8f, target);
                tabAnim[label] = animT;

                uint col = YerbaColors.LerpColor(YerbaColors.TextIdle, YerbaColors.TextActive, animT);
                float scale = isActive ? 1f + 0.05f * (float)Math.Sin(animT * Math.PI) : 1f;

                var textSize = ImGui.CalcTextSize(label);
                var scaledSize = new Vector2(textSize.X * scale, textSize.Y * scale);
                var center = new Vector2(x + textSize.X * 0.5f, centerY);
                var textPos = new Vector2(
                    center.X - scaledSize.X * 0.5f,
                    center.Y - scaledSize.Y * 0.5f);

                dl.AddText(textPos, col, label);

                if (isActive && animT > 0.1f)
                {
                    float lineT = Math.Clamp((animT - 0.1f) / 0.9f, 0f, 1f);
                    float lineW = textSize.X * lineT;
                    var lineStart = new Vector2(textPos.X + (textSize.X - lineW) * 0.5f, textPos.Y + textSize.Y + 2f);
                    var lineEnd = new Vector2(lineStart.X + lineW, lineStart.Y);
                    dl.AddLine(lineStart, lineEnd, YerbaColors.WithAlpha(YerbaColors.IceBlue, 0.8f * lineT), 2f);
                }

                // Interactive tab hit area
                var itemMin = textPos - new Vector2(6f, 8f);
                var itemMax = new Vector2(textPos.X + textSize.X + 6f, textPos.Y + textSize.Y + 8f);
                if (YerbaWidgets.IsMouseHoveringRect(itemMin, itemMax))
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        activeTab = (NavTab)i;
                }

                x += textSize.X + YerbaLayout.NavGap;
            }

            // Search pill
            var searchMin = new Vector2(max.X - YerbaLayout.PadX - YerbaLayout.SearchW, centerY - YerbaLayout.SearchH * 0.5f);
            var searchMax = new Vector2(max.X - YerbaLayout.PadX, centerY + YerbaLayout.SearchH * 0.5f);
            DrawSearchPill(dl, searchMin, searchMax);
        }

        private static void DrawLogo(ImDrawListPtr dl, Vector2 pos, float size)
        {
            var center = new Vector2(pos.X + size * 0.5f, pos.Y + size * 0.52f);
            string letter = "A";
            var textSize = ImGui.CalcTextSize(letter);
            var textPos = new Vector2(
                center.X - textSize.X * 0.5f,
                center.Y - textSize.Y * 0.55f);

            dl.AddText(new Vector2(textPos.X + 1f, textPos.Y + 1f),
                YerbaColors.WithAlpha(YerbaColors.LogoBlueLo, 0.35f), letter);
            dl.AddText(textPos, YerbaColors.LogoBlueHi, letter);
        }

        private static void DrawSearchPill(ImDrawListPtr dl, Vector2 min, Vector2 max)
        {
            dl.AddRectFilled(min, max, YerbaColors.SearchBg, YerbaLayout.SearchRound);
            YerbaWidgets.DrawFieldOutline(dl, min, max, YerbaColors.SearchBorder, YerbaLayout.SearchRound, YerbaLayout.SearchFieldOutlineW);

            // Search icon
            float iconR = 4.5f;
            var iconCenter = new Vector2(min.X + YerbaLayout.SearchIconPad + iconR, (min.Y + max.Y) * 0.5f);
            dl.AddCircle(iconCenter, iconR, YerbaColors.SearchIcon, 12, 1.4f);
            dl.AddLine(
                new Vector2(iconCenter.X + iconR * 0.65f, iconCenter.Y + iconR * 0.65f),
                new Vector2(iconCenter.X + iconR * 1.5f, iconCenter.Y + iconR * 1.5f),
                YerbaColors.SearchIcon, 1.4f);

            // Search text input is a transparent overlay inside the window
            ImGui.SetCursorScreenPos(min);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(0f, 0f, 0f, 0f));
            ImGui.SetNextItemWidth(max.X - min.X - YerbaLayout.SearchTextPadLeft - 8f);
            string buffer = SearchQuery;
            if (ImGui.InputText("##search", ref buffer, 64))
                SearchQuery = buffer;
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar(2);

            // Draw the query text ourselves (search result display is handled
            // by the tabs; typing shows the text over the pill)
            string query = SearchQuery;
            if (query.Length > 0)
            {
                var textPos = new Vector2(min.X + YerbaLayout.SearchTextPadLeft,
                    (min.Y + max.Y) * 0.5f - ImGui.CalcTextSize(query).Y * 0.5f);
                dl.AddText(textPos, YerbaColors.TextActive, query);
            }
            else
            {
                var hintPos = new Vector2(min.X + YerbaLayout.SearchTextPadLeft,
                    (min.Y + max.Y) * 0.5f - ImGui.CalcTextSize("search").Y * 0.5f);
                dl.AddText(hintPos, YerbaColors.SearchHint, "search");
            }
        }

        // ── Header separator (gradient blue strip) ──────────────────────────
        private static void DrawHeaderSeparator(ImDrawListPtr dl, Vector2 min, Vector2 max)
        {
            DrawGradientSeparator(dl, min, max, true);
        }

        public static void DrawGradientSeparator(ImDrawListPtr dl, Vector2 min, Vector2 max, bool horizontalFade, float fadePower = -1f)
        {
            if (fadePower < 0f) fadePower = YerbaLayout.SeparatorFadePower;

            float width = max.X - min.X;
            float height = max.Y - min.Y;
            if (width <= 0f || height <= 0f) return;

            const int vStrips = 24;

            for (int vx = 0; vx < YerbaLayout.SeparatorSegments; ++vx)
            {
                float tx0 = (float)vx / YerbaLayout.SeparatorSegments;
                float tx1 = (float)(vx + 1) / YerbaLayout.SeparatorSegments;
                float xMid = (tx0 + tx1) * 0.5f;

                float hAlpha = 1f;
                if (horizontalFade)
                {
                    float dist = Math.Abs(xMid - 0.5f) * 2f;
                    hAlpha = (float)Math.Pow(1f - dist, fadePower);
                    if (hAlpha <= 0.04f) continue;
                }

                var colMin = new Vector2(min.X + width * tx0, min.Y);
                var colMax = new Vector2(min.X + width * tx1, max.Y);

                for (int vy = 0; vy < vStrips; ++vy)
                {
                    float ty0 = (float)vy / vStrips;
                    float ty1 = (float)(vy + 1) / vStrips;
                    float tyMid = (ty0 + ty1) * 0.5f;

                    uint col = YerbaColors.WithAlpha(SeparatorColorAt(tyMid), hAlpha);
                    var segMin = new Vector2(colMin.X, min.Y + height * ty0);
                    var segMax = new Vector2(colMax.X, min.Y + height * ty1);
                    dl.AddRectFilled(segMin, segMax, col);
                }
            }
        }

        private static uint SeparatorColorAt(float t)
        {
            uint[] stops = {
                YerbaColors.SepGrad0, YerbaColors.SepGrad1, YerbaColors.SepGrad2,
                YerbaColors.SepGrad3, YerbaColors.SepGrad4, YerbaColors.SepGrad5
            };
            if (t <= 0f) return stops[0];
            if (t >= 1f) return stops[5];
            float scaled = t * 5f;
            int idx = (int)scaled;
            float local = scaled - idx;
            return YerbaColors.LerpColor(stops[idx], stops[idx + 1], local);
        }

        // ── Content panels — dispatch to current tab ────────────────────────
        // Defined in the partial class MenuUITabs.cs
        private static void DrawContentPanels(ImDrawListPtr dl, float separatorBottomY)
        {
            DrawTabContent(dl, separatorBottomY);
        }

        // ── Panel shell (header gradient + inner bg + title) ───────────────
        public static void DrawPanelShell(ImDrawListPtr dl, Vector2 min, Vector2 max, string title)
        {
            var headerMin = min;
            var headerMax = new Vector2(max.X, min.Y + YerbaLayout.PanelHeaderH);
            var innerMin = new Vector2(min.X, headerMax.Y + YerbaLayout.PanelHeaderSep);
            var innerMax = max;

            dl.AddRectFilled(innerMin, innerMax, YerbaColors.PanelInnerBg, YerbaLayout.PanelRound, ImDrawFlags.RoundCornersBottom);
            DrawHeaderBackground(dl, headerMin, headerMax, YerbaLayout.PanelRound);

            // panel header line
            var lineMin = new Vector2(headerMin.X, headerMax.Y);
            var lineMax = new Vector2(headerMax.X, headerMax.Y + YerbaLayout.PanelHeaderSep);
            dl.AddRectFilled(lineMin, lineMax, YerbaColors.PanelHeaderLine);

            // title
            var textSize = ImGui.CalcTextSize(title);
            var textPos = new Vector2(
                headerMin.X + YerbaLayout.PanelPad,
                (headerMin.Y + headerMax.Y) * 0.5f - textSize.Y * 0.5f);
            dl.AddText(textPos, YerbaColors.PanelTitle, title);
        }

        public static bool MatchesSearch(string label, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Shared row layout helper ────────────────────────────────────────
        public static void DrawLabelRow(ImDrawListPtr dl, Vector2 rowMin, Vector2 rowMax, string label)
        {
            var textSize = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(rowMin.X + YerbaLayout.SettingLabelPad,
                (rowMin.Y + rowMax.Y) * 0.5f - textSize.Y * 0.5f), YerbaColors.TextActive, label);
        }

        public static void DrawCheckboxRight(ImDrawListPtr dl, Vector2 rowMin, Vector2 rowMax, ref bool value)
        {
            float size = YerbaLayout.CheckboxSize;
            var cbMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - size, (rowMin.Y + rowMax.Y) * 0.5f - size * 0.5f);
            var cbMax = new Vector2(rowMax.X - YerbaLayout.SettingControlPad, (rowMin.Y + rowMax.Y) * 0.5f + size * 0.5f);
            YerbaWidgets.GradientCheckbox(cbMin, cbMax, ref value);
        }
    }
}