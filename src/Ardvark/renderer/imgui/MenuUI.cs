using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
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
using SInstance = FoulzExternal.SDK.Instance;

namespace IMGUI
{
    // ────────────────────────────────────────────────────────────────────────
    //  MenuUI — jew hack-style custom menu replicated in C#/ImGui.NET
    //  F12 toggles the menu. All Ardvark features are preserved.
    // ────────────────────────────────────────────────────────────────────────
    public static class MenuUI
    {
        public static bool Open { get; private set; }

        public static bool ShowWatermark = true;
        public static bool ShowEspPreview = true;

        // theme colors
        public static Vector4 Accent = new(0.75f, 0.82f, 1.0f, 1.0f);
        public static Vector4 TextActive = new(0.92f, 0.93f, 0.96f, 1.0f);
        public static Vector4 TextInactive = new(0.55f, 0.58f, 0.64f, 1.0f);
        public static Vector4 OuterBorder = new(0.08f, 0.09f, 0.11f, 0.78f);
        public static Vector4 ChildFill = new(0.06f, 0.065f, 0.08f, 0.80f);

        // watermark fields: 0=build 1=player 2=place id 3=game id 4=time 5=fps
        // default: only player (user) + fps
        public static bool[] WmFields = { false, true, false, false, false, true };
        public static float WmX = 10, WmY = 10;

        // config state
        private static string cfgName = "default";
        private static int cfgSel = -1;
        private static string[] cfgItems = Array.Empty<string>();
        private static float cfgRefreshAt;

        // script runner state
        private static string scriptCode = "-- Write your script here\nprint(\"Delete everything and paste your script!\")";
        private static int scriptSel = -1;
        private static string[] scriptItems = Array.Empty<string>();
        private static string scriptOutput = "";

        // F7 menu toggle
        private const int VK_F7 = 0x76;
        private static bool kbPrev;

        // tabs
        private static int tab;
        private static readonly string[] tabNames = { "aim", "esp", "misc", "settings" };
        private static readonly float[] tabAnim = new float[4];

        // animation
        private static float vis = 1f;
        private static bool inited;
        private static Vector2 cur, tgt;
        private static bool dragHold;
        private static int rs;
        private static Vector2 rsMouse, rsPos, rsSz;
        private static Vector2 holdSz, holdPos;
        private static bool searchOpen;

        // island
        private static readonly float[] islandAnim = new float[3];
        private static bool luaOpen, explorerOpen, playersOpen;

        // watermark drag
        private static bool wmDragging;
        private static Vector2 wmGrab;

        // misc tab local state
        private static bool fpsEnabled;
        private static int fpsCap = 60;
        private static bool fovChanger;

        // ── Native helpers ───────────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        private static float Approach(float cur, float target, float speed, float dt)
            => cur < target ? Math.Min(cur + speed * dt, target) : Math.Max(cur - speed * dt, target);

        private static float EaseOutCubic(float t)
        {
            float p = 1f - t;
            return 1f - p * p * p;
        }

        private static uint Col(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);
        private static uint AccentU32(int a = 255)
        {
            var v = Accent;
            v.W = Math.Clamp(a / 255f, 0f, 1f);
            return Col(v);
        }
        private static readonly uint LabelColor = Col(new Vector4(0.86f, 0.89f, 0.93f, 0.90f));
        private static readonly uint MutedColor = Col(new Vector4(0.55f, 0.58f, 0.64f, 0.9f));
        private static readonly uint WhiteSoft = Col(new Vector4(0.96f, 0.97f, 1f, 0.9f));

        // ── Layout ───────────────────────────────────────────────────────────
        private const float PadX = 14f;
        private const float ItemGap = 8f;
        private const float TopH = 52f;

        private static void Gap()
        {
            ImGui.SetCursorPosX(PadX);
            ImGui.Dummy(new Vector2(0.01f, ItemGap));
            ImGui.SetCursorPosX(PadX);
        }

        private static void Pad() => ImGui.SetCursorPosX(PadX);

        // ── Custom labeled checkbox (jew-hack style) ────────────────────────
        private static readonly HashSet<string> checkIds = new();
        private static readonly Dictionary<string, float> checkAnimHover = new();
        private static readonly Dictionary<string, float> checkAnimValue = new();

        private static bool StyleCheckbox(string label, ref bool value)
        {
            Pad();
            string id = label;
            bool pressed = ImGui.Checkbox("##" + id, ref value);

            // draw text manually (unstyled)
            var p = ImGui.GetItemRectMin();
            var sz = ImGui.GetItemRectSize();
            var dl = ImGui.GetWindowDrawList();

            if (!checkAnimHover.TryGetValue(id, out float hov)) hov = 0;
            if (!checkAnimValue.TryGetValue(id, out float chk)) chk = value ? 1f : 0f;
            float dt = ImGui.GetIO().DeltaTime;
            hov = Approach(hov, (ImGui.IsItemHovered() || value) ? 1f : 0f, 15f, dt);
            chk = Approach(chk, value ? 1f : 0f, 15f, dt);
            checkAnimHover[id] = hov;
            checkAnimValue[id] = chk;

            float box = 14f;
            ImGui.SameLine(0, 8f);
            var tp = ImGui.GetCursorScreenPos();
            dl.AddText(new Vector2((float)Math.Floor(tp.X), (float)Math.Floor(tp.Y + (box - ImGui.GetFontSize()) * 0.5f)),
                Col(new Vector4(TextActive.X, TextActive.Y, TextActive.Z, TextActive.W * Math.Max(0.6f, hov))), label);

            return pressed;
        }

        // ── Custom colored checkbox row (jew-hack style) ────────────────────
        private static bool StyleCheckboxColor(string label, ref bool value, ref Vector4 color, string colId)
        {
            bool ch = StyleCheckbox(label, ref value);

            // color swatch after checkbox on same line
            ImGui.SameLine(0, ImGui.GetContentRegionAvail().X - 70f - 8f);
            bool colChanged = ColorSwatch("##" + colId, ref color);

            return ch || colChanged;
        }

        private static bool ColorSwatch(string id, ref Vector4 color)
        {
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(60, 18);
            var dl = ImGui.GetWindowDrawList();

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1);
            bool pressed = ImGui.Button(id, size);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1);

            // overlay swatch on top
            dl.AddRectFilled(new Vector2(pos.X + 1, pos.Y + 1), new Vector2(pos.X + size.X - 1, pos.Y + size.Y - 1),
                Col(new Vector4(color.X, color.Y, color.Z, 1f)));
            dl.AddRect(pos, pos + size, Col(new Vector4(1, 1, 1, 0.15f)));

            if (ImGui.IsItemHovered())
                dl.AddRect(pos, pos + size, AccentU32(255), 2f);

            if (pressed)
                ImGui.OpenPopup(id + "_picker");

            bool changed = false;
            if (ImGui.BeginPopup(id + "_picker"))
            {
                changed = ImGui.ColorPicker4("##picker" + id, ref color,
                    ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview);
                ImGui.EndPopup();
            }
            return changed;
        }

        // ── Custom slider with label (jew-hack style) ───────────────────────
        private static bool StyleSlider(string label, ref float value, float min, float max, string fmt = "%0.1f")
        {
            Pad();
            // reserve space for the label above the control
            ImGui.Dummy(new Vector2(0, ImGui.GetFontSize() + 4f));
            Pad();
            bool changed = ImGui.SliderFloat("##" + label, ref value, min, max, fmt);
            var p = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();

            // label above
            dl.AddText(new Vector2((float)Math.Floor(p.X), (float)Math.Floor(p.Y - ImGui.GetFontSize() - 4f)),
                LabelColor, label);
            return changed;
        }

        private static bool StyleSliderInt(string label, ref int value, int min, int max)
        {
            Pad();
            // reserve space for the label above the control
            ImGui.Dummy(new Vector2(0, ImGui.GetFontSize() + 4f));
            Pad();
            bool changed = ImGui.SliderInt("##" + label, ref value, min, max);
            var p = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();
            dl.AddText(new Vector2((float)Math.Floor(p.X), (float)Math.Floor(p.Y - ImGui.GetFontSize() - 4f)),
                LabelColor, label);
            return changed;
        }

        // ── Custom combo (jew-hack style) ───────────────────────────────────
        private static bool StyleCombo(string label, ref int current, string[] items)
        {
            Pad();
            // reserve space for the label above the control
            ImGui.Dummy(new Vector2(0, ImGui.GetFontSize() + 4f));
            Pad();
            bool changed = ImGui.Combo("##" + label, ref current, items, items.Length);
            var p = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();
            dl.AddText(new Vector2((float)Math.Floor(p.X), (float)Math.Floor(p.Y - ImGui.GetFontSize() - 4f)),
                LabelColor, label);
            return changed;
        }

        private static bool StyleMultiCombo(string label, ref bool[] selected, string[] items)
        {
            Pad();
            // reserve space for the label above the control
            ImGui.Dummy(new Vector2(0, ImGui.GetFontSize() + 4f));
            Pad();
            // Build preview
            var sb = new StringBuilder();
            int count = 0;
            for (int i = 0; i < selected.Length && i < items.Length; i++)
            {
                if (selected[i])
                {
                    if (count > 0) sb.Append(", ");
                    sb.Append(items[i]);
                    count++;
                }
            }
            if (count == 0) sb.Append("none showing");

            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(ImGui.GetContentRegionAvail().X, 22);
            var dl = ImGui.GetWindowDrawList();
            bool pressed = ImGui.Button("##" + label, size);
            var p = ImGui.GetItemRectMin();
            var sz = ImGui.GetItemRectSize();
            dl.AddText(new Vector2((float)Math.Floor(p.X), (float)Math.Floor(p.Y - ImGui.GetFontSize() - 4f)),
                LabelColor, label);
            dl.AddText(new Vector2(p.X + 8, p.Y + (sz.Y - ImGui.GetFontSize()) * 0.5f), Col(TextActive), sb.ToString());

            if (pressed) ImGui.OpenPopup(label + "_multi_popup");

            bool changed = false;
            if (ImGui.BeginPopup(label + "_multi_popup"))
            {
                for (int i = 0; i < items.Length; i++)
                {
                    bool sel = selected[i];
                    if (ImGui.Selectable((sel ? "[x] " : "[  ] ") + items[i], sel))
                    {
                        selected[i] = !selected[i];
                        changed = true;
                    }
                }
                ImGui.EndPopup();
            }
            return changed;
        }

        // ── Keybind box ─────────────────────────────────────────────────────
        private static readonly Dictionary<string, bool> kbCapture = new();
        private static readonly Dictionary<string, bool[]> kbSnap = new();

        private static bool KeybindBox(string id, ref int key, ref int mode)
        {
            Pad();
            bool changed = false;

            // label
            var p = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            dl.AddText(p, LabelColor, id);

            // right-aligned key + mode boxes
            string kbId = "kb_" + id;
            float keyW = 72f, modeW = 56f, gap = 5f, rightM = 14f;
            float avail = ImGui.GetContentRegionAvail().X;
            float boxLeft = avail - keyW - gap - modeW - rightM;

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, boxLeft - ImGui.GetFontSize() * 2f));

            string displayKey = kbCapture.TryGetValue(kbId, out bool cap) && cap ? "..." : KeyNameNative(key);
            if (ImGui.Button("##kb_" + id + "_key", new Vector2(keyW, 22)))
            {
                bool now = !(kbCapture.TryGetValue(kbId, out bool c) && c);
                kbCapture[kbId] = now;
                if (now) TakeKeySnapshot(kbId);
            }
            if (kbCapture.TryGetValue(kbId, out bool capturing) && capturing)
            {
                if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
                {
                    key = 0;
                    kbCapture[kbId] = false;
                    changed = true;
                }
                else
                {
                    int captured = FindNewKeySinceSnapshot(kbId);
                    if (captured != 0)
                    {
                        key = captured;
                        kbCapture[kbId] = false;
                        changed = true;
                    }
                }
            }
            ImGui.SameLine(0, gap);

            string modeText = mode switch { 1 => "toggle", 2 => "always", _ => "hold" };
            if (ImGui.Button("##kb_" + id + "_mode", new Vector2(modeW, 22)))
            {
                mode = (mode + 1) % 3;
                changed = true;
            }

            // overlay text on buttons
            var kp = ImGui.GetItemRectMin();
            dl.AddText(new Vector2(kp.X + (modeW - ImGui.CalcTextSize(modeText).X) / 2, kp.Y + (22 - ImGui.GetFontSize()) / 2),
                Col(TextActive), modeText);

            var keyBtnPos = ImGui.GetItemRectMin() - new Vector2(keyW + gap, 0);
            dl.AddText(new Vector2(keyBtnPos.X + (keyW - ImGui.CalcTextSize(displayKey).X) / 2, keyBtnPos.Y + (22 - ImGui.GetFontSize()) / 2),
                Col(TextActive), displayKey);

            return changed;
        }

        private static void TakeKeySnapshot(string id)
        {
            var snap = new bool[256];
            for (int vk = 1; vk < 256; vk++)
                snap[vk] = (GetAsyncKeyState(vk) & 0x8000) != 0;
            kbSnap[id] = snap;
        }

        private static int FindNewKeySinceSnapshot(string id)
        {
            if (!kbSnap.TryGetValue(id, out var snap)) return 0;
            for (int vk = 1; vk < 256; vk++)
            {
                if (vk == 0x01) continue;
                if (!snap[vk] && (GetAsyncKeyState(vk) & 0x8000) != 0)
                    return vk;
            }
            return 0;
        }

        private static string KeyNameNative(int vk)
        {
            if (vk == 0) return "none";
            if (vk == 0x01) return "mouse1";
            if (vk == 0x02) return "mouse2";
            if (vk == 0x04) return "mouse3";
            if (vk == 0x05) return "mouse4";
            if (vk == 0x06) return "mouse5";
            if (vk == VK_F7) return "F7";

            uint scan = MapVirtualKey((uint)vk, 0);
            int lparam = (int)(scan << 16);
            if (vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28
                || vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24
                || vk == 0x2D || vk == 0x2E || vk == 0x6F || vk == 0x90)
                lparam |= (1 << 24);
            var sb = new StringBuilder(64);
            return GetKeyNameText(lparam, sb, 64) > 0 ? sb.ToString() : $"key{vk}";
        }

        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int nMaxCount);

        // ── Child panel (jew-hack style) ────────────────────────────────────
        private static bool BeginChildPanel(string id, Vector2 size, string title)
        {
            var cursor = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            var sizeInt = new Vector2((float)Math.Floor(size.X), (float)Math.Floor(size.Y));

            // outer / inner / fill rects
            var outer = new Vector4(cursor.X, cursor.Y, cursor.X + sizeInt.X, cursor.Y + sizeInt.Y);
            var inner = new Vector4(outer.X + 1, outer.Y + 1, outer.Z - 1, outer.W - 1);
            var fill = new Vector4(outer.X + 2, outer.Y + 1, outer.Z - 2, outer.W - 2);

            bool hasTitle = !string.IsNullOrEmpty(title);

            // background
            dl.AddRectFilled(new Vector2(fill.X, fill.Y), new Vector2(fill.Z, fill.W), Col(ChildFill));

            // header gradient
            if (hasTitle)
            {
                int headerB = (int)fill.Y + 21;
                for (int i = 0; i < 22 && (int)fill.Y + i <= headerB; i++)
                {
                    float t = i / 22f;
                    var grad = new Vector4(
                        0.14f + (Accent.X * 0.18f - 0.14f) * t,
                        0.16f + (Accent.Y * 0.18f - 0.16f) * t,
                        0.22f + (Accent.Z * 0.18f - 0.22f) * t,
                        1f);
                    dl.AddRectFilled(new Vector2(fill.X, (int)fill.Y + i), new Vector2(fill.Z, (int)fill.Y + i + 1), Col(grad));
                }
                // separator below header
                dl.AddRectFilled(new Vector2(fill.X, headerB + 1), new Vector2(fill.Z, headerB + 2),
                    Col(new Vector4(1, 1, 1, 0.06f)));
            }

            // borders
            dl.AddRect(new Vector2(inner.X, inner.Y), new Vector2(inner.Z, inner.W), Col(new Vector4(1, 1, 1, 0.06f)));
            // inner left/right/bottom only (no top)
            dl.AddRect(new Vector2(outer.X, outer.Y), new Vector2(outer.Z, outer.W), Col(new Vector4(1, 1, 1, 0.12f)));

            // title text
            if (hasTitle)
            {
                dl.AddText(new Vector2(fill.X + 6, (float)Math.Floor(fill.Y + (22 - ImGui.GetFontSize()) * 0.5f)),
                    Col(TextActive), title);
            }

            // content child
            float headerH = hasTitle ? 23f : 0f;
            float contentY = cursor.Y + headerH;
            float contentH = sizeInt.Y - headerH;
            ImGui.SetCursorScreenPos(new Vector2(cursor.X + 2, contentY));
            if (contentH < 1) contentH = 1;

            bool ok = ImGui.BeginChild(id, new Vector2(sizeInt.X - 4, contentH), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

            Pad();
            return ok;
        }

        private static void EndChildPanel()
        {
            ImGui.EndChild();
        }

        // ── Push theme ──────────────────────────────────────────────────────
        private static void PushTheme()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 12f;
            style.ChildRounding = 10f;
            style.FrameRounding = 6f;
            style.PopupRounding = 8f;
            style.GrabRounding = 6f;
            style.ScrollbarRounding = 8f;

            style.WindowBorderSize = 0;
            style.ChildBorderSize = 0;
            style.FrameBorderSize = 0;
            style.PopupBorderSize = 0;

            style.WindowPadding = new Vector2(10, 10);
            style.ItemSpacing = new Vector2(8, 6);
            style.ItemInnerSpacing = new Vector2(6, 4);
            style.ScrollbarSize = 8f;

            var c = style.Colors;
            c[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.09f, 0.11f, 0.78f);
            c[(int)ImGuiCol.ChildBg] = ChildFill;
            c[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.11f, 0.14f, 0.95f);
            c[(int)ImGuiCol.Border] = new Vector4(1, 1, 1, 0.08f);
            c[(int)ImGuiCol.Text] = TextActive;
            c[(int)ImGuiCol.TextDisabled] = TextInactive;
            c[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.13f, 0.16f, 0.70f);
            c[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.16f, 0.17f, 0.21f, 0.80f);
            c[(int)ImGuiCol.FrameBgActive] = new Vector4(0.18f, 0.19f, 0.24f, 0.90f);
            c[(int)ImGuiCol.Button] = new Vector4(1, 1, 1, 0.06f);
            c[(int)ImGuiCol.ButtonHovered] = new Vector4(1, 1, 1, 0.12f);
            c[(int)ImGuiCol.ButtonActive] = new Vector4(1, 1, 1, 0.18f);
            c[(int)ImGuiCol.Header] = Vector4.Zero;
            c[(int)ImGuiCol.HeaderHovered] = new Vector4(1, 1, 1, 0.08f);
            c[(int)ImGuiCol.HeaderActive] = new Vector4(1, 1, 1, 0.12f);
            c[(int)ImGuiCol.CheckMark] = new Vector4(Accent.X, Accent.Y, Accent.Z, 1f);
            c[(int)ImGuiCol.SliderGrab] = new Vector4(Accent.X, Accent.Y, Accent.Z, 1f);
            c[(int)ImGuiCol.SliderGrabActive] = new Vector4(Accent.X * 1.1f, Accent.Y * 1.1f, Accent.Z * 1.1f, 1f);
            c[(int)ImGuiCol.Separator] = new Vector4(1, 1, 1, 0.08f);
            c[(int)ImGuiCol.TitleBg] = c[(int)ImGuiCol.WindowBg];
            c[(int)ImGuiCol.TitleBgActive] = c[(int)ImGuiCol.WindowBg];
            c[(int)ImGuiCol.TitleBgCollapsed] = c[(int)ImGuiCol.WindowBg];
            c[(int)ImGuiCol.ScrollbarBg] = new Vector4(0, 0, 0, 0);
            c[(int)ImGuiCol.ScrollbarGrab] = new Vector4(1, 1, 1, 0.15f);
            c[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(1, 1, 1, 0.25f);
            c[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(1, 1, 1, 0.35f);
        }

        // ── Soft glow ───────────────────────────────────────────────────────
        private static void DrawSoftGlow(Vector2 a, Vector2 b, float rnd)
        {
            var back = ImGui.GetBackgroundDrawList();
            var dl = ImGui.GetWindowDrawList();
            float fade = ImGui.GetStyle().Alpha;

            for (int i = 6; i >= 1; i--)
            {
                float t = i / 6f;
                float dist = 8f * t;
                float a01 = (1f - t) * (1f - t);
                int al = (int)(a01 * 16f * fade);
                if (al < 1) continue;
                back.AddRect(
                    new Vector2(a.X - dist, a.Y - dist), new Vector2(b.X + dist, b.Y + dist),
                    Col(new Vector4(190f / 255f, 205f / 255f, 235f / 255f, al / 255f)),
                    rnd + dist * 0.2f, 0, 1.1f + t * 1.1f);
            }

            int edgeA = (int)(22f * fade);
            if (edgeA < 1) edgeA = 1;
            dl.AddRect(a, b, Col(new Vector4(220f / 255f, 230f / 255f, 245f / 255f, edgeA / 255f)), rnd, 0, 1f);
        }

        // ── Watermark ───────────────────────────────────────────────────────
        private static Vector2 wmSize = new(100, 30);

        private static void Watermark(float alpha)
        {
            if (!ShowWatermark || alpha <= 0.001f) return;
            var io = ImGui.GetIO();
            var dl = ImGui.GetForegroundDrawList();

            var segs = new List<string>();
            if (WmFields[0]) segs.Add("build  2026");
            if (WmFields[1])
            {
                string name = "";
                try { if (Storage.IsInitialized) name = Storage.LocalPlayerName; } catch { }
                segs.Add(string.IsNullOrEmpty(name) ? "user  -" : "user  " + name);
            }
            if (WmFields[2])
            {
                long place = 0;
                try { if (Storage.DataModelInstance.IsValid) place = Storage.DataModelInstance.GetPlaceID(); } catch { }
                segs.Add("place  " + place);
            }
            if (WmFields[3])
            {
                long game = 0;
                try { if (Storage.DataModelInstance.IsValid) game = Storage.DataModelInstance.GetGameID(); } catch { }
                segs.Add("game  " + game);
            }
            if (WmFields[4]) segs.Add("time  " + DateTime.Now.ToString("HH:mm:ss"));
            if (WmFields[5]) segs.Add("fps  " + Math.Round(io.Framerate));

            string brand = "jewsploit";
            var brandTs = ImGui.CalcTextSize(brand);
            float textH = brandTs.Y;

            float padX = 14f, padY = 9f, gap = 12f, sepW = 1f;
            float contentW = brandTs.X;
            foreach (var s in segs)
            {
                contentW += gap + sepW + gap;
                contentW += ImGui.CalcTextSize(s).X;
            }
            float totalW = padX * 2f + contentW;
            float totalH = padY * 2f + textH;
            wmSize = new Vector2(totalW, totalH);

            float maxX = Math.Max(0, io.DisplaySize.X - totalW);
            float maxY = Math.Max(0, io.DisplaySize.Y - totalH);
            WmX = Math.Clamp(WmX, 0, maxX);
            WmY = Math.Clamp(WmY, 0, maxY);

            // drag window
            ImGui.SetNextWindowPos(new Vector2(WmX, WmY), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(totalW, totalH), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);

            if (ImGui.Begin("##wm_drag", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground))
            {
                ImGui.InvisibleButton("##wm_drag_btn", new Vector2(totalW, totalH));
                if (ImGui.IsItemActivated())
                {
                    wmDragging = true;
                    wmGrab = io.MousePos - new Vector2(WmX, WmY);
                }
                if (wmDragging)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        WmX = Math.Clamp(io.MousePos.X - wmGrab.X, 0, maxX);
                        WmY = Math.Clamp(io.MousePos.Y - wmGrab.Y, 0, maxY);
                        ImGui.SetWindowPos(new Vector2(WmX, WmY), ImGuiCond.Always);
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        ImGui.SetNextFrameWantCaptureMouse(true);
                    }
                    else wmDragging = false;
                }
                else if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
            ImGui.End();
            ImGui.PopStyleVar(3);

            var o = new Vector2(WmX, WmY);
            var b = new Vector2(o.X + totalW, o.Y + totalH);
            float rnd = 12f;

            var bgV = new Vector4(OuterBorder.X, OuterBorder.Y, OuterBorder.Z, Math.Clamp(OuterBorder.W * alpha, 0.55f, 0.92f));
            dl.AddRectFilled(o, b, Col(bgV), rnd);
            dl.AddRect(o, b, Col(new Vector4(1, 1, 1, 14f / 255f * alpha)), rnd, 0, 1f);

            float ty = (float)Math.Floor(o.Y + (totalH - textH) * 0.5f);
            float x = o.X + padX;

            dl.AddText(new Vector2((float)Math.Floor(x), ty), AccentU32(), brand);
            x += brandTs.X;

            uint muted = Col(new Vector4(160f / 255f, 166f / 255f, 178f / 255f, alpha));
            uint sepC = Col(new Vector4(1, 1, 1, 28f / 255f * alpha));

            foreach (var s in segs)
            {
                x += gap;
                float sy0 = o.Y + padY + 2f;
                float sy1 = o.Y + totalH - padY - 2f;
                dl.AddLine(new Vector2((float)Math.Floor(x), sy0), new Vector2((float)Math.Floor(x), sy1), sepC, 1f);
                x += sepW + gap;
                dl.AddText(new Vector2((float)Math.Floor(x), ty), muted, s);
                x += ImGui.CalcTextSize(s).X;
            }
        }

        // ── Island ──────────────────────────────────────────────────────────
        private static void Island()
        {
            if (!Open) return;
            var io = ImGui.GetIO();

            string[] names = { "lua", "explorer", "players" };
            bool[] tabOn = { luaOpen, explorerOpen, playersOpen };

            float padX = 16f, padY = 8f, tabGap = 22f, hitPad = 8f, hitH = 26f;
            float[] widths = new float[3];
            float rowW = 0;
            for (int i = 0; i < 3; i++)
            {
                widths[i] = ImGui.CalcTextSize(names[i]).X;
                rowW += widths[i] + hitPad * 2f;
                if (i + 1 < 3) rowW += tabGap;
            }

            float boxW = rowW + padX * 2f;
            float boxH = hitH + padY * 2f;
            float bx = (io.DisplaySize.X - boxW) * 0.5f;
            float by = 12f;

            ImGui.SetNextWindowPos(new Vector2(bx, by), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(boxW, boxH), ImGuiCond.Always);

            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, boxH * 0.5f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(OuterBorder.X, OuterBorder.Y, OuterBorder.Z, 0.96f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1, 1, 1, 0.10f));

            ImGui.Begin("##island_pill", flags);
            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            var wsz = ImGui.GetWindowSize();
            float pillR = wsz.Y * 0.5f;

            dl.AddRect(wp, new Vector2(wp.X + wsz.X, wp.Y + wsz.Y),
                Col(new Vector4(1, 1, 1, 22f / 255f)), pillR, 0, 1f);

            float midY = wp.Y + wsz.Y * 0.5f;
            float x = wp.X + padX;

            for (int i = 0; i < 3; i++)
            {
                float bw = widths[i] + hitPad * 2f;
                ImGui.SetCursorScreenPos(new Vector2(x, midY - hitH * 0.5f));
                if (ImGui.InvisibleButton($"##isl{i}", new Vector2(bw, hitH)))
                    tabOn[i] = !tabOn[i];

                bool hov = ImGui.IsItemHovered();
                bool sel = tabOn[i];

                float want = sel ? 1f : 0f;
                islandAnim[i] = Approach(islandAnim[i], want, 14f, io.DeltaTime);
                float t = islandAnim[i];

                int a = 120;
                if (hov) a = 190;
                a = (int)(a + (235 - a) * t);

                float lift = t * 1.5f;
                var ts = ImGui.CalcTextSize(names[i]);
                float tx = x + (bw - ts.X) * 0.5f;
                float ty = midY - ts.Y * 0.5f - lift;

                if (t > 0.01f)
                    dl.AddText(new Vector2(tx + 0.6f, ty + 0.6f), Col(new Vector4(Accent.X, Accent.Y, Accent.Z, 50f / 255f * t)), names[i]);

                dl.AddText(new Vector2(tx, ty), Col(new Vector4(245f / 255f, 248f / 255f, 1f, a / 255f)), names[i]);

                x += bw + tabGap;
            }

            ImGui.End();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(3);

            luaOpen = tabOn[0];
            explorerOpen = tabOn[1];
            playersOpen = tabOn[2];
        }

        // ── Render ──────────────────────────────────────────────────────────
        public static void Render()
        {
            var io = ImGui.GetIO();

            // F7 toggle
            bool kbDown = (GetAsyncKeyState(VK_F7) & 0x8000) != 0;
            if (kbDown && !kbPrev)
                Open = !Open;
            kbPrev = kbDown;

            PushTheme();

            // animation
            float visSpd = Open ? 13f : 26f;
            vis = Approach(vis, Open ? 1f : 0f, visSpd, io.DeltaTime);
            float e = Open ? EaseOutCubic(vis) : vis;

            if (vis >= 0.001f)
                DrawShell(e, vis);

            Island();

            if (Open)
            {
                DrawLuaPanel();
                DrawExplorerPanel();
                DrawPlayersPanel();
            }

            Watermark(1f);
        }

        private static void DrawShell(float e, float vis)
        {
            var io = ImGui.GetIO();

            float s = 0.94f + 0.06f * e;
            bool uiLive = Open && vis > 0.995f && !searchOpen;

            float w = io.DisplaySize.X * 0.33f;
            float h = io.DisplaySize.Y * 0.48f;
            if (w < 640f) w = 640f;
            if (h < 420f) h = 420f;
            if (w > 920f) w = 920f;
            if (h > 740f) h = 740f;

            const float prevW = 380f;
            const float prevH = 500f;
            const float dockGap = 14f;
            float totalW = w;
            if (ShowEspPreview) totalW = w + dockGap + prevW;

            ImGui.SetNextWindowPos(new Vector2((io.DisplaySize.X - totalW) * 0.5f, (io.DisplaySize.Y - h) * 0.5f), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(480f, 300f), new Vector2(float.MaxValue, float.MaxValue));

            var flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
            if (!uiLive) flags |= ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, e);
            ImGui.Begin("##jewsploit_shell", flags);

            float minW = 480f, minH = 300f;
            var wsz = ImGui.GetWindowSize();
            var wp = ImGui.GetWindowPos();

            if (!inited)
            {
                cur = tgt = wp;
                inited = true;
            }

            var dl = ImGui.GetWindowDrawList();
            float rnd = ImGui.GetStyle().WindowRounding;

            if (tab < 0) tab = 0;
            if (tab >= tabNames.Length) tab = tabNames.Length - 1;

            // ── Top bar ──
            float midY = wp.Y + TopH * 0.5f;
            bool onNav = ImGui.IsAnyItemHovered() || ImGui.IsAnyItemActive();

            // logo
            {
                var ts = ImGui.CalcTextSize("jewsploit");
                float lx = wp.X + 18f;
                float ly = midY - ts.Y * 0.5f;
                ImGui.SetCursorScreenPos(new Vector2(lx - 6f, midY - 14f));
                ImGui.InvisibleButton("##logo", new Vector2(ts.X + 12f, 28f));
                int a = ImGui.IsItemHovered() ? 230 : 170;
                dl.AddText(new Vector2(lx, ly), Col(new Vector4(245f / 255f, 248f / 255f, 1f, a / 255f)), "jewsploit");
            }

            // tabs
            const float tabGap = 28f;
            const float hitPadX = 6f;
            const float hitH = 28f;
            float[] widths = new float[4];
            float rowW = 0;
            for (int i = 0; i < 4; i++)
            {
                widths[i] = ImGui.CalcTextSize(tabNames[i]).X;
                rowW += widths[i] + hitPadX * 2f;
                if (i + 1 < 4) rowW += tabGap;
            }
            float rowX = wp.X + (wsz.X - rowW) * 0.5f;
            float x = rowX;
            for (int i = 0; i < 4; i++)
            {
                float bw = widths[i] + hitPadX * 2f;
                ImGui.SetCursorScreenPos(new Vector2(x, midY - hitH * 0.5f));
                if (ImGui.InvisibleButton($"##tab{i}", new Vector2(bw, hitH)))
                    tab = i;

                bool hov = ImGui.IsItemHovered();
                bool sel = tab == i;
                float want = sel ? 1f : 0f;
                tabAnim[i] = Approach(tabAnim[i], want, 14f, io.DeltaTime);
                float t = tabAnim[i];

                int a = 110;
                if (hov) a = 190;
                a = (int)(a + (245 - a) * t);

                var ts = ImGui.CalcTextSize(tabNames[i]);
                float tx = x + hitPadX;
                float ty = midY - ts.Y * 0.5f - t * 3f;

                if (t > 0.01f)
                {
                    uint gc = Col(new Vector4(245f / 255f, 248f / 255f, 1f, (36f * t) / 255f));
                    float g = 1.15f + t * 0.6f;
                    dl.AddText(new Vector2(tx - g, ty), gc, tabNames[i]);
                    dl.AddText(new Vector2(tx + g, ty), gc, tabNames[i]);
                    dl.AddText(new Vector2(tx, ty - g), gc, tabNames[i]);
                    dl.AddText(new Vector2(tx, ty + g), gc, tabNames[i]);
                }
                dl.AddText(new Vector2(tx, ty), Col(new Vector4(245f / 255f, 248f / 255f, 1f, a / 255f)), tabNames[i]);

                x += bw + tabGap;
            }

            // search magnifier
            {
                float cx = wp.X + wsz.X - 22f;
                float cy = midY;
                ImGui.SetCursorScreenPos(new Vector2(cx - 14f, cy - 14f));
                if (ImGui.InvisibleButton("##search", new Vector2(28f, 28f)))
                    searchOpen = !searchOpen;

                bool hov = ImGui.IsItemHovered();
                int a = hov || searchOpen ? 235 : 140;
                uint col = Col(new Vector4(245f / 255f, 248f / 255f, 1f, a / 255f));
                float th = hov || searchOpen ? 1.7f : 1.45f;
                float rd = 4.6f;
                var cc = new Vector2(cx - 1.2f, cy - 1.2f);
                dl.AddCircle(cc, rd, col, 0, th);
                dl.AddLine(new Vector2(cc.X + rd * 0.72f, cc.Y + rd * 0.72f), new Vector2(cx + 5.2f, cy + 5.2f), col, th + 0.25f);
            }

            dl.AddLine(new Vector2(wp.X + 8f, wp.Y + TopH), new Vector2(wp.X + wsz.X - 8f, wp.Y + TopH),
                Col(new Vector4(1, 1, 1, 18f / 255f)), 1f);

            // drag
            var m = io.MousePos;
            bool overTop = m.X >= wp.X && m.X <= wp.X + wsz.X && m.Y >= wp.Y && m.Y <= wp.Y + TopH;

            if (!uiLive) { dragHold = false; rs = 0; }
            if (!io.MouseDown[0]) dragHold = false;
            else if (uiLive && io.MouseClicked[0] && overTop && !onNav && rs == 0)
            {
                dragHold = true;
                tgt = cur = wp;
            }

            bool drag = uiLive && dragHold && ImGui.IsMouseDragging(ImGuiMouseButton.Left);
            if (drag && rs == 0)
            {
                // follow mouse exactly — no lag, no slide
                cur.X += io.MouseDelta.X;
                cur.Y += io.MouseDelta.Y;
                tgt = cur;
                ImGui.SetWindowPos(cur);
                wp = cur;
            }

            // resize
            {
                const float hit = 6f, corner = 14f;
                float x0 = wp.X, y0 = wp.Y, x1 = wp.X + wsz.X, y1 = wp.Y + wsz.Y;

                if (uiLive && rs == 0 && !drag && io.MouseClicked[0])
                {
                    bool bl = m.X >= x0 - hit && m.X <= x0 + corner && m.Y >= y1 - corner && m.Y <= y1 + hit;
                    bool br = m.X >= x1 - corner && m.X <= x1 + hit && m.Y >= y1 - corner && m.Y <= y1 + hit;
                    bool l = !bl && m.X >= x0 - hit && m.X <= x0 + hit && m.Y > y0 + TopH && m.Y <= y1;
                    bool r = !br && m.X >= x1 - hit && m.X <= x1 + hit && m.Y > y0 + TopH && m.Y <= y1;
                    bool b = !bl && !br && m.Y >= y1 - hit && m.Y <= y1 + hit && m.X >= x0 && m.X <= x1;

                    if (bl) rs = 4;
                    else if (br) rs = 5;
                    else if (l) rs = 1;
                    else if (r) rs = 2;
                    else if (b) rs = 3;

                    if (rs > 0) { rsMouse = m; rsPos = wp; rsSz = wsz; }
                }

                if (rs > 0 && io.MouseDown[0])
                {
                    var d = new Vector2(m.X - rsMouse.X, m.Y - rsMouse.Y);
                    var np = rsPos;
                    var ns = rsSz;
                    if (rs == 1 || rs == 4) { ns.X = rsSz.X - d.X; np.X = rsPos.X + d.X; }
                    if (rs == 2 || rs == 5) { ns.X = rsSz.X + d.X; }
                    if (rs == 3 || rs == 4 || rs == 5) { ns.Y = rsSz.Y + d.Y; }

                    if (ns.X < minW)
                    {
                        if (rs == 1 || rs == 4) np.X = rsPos.X + rsSz.X - minW;
                        ns.X = minW;
                    }
                    if (ns.Y < minH) ns.Y = minH;

                    ImGui.SetWindowSize(ns);
                    wsz = ns;
                    cur = tgt = np;
                    ImGui.SetWindowPos(np);
                    wp = np;
                }
                if (rs > 0 && !io.MouseDown[0]) rs = 0;
            }

            if (uiLive && rs == 0)
            {
                // only smooth when NOT dragging (e.g. close animation), drag is instant above
                if (!dragHold)
                {
                    cur = new Vector2(Approach(cur.X, tgt.X, 18f, io.DeltaTime), Approach(cur.Y, tgt.Y, 18f, io.DeltaTime));
                    ImGui.SetWindowPos(cur);
                    wp = ImGui.GetWindowPos();
                }
            }

            if (uiLive)
            {
                holdSz = wsz;
                holdPos = wp;
            }
            else if (holdSz.X > 1f)
            {
                var sz = new Vector2(holdSz.X * s, holdSz.Y * s);
                var pos = new Vector2(
                    holdPos.X + (holdSz.X - sz.X) * 0.5f,
                    holdPos.Y + (holdSz.Y - sz.Y) * 0.5f);
                ImGui.SetWindowSize(sz);
                ImGui.SetWindowPos(pos);
                wsz = sz;
                wp = pos;
                cur = tgt = holdPos;
            }

            DrawSoftGlow(wp, new Vector2(wp.X + wsz.X, wp.Y + wsz.Y), rnd);

            // ── Tab content ──
            {
                const float pad = 14f;
                ImGui.SetCursorPos(new Vector2(pad, TopH + pad));
                string cid = $"##page_{tabNames[tab]}";

                ImGui.BeginChild(cid, new Vector2(wsz.X - pad * 2f, wsz.Y - TopH - pad * 2f), false,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

                switch (tab)
                {
                    case 0: DrawAimTab(); break;
                    case 1: DrawEspTab(); break;
                    case 2: DrawMiscTab(); break;
                    default: DrawSettingsTab(); break;
                }

                ImGui.EndChild();
            }

            var mainPos = wp;
            var mainSz = wsz;

            ImGui.End();
            ImGui.PopStyleVar();

            // ── ESP preview ──
            if (ShowEspPreview)
            {
                float pw = prevW * s;
                float ph = prevH * s;
                var pp = new Vector2(mainPos.X + mainSz.X + dockGap, mainPos.Y);
                if (pp.X + pw > io.DisplaySize.X - 8f)
                    pp.X = mainPos.X - dockGap - pw;

                ImGui.SetNextWindowPos(pp, ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(pw, ph), ImGuiCond.Always);

                var pflags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing;
                if (!uiLive) pflags |= ImGuiWindowFlags.NoInputs;

                if (ImGui.Begin("##esp_preview", pflags))
                {
                    var pwp = ImGui.GetWindowPos();
                    var pwz = ImGui.GetWindowSize();
                    float pr = ImGui.GetStyle().WindowRounding;

                    DrawSoftGlow(pwp, new Vector2(pwp.X + pwz.X, pwp.Y + pwz.Y), pr);

                    var pdl = ImGui.GetWindowDrawList();
                    pdl.AddRect(pwp, new Vector2(pwp.X + pwz.X, pwp.Y + pwz.Y),
                        Col(new Vector4(1, 1, 1, 12f / 255f)), pr, 0, 1f);

                    const float headH = 34f;
                    const float linePad = 12f;
                    {
                        var ts = ImGui.CalcTextSize("esp preview");
                        float cx = pwp.X + (pwz.X - ts.X) * 0.5f;
                        float cy = pwp.Y + (headH - ts.Y) * 0.5f;
                        pdl.AddText(new Vector2((float)Math.Floor(cx), (float)Math.Floor(cy)),
                            Col(new Vector4(230f / 255f, 235f / 255f, 245f / 255f, 200f / 255f)), "esp preview");
                    }

                    float ly = (float)Math.Floor(pwp.Y + headH);
                    pdl.AddLine(new Vector2((float)Math.Floor(pwp.X + linePad), ly), new Vector2((float)Math.Floor(pwp.X + pwz.X - linePad), ly),
                        Col(new Vector4(1, 1, 1, 18f / 255f)), 1f);

                    ImGui.SetCursorPos(new Vector2(0, headH));
                    ImGui.BeginChild("##esp_prev_body", new Vector2(pwz.X, pwz.Y - headH), false,
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

                    DrawEspPreview();

                    ImGui.EndChild();
                }
                ImGui.End();
            }

            // search popup
            if (searchOpen)
            {
                ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f - 150f, 80f), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(300f, 0f), ImGuiCond.Always);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
                ImGui.Begin("##search_popup", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing);

                ImGui.Text("search features");
                ImGui.Separator();
                ImGui.Spacing();

                DrawSearchFeatureToggle("esp enabled", () => Settings.Visuals.BoxESP = !Settings.Visuals.BoxESP);
                DrawSearchFeatureToggle("esp preview", () => ShowEspPreview = !ShowEspPreview);
                DrawSearchFeatureToggle("sticky target", () => Settings.Aiming.StickyAim = !Settings.Aiming.StickyAim);
                DrawSearchFeatureToggle("lua executor", () => luaOpen = !luaOpen);
                DrawSearchFeatureToggle("explorer", () => explorerOpen = !explorerOpen);
                DrawSearchFeatureToggle("watermark", () => ShowWatermark = !ShowWatermark);
                DrawSearchFeatureToggle("teamcheck", () => Settings.Checks.TeamCheck = !Settings.Checks.TeamCheck);
                DrawSearchFeatureToggle("fly", () => Settings.Flight.VFlight = !Settings.Flight.VFlight);
                DrawSearchFeatureToggle("accent", () => { });
                DrawSearchFeatureToggle("menu keybind", () => { });

                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                    searchOpen = false;

                ImGui.End();
                ImGui.PopStyleVar();
            }
        }

        private static void DrawSearchFeatureToggle(string label, Action action)
        {
            if (ImGui.Selectable(label))
                action();
        }

        private static void DrawEspPreview()
        {
            ImGui.SetCursorPos(new Vector2(14, 14));
            ImGui.TextUnformatted("box:  white");
            ImGui.SetCursorPos(new Vector2(14, 34));
            ImGui.TextUnformatted("fill:  translucent");
            ImGui.SetCursorPos(new Vector2(14, 54));
            ImGui.TextUnformatted("name:  " + (Storage.IsInitialized && !string.IsNullOrEmpty(Storage.LocalPlayerName) ? Storage.LocalPlayerName : "player"));
            ImGui.SetCursorPos(new Vector2(14, 74));
            ImGui.TextUnformatted("dist:  100 studs");
            ImGui.SetCursorPos(new Vector2(14, 94));
            ImGui.TextUnformatted("health:  100/100");

            var dl = ImGui.GetWindowDrawList();
            var st = ImGui.GetWindowPos();
            var sz = ImGui.GetWindowSize();

            var boxMin = new Vector2(st.X + sz.X * 0.5f - 40f, st.Y + sz.Y * 0.5f - 80f);
            var boxMax = new Vector2(boxMin.X + 80f, boxMin.Y + 160f);
            dl.AddRectFilled(new Vector2(boxMin.X + 1, boxMin.Y + 1), new Vector2(boxMax.X + 1, boxMax.Y + 1), 0x30000000);
            dl.AddRectFilled(boxMin, boxMax, 0x40000000);
            dl.AddRect(boxMin, boxMax, 0xFFFFFFFF);
            dl.AddRect(new Vector2(boxMin.X + 1, boxMin.Y + 1), new Vector2(boxMax.X - 1, boxMax.Y - 1), 0x80FFFFFF);
            dl.AddLine(new Vector2(boxMin.X + 40f, boxMin.Y), new Vector2(boxMin.X + 40f, boxMin.Y - 20f), 0xFFFFFFFF, 1.5f);
        }

        // ── AIM TAB ──────────────────────────────────────────────────────────
        private static void DrawAimTab()
        {
            float sideGap = 10f;
            float availW = ImGui.GetContentRegionAvail().X;
            float availH = ImGui.GetContentRegionAvail().Y;
            float cw = (availW - sideGap) * 0.5f;

            // ── Left child: aim ──
            if (BeginChildPanel("##aim_child1", new Vector2(cw, availH), "aim"))
            {
                var cfg = Settings.Aiming;

                int aimType = cfg.AimingType;
                if (StyleCombo("type", ref aimType, new[] { "mouse", "camera", "off" }))
                    cfg.AimingType = aimType;
                Gap();

                int aimKey = cfg.AimbotKey.Key;
                int aimMode = 0;
                if (KeybindBox("aim key", ref aimKey, ref aimMode))
                    cfg.AimbotKey.Key = aimKey;
                Gap();

                float smoothX = cfg.SmoothnessX;
                if (StyleSlider("smoothness x", ref smoothX, 0f, 1f)) cfg.SmoothnessX = smoothX;
                Gap();
                float smoothY = cfg.SmoothnessY;
                if (StyleSlider("smoothness y", ref smoothY, 0f, 1f)) cfg.SmoothnessY = smoothY;
                Gap();

                int targetSelect = 0;
                StyleCombo("target select", ref targetSelect, new[] { "fov center", "distance", "lowest hp" });
                Gap();

                StyleCheckbox("sticky target", ref cfg.StickyAim);
                Gap();

                StyleCheckbox("fov check", ref cfg.ShowFOV);
                Gap();

                if (cfg.ShowFOV)
                {
                    int fovStyle = cfg.FillFOV ? 1 : 0;
                    if (StyleCombo("fov style", ref fovStyle, new[] { "circle", "filled" }))
                        cfg.FillFOV = fovStyle == 1;
                    Gap();
                    float fov = cfg.FOV;
                    if (StyleSlider("fov size", ref fov, 10f, 600f, "%0.0f")) cfg.FOV = fov;
                    Gap();
                }

                int fovPos = 0;
                StyleCombo("aim pos", ref fovPos, new[] { "center", "mouse" });
                Gap();

                bool enabled = cfg.Prediction;
                if (StyleCheckbox("prediction", ref enabled)) cfg.Prediction = enabled;
                Gap();
                if (cfg.Prediction)
                {
                    float px = cfg.PredictionX;
                    if (StyleSlider("prediction x", ref px, 0f, 4f, "%0.0f")) cfg.PredictionX = px;
                    Gap();
                    float py = cfg.PredictionY;
                    if (StyleSlider("prediction y", ref py, 0f, 4f, "%0.0f")) cfg.PredictionY = py;
                    Gap();
                }

                float sens = cfg.Sensitivity;
                if (StyleSlider("sensitivity", ref sens, 0.1f, 5f)) cfg.Sensitivity = sens;
                Gap();
                float range = cfg.Range;
                if (StyleSlider("max distance", ref range, 50f, 5000f, "%0.0f")) cfg.Range = range;
                Gap();

                int bone = cfg.TargetBone;
                if (StyleCombo("target bone", ref bone, new[] { "head", "hrp", "torso", "left arm", "right arm" }))
                    cfg.TargetBone = bone;
            }
            EndChildPanel();

            ImGui.SameLine(0, sideGap);

            // ── Right child: silent aim ──
            if (BeginChildPanel("##aim_child2", new Vector2(cw, availH), "silent aim"))
            {
                var scfg = Settings.Silent;

                int silentKey = scfg.SilentAimbotKey.Key;
                int silentMode = 0;
                if (KeybindBox("silent key", ref silentKey, ref silentMode))
                    scfg.SilentAimbotKey.Key = silentKey;
                Gap();

                int method = scfg.SilentMethod;
                if (StyleCombo("silent method", ref method, new[] { "off", "rivals", "raycast", "magic bullet" }))
                {
                    scfg.SilentMethod = method;
                    scfg.RaycastSilent = method == 2;
                    scfg.MagicBullet = method == 3;
                }
                Gap();

                StyleCheckbox("always on", ref scfg.AlwaysOn);
                Gap();

                StyleCheckbox("fov check", ref scfg.ShowSilentFOV);
                Gap();

                if (scfg.ShowSilentFOV)
                {
                    float sf = scfg.SFOV;
                    if (StyleSlider("fov size", ref sf, 10f, 600f, "%0.0f")) scfg.SFOV = sf;
                    Gap();
                }

                float spx = scfg.PredictionX;
                if (StyleSlider("prediction x", ref spx, 0f, 4f, "%0.0f")) scfg.PredictionX = spx;
                Gap();
                float spy = scfg.PredictionY;
                if (StyleSlider("prediction y", ref spy, 0f, 4f, "%0.0f")) scfg.PredictionY = spy;
                Gap();

                StyleCheckbox("enable prediction", ref scfg.SPrediction);
                Gap();

                StyleCheckbox("visualizer", ref scfg.SilentVisualizer);
                Gap();

                StyleCheckbox("show fov", ref scfg.ShowSilentFOV);
            }
            EndChildPanel();
        }

        private static Vector4 espBoxColor = new(1, 1, 1, 1);
        private static Vector4 espBoxFillColor = new(0.25f, 0.35f, 1f, 0.6f);
        private static Vector4 espNameColor = new(1, 1, 1, 1);
        private static Vector4 espSkeletonColor = new(0.75f, 0.82f, 1f, 1);
        private static Vector4 espHealthColor = new(0.2f, 1f, 0.3f, 1);
        private static Vector4 espDistanceColor = new(1, 1, 1, 1);
        private static Vector4 espHeadColor = new(1, 1, 1, 1);
        private static Vector4 espTracerColor = new(0.75f, 0.82f, 1f, 1);
        private static Vector4 espChinaHatColor = new(1, 0.2f, 0.2f, 1);
        private static Vector4 espCornerColor = new(0.75f, 0.82f, 1f, 1);

        // ── ESP TAB ──────────────────────────────────────────────────────────
        private static void DrawEspTab()
        {
            float sideGap = 10f;
            float availW = ImGui.GetContentRegionAvail().X;
            float availH = ImGui.GetContentRegionAvail().Y;
            float cw = (availW - sideGap) * 0.5f;

            if (BeginChildPanel("##esp_child1", new Vector2(cw, availH), "esp"))
            {
                StyleCheckbox("enabled", ref Settings.Visuals.BoxESP);
                Gap();

                StyleCheckbox("draw local", ref Settings.Visuals.LocalPlayerESP);
                Gap();

                StyleCheckboxColor("bounding box", ref Settings.Visuals.Box, ref espBoxColor, "esp_box_color");
                Gap();

                if (Settings.Visuals.Box)
                {
                    StyleCheckboxColor("box fill", ref Settings.Visuals.FilledBox, ref espBoxFillColor, "esp_box_fill_color");
                    Gap();
                }

                StyleCheckboxColor("name", ref Settings.Visuals.Name, ref espNameColor, "esp_name_color");
                Gap();
                if (Settings.Visuals.Name)
                {
                    float nameSz = Settings.Visuals.NameSize;
                    if (StyleSlider("name size", ref nameSz, 8f, 32f, "%0.0f")) Settings.Visuals.NameSize = nameSz;
                    Gap();
                }

                StyleCheckboxColor("skeleton", ref Settings.Visuals.Skeleton, ref espSkeletonColor, "esp_skeleton_color");
                Gap();

                StyleCheckboxColor("health bar", ref Settings.Visuals.Health, ref espHealthColor, "esp_health_color");
                Gap();

                StyleCheckboxColor("distance", ref Settings.Visuals.Distance, ref espDistanceColor, "esp_distance_color");
                Gap();
                if (Settings.Visuals.Distance)
                {
                    float distSz = Settings.Visuals.DistanceSize;
                    if (StyleSlider("distance size", ref distSz, 8f, 32f, "%0.0f")) Settings.Visuals.DistanceSize = distSz;
                    Gap();
                }

                StyleCheckboxColor("head circle", ref Settings.Visuals.HeadCircle, ref espHeadColor, "esp_head_color");
                Gap();
                if (Settings.Visuals.HeadCircle)
                {
                    float hs = Settings.Visuals.HeadCircleMaxScale;
                    if (StyleSlider("head scale", ref hs, 0.5f, 4f)) Settings.Visuals.HeadCircleMaxScale = hs;
                    Gap();
                }

                StyleCheckboxColor("tracer", ref Settings.Visuals.Tracers, ref espTracerColor, "esp_tracer_color");
                Gap();
                if (Settings.Visuals.Tracers)
                {
                    float tw = Settings.Visuals.TracerThickness;
                    if (StyleSlider("tracer thickness", ref tw, 0.5f, 6f)) Settings.Visuals.TracerThickness = tw;
                    Gap();
                }

                StyleCheckboxColor("china hat", ref Settings.Visuals.ChinaHat, ref espChinaHatColor, "esp_china_hat_color");
                Gap();
                StyleCheckboxColor("corner esp", ref Settings.Visuals.CornerESP, ref espCornerColor, "esp_corner_color");
            }
            EndChildPanel();

            ImGui.SameLine(0, sideGap);

            if (BeginChildPanel("##esp_child2", new Vector2(cw, availH), "settings"))
            {
                float fontSz = Settings.Visuals.NameSize;
                if (StyleSlider("esp font size", ref fontSz, 8f, 24f, "%0.0f")) Settings.Visuals.NameSize = fontSz;
                Gap();

                int boxMode = Settings.Visuals.CornerESP ? 1 : 0;
                if (StyleCombo("box style", ref boxMode, new[] { "bounding", "corner", "3d" }))
                    Settings.Visuals.CornerESP = boxMode == 1;
                Gap();

                float thick = Settings.Visuals.TracerThickness;
                if (StyleSlider("box thickness", ref thick, 0.5f, 6f)) Settings.Visuals.TracerThickness = thick;
                Gap();

                bool distCheck = Settings.Visuals.Distance;
                if (StyleCheckbox("distance check", ref distCheck)) Settings.Visuals.Distance = distCheck;
                Gap();
            }
            EndChildPanel();
        }

        // ── MISC TAB ────────────────────────────────────────────────────────
        private static void DrawMiscTab()
        {
            float sideGap = 10f;
            float availW = ImGui.GetContentRegionAvail().X;
            float availH = ImGui.GetContentRegionAvail().Y;
            float cw = (availW - sideGap) * 0.5f;

            if (BeginChildPanel("##misc_child1", new Vector2(cw, availH), "world"))
            {
                StyleCheckbox("teamcheck", ref Settings.Checks.TeamCheck);
                Gap();
                StyleCheckbox("pf teamcheck", ref Settings.Checks.PFTeamCheck);
                Gap();
                StyleCheckbox("pf switch team", ref Settings.Checks.PFSwitchTeam);
                Gap();
                StyleCheckbox("downed check", ref Settings.Checks.DownedCheck);
                Gap();
                StyleCheckbox("transparency check", ref Settings.Checks.TransparencyCheck);
                Gap();

                bool fps = fpsEnabled;
                if (StyleCheckbox("fps unlocker", ref fps)) { fpsEnabled = fps; Settings.FPS.FPSEnabled = fps; }
                Gap();
                if (fpsEnabled)
                {
                    int cap = fpsCap;
                    if (StyleSliderInt("fps cap", ref cap, 60, 1000)) fpsCap = cap;
                    Gap();
                }

                bool fovC = fovChanger;
                if (StyleCheckbox("fov changer", ref fovC)) { fovChanger = fovC; Settings.Camera.FOVEnabled = fovC; }
                Gap();
                float fovV = Settings.Camera.FOV;
                if (StyleSlider("fov value", ref fovV, 10f, 120f, "%0.0f")) Settings.Camera.FOV = fovV;
                Gap();
            }
            EndChildPanel();

            ImGui.SameLine(0, sideGap);

            if (BeginChildPanel("##misc_child2", new Vector2(cw, availH), "local"))
            {
                StyleCheckbox("walkspeed", ref Settings.Humanoid.WalkspeedEnabled);
                Gap();
                float ws = Settings.Humanoid.Walkspeed;
                if (StyleSlider("ws value", ref ws, 1f, 500f, "%0.0f")) Settings.Humanoid.Walkspeed = ws;
                Gap();

                StyleCheckbox("jump power", ref Settings.Humanoid.JumpPowerEnabled);
                Gap();
                float jp = Settings.Humanoid.JumpPower;
                if (StyleSlider("jump value", ref jp, 50f, 500f, "%0.0f")) Settings.Humanoid.JumpPower = jp;
                Gap();

                StyleCheckbox("fly", ref Settings.Flight.VFlight);
                Gap();

                int flyKey = Settings.Flight.VFlightBind.Key;
                int flyMode = 0;
                if (KeybindBox("fly key", ref flyKey, ref flyMode))
                    Settings.Flight.VFlightBind.Key = flyKey;
                Gap();

                float fs = Settings.Flight.VFlightSpeed;
                if (StyleSlider("fly speed", ref fs, 5f, 1000f, "%0.0f")) Settings.Flight.VFlightSpeed = fs;
                Gap();

                int flyMethod = Settings.Flight.VFlightMethod;
                if (StyleCombo("flight method", ref flyMethod, new[] { "position", "velocity" }))
                    Settings.Flight.VFlightMethod = flyMethod;
                Gap();

                StyleCheckbox("car fly (jailbreak)", ref Settings.CarFly.CarFlyEnabled);
                Gap();

                int carKey = Settings.CarFly.CarFlyBind.Key;
                int carMode = 0;
                if (KeybindBox("car fly key", ref carKey, ref carMode))
                    Settings.CarFly.CarFlyBind.Key = carKey;
                Gap();

                float cs = Settings.CarFly.CarFlySpeed;
                if (StyleSlider("car speed", ref cs, 50f, 2000f, "%0.0f")) Settings.CarFly.CarFlySpeed = cs;
                Gap();

                StyleCheckbox("noclip", ref noclip.Enabled);
                Gap();
                StyleCheckbox("noclip bind mode", ref noclip.BindMode);
                Gap();

                int noclipKey = noclip.Bind.Key;
                int noclipMode = 0;
                if (KeybindBox("noclip key", ref noclipKey, ref noclipMode))
                    noclip.Bind.Key = noclipKey;
                Gap();

                StyleCheckbox("gravity", ref Settings.Gravity.Enabled);
                Gap();
                float gv = Settings.Gravity.Value;
                if (StyleSlider("gravity value", ref gv, -500f, 500f, "%0.1f")) Settings.Gravity.Value = gv;
                Gap();

                StyleCheckbox("tickrate", ref Settings.Tickrate.Enabled);
                Gap();
                float tv = Settings.Tickrate.Value;
                if (StyleSlider("tickrate value", ref tv, 1f, 5000f, "%0.0f")) Settings.Tickrate.Value = tv;
                Gap();

                StyleCheckbox("fov", ref Settings.Camera.FOVEnabled);
                Gap();
                float cf = Settings.Camera.FOV;
                if (StyleSlider("fov value", ref cf, 70f, 120f, "%0.0f")) Settings.Camera.FOV = cf;
                Gap();

                bool bt = btools.Enabled;
                if (StyleCheckbox("btools", ref bt)) btools.Enabled = bt;
                Gap();

                int tool = btools.SelectedTool;
                if (StyleCombo("tool", ref tool, new[] { "hammer", "grab", "clone" }))
                    btools.SelectedTool = tool;
            }
            EndChildPanel();
        }

        // ── Attach / status ──────────────────────────────────────────────────
        private static string attachStatus = "IDLE";
        private static bool featureSystemsStarted;
        private static bool autoAttachStarted;

        private static void StartFeatureSystems()
        {
            if (featureSystemsStarted) return;
            featureSystemsStarted = true;
            try
            {
                player.Start();
                playerobjects.Start();
                HumanoidModule.Start();
                TPHandler.Start();
                CameraModule.Start();
                visuals.Start();
                aiming.Start();
                desync.Start();
                flight.Start();
                carfly.Start();
                noclip.Start();
                fps.Start();
                gravity.Start();
                tickrate.Start();
                silentaiming.Start();
                raycastsilent.Start();
                phantomsilent.Start();
                btools.Start();
            }
            catch { }
        }

        private static void AutoAttach()
        {
            if (autoAttachStarted) return;
            autoAttachStarted = true;
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
                    new Thread(() =>
                    {
                        try
                        {
                            Thread.Sleep(2000);
                            if (!Storage.IsInitialized)
                            {
                                var m2 = new FoulzExternal.Memory();
                                bool ok2 = m2.Attach("RobloxPlayerBeta") || m2.Attach("RobloxPlayer");
                                if (ok2)
                                {
                                    Storage.Initialize(m2);
                                    attachStatus = Storage.IsInitialized ? "ACTIVE" : "ACTIVE (partial)";
                                    if (Storage.IsInitialized) StartFeatureSystems();
                                }
                            }
                        }
                        catch { }
                    }) { IsBackground = true }.Start();
                }
            }
            catch { }
        }

        private static void AttachButton()
        {
            if (ImGui.Button("ATTACH", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                attachStatus = "ATTACHING";
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
                    }
                }
                catch { attachStatus = "ERROR"; }
            }
        }

        // ── SETTINGS TAB ─────────────────────────────────────────────────────
        private static void DrawSettingsTab()
        {
            float colGap = 10f;
            float availW = ImGui.GetContentRegionAvail().X;
            float availH = ImGui.GetContentRegionAvail().Y;
            float cw = (availW - colGap) * 0.5f;

            if (BeginChildPanel("##set_child1", new Vector2(cw, availH), "menu"))
            {
                ImGui.Dummy(new Vector2(0, 2f));
                Gap();

                // attach button + status
                Pad();
                AttachButton();
                Gap();

                Pad();
                ImGui.TextUnformatted("status: " + attachStatus);
                Gap();

                Pad();
                ImGui.TextUnformatted("menu key: F7");
                Gap();

                Pad();
                StyleCheckbox("esp preview", ref ShowEspPreview);
                Gap();

                Pad();
                StyleCheckbox("watermark", ref ShowWatermark);
                Gap();

                if (ShowWatermark)
                {
                    StyleMultiCombo("watermark info", ref WmFields, new[] { "build", "player", "place id", "game id", "time", "fps" });
                    Gap();
                }

                int theme = 0;
                if (StyleCombo("theme", ref theme, new[] { "default", "dark blue", "dark purple", "dark red", "dark green" }))
                {
                    if (theme == 0) Accent = new Vector4(0.75f, 0.82f, 1f, 1f);
                    else if (theme == 1) Accent = new Vector4(0.3f, 0.5f, 1f, 1f);
                    else if (theme == 2) Accent = new Vector4(0.7f, 0.4f, 1f, 1f);
                    else if (theme == 3) Accent = new Vector4(1f, 0.3f, 0.3f, 1f);
                    else Accent = new Vector4(0.3f, 1f, 0.5f, 1f);
                }
                Gap();

                Pad();
                float op = OuterBorder.W * 100f;
                if (StyleSlider("opacity", ref op, 0f, 100f, "%0.0f"))
                {
                    float a = Math.Clamp(op * 0.01f, 0f, 1f);
                    OuterBorder.W = a;
                    ChildFill.W = a * 0.52f;
                }
                Gap();

                Pad();
                ImGui.TextUnformatted("accent color");
                ImGui.SameLine();
                ColorSwatch("##acc_swatch", ref Accent);
            }
            EndChildPanel();

            ImGui.SameLine(0, colGap);

            if (BeginChildPanel("##set_child2", new Vector2(cw, availH), "configs"))
            {
                ImGui.Dummy(new Vector2(0, 2f));
                Gap();

                Pad();
                if (InputText("##cfg_name", "config name", ref cfgName, cw - 60f))
                { }
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("▼", new Vector2(28, 28)))
                {
                    RefreshCfgList();
                }
                Gap();

                Pad();
                if (ImGui.Button("save", new Vector2((cw - 30f) / 3f, 26f)))
                    SaveCfg();
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("load", new Vector2((cw - 30f) / 3f, 26f)))
                    LoadCfg();
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("delete", new Vector2((cw - 30f) / 3f, 26f)))
                    DeleteCfg();
                Gap();

                Pad();
                if (ImGui.Button("set as default", new Vector2((cw - 15f) / 2f, 26f)))
                    SetDefaultCfg();
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("open folder", new Vector2((cw - 15f) / 2f, 26f)))
                    OpenCfgFolder();
                Gap();

                Pad();
                ImGui.TextUnformatted("saved configs:");
                Gap();

                if (ImGui.GetTime() >= cfgRefreshAt)
                {
                    RefreshCfgList();
                    cfgRefreshAt = (float)ImGui.GetTime() + 1.0f;
                }

                Pad();
                foreach (var cfg in cfgItems)
                {
                    bool selected = Array.IndexOf(cfgItems, cfg) == cfgSel;
                    if (ImGui.Selectable("  " + cfg, selected))
                    {
                        cfgSel = Array.IndexOf(cfgItems, cfg);
                        cfgName = cfg;
                    }
                }
            }
            EndChildPanel();
        }

        private static bool InputText(string id, string hint, ref string buffer, float width = 0f)
        {
            if (width <= 0)
                width = ImGui.GetContentRegionAvail().X;
            ImGui.SetNextItemWidth(Math.Min(width, ImGui.GetContentRegionAvail().X));
            return ImGui.InputTextWithHint(id, hint, ref buffer, 256);
        }

        private static void RefreshCfgList()
        {
            cfgItems = ConfigManager.GetAvailableConfigs();
            if (cfgSel >= cfgItems.Length) cfgSel = cfgItems.Length - 1;
        }

        private static void SaveCfg()
        {
            string name = string.IsNullOrWhiteSpace(cfgName) ? "default" : cfgName.Trim();
            if (ConfigManager.SaveConfig(name))
            {
                RefreshCfgList();
                notify.Notify("Config saved", $"Configuration '{name}' saved");
            }
        }

        private static void LoadCfg()
        {
            string name = string.IsNullOrWhiteSpace(cfgName) ? "default" : cfgName.Trim();
            if (ConfigManager.LoadConfig(name))
            {
                cfgName = name;
                notify.Notify("Config loaded", $"Configuration '{name}' loaded");
            }
        }

        private static void DeleteCfg()
        {
            string name = string.IsNullOrWhiteSpace(cfgName) ? "default" : cfgName.Trim();
            if (ConfigManager.DeleteConfig(name))
            {
                RefreshCfgList();
                notify.Notify("Config deleted", $"Configuration '{name}' deleted");
            }
        }

        private static void SetDefaultCfg()
        {
            string name = string.IsNullOrWhiteSpace(cfgName) ? "default" : cfgName.Trim();
            if (ConfigManager.SetDefaultConfigName(name))
                notify.Notify("Default set", $"'{name}' is now the default config");
        }

        private static void OpenCfgFolder()
        {
            try { System.Diagnostics.Process.Start("explorer.exe", ConfigManager.GetConfigDirectory()); }
            catch { }
        }

        // ── LUA / EXPLORER / PLAYERS panels ──────────────────────────────────
        private static void DrawLuaPanel()
        {
            if (!luaOpen) return;
            var io = ImGui.GetIO();

            float w = 560f, h = 480f;
            float lx = (io.DisplaySize.X - w) * 0.5f - 300f;
            float ly = 80f;

            ImGui.SetNextWindowPos(new Vector2(lx, ly), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
            bool open = luaOpen;
            if (ImGui.Begin("##lua_panel", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar))
            {
                if (!open) luaOpen = false;

                ImGui.Text("lua executor");
                ImGui.Separator();
                ImGui.Spacing();

                Pad();
                if (scriptItems.Length == 0) RefreshScriptList();
                string[] items = scriptItems.Length == 0 ? new[] { "(no scripts)" } : scriptItems;
                int prevSel = scriptSel;
                if (ImGui.Combo("##script", ref scriptSel, items, items.Length))
                {
                    if (scriptSel >= 0 && scriptSel < scriptItems.Length)
                    {
                        string path = System.IO.Path.Combine(ScriptEngine.ScriptsDir, scriptItems[scriptSel]);
                        if (System.IO.File.Exists(path))
                            scriptCode = System.IO.File.ReadAllText(path);
                    }
                }
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("new", new Vector2(40, 24))) { scriptCode = ""; scriptSel = -1; }
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("save", new Vector2(40, 24)))
                {
                    string dir = ScriptEngine.ScriptsDir;
                    System.IO.Directory.CreateDirectory(dir);
                    string name = scriptSel >= 0 && scriptSel < scriptItems.Length ? scriptItems[scriptSel] : "untitled.lua";
                    if (!name.EndsWith(".lua") && !name.EndsWith(".luau")) name += ".lua";
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name), scriptCode);
                    RefreshScriptList();
                }
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("📁", new Vector2(24, 24)))
                {
                    System.IO.Directory.CreateDirectory(ScriptEngine.ScriptsDir);
                    System.Diagnostics.Process.Start("explorer.exe", ScriptEngine.ScriptsDir);
                }
                Gap();

                float editH = h - 190f;
                if (editH < 60f) editH = 60f;
                string codeBuffer = scriptCode;
                if (codeBuffer.Length > 1_000_000) codeBuffer = codeBuffer.Substring(0, 1_000_000);
                if (ImGui.InputTextMultiline("##code", ref codeBuffer, 1_000_000, new Vector2(ImGui.GetContentRegionAvail().X, editH),
                    ImGuiInputTextFlags.AllowTabInput))
                {
                    scriptCode = codeBuffer;
                }
                Gap();

                if (ImGui.Button("▶  RUN", new Vector2(ImGui.GetContentRegionAvail().X - 50f, 30)))
                {
                    ScriptEngine.Stop();
                    ScriptEngine.Run(scriptCode);
                    scriptOutput = "";
                }
                ImGui.SameLine(0, 5f);
                if (ImGui.Button("■  STOP", new Vector2(45f, 30)))
                    ScriptEngine.Stop();
                Gap();

                ImGui.Text("console");
                ImGui.Spacing();

                while (ScriptEngine.Output.TryDequeue(out var item))
                {
                    if (scriptOutput.Length > 8000) scriptOutput = "";
                    scriptOutput += item.text + "\n";
                }

                string consoleBuffer = scriptOutput;
                if (consoleBuffer.Length > 100_000) consoleBuffer = consoleBuffer.Substring(Math.Max(0, consoleBuffer.Length - 100_000));
                ImGui.InputTextMultiline("##console", ref consoleBuffer, 100_000, new Vector2(ImGui.GetContentRegionAvail().X, 80f),
                    ImGuiInputTextFlags.ReadOnly);
                scriptOutput = consoleBuffer;
            }
            ImGui.End();
            ImGui.PopStyleVar();
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

        private static void DrawExplorerPanel()
        {
            if (!explorerOpen) return;
            var io = ImGui.GetIO();

            float w = 560f, h = 480f;
            float lx = (io.DisplaySize.X - w) * 0.5f;
            float ly = 80f;

            ImGui.SetNextWindowPos(new Vector2(lx, ly), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
            bool open = explorerOpen;
            if (ImGui.Begin("##explorer_panel", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar))
            {
                if (!open) explorerOpen = false;

                ImGui.Text("instance explorer");
                ImGui.Separator();
                ImGui.Spacing();

                if (!Storage.IsInitialized || !Storage.DataModelInstance.IsValid)
                {
                    ImGui.TextDisabled("attach to roblox first...");
                }
                else
                {
                    Pad();
                    if (ImGui.Button("refresh", new Vector2(80, 24)))
                    {
                        if (SInstance.Mem != null) Storage.Refresh(SInstance.Mem);
                    }
                    Gap();

                    ImGui.BeginChild("##explorer_tree", new Vector2(ImGui.GetContentRegionAvail().X, h - 100f));
                    try
                    {
                        var dm = Storage.DataModelInstance;
                        foreach (var kid in dm.GetChildren())
                        {
                            string name = kid.GetName() ?? "?";
                            string cls = kid.GetClass() ?? "?";
                            if (ImGui.TreeNode($"{name}  ({cls})##expl{name}{kid.Address}"))
                            {
                                DrawExplorerChildren(kid, 0);
                                ImGui.TreePop();
                            }
                        }
                    }
                    catch { }
                    ImGui.EndChild();
                }
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        private static void DrawExplorerChildren(SInstance parent, int depth)
        {
            if (depth > 8) return;
            try
            {
                foreach (var kid in parent.GetChildren())
                {
                    string name = kid.GetName() ?? "?";
                    string cls = kid.GetClass() ?? "?";
                    if (ImGui.TreeNode($"{name}  ({cls})##exp{parent.Address}{kid.Address}"))
                    {
                        DrawExplorerChildren(kid, depth + 1);
                        ImGui.TreePop();
                    }
                }
            }
            catch { }
        }

        private static void DrawPlayersPanel()
        {
            if (!playersOpen) return;
            var io = ImGui.GetIO();

            float w = 560f, h = 480f;
            float lx = (io.DisplaySize.X - w) * 0.5f + 300f;
            float ly = 80f;

            ImGui.SetNextWindowPos(new Vector2(lx, ly), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
            bool open = playersOpen;
            if (ImGui.Begin("##players_panel", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar))
            {
                if (!open) playersOpen = false;

                ImGui.Text("players");
                ImGui.Separator();
                ImGui.Spacing();

                if (!Storage.IsInitialized)
                {
                    ImGui.TextDisabled("attach to roblox first...");
                }
                else
                {
                    ImGui.BeginChild("##players_list", new Vector2(ImGui.GetContentRegionAvail().X, h - 60f));
                    try
                    {
                        var snap = FoulzExternal.SDK.caches.player.CachedPlayers;
                        if (snap != null)
                        {
                            foreach (var p in snap)
                            {
                                if (!p.IsValid) continue;
                                string name = p.GetName() ?? "???";
                                ImGui.TextUnformatted(name);
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip("teleport / spectate");

                                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 90f);
                                if (ImGui.SmallButton($"tp##{name}"))
                                {
                                    TeleportTo(name);
                                }
                                ImGui.SameLine(0, 5f);
                                if (ImGui.SmallButton($"spectate##{name}"))
                                {
                                    Spectate(name);
                                }
                                ImGui.Separator();
                            }
                        }
                    }
                    catch { }
                    ImGui.EndChild();
                }
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        private static void TeleportTo(string name)
        {
            try
            {
                var tar = FoulzExternal.SDK.caches.playerobjects.CachedPlayerObjects.Find(o => o.Name == name);
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

        private static void Spectate(string name)
        {
            try
            {
                var tar = FoulzExternal.SDK.caches.playerobjects.CachedPlayerObjects.Find(o => o.Name == name);
                if (tar.address == 0 || !tar.Humanoid.IsValid) return;
                if (!Storage.CameraInstance.IsValid) return;

                var mem = SInstance.Mem;
                if (mem == null) return;
                mem.Write(Storage.CameraInstance.Address + Offsets.Camera.CameraSubject, tar.Humanoid.Address);
            }
            catch { }
        }
    }
}