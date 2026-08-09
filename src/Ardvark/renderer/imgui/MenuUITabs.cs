using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using SInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.config;
using FoulzExternal.features.games.universal.btools;
using FoulzExternal.features.games.universal.camera;
using FoulzExternal.features.games.universal.carfly;
using FoulzExternal.features.games.universal.desync;
using FoulzExternal.features.games.universal.flight;
using FoulzExternal.features.games.universal.fps;
using FoulzExternal.features.games.universal.gravity;
using FoulzExternal.features.games.universal.noclip;
using FoulzExternal.features.games.universal.aiming.silent;
using FoulzExternal.features.games.universal.scriptrunner;
using FoulzExternal.features.games.universal.tickrate;
using FoulzExternal.games.universal.aiming;
using FoulzExternal.games.universal.humanoid;
using FoulzExternal.games.universal.visuals;
using FoulzExternal.logging;
using FoulzExternal.logging.notifications;
using FoulzExternal.SDK.caches;
using FoulzExternal.SDK.tphandler;
using FoulzExternal.storage;
using Options;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  MenuUITabs — partial MenuUI that renders all Ardvark feature tabs
    //  (aimbot / visuals / misc / settings / scripts) inside the Yerba shell
    //  using the Yerba two-column panel + widget style.
    // ────────────────────────────────────────────────────────────────────────
    public static partial class MenuUI
    {
        // ── Tab dispatch ────────────────────────────────────────────────────
        private static void DrawTabContent(ImDrawListPtr dl, float separatorBottomY)
        {
            // Anchor content to the actual window position/size so it moves
            // and resizes with the shell when dragged.
            float winW = MenuSize.X;
            float winH = MenuSize.Y;
            float originX = MenuPos.X;
            float originY = MenuPos.Y;

            float contentTop = separatorBottomY + YerbaLayout.ContentTopOffset;
            float contentLeft = originX + YerbaLayout.OuterBorder + YerbaLayout.ContentPadX;
            float contentRight = originX + YerbaLayout.OuterBorder + winW - YerbaLayout.OuterBorder * 2f - YerbaLayout.ContentPadX;
            float contentBottom = originY + YerbaLayout.OuterBorder + winH - YerbaLayout.OuterBorder * 2f - YerbaLayout.ContentPadBottom;

            float columnW = (contentRight - contentLeft - YerbaLayout.ColumnGap) * 0.5f;

            var leftMin = new Vector2(contentLeft, contentTop);
            var leftMax = new Vector2(contentLeft + columnW, contentBottom);
            var rightMin = new Vector2(leftMax.X + YerbaLayout.ColumnGap, contentTop);
            var rightMax = new Vector2(contentRight, contentBottom);

            switch (activeTab)
            {
                case NavTab.Aimbot:
                    DrawAimbotTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.Visuals:
                    DrawVisualsTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.Character:
                    DrawCharacterTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.World:
                    DrawWorldTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.Misc:
                    DrawMiscTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.Scripts:
                    DrawScriptsTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
                case NavTab.Settings:
                    DrawSettingsTab(dl, leftMin, leftMax, rightMin, rightMax);
                    break;
            }
        }

        // ── Panel helpers ───────────────────────────────────────────────────
        private static void BeginPanelContent(Vector2 panelMin, Vector2 panelMax,
            out float innerLeft, out float innerRight, out float innerTop, out float innerBottom)
        {
            innerLeft = panelMin.X + YerbaLayout.PanelPad;
            innerRight = panelMax.X - YerbaLayout.PanelPad;
            innerTop = panelMin.Y + YerbaLayout.PanelHeaderH + YerbaLayout.PanelHeaderSep;
            innerBottom = panelMax.Y - YerbaLayout.PanelPad;
        }

        private static float RowY(float y) => y;
        private static float NextRow(float y) => y + YerbaLayout.SettingRowH + YerbaLayout.SettingRowGap;

        private static Vector2 RowRectMin(float innerLeft, float y) => new(innerLeft, y);

        private static void DrawSettingRow(ImDrawListPtr dl, float innerLeft, float innerRight, float y, string label, Action<Vector2, Vector2> control)
        {
            if (!MatchesSearch(label, SearchQuery)) return;
            var rowMin = new Vector2(innerLeft, y);
            var rowMax = new Vector2(innerRight, y + YerbaLayout.SettingRowH);
            DrawLabelRow(dl, rowMin, rowMax, label);
            control(rowMin, rowMax);
        }

        private static void DrawColorRow(ImDrawListPtr dl, float innerLeft, float innerRight, float y, string label, ref Vector4 color)
        {
            if (!MatchesSearch(label, SearchQuery)) return;
            var rowMin = new Vector2(innerLeft, y);
            var rowMax = new Vector2(innerRight, y + YerbaLayout.SettingRowH);
            DrawLabelRow(dl, rowMin, rowMax, label);

            float size = YerbaLayout.CheckboxSize + 2f;
            var colorMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - size, (rowMin.Y + rowMax.Y) * 0.5f - size * 0.5f);
            var colorMax = new Vector2(rowMax.X - YerbaLayout.SettingControlPad, (rowMin.Y + rowMax.Y) * 0.5f + size * 0.5f);
            YerbaWidgets.ColorPicker(colorMin, colorMax, ref color, ref colorPickerOpen, ref rainbowMode, label);
        }

        private static void DrawSliderRow(ImDrawListPtr dl, float innerLeft, float innerRight, float y, string label, ref float value, float min, float max)
        {
            if (!MatchesSearch(label, SearchQuery)) return;
            var rowMin = new Vector2(innerLeft, y);
            var rowMax = new Vector2(innerRight, y + YerbaLayout.SettingRowH);
            DrawLabelRow(dl, rowMin, rowMax, label);

            var sliderMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - 100f, (rowMin.Y + rowMax.Y) * 0.5f - 5f);
            var sliderMax = new Vector2(rowMax.X - YerbaLayout.SettingControlPad, (rowMin.Y + rowMax.Y) * 0.5f + 5f);
            YerbaWidgets.RoundedSlider(sliderMin, sliderMax, ref value, min, max, label);
        }

        private static void DrawKeybindRow(ImDrawListPtr dl, float innerLeft, float innerRight, float y, string label, Func<int> getKey, Action<int> setKey, ref bool listening)
        {
            if (!MatchesSearch(label, SearchQuery)) return;
            var rowMin = new Vector2(innerLeft, y);
            var rowMax = new Vector2(innerRight, y + YerbaLayout.SettingRowH);
            DrawLabelRow(dl, rowMin, rowMax, label);

            var keyMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - YerbaLayout.KeybindW, (rowMin.Y + rowMax.Y) * 0.5f - YerbaLayout.KeybindH * 0.5f);
            var keyMax = new Vector2(keyMin.X + YerbaLayout.KeybindW, keyMin.Y + YerbaLayout.KeybindH);
            int k = getKey();
            if (YerbaWidgets.KeybindField(keyMin, keyMax, ref k, ref listening))
                setKey(k);
        }

        // ── AIMBOT TAB ──────────────────────────────────────────────────────
        private static void DrawAimbotTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            // Left: aim
            DrawPanelShell(dl, leftMin, leftMax, "aim");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            // Aimbot on toggle
            DrawSettingRow(dl, ll, lr, y, "aimbot", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.Aimbot));
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "type", (a, b) =>
            {
                int aimType = Settings.Aiming.AimingType;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "camera", "mouse", "off" }[Math.Clamp(aimType, 0, 2)], YerbaLayout.ConfigBtnFont))
                {
                    aimType = (aimType + 1) % 3;
                    Settings.Aiming.AimingType = aimType;
                }
            });
            y = NextRow(y);

            DrawKeybindRow(dl, ll, lr, y, "aim key", () => Settings.Aiming.AimbotKey.Key, v => Settings.Aiming.AimbotKey.Key = v, ref KB_AimKey);
            y = NextRow(y);

            DrawSliderRow(dl, ll, lr, y, "smoothness x", ref Settings.Aiming.SmoothnessX, 0f, 1f);
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "smoothness y", ref Settings.Aiming.SmoothnessY, 0f, 1f);
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "sticky target", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.StickyAim));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "fov check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.ShowFOV));
            y = NextRow(y);

            if (Settings.Aiming.ShowFOV)
            {
                DrawSliderRow(dl, ll, lr, y, "fov size", ref Settings.Aiming.FOV, 10f, 600f);
                y = NextRow(y);
                DrawSettingRow(dl, ll, lr, y, "fov filled", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.FillFOV));
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "prediction", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.Prediction));
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "prediction x", ref Settings.Aiming.PredictionX, 0f, 4f);
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "prediction y", ref Settings.Aiming.PredictionY, 0f, 4f);
            y = NextRow(y);

            DrawSliderRow(dl, ll, lr, y, "sensitivity", ref Settings.Aiming.Sensitivity, 0.1f, 5f);
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "max distance", ref Settings.Aiming.Range, 50f, 5000f);
            y = NextRow(y);

            // ── ported module settings ─────────────────────────────────────────
            DrawSettingRow(dl, ll, lr, y, "toggle type", (a, b) =>
            {
                int tt = Settings.Aiming.ToggleType;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "hold", "toggle" }[Math.Clamp(tt, 0, 1)], YerbaLayout.ConfigBtnFont))
                {
                    tt = (tt + 1) % 2;
                    Settings.Aiming.ToggleType = tt;
                }
            });
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "aim part", (a, b) =>
            {
                int p = Settings.Aiming.AimPart;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "head", "upper", "lower", "hrp", "cursor", "point", "calf" }[Math.Clamp(p, 0, 6)], YerbaLayout.ConfigBtnFont))
                {
                    p = (p + 1) % 7;
                    Settings.Aiming.AimPart = p;
                }
            });
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "fov anchor", (a, b) =>
            {
                int f = Settings.Aiming.FOVType;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "cursor", "center" }[Math.Clamp(f, 0, 1)], YerbaLayout.ConfigBtnFont))
                {
                    f = (f + 1) % 2;
                    Settings.Aiming.FOVType = f;
                }
            });
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "easing", (a, b) =>
            {
                int e = Settings.Aiming.SmoothingStyle;
                string[] names = { "linear", "speed", "quad-in", "quad-out", "quad-io", "cubic-in", "cubic-out", "cubic-io", "sine-out", "sine-in", "sine-io" };
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, names[Math.Clamp(e, 0, 10)], YerbaLayout.ConfigBtnFont))
                {
                    e = (e + 1) % 11;
                    Settings.Aiming.SmoothingStyle = e;
                }
            });
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "use fov", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.UseFOV));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "auto switch", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.Autoswitch));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "unlock on death", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.UnlockOnDeath));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "range check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.RangeCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "health check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.HealthCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "knock check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.KnockCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "camera shake", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Aiming.CamlockShake));
            y = NextRow(y);
            if (Settings.Aiming.CamlockShake)
            {
                DrawSliderRow(dl, ll, lr, y, "shake strength", ref Settings.Aiming.CamlockShakeX, 0f, 5f);
                y = NextRow(y);
            }

            // Right: silent aim
            DrawPanelShell(dl, rightMin, rightMax, "silent aim");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            float ry = rt;

            // Silent aim on toggle
            DrawSettingRow(dl, rl, rr, ry, "silent aim", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Silent.SilentAimbot));
            ry = NextRow(ry);

            DrawKeybindRow(dl, rl, rr, ry, "silent key", () => Settings.Silent.SilentAimbotKey.Key, v => Settings.Silent.SilentAimbotKey.Key = v, ref KB_SilentKey);
            ry = NextRow(ry);

            DrawSettingRow(dl, rl, rr, ry, "silent method", (a, b) =>
            {
                int method = Settings.Silent.SilentMethod;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "off", "rivals", "raycast", "magic bullet" }[Math.Clamp(method, 0, 3)], YerbaLayout.ConfigBtnFont))
                {
                    method = (method + 1) % 4;
                    Settings.Silent.SilentMethod = method;
                    Settings.Silent.RaycastSilent = method == 2;
                    Settings.Silent.MagicBullet = method == 3;
                }
            });
            ry = NextRow(ry);

            DrawSettingRow(dl, rl, rr, ry, "always on", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Silent.AlwaysOn));
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "fov check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Silent.ShowSilentFOV));
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "visualizer", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Silent.SilentVisualizer));
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "prediction", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Silent.SPrediction));
            ry = NextRow(ry);

            DrawSliderRow(dl, rl, rr, ry, "prediction x", ref Settings.Silent.PredictionX, 0f, 4f);
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "prediction y", ref Settings.Silent.PredictionY, 0f, 4f);
            ry = NextRow(ry);

            if (Settings.Silent.ShowSilentFOV)
            {
                DrawSliderRow(dl, rl, rr, ry, "fov size", ref Settings.Silent.SFOV, 10f, 600f);
            }
        }

        // keybind listening state
        private static bool KB_AimKey;
        private static bool KB_SilentKey;
        private static bool KB_FlyKey;
        private static bool KB_CarKey;
        private static bool KB_NoclipKey;
        private static bool KB_FreecamKey;

        // ── VISUALS TAB ─────────────────────────────────────────────────────
        private static Vector4 espBoxColor = new(1, 1, 1, 1);
        private static Vector4 espBoxFillColor = new(0.25f, 0.35f, 1f, 0.6f);
        private static Vector4 espNameColor = new(1, 1, 1, 1);
        private static Vector4 espSkeletonColor = new(0.75f, 0.82f, 1f, 1);
        private static Vector4 espHealthColor = new(0.2f, 1f, 0.3f, 1);
        private static Vector4 espDistanceColor = new(1, 1, 1, 1);
        private static Vector4 espHeadColor = new(1, 1, 1, 1);
        private static Vector4 espTracerColor = new(0.75f, 0.82f, 1f, 1);
        private static Vector4 espChinaHatColor = new(1, 0.2f, 0.2f, 1);

        private static void DrawVisualsTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            DrawPanelShell(dl, leftMin, leftMax, "esp");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            DrawSettingRow(dl, ll, lr, y, "enabled", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Enabled));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "draw local", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.LocalPlayerESP));
            y = NextRow(y);

            // Box ESP — box config only appears once "bounding box" is toggled on
            DrawSettingRow(dl, ll, lr, y, "bounding box", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.BoxESP));
            y = NextRow(y);
            if (Settings.Visuals.BoxESP)
            {
                DrawSettingRow(dl, ll, lr, y, "box fill", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.FilledBox));
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "name", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Name));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "skeleton", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Skeleton));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "health bar", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Health));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "health text", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.HealthText));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "distance", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Distance));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "head circle", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.HeadCircle));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "tracer", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Tracers));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "china hat", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.ChinaHat));
            y = NextRow(y);

            // Non-ESP filters
            DrawSettingRow(dl, ll, lr, y, "dead check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.DeadCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "distance check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.DistanceCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "offscreen arrows", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.OffscreenArrows));
            y = NextRow(y);

            // Chams — extra options pop up only after toggling chams on
            DrawSettingRow(dl, ll, lr, y, "chams", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Visuals.Chams));
            y = NextRow(y);

            // ── Right: per-feature settings, shown only when toggled on ───────
            DrawPanelShell(dl, rightMin, rightMax, "settings");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            float ry = rt;

            if (Settings.Visuals.BoxESP)
            {
                DrawSettingRow(dl, rl, rr, ry, "box mode", (a, b) =>
                {
                    // box mode: only the regular bounding box remains (corner ESP removed)
                    Settings.Visuals.BoxMode = 0;
                    _ = YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                        new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, "regular", YerbaLayout.ConfigBtnFont);
                });
                ry = NextRow(ry);
                DrawColorRow(dl, rl, rr, ry, "box color", ref espBoxColor);
                ry = NextRow(ry);
                if (Settings.Visuals.FilledBox)
                {
                    DrawColorRow(dl, rl, rr, ry, "box fill color", ref espBoxFillColor);
                    ry = NextRow(ry);
                }
            }

            if (Settings.Visuals.Name)
            {
                DrawColorRow(dl, rl, rr, ry, "name color", ref espNameColor);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "name size", ref Settings.Visuals.NameSize, 8f, 32f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.Skeleton)
            {
                DrawColorRow(dl, rl, rr, ry, "skeleton color", ref espSkeletonColor);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.Health || Settings.Visuals.HealthText)
            {
                DrawColorRow(dl, rl, rr, ry, "health color", ref espHealthColor);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.Distance)
            {
                DrawColorRow(dl, rl, rr, ry, "distance color", ref espDistanceColor);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "distance size", ref Settings.Visuals.DistanceSize, 8f, 32f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.HeadCircle)
            {
                DrawColorRow(dl, rl, rr, ry, "head color", ref espHeadColor);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "head scale", ref Settings.Visuals.HeadCircleMaxScale, 0.5f, 4f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.Tracers)
            {
                DrawColorRow(dl, rl, rr, ry, "tracer color", ref espTracerColor);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "tracer thickness", ref Settings.Visuals.TracerThickness, 0.5f, 6f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.ChinaHat)
            {
                DrawColorRow(dl, rl, rr, ry, "china hat color", ref espChinaHatColor);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.DistanceCheck)
            {
                DrawSliderRow(dl, rl, rr, ry, "max distance", ref Settings.Visuals.MaxDistance, 50f, 5000f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.OffscreenArrows)
            {
                DrawSliderRow(dl, rl, rr, ry, "arrow size", ref Settings.Visuals.ArrowSize, 6f, 30f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "arrow radius", ref Settings.Visuals.ArrowRadius, 40f, 400f);
                ry = NextRow(ry);
            }

            if (Settings.Visuals.Chams)
            {
                DrawSettingRow(dl, rl, rr, ry, "chams mode", (a, b) =>
                {
                    int cm = Settings.Visuals.ChamsMode;
                    if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                        new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, cm == 1 ? "wireframe" : "solid", YerbaLayout.ConfigBtnFont))
                    {
                        cm = (cm == 1) ? 0 : 1;
                        Settings.Visuals.ChamsMode = cm;
                    }
                });
                ry = NextRow(ry);

                DrawSliderRow(dl, rl, rr, ry, "chams fill alpha", ref Settings.Visuals.ChamsFillAlpha, 0f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "chams outline alpha", ref Settings.Visuals.ChamsOutlineAlpha, 0f, 1f);
                ry = NextRow(ry);
            }
        }

        // ── CHARACTER TAB (movement stuff) ─────────────────────────────────
        private static void DrawCharacterTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            // Left: movement
            DrawPanelShell(dl, leftMin, leftMax, "movement");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            DrawSettingRow(dl, ll, lr, y, "walkspeed", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Humanoid.WalkspeedEnabled));
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "ws value", ref Settings.Humanoid.Walkspeed, 1f, 500f);
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "jump power", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Humanoid.JumpPowerEnabled));
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "jump value", ref Settings.Humanoid.JumpPower, 50f, 500f);
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "gravity", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Gravity.Enabled));
            y = NextRow(y);
            DrawSliderRow(dl, ll, lr, y, "gravity value", ref Settings.Gravity.Value, -500f, 500f);
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "noclip", (a, b) => DrawCheckboxRight(dl, a, b, ref noclip.Enabled));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "noclip bind", (a, b) => DrawCheckboxRight(dl, a, b, ref noclip.BindMode));
            y = NextRow(y);
            DrawKeybindRow(dl, ll, lr, y, "noclip key", () => noclip.Bind.Key, v => noclip.Bind.Key = v, ref KB_NoclipKey);
            y = NextRow(y);

            // Right: flight / misc
            DrawPanelShell(dl, rightMin, rightMax, "flight & tools");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            float ry = rt;

            DrawSettingRow(dl, rl, rr, ry, "fly", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Flight.VFlight));
            ry = NextRow(ry);
            DrawKeybindRow(dl, rl, rr, ry, "fly key", () => Settings.Flight.VFlightBind.Key, v => Settings.Flight.VFlightBind.Key = v, ref KB_FlyKey);
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "fly speed", ref Settings.Flight.VFlightSpeed, 5f, 1000f);
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "flight method", (a, b) =>
            {
                int method = Settings.Flight.VFlightMethod;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, method == 0 ? "position" : "velocity", YerbaLayout.ConfigBtnFont))
                {
                    method = (method + 1) % 2;
                    Settings.Flight.VFlightMethod = method;
                }
            });
            ry = NextRow(ry);

            DrawSettingRow(dl, rl, rr, ry, "car fly", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.CarFly.CarFlyEnabled));
            ry = NextRow(ry);
            DrawKeybindRow(dl, rl, rr, ry, "car key", () => Settings.CarFly.CarFlyBind.Key, v => Settings.CarFly.CarFlyBind.Key = v, ref KB_CarKey);
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "car speed", ref Settings.CarFly.CarFlySpeed, 50f, 2000f);
            ry = NextRow(ry);

            DrawSettingRow(dl, rl, rr, ry, "tickrate", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Tickrate.Enabled));
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "tickrate value", ref Settings.Tickrate.Value, 1f, 5000f);
            ry = NextRow(ry);

            DrawSettingRow(dl, rl, rr, ry, "btools", (a, b) => DrawCheckboxRight(dl, a, b, ref btools.Enabled));
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "tool", (a, b) =>
            {
                int tool = btools.SelectedTool;
                if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                    new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "hammer", "grab", "clone" }[Math.Clamp(tool, 0, 2)], YerbaLayout.ConfigBtnFont))
                {
                    tool = (tool + 1) % 3;
                    btools.SelectedTool = tool;
                }
            });
        }

        // ── WORLD TAB (geeg lad world features) ────────────────────────────
        private static void DrawWorldTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            var w = Settings.World;

            // Left: lighting
            DrawPanelShell(dl, leftMin, leftMax, "lighting");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            DrawSettingRow(dl, ll, lr, y, "no shadow", (a, b) => DrawCheckboxRight(dl, a, b, ref w.NoShadow));
            y = NextRow(y);

            DrawSettingRow(dl, ll, lr, y, "time changer", (a, b) => DrawCheckboxRight(dl, a, b, ref w.TimeChanger));
            y = NextRow(y);
            if (w.TimeChanger)
            {
                DrawSliderRow(dl, ll, lr, y, "clock time", ref w.ClockTime, 0f, 24f);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "ambient", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Ambient));
            y = NextRow(y);
            if (w.Ambient)
            {
                DrawColorRow(dl, ll, lr, y, "ambient color", ref w.AmbientCol);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "outdoor", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Outdoor));
            y = NextRow(y);
            if (w.Outdoor)
            {
                DrawColorRow(dl, ll, lr, y, "outdoor color", ref w.OutdoorCol);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "brightness", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Brightness));
            y = NextRow(y);
            if (w.Brightness)
            {
                DrawSliderRow(dl, ll, lr, y, "bri value", ref w.BrightnessVal, 0f, 20f);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "exposure", (a, b) => DrawCheckboxRight(dl, a, b, ref w.ExposureOn));
            y = NextRow(y);
            if (w.ExposureOn)
            {
                DrawSliderRow(dl, ll, lr, y, "exposure val", ref w.Exposure, -5f, 5f);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "light", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Light));
            y = NextRow(y);
            if (w.Light)
            {
                DrawColorRow(dl, ll, lr, y, "light color", ref w.LightCol);
                y = NextRow(y);
                DrawSliderRow(dl, ll, lr, y, "light dir x", ref w.LightDirX, -1f, 1f);
                y = NextRow(y);
                DrawSliderRow(dl, ll, lr, y, "light dir y", ref w.LightDirY, -1f, 1f);
                y = NextRow(y);
                DrawSliderRow(dl, ll, lr, y, "light dir z", ref w.LightDirZ, -1f, 1f);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "fog", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Fog));
            y = NextRow(y);
            if (w.Fog)
            {
                DrawSliderRow(dl, ll, lr, y, "fog start", ref w.FogStart, 0f, 100f);
                y = NextRow(y);
                DrawSliderRow(dl, ll, lr, y, "fog end", ref w.FogEnd, 0f, 2000f);
                y = NextRow(y);
                DrawColorRow(dl, ll, lr, y, "fog color", ref w.FogColor);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "env scale", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Env));
            y = NextRow(y);
            if (w.Env)
            {
                DrawSliderRow(dl, ll, lr, y, "env diffuse", ref w.EnvDiffuse, 0f, 2f);
                y = NextRow(y);
                DrawSliderRow(dl, ll, lr, y, "env specular", ref w.EnvSpecular, 0f, 2f);
                y = NextRow(y);
            }

            DrawSettingRow(dl, ll, lr, y, "color shift", (a, b) => DrawCheckboxRight(dl, a, b, ref w.ColorShift));
            y = NextRow(y);
            if (w.ColorShift)
            {
                DrawColorRow(dl, ll, lr, y, "shift top", ref w.ShiftTop);
                y = NextRow(y);
                DrawColorRow(dl, ll, lr, y, "shift bot", ref w.ShiftBot);
                y = NextRow(y);
            }

            // Right: atmosphere / sky / effects
            DrawPanelShell(dl, rightMin, rightMax, "atmosphere & sky");
            BeginPanelContent(rightMin, rightMax, out var rl, out var rr, out var rt, out var rb);
            float ry = rt;

            DrawSettingRow(dl, rl, rr, ry, "atmosphere", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Atmosphere));
            ry = NextRow(ry);
            if (w.Atmosphere)
            {
                DrawSliderRow(dl, rl, rr, ry, "density", ref w.AtmoDensity, 0f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "haze", ref w.AtmoHaze, 0f, 10f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "glare", ref w.AtmoGlare, 0f, 10f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "atmo offset", ref w.AtmoOffset, 0f, 1f);
                ry = NextRow(ry);
                DrawColorRow(dl, rl, rr, ry, "atmo color", ref w.AtmoColor);
                ry = NextRow(ry);
                DrawColorRow(dl, rl, rr, ry, "atmo decay", ref w.AtmoDecay);
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "sky", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Sky));
            ry = NextRow(ry);
            if (w.Sky)
            {
                DrawSliderRow(dl, rl, rr, ry, "sun size", ref w.SunAngular, 0f, 60f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "moon size", ref w.MoonAngular, 0f, 60f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "orient x", ref w.SkyOrientX, -180f, 180f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "orient y", ref w.SkyOrientY, -180f, 180f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "orient z", ref w.SkyOrientZ, -180f, 180f);
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "skybox changer", (a, b) => DrawCheckboxRight(dl, a, b, ref w.SkyboxChanger));
            ry = NextRow(ry);
            if (w.SkyboxChanger)
            {
                DrawSettingRow(dl, rl, rr, ry, "preset", (a, b) =>
                {
                    int preset = w.SkyboxPreset;
                    string name = preset >= 0 && preset < FoulzExternal.features.games.universal.world.world.SkyboxPresets.Length
                        ? FoulzExternal.features.games.universal.world.world.SkyboxPresets[preset].Name : "?";
                    if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                        new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, name, YerbaLayout.ConfigBtnFont))
                    {
                        preset = (preset + 1) % FoulzExternal.features.games.universal.world.world.SkyboxPresets.Length;
                        w.SkyboxPreset = preset;
                    }
                });
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "bloom", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Bloom));
            ry = NextRow(ry);
            if (w.Bloom)
            {
                DrawSliderRow(dl, rl, rr, ry, "bloom inten", ref w.BloomIntensity, 0f, 5f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "bloom size", ref w.BloomSize, 0f, 56f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "bloom thr", ref w.BloomThreshold, 0f, 3f);
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "color corr", (a, b) => DrawCheckboxRight(dl, a, b, ref w.ColorCorr));
            ry = NextRow(ry);
            if (w.ColorCorr)
            {
                DrawSliderRow(dl, rl, rr, ry, "cc bri", ref w.CcBri, -1f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "cc contrast", ref w.CcCon, -1f, 1f);
                ry = NextRow(ry);
                DrawColorRow(dl, rl, rr, ry, "cc tint", ref w.CcTint);
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "color grade", (a, b) => DrawCheckboxRight(dl, a, b, ref w.ColorGrade));
            ry = NextRow(ry);
            if (w.ColorGrade)
            {
                DrawSettingRow(dl, rl, rr, ry, "tonemapper", (a, b) =>
                {
                    int tm = w.Tonemapper;
                    if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                        new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, tm == 0 ? "default" : "futuristic", YerbaLayout.ConfigBtnFont))
                    {
                        tm = (tm + 1) % 2;
                        w.Tonemapper = tm;
                    }
                });
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "dof", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Dof));
            ry = NextRow(ry);
            if (w.Dof)
            {
                DrawSliderRow(dl, rl, rr, ry, "dof far", ref w.DofFar, 0f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "dof near", ref w.DofNear, 0f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "dof focus", ref w.DofFocus, 0f, 200f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "dof radius", ref w.DofRadius, 0f, 200f);
                ry = NextRow(ry);
            }

            DrawSettingRow(dl, rl, rr, ry, "terrain", (a, b) => DrawCheckboxRight(dl, a, b, ref w.Terrain));
            ry = NextRow(ry);
            if (w.Terrain)
            {
                DrawSliderRow(dl, rl, rr, ry, "grass len", ref w.GrassLen, 0f, 1f);
                ry = NextRow(ry);
                DrawColorRow(dl, rl, rr, ry, "water color", ref w.WaterCol);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "water refl", ref w.WaterRefl, 0f, 1f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "water trans", ref w.WaterTrans, 0f, 1f);
            }
        }

        // ── MISC TAB (checks left, misc right) ─────────────────────────────
        private static bool fpsEnabled;
        private static bool fovChanger;

        private static void DrawMiscTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            // Left: checks
            DrawPanelShell(dl, leftMin, leftMax, "checks");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            DrawSettingRow(dl, ll, lr, y, "teamcheck", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Checks.TeamCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "pf teamcheck", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Checks.PFTeamCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "pf switch team", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Checks.PFSwitchTeam));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "downed check", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Checks.DownedCheck));
            y = NextRow(y);
            DrawSettingRow(dl, ll, lr, y, "transparency", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Checks.TransparencyCheck));

            // Right: misc
            DrawPanelShell(dl, rightMin, rightMax, "misc");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            float ry = rt;

            DrawSettingRow(dl, rl, rr, ry, "fps unlocker", (a, b) =>
            {
                fpsEnabled = Settings.FPS.FPSEnabled;
                DrawCheckboxRight(dl, a, b, ref fpsEnabled);
                Settings.FPS.FPSEnabled = fpsEnabled;
            });
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "external fps", ref Settings.FPS.Value, 0f, 240f);
            ry = NextRow(ry);
            DrawSettingRow(dl, rl, rr, ry, "fov changer", (a, b) =>
            {
                fovChanger = Settings.Camera.FOVEnabled;
                DrawCheckboxRight(dl, a, b, ref fovChanger);
                Settings.Camera.FOVEnabled = fovChanger;
            });
            ry = NextRow(ry);
            DrawSliderRow(dl, rl, rr, ry, "fov value", ref Settings.Camera.FOV, 10f, 120f);
            ry = NextRow(ry);

            // Crosshair (geeg lad style)
            DrawSettingRow(dl, rl, rr, ry, "crosshair", (a, b) => DrawCheckboxRight(dl, a, b, ref Program.crosshairEnabled));
            ry = NextRow(ry);
            if (Program.crosshairEnabled)
            {
                DrawSliderRow(dl, rl, rr, ry, "cross length", ref Program.crosshairLength, 1f, 40f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "cross gap", ref Program.crosshairGap, 0f, 30f);
                ry = NextRow(ry);
                DrawSliderRow(dl, rl, rr, ry, "cross thick", ref Program.crosshairThickness, 1f, 6f);
                ry = NextRow(ry);
                DrawSettingRow(dl, rl, rr, ry, "cross dot", (a, b) => DrawCheckboxRight(dl, a, b, ref Program.crosshairDot));
                ry = NextRow(ry);
                if (Program.crosshairDot)
                {
                    DrawSliderRow(dl, rl, rr, ry, "dot size", ref Program.crosshairDotSize, 1f, 8f);
                }
                ry = NextRow(ry);
            }

            // Freecam (geeg lad Misc.cpp)
            DrawSettingRow(dl, rl, rr, ry, "freecam", (a, b) => DrawCheckboxRight(dl, a, b, ref Settings.Freecam.Enabled));
            ry = NextRow(ry);
            if (Settings.Freecam.Enabled)
            {
                DrawSliderRow(dl, rl, rr, ry, "freecam speed", ref Settings.Freecam.Speed, 5f, 200f);
                ry = NextRow(ry);
                DrawSettingRow(dl, rl, rr, ry, "freecam mode", (a, b) =>
                {
                    int mode = Settings.Freecam.Mode;
                    if (YerbaWidgets.ActionButton(new Vector2(b.X - YerbaLayout.SettingControlPad - 90f, a.Y + 5f),
                        new Vector2(b.X - YerbaLayout.SettingControlPad, b.Y - 5f), 5f, new[] { "hold", "toggle", "always" }[Math.Clamp(mode, 0, 2)], YerbaLayout.ConfigBtnFont))
                    {
                        mode = (mode + 1) % 3;
                        Settings.Freecam.Mode = mode;
                    }
                });
                ry = NextRow(ry);
                if (Settings.Freecam.Mode != 2)
                {
                    DrawKeybindRow(dl, rl, rr, ry, "freecam key", () => Settings.Freecam.Key, v => Settings.Freecam.Key = v, ref KB_FreecamKey);
                }
            }
        }

        // ── SETTINGS TAB (Yerba settings + configs) ────────────────────────
        private static readonly string[] SettingLabels = {
            "menu key", "debug console", "dex explorer", "accent color", "global accent color", "overlay fps"
        };

        private static void DrawSettingsTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            DrawPanelShell(dl, leftMin, leftMax, "settings");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            for (int i = 0; i < SettingLabels.Length; ++i)
            {
                string label = SettingLabels[i];
                if (!MatchesSearch(label, SearchQuery)) continue;

                var rowMin = new Vector2(ll, y);
                var rowMax = new Vector2(lr, y + YerbaLayout.SettingRowH);
                if (i != 5)
                    DrawLabelRow(dl, rowMin, rowMax, label);

                switch (i)
                {
                    case 0:
                    {
                        var keyMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - YerbaLayout.KeybindW, (rowMin.Y + rowMax.Y) * 0.5f - YerbaLayout.KeybindH * 0.5f);
                        var keyMax = new Vector2(keyMin.X + YerbaLayout.KeybindW, keyMin.Y + YerbaLayout.KeybindH);
                        YerbaWidgets.KeybindField(keyMin, keyMax, ref menuKey, ref menuKeyListening);
                        break;
                    }
                    case 1:
                    {
                        // debug console toggle — when on, show the ImGui console
                        DrawCheckboxRight(dl, rowMin, rowMax, ref debugConsole);
                        DebugConsole.Open = debugConsole;
                        break;
                    }
                    case 2:
                    {
                        DrawCheckboxRight(dl, rowMin, rowMax, ref dexExplorer);
                        ExplorerWindow.Open = dexExplorer;
                        break;
                    }
                    case 3:
                    {
                        float size = YerbaLayout.CheckboxSize + 2f;
                        var colorMin = new Vector2(rowMax.X - YerbaLayout.SettingControlPad - size, (rowMin.Y + rowMax.Y) * 0.5f - size * 0.5f);
                        var colorMax = new Vector2(rowMax.X - YerbaLayout.SettingControlPad, (rowMin.Y + rowMax.Y) * 0.5f + size * 0.5f);
                        YerbaWidgets.ColorPicker(colorMin, colorMax, ref globalAccent, ref colorPickerOpen, ref rainbowMode, "Global Accent");
                        break;
                    }
                    case 4: DrawCheckboxRight(dl, rowMin, rowMax, ref accentColorEnabled); break;
                    case 5:
                    {
                        // overlay fps — controls the ImGui render loop rate
                        DrawSliderRow(dl, ll, lr, y, "overlay fps", ref Program.overlayFps, 1f, 240f);
                        break;
                    }
                }

                y = NextRow(y);
            }

            if (MatchesSearch("show watermark", SearchQuery))
            {
                var wmRowMin = new Vector2(ll, y);
                var wmRowMax = new Vector2(lr, y + YerbaLayout.SettingRowH);
                DrawLabelRow(dl, wmRowMin, wmRowMax, "show watermark");
                DrawCheckboxRight(dl, wmRowMin, wmRowMax, ref showWatermark);
                y = NextRow(y);
            }

            if (MatchesSearch("attach", SearchQuery))
            {
                y += YerbaLayout.UnloadTopGap;
                var unloadMin = new Vector2(ll + YerbaLayout.SettingLabelPad, y);
                var unloadMax = new Vector2(lr - YerbaLayout.SettingLabelPad, y + YerbaLayout.UnloadH);
                if (YerbaWidgets.ActionButton(unloadMin, unloadMax, YerbaLayout.UnloadRound, "ATTACH", YerbaLayout.SettingRowFont))
                    Reattach();
            }

            // Right: configs
            DrawPanelShell(dl, rightMin, rightMax, "configs");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            DrawConfigsPanelContent(dl, rl, rr, rt, rb);
        }

        private static void DrawConfigsPanelContent(ImDrawListPtr dl, float innerLeft, float innerRight, float innerTop, float innerBottom)
        {
            float y = innerTop + YerbaLayout.ConfigPad;

            // "list" label
            var listLabelSize = ImGui.CalcTextSize("list");
            dl.AddText(new Vector2(innerLeft + YerbaLayout.ConfigPad, y), YerbaColors.TextActive, "list");
            y += listLabelSize.Y + YerbaLayout.ConfigListGap;

            // Refresh config names occasionally
            if (ImGui.GetTime() >= cfgRefreshAt)
            {
                RefreshConfigNames();
                cfgRefreshAt = (float)ImGui.GetTime() + 1.0f;
            }

            // List box
            float bottomBlockH = YerbaLayout.ConfigInputH + YerbaLayout.ConfigRowGap + YerbaLayout.ConfigActionH;
            var listMin = new Vector2(innerLeft + YerbaLayout.ConfigPad, y);
            var listMax = new Vector2(innerRight - YerbaLayout.ConfigPad, innerBottom - YerbaLayout.ConfigPad - bottomBlockH - YerbaLayout.ConfigRowGap);

            dl.AddRectFilled(listMin, listMax, YerbaColors.ConfigListBg, YerbaLayout.ConfigListRound);
            dl.AddRect(listMin, listMax, YerbaColors.ConfigListBorder, YerbaLayout.ConfigListRound, ImDrawFlags.None, YerbaLayout.ConfigListOutline);

            float itemY = listMin.Y + 6f;
            for (int i = 0; i < configNames.Count; ++i)
            {
                if (itemY + YerbaLayout.ConfigListItemH > listMax.Y - 4f) break;

                var itemMin = new Vector2(listMin.X + 4f, itemY);
                var itemMax = new Vector2(listMax.X - 4f, itemY + YerbaLayout.ConfigListItemH);

                if (i == selectedConfig)
                    dl.AddRectFilled(itemMin, itemMax, YerbaColors.ConfigListSelBg, 4f);

                var itemTextSize = ImGui.CalcTextSize(configNames[i]);
                dl.AddText(new Vector2(itemMin.X + YerbaLayout.ConfigListItemPad,
                    (itemMin.Y + itemMax.Y) * 0.5f - itemTextSize.Y * 0.5f),
                    YerbaColors.TextActive, configNames[i]);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                    YerbaWidgets.IsMouseHoveringRect(itemMin, itemMax))
                {
                    selectedConfig = i;
                }

                itemY += YerbaLayout.ConfigListItemH;
            }

            y = listMax.Y + YerbaLayout.ConfigRowGap;

            // Input field + create button
            var inputMin = new Vector2(innerLeft + YerbaLayout.ConfigPad, y);
            var inputMax = new Vector2(innerRight - YerbaLayout.ConfigCreateW - YerbaLayout.ConfigCreateGap, y + YerbaLayout.ConfigInputH);

            dl.AddRectFilled(inputMin, inputMax, YerbaColors.ConfigInputBg, YerbaLayout.ConfigInputRound);
            YerbaWidgets.DrawFieldOutline(dl, inputMin, inputMax, YerbaColors.ConfigInputBorder, YerbaLayout.ConfigInputRound, YerbaLayout.ConfigInputOutline);

            // transparent input text
            ImGui.SetCursorScreenPos(new Vector2(inputMin.X + YerbaLayout.ConfigInputPad, inputMin.Y + 4f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            string nameBuffer = newConfigName;
            if (ImGui.InputText("##config_name", ref nameBuffer, 64))
                newConfigName = nameBuffer;
            ImGui.PopStyleColor(5);
            ImGui.PopStyleVar();
            ImGui.SetCursorScreenPos(inputMin);
            var inputTextSize = ImGui.CalcTextSize(newConfigName);
            dl.AddText(new Vector2(inputMin.X + YerbaLayout.ConfigInputPad,
                (inputMin.Y + inputMax.Y) * 0.5f - inputTextSize.Y * 0.5f),
                YerbaColors.TextActive, newConfigName);

            var createMin = new Vector2(inputMax.X + YerbaLayout.ConfigCreateGap, y);
            var createMax = new Vector2(innerRight - YerbaLayout.ConfigPad, y + YerbaLayout.ConfigInputH);
            if (YerbaWidgets.ActionButton(createMin, createMax, YerbaLayout.ConfigBtnRound, "create", YerbaLayout.ConfigBtnFont))
                CreateConfig();

            y += YerbaLayout.ConfigInputH + YerbaLayout.ConfigRowGap;

            // Delete + Load buttons
            float actionW = (innerRight - innerLeft - YerbaLayout.ConfigPad * 2f - YerbaLayout.ConfigActionGap) * 0.5f;
            var deleteMin = new Vector2(innerLeft + YerbaLayout.ConfigPad, y);
            var deleteMax = new Vector2(innerLeft + YerbaLayout.ConfigPad + actionW, y + YerbaLayout.ConfigActionH);
            if (YerbaWidgets.ActionButton(deleteMin, deleteMax, YerbaLayout.ConfigBtnRound, "delete", YerbaLayout.ConfigBtnFont))
                DeleteConfig();

            var loadMin = new Vector2(deleteMax.X + YerbaLayout.ConfigActionGap, y);
            var loadMax = new Vector2(deleteMax.X + YerbaLayout.ConfigActionGap + actionW, y + YerbaLayout.ConfigActionH);
            if (YerbaWidgets.ActionButton(loadMin, loadMax, YerbaLayout.ConfigBtnRound, "load", YerbaLayout.ConfigBtnFont))
                LoadSelectedConfig();
        }

        // ── SCRIPTS TAB ─────────────────────────────────────────────────────
        private static string scriptCode = "-- Write your script here\nprint(\"Delete everything and paste your script!\")";
        private static int scriptSel = -1;
        private static string[] scriptItems = Array.Empty<string>();
        private static string scriptOutput = "";

        private static void DrawScriptsTab(ImDrawListPtr dl, Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            DrawPanelShell(dl, leftMin, leftMax, "lua executor");
            BeginPanelContent(leftMin, leftMax, out float ll, out float lr, out float lt, out float lb);
            float y = lt;

            // ── Script selector (Yerba-styled: dark pill + prev/next action buttons)
            if (scriptItems.Length == 0) RefreshScriptList();
            string[] items = scriptItems.Length == 0 ? new[] { "(no scripts)" } : scriptItems;
            int sel = Math.Clamp(scriptSel, 0, items.Length - 1);
            string curName = sel >= 0 && sel < items.Length ? items[sel] : "(no scripts)";

            float selW = lr - ll - YerbaLayout.SettingLabelPad * 2f - 74f;
            var selMin = new Vector2(ll + YerbaLayout.SettingLabelPad, y);
            var selMax = new Vector2(selMin.X + selW, y + YerbaLayout.ConfigInputH);

            dl.AddRectFilled(selMin, selMax, YerbaColors.ConfigInputBg, YerbaLayout.ConfigInputRound);
            YerbaWidgets.DrawFieldOutline(dl, selMin, selMax, YerbaColors.ConfigInputBorder, YerbaLayout.ConfigInputRound, YerbaLayout.ConfigInputOutline);

            var selTextSize = ImGui.CalcTextSize(curName);
            dl.AddText(new Vector2(selMin.X + YerbaLayout.ConfigInputPad,
                (selMin.Y + selMax.Y) * 0.5f - selTextSize.Y * 0.5f), YerbaColors.TextActive, curName);

            var prevMin = new Vector2(selMax.X + 6f, y);
            var prevMax = new Vector2(prevMin.X + 34f, y + YerbaLayout.ConfigInputH);
            if (YerbaWidgets.ActionButton(prevMin, prevMax, YerbaLayout.ConfigBtnRound, "<", YerbaLayout.ConfigBtnFont))
            {
                if (scriptItems.Length > 0)
                {
                    scriptSel = (scriptSel - 1 + scriptItems.Length) % scriptItems.Length;
                    LoadScriptFile(scriptItems[scriptSel]);
                }
            }

            var nextMin = new Vector2(prevMax.X + 6f, y);
            var nextMax = new Vector2(nextMin.X + 34f, y + YerbaLayout.ConfigInputH);
            if (YerbaWidgets.ActionButton(nextMin, nextMax, YerbaLayout.ConfigBtnRound, ">", YerbaLayout.ConfigBtnFont))
            {
                if (scriptItems.Length > 0)
                {
                    scriptSel = (scriptSel + 1) % scriptItems.Length;
                    LoadScriptFile(scriptItems[scriptSel]);
                }
            }
            y = NextRow(y);

            // ── Run + Stop (Yerba action buttons)
            var runMin = new Vector2(ll + YerbaLayout.SettingLabelPad, y);
            var runMax = new Vector2(ll + YerbaLayout.SettingLabelPad + 90f, y + YerbaLayout.UnloadH);
            if (YerbaWidgets.ActionButton(runMin, runMax, YerbaLayout.UnloadRound, "RUN", YerbaLayout.SettingRowFont))
            {
                ScriptEngine.Stop();
                ScriptEngine.Run(scriptCode);
                scriptOutput = "";
            }

            var stopMin = new Vector2(runMax.X + YerbaLayout.ConfigActionGap, y);
            var stopMax = new Vector2(runMax.X + YerbaLayout.ConfigActionGap + 90f, y + YerbaLayout.UnloadH);
            if (YerbaWidgets.ActionButton(stopMin, stopMax, YerbaLayout.UnloadRound, "STOP", YerbaLayout.SettingRowFont))
                ScriptEngine.Stop();
            y = NextRow(y);

            // ── Script editor (dark bg + thin outline, transparent input)
            float editH = 160f;
            var editMin = new Vector2(ll + YerbaLayout.SettingLabelPad, y);
            var editMax = new Vector2(lr - YerbaLayout.SettingControlPad, y + editH);

            dl.AddRectFilled(editMin, editMax, YerbaColors.ConfigListBg, 6f);

            string codeBuffer = scriptCode;
            if (codeBuffer.Length > 1_000_000) codeBuffer = codeBuffer.Substring(0, 1_000_000);
            ImGui.SetCursorScreenPos(editMin);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.87f, 0.9f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(0.2f, 0.35f, 0.45f, 0.4f));
            if (ImGui.InputTextMultiline("##code", ref codeBuffer, 1_000_000,
                new Vector2(editMax.X - editMin.X, editH), ImGuiInputTextFlags.AllowTabInput))
            {
                scriptCode = codeBuffer;
            }
            ImGui.PopStyleColor(6);
            ImGui.PopStyleVar();
            YerbaWidgets.DrawFieldOutline(dl, editMin, editMax, YerbaColors.ConfigListBorder, 6f, 0.5f);
            y += editH + YerbaLayout.ConfigRowGap;

            // ── Console output (same Yerba style)
            dl.AddText(new Vector2(ll + YerbaLayout.SettingLabelPad, y), YerbaColors.TextActive, "console");
            y += 20f;

            while (ScriptEngine.Output.TryDequeue(out var item))
            {
                if (scriptOutput.Length > 8000) scriptOutput = "";
                scriptOutput += item.text + "\n";
            }

            string consoleBuffer = scriptOutput;
            if (consoleBuffer.Length > 100_000) consoleBuffer = consoleBuffer.Substring(Math.Max(0, consoleBuffer.Length - 100_000));
            float consoleH = 90f;
            var consoleMin = new Vector2(ll + YerbaLayout.SettingLabelPad, y);
            var consoleMax = new Vector2(lr - YerbaLayout.SettingControlPad, y + consoleH);

            dl.AddRectFilled(consoleMin, consoleMax, YerbaColors.ConfigListBg, 6f);
            ImGui.SetCursorScreenPos(consoleMin);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.72f, 0.75f, 1f));
            ImGui.InputTextMultiline("##console", ref consoleBuffer, 100_000,
                new Vector2(consoleMax.X - consoleMin.X, consoleH), ImGuiInputTextFlags.ReadOnly);
            ImGui.PopStyleColor(5);
            ImGui.PopStyleVar();
            YerbaWidgets.DrawFieldOutline(dl, consoleMin, consoleMax, YerbaColors.ConfigListBorder, 6f, 0.5f);
            scriptOutput = consoleBuffer;

            // ── Right panel: players (Yerba-styled)
            DrawPanelShell(dl, rightMin, rightMax, "players");
            BeginPanelContent(rightMin, rightMax, out float rl, out float rr, out float rt, out float rb);
            float ry = rt + 8f;

            dl.AddText(new Vector2(rl + YerbaLayout.SettingLabelPad, ry), YerbaColors.TextActive,
                Storage.IsInitialized ? "players:" : "attach to roblox first...");
            ry += 24f;

            if (Storage.IsInitialized)
            {
                try
                {
                    var snap = player.CachedPlayers;
                    if (snap != null)
                    {
                        foreach (var p in snap)
                        {
                            if (!p.IsValid) continue;
                            if (ry > rb - 30f) break;

                            string name = p.GetName() ?? "???";
                            dl.AddText(new Vector2(rl + YerbaLayout.SettingLabelPad, ry), YerbaColors.TextActive, name);

                            var tpMin = new Vector2(rr - YerbaLayout.SettingControlPad - 50f, ry);
                            var tpMax = new Vector2(rr - YerbaLayout.SettingControlPad, ry + 20f);
                            if (YerbaWidgets.ActionButton(tpMin, tpMax, 4f, "tp", 11f))
                                TeleportTo(name);

                            ry += 26f;
                        }
                    }
                }
                catch { }
            }
        }

        private static void LoadScriptFile(string fileName)
        {
            try
            {
                string path = System.IO.Path.Combine(ScriptEngine.ScriptsDir, fileName);
                if (System.IO.File.Exists(path))
                    scriptCode = System.IO.File.ReadAllText(path);
            }
            catch { }
        }

        private static void RefreshScriptList()
        {
            string dir = ScriptEngine.ScriptsDir;
            System.IO.Directory.CreateDirectory(dir);
            var all = new List<string>();
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.lua")) all.Add(System.IO.Path.GetFileName(f));
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.luau")) all.Add(System.IO.Path.GetFileName(f));
            all.Sort();
            scriptItems = all.ToArray();
            if (scriptSel >= scriptItems.Length) scriptSel = scriptItems.Length - 1;
        }

        private static void TeleportTo(string name)
        {
            try
            {
                var tar = playerobjects.CachedPlayerObjects.Find(o => o.Name == name);
                if (tar.address == 0) return;
                var localChar = Storage.LocalPlayerInstance.GetCharacter();
                if (!localChar.IsValid) return;
                var hrp = localChar.FindFirstChild("HumanoidRootPart");
                if (!hrp.IsValid) return;

                var targetPos = tar.HumanoidRootPart.IsValid
                    ? tar.HumanoidRootPart.GetPosition()
                    : (tar.Head.IsValid ? tar.Head.GetPosition() : default);
                var mem = SInstance.Mem;
                if (mem == null) return;
                long prim = mem.ReadPtr(hrp.Address + Offsets.BasePart.Primitive);
                if (prim != 0)
                    mem.Write(prim + Offsets.Primitive.Position, targetPos);
            }
            catch { }
        }

        // ── Config operations (using ConfigManager) ─────────────────────────
        private static float cfgRefreshAt;

        private static void RefreshConfigNames()
        {
            try
            {
                configNames.Clear();
                foreach (var c in ConfigManager.GetAvailableConfigs())
                    configNames.Add(c);
                if (selectedConfig >= configNames.Count) selectedConfig = configNames.Count - 1;
            }
            catch { }
        }

        private static void CreateConfig()
        {
            string name = string.IsNullOrWhiteSpace(newConfigName) ? "default" : newConfigName.Trim();
            foreach (var c in configNames)
            {
                if (string.Equals(c, name, StringComparison.OrdinalIgnoreCase)) return;
            }
            if (ConfigManager.SaveConfig(name))
            {
                RefreshConfigNames();
                selectedConfig = configNames.Count - 1;
                notify.Notify("Config created", $"Configuration '{name}' saved");
            }
        }

        private static void DeleteConfig()
        {
            if (selectedConfig < 0 || selectedConfig >= configNames.Count) return;
            string name = configNames[selectedConfig];
            if (ConfigManager.DeleteConfig(name))
            {
                configNames.RemoveAt(selectedConfig);
                if (selectedConfig >= configNames.Count) selectedConfig = configNames.Count - 1;
                notify.Notify("Config deleted", $"Configuration '{name}' deleted");
            }
        }

        private static void LoadSelectedConfig()
        {
            if (selectedConfig < 0 || selectedConfig >= configNames.Count) return;
            string name = configNames[selectedConfig];
            if (ConfigManager.LoadConfig(name))
            {
                newConfigName = name;
                notify.Notify("Config loaded", $"Configuration '{name}' loaded");
            }
        }

        // ── Attach / status (preserved from Ardvark) ────────────────────────
        private static string attachStatus = "IDLE";
        private static bool featureSystemsStarted;
        private static bool autoAttachStarted;

        private static void StartFeatureSystems()
        {
            if (featureSystemsStarted) return;
            featureSystemsStarted = true;

            // Start each system independently so one failure can't kill the rest.
            void SafeStart(string name, Action start)
            {
                try { start(); }
                catch (Exception ex) { attachStatus = name + " failed"; LogsWindow.Log("[start] {0}: {1}", name, ex.Message); }
            }

            SafeStart("player", player.Start);
            SafeStart("playerobjects", playerobjects.Start);
            SafeStart("humanoid", HumanoidModule.Start);
            SafeStart("tphandler", TPHandler.Start);
            SafeStart("camera", CameraModule.Start);
            SafeStart("visuals", visuals.Start);
            SafeStart("aiming", aiming.Start);
            SafeStart("desync", desync.Start);
            SafeStart("flight", flight.Start);
            SafeStart("carfly", carfly.Start);
            SafeStart("noclip", noclip.Start);
            SafeStart("fps", fps.Start);
            SafeStart("gravity", gravity.Start);
            SafeStart("tickrate", tickrate.Start);
            SafeStart("silent", silentaiming.Start);
            SafeStart("raycast", raycastsilent.Start);
            SafeStart("phantom", phantomsilent.Start);
            SafeStart("btools", btools.Start);
            SafeStart("world", FoulzExternal.features.games.universal.world.world.Start);
            SafeStart("freecam", FoulzExternal.features.games.universal.freecam.freecam.Start);

            if (attachStatus != "ACTIVE") attachStatus = "ACTIVE";
        }

        private static void AutoAttach()
        {
            if (autoAttachStarted) return;
            autoAttachStarted = true;

            // Always run on a background thread — Memory.Attach can block for
            // seconds and would freeze the ImGui render loop if called inline.
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    var m = new FoulzExternal.Memory();
                    bool ok = m.Attach("RobloxPlayerBeta") || m.Attach("RobloxPlayer");
                    if (ok)
                    {
                        Storage.Initialize(m);
                        attachStatus = Storage.IsInitialized ? "ACTIVE" : "ACTIVE (partial)";
                        if (Storage.IsInitialized) StartFeatureSystems();
                    }
                    else
                    {
                        attachStatus = "WAITING";
                        // keep retrying until Roblox is found
                        while (!Storage.IsInitialized)
                        {
                            try
                            {
                                System.Threading.Thread.Sleep(2000);
                                if (Storage.IsInitialized) break;
                                var m2 = new FoulzExternal.Memory();
                                bool ok2 = m2.Attach("RobloxPlayerBeta") || m2.Attach("RobloxPlayer");
                                if (ok2)
                                {
                                    Storage.Initialize(m2);
                                    attachStatus = Storage.IsInitialized ? "ACTIVE" : "ACTIVE (partial)";
                                    if (Storage.IsInitialized) StartFeatureSystems();
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { attachStatus = "ERROR"; }
            }) { IsBackground = true };
            t.Start();
        }
    }
}