using ImGuiNET;
using ClickableTransparentOverlay;
using System.Numerics;
using FoulzExternal.games.universal.visuals;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Diagnostics;
using FoulzExternal.games.universal.aiming;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using System.Windows.Media;
using FoulzExternal.features.games.universal.desync;
using FoulzExternal.features.games.universal.aiming.silent;
using FoulzExternal.features.games.universal.scriptrunner;
using System.Windows;
using SixLabors.ImageSharp.PixelFormats;
using ISImage = SixLabors.ImageSharp.Image;

// just imgui code that i had to made for visuals and shit

namespace IMGUI
{
    public class Program : Overlay
    {
        private const ImGuiWindowFlags ScriptCaptureFlags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoScrollbar;

        private static readonly object l = new();
        private static bool running = false;
        private static readonly System.Collections.Generic.Dictionary<long, long> localCache = new();

        private static System.Windows.Threading.Dispatcher watcher = null!;

        // ── Crosshair state (geeg lad style) ────────────────────────────────
        public static bool crosshairEnabled;
        public static float crosshairLength = 8f;
        public static float crosshairGap = 4f;
        public static float crosshairThickness = 1.5f;
        public static bool crosshairDot;
        public static float crosshairDotSize = 1.5f;
        public static uint crosshairColor = 0xFFFFFFFF;

        // ── Overlay FPS (config value; render loop is unthrottled like C++) ──
        public static float overlayFps = 240f;

        // ── Measured render FPS (for the on-screen counter) ─────────────────
        private static readonly Stopwatch fpsClock = Stopwatch.StartNew();
        private static int fpsFrameCount;
        private static float measuredFps = 0f;
        public static float MeasuredFps => measuredFps;

        // ── Watermark position/drag state ─────────────────────────────────
        // Draggable only while the menu is open; locked in place when the menu
        // is closed so it never interferes with gameplay interaction.
        private static Vector2 wmPos = new(12f, 12f);
        private static bool wmDrag;
        private static Vector2 wmDragOffset;

        private static void TickFps()
        {
            fpsFrameCount++;
            if (fpsClock.ElapsedMilliseconds >= 500)
            {
                measuredFps = fpsFrameCount * 1000f / (float)fpsClock.ElapsedMilliseconds;
                fpsFrameCount = 0;
                fpsClock.Restart();
            }
        }

        // ── Watermark (Yerba-styled): ardvark · username · fps ──
        private static void DrawWatermark()
        {
            if (!MenuUI.showWatermark) return;
            try
            {
                var dl = ImGui.GetForegroundDrawList();
                var io = ImGui.GetIO();

                // Roblox username (display name fallback)
                string user = "not attached";
                try
                {
                    if (FoulzExternal.storage.Storage.LocalPlayerInstance.IsValid)
                    {
                        string n = FoulzExternal.storage.Storage.LocalPlayerInstance.GetName();
                        user = string.IsNullOrWhiteSpace(n) ? "guest" : n;
                    }
                }
                catch { user = "guest"; }

                string head = "ardvark";
                string userLine = user;
                string statLine = $"{measuredFps:0} fps";

                var pad = new Vector2(10f, 8f);
                var sHead = ImGui.CalcTextSize(head);
                var sUser = ImGui.CalcTextSize(userLine);
                var sStat = ImGui.CalcTextSize(statLine);
                float w = Math.Max(sHead.X, Math.Max(sUser.X, sStat.X)) + pad.X * 2f + 10f;
                float h = pad.Y * 2f + sHead.Y + sUser.Y + sStat.Y + 12f;

                var min = wmPos;
                var max = new Vector2(min.X + w, min.Y + h);

                // ── Draggable only while the menu is open ──────────────────
                // Uses raw Win32 input: the transparent overlay is click-through
                // outside its ImGui widgets, so io.MouseClicked isn't reliable
                // over the watermark. Reading the global button/cursor works.
                if (MenuUI.Open)
                {
                    GetCursorPos(out var pt);
                    var m = new Vector2(pt.X, pt.Y);
                    bool pressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                    bool over = m.X >= min.X && m.X <= max.X && m.Y >= min.Y && m.Y <= max.Y;

                    if (pressed && over && !wmDrag)
                    {
                        wmDrag = true;
                        wmDragOffset = m - wmPos;
                    }
                    else if (!pressed)
                    {
                        wmDrag = false;
                    }

                    if (wmDrag && pressed)
                    {
                        wmPos = m - wmDragOffset;
                        wmPos = Vector2.Max(new Vector2(4f, 4f),
                            Vector2.Min(new Vector2(io.DisplaySize.X - w - 4f, io.DisplaySize.Y - h - 4f), wmPos));
                        min = wmPos;
                        max = new Vector2(min.X + w, min.Y + h);
                    }
                }

                // background + border
                dl.AddRectFilled(min, max, 0xE80F1013, 6f);
                dl.AddRect(min, max, 0x55202A38, 6f, ImDrawFlags.None, 1f);
                // left accent bar
                dl.AddRectFilled(new Vector2(min.X, min.Y + 6f), new Vector2(min.X + 3f, max.Y - 6f), 0xFF4FA3E1, 2f);

                float x = min.X + pad.X + 8f;
                float yTop = min.Y + pad.Y;

                // header
                dl.AddText(new Vector2(x, yTop), 0xFFFFFFFF, head);
                yTop += sHead.Y;
                // username
                dl.AddText(new Vector2(x, yTop), 0xFF76D0F2, userLine);
                yTop += sUser.Y + 4f;
                // divider
                dl.AddLine(new Vector2(x, yTop), new Vector2(max.X - pad.X, yTop), 0x40202A38, 1f);
                yTop += 4f;
                // stats: fps · ping · game
                dl.AddText(new Vector2(x, yTop), 0xFF9AA0B0, statLine);
            }
            catch { }
        }

        private static ExitEventHandler? onAppExit;
        private static EventHandler? onDispatcherShutdown;
        private static EventHandler? onProcessExit;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        private const int VK_LBUTTON = 0x01;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        // winmm.dll high-resolution timer: keeps the system-wide timer at 1ms
        // granularity so the unthrottled render loop (Present(0,0) style) stays
        // smooth regardless of Windows' default ~15.6ms timer resolution.
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint uMilliseconds);
        private const uint WM_CLOSE = 0x0010;

        protected override Task PostInitialized()
        {
            // The base class locks the render loop to the monitor refresh via
            // VSync unless we disable it. With VSync off we throttle the loop
            // ourselves in Render() using the configurable overlay FPS.
            this.VSync = false;
            // Enable 1ms timer resolution so the sleep-based FPS throttle in
            // Render() can actually hit high frame rates (240fps) instead of
            // being crushed to ~60fps by Windows' default ~15.6ms timer.
            try { timeBeginPeriod(1); } catch { }
            return Task.CompletedTask;
        }

        protected override void Render()
        {
            // Measure the true render-loop throughput for the on-screen FPS
            // counter in the top-right corner.
            TickFps();

            // ── Inline ESP render (ported from C++ Renderer::MainLoop) ──────
            // The C++ external renders ESP directly on the render thread with
            // an unthrottled Present(0,0) — no worker thread, no scene
            // snapshots, no sleeps. We do the same here: read the view matrix
            // and draw the ESP inline every frame.
            try
            {
                if (FoulzExternal.SDK.Instance.Mem != null && Storage.IsInitialized)
                {
                    var view = FoulzExternal.SDK.Instance.Mem.Read<FoulzExternal.SDK.structures.Matrix4>(
                        Storage.VisualEngine + Offsets.VisualEngine.ViewMatrix);

                    var io = ImGui.GetIO();
                    var viewport = new FoulzExternal.SDK.structures.Vector2
                    {
                        x = io.DisplaySize.X,
                        y = io.DisplaySize.Y
                    };

                    visuals.RenderImGui(view, viewport);
                }
            }
            catch { }

            // ── FPS counter (top-right) ─────────────────────────────────────
            try
            {
                if (measuredFps > 0f)
                {
                    var dFps = ImGui.GetBackgroundDrawList();
                    var ioFps = ImGui.GetIO();
                    string fpsText = $"{measuredFps:0} fps";
                    var fpsSize = ImGui.CalcTextSize(fpsText);
                    var fpsPos = new Vector2(ioFps.DisplaySize.X - fpsSize.X - 10f, 8f);
                    dFps.AddText(fpsPos, 0xFFFFFFFF, fpsText);
                }
            }
            catch { }

            // ── Yerba-style watermark (ardvark · user · fps · ping · game) ───
            try
            {
                DrawWatermark();
            }
            catch { }

            try
            {
                var a = aiming.GetSceneSnapshot();
                if (a != null && a.circles != null && a.circles.Count > 0)
                {
                    var d2 = ImGui.GetForegroundDrawList();
                    foreach (var c in a.circles)
                    {
                        var center = new Vector2((float)c.center.X, (float)c.center.Y);
                        float radius = Math.Max(1f, c.radius);

                        uint outline = u32(c.outline);
                        uint fill = u32(Color.FromArgb((byte)(c.fillColor.A), c.fillColor.R, c.fillColor.G, c.fillColor.B));

                        if (c.fill)
                            d2.AddCircleFilled(center, radius, fill, 100);

                        d2.AddCircle(center, radius, outline, 100, 1.5f);
                    }
                }

                var sa = silentaiming.GetSceneSnapshot();
                if (sa != null && sa.circles != null && sa.circles.Count > 0)
                {
                    var d2 = ImGui.GetForegroundDrawList();
                    foreach (var c in sa.circles)
                    {
                        var center = new Vector2((float)c.center.X, (float)c.center.Y);
                        float radius = Math.Max(1f, c.radius);

                        uint outline = u32(c.outline);
                        uint fill = u32(Color.FromArgb((byte)(c.fillColor.A), c.fillColor.R, c.fillColor.G, c.fillColor.B));

                        if (c.fill)
                            d2.AddCircleFilled(center, radius, fill, 100);

                        d2.AddCircle(center, radius, outline, 100, 1.5f);
                    }
                }
            }
            catch { }

            try
            {
                var s = desync.GetSceneSnapshot();
                if (s != null && s.Active)
                {
                    var screen = FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.WorldToScreen(s.Position);

                    string localWorld = "?";
                    try {
                        var lp = FoulzExternal.storage.Storage.LocalPlayerInstance;
                        if (lp.IsValid)
                        {
                            var guys = FoulzExternal.SDK.caches.playerobjects.CachedPlayerObjects;
                            if (guys != null)
                            {
                                var localObj = System.Linq.Enumerable.FirstOrDefault(guys, x => x.address == lp.Address);
                                if (localObj.address != 0)
                                {
                                    if (localObj.HumanoidRootPart.IsValid)
                                    {
                                        var pos = FoulzExternal.games.universal.visuals.visuals.GetPos(localObj.HumanoidRootPart, true);
                                        localWorld = $"HRP: {pos.x:0.00}, {pos.y:0.00}, {pos.z:0.00}";
                                    }
                                    else if (localObj.Humanoid.IsValid)
                                    {
                                        var pos = FoulzExternal.games.universal.visuals.visuals.GetPos(localObj.Humanoid, true);
                                        localWorld = $"Humanoid: {pos.x:0.00}, {pos.y:0.00}, {pos.z:0.00}";
                                    }
                                    else
                                    {
                                        localWorld = "localObj found, but no valid HRP or Humanoid";
                                    }
                                }
                                else
                                {
                                    localWorld = "localObj not found in playerobjects";
                                }
                            }
                            else
                            {
                                localWorld = "playerobjects.CachedPlayerObjects null";
                            }
                        }
                        else
                        {
                            localWorld = "LocalPlayerInstance not valid";
                        }
                    } catch (Exception ex) { localWorld = $"EX: {ex.Message}"; }
                    FoulzExternal.logging.LogsWindow.Log($"[Desync VIS] World: {s.Position.x:0.00}, {s.Position.y:0.00}, {s.Position.z:0.00} | Screen: {screen.x:0.00}, {screen.y:0.00} | Local: {localWorld}");
                    if (screen.x != -1 && screen.y != -1)
                    {
                        var d3 = ImGui.GetForegroundDrawList();
                        var center = new Vector2(screen.x, screen.y);

                        uint blackFill = 0x60000000;
                        uint whiteOutline = 0xFFFFFFFF;
                        uint whiteThin = 0x80FFFFFF;

                        float radius = 48.0f;

                        d3.AddCircleFilled(center, radius + 6.0f, 0x20000000, 64);
                        d3.AddCircleFilled(center, radius + 2.0f, 0x30000000, 64);
                        d3.AddCircleFilled(center, radius, blackFill, 64);

                        d3.AddCircle(center, radius + 0.5f, whiteOutline, 128, 2.0f);

                        d3.AddCircle(center, radius * 0.6f, whiteThin, 64, 1.0f);

                        float crossLen = 10.0f;
                        d3.AddLine(new Vector2(center.X - crossLen, center.Y), new Vector2(center.X + crossLen, center.Y), whiteOutline, 1.2f);
                        d3.AddLine(new Vector2(center.X, center.Y - crossLen), new Vector2(center.X, center.Y + crossLen), whiteOutline, 1.2f);
                        d3.AddCircleFilled(center, 3.0f, whiteOutline, 12);
                    }
                }
            }
            catch { }

            // ── Crosshair (geeg lad style) ────────────────────────────────────
            try
            {
                if (crosshairEnabled)
                {
                    var io2 = ImGui.GetIO();
                    var center = io2.MousePos;
                    var dch = ImGui.GetForegroundDrawList();
                    float len = crosshairLength;
                    float gap = crosshairGap;
                    float thick = crosshairThickness;
                    uint col = crosshairColor;

                    // 4 lines
                    dch.AddLine(new Vector2(center.X - gap - len, center.Y), new Vector2(center.X - gap, center.Y), col, thick);
                    dch.AddLine(new Vector2(center.X + gap, center.Y), new Vector2(center.X + gap + len, center.Y), col, thick);
                    dch.AddLine(new Vector2(center.X, center.Y - gap - len), new Vector2(center.X, center.Y - gap), col, thick);
                    dch.AddLine(new Vector2(center.X, center.Y + gap), new Vector2(center.X, center.Y + gap + len), col, thick);

                    if (crosshairDot)
                        dch.AddCircleFilled(center, crosshairDotSize, col, 12);
                }
            }
            catch { }

            // ── Yerba-style menu (main UI, same overlay as ESP) ───────────────
            try
            {
                MenuUI.Render();
            }
            catch { }

            // ── Debug console (Yerba-styled ImGui popup) ──────────────────────
            try
            {
                DebugConsole.Render();
            }
            catch { }

            // ── Dex Explorer (Yerba-styled ImGui popup) ───────────────────────
            try
            {
                ExplorerWindow.Render();
            }
            catch { }

            // ── Script Drawing Layer ──────────────────────────────────────────
            try
            {
                var scriptObjects = new List<LuaDrawingObject>(ScriptDrawingLayer.Snapshot());
                // Sort by ZIndex ascending so lower ZIndex draws first (behind higher ZIndex).
                // Objects with equal ZIndex preserve their creation order via stable sort.
                scriptObjects.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
                RenderScriptCapture(scriptObjects);

                var sd = ImGui.GetForegroundDrawList();
                foreach (var obj in scriptObjects)
                {
                    if (!obj.Visible) continue;
                    uint col = ScriptDrawingLayer.ToImColor(obj.Color, obj.Transparency);

                    switch (obj.DrawType)
                    {
                        case "Square":
                        {
                            var min = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                            var max = new Vector2(min.X + obj.SizeVec.X, min.Y + obj.SizeVec.Y);
                            if (obj.Filled)
                                sd.AddRectFilled(min, max, col, obj.Corner);
                            else
                                sd.AddRect(min, max, col, obj.Corner, ImDrawFlags.None, obj.Thickness);
                            break;
                        }
                        case "Circle":
                        {
                            var center = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                            int segs = Math.Max(4, obj.NumSides);
                            if (obj.Filled)
                                sd.AddCircleFilled(center, obj.Radius, col, segs);
                            else
                                sd.AddCircle(center, obj.Radius, col, segs, obj.Thickness);
                            break;
                        }
                        case "Line":
                        {
                            sd.AddLine(
                                new Vector2(obj.FromVec.X, obj.FromVec.Y),
                                new Vector2(obj.ToVec.X, obj.ToVec.Y),
                                col, obj.Thickness);
                            break;
                        }
                        case "Text":
                        {
                            var pos = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                            if (obj.Center)
                            {
                                float w = obj.Text.Length * obj.FontSize * 0.55f;
                                pos.X -= w / 2;
                            }
                            if (obj.Outline)
                            {
                                uint oc = ScriptDrawingLayer.ToImColor(obj.OutlineColor, 0f);
                                sd.AddText(new Vector2(pos.X + 1, pos.Y + 1), oc, obj.Text);
                                sd.AddText(new Vector2(pos.X - 1, pos.Y - 1), oc, obj.Text);
                            }
                            sd.AddText(pos, col, obj.Text);
                            break;
                        }
                        case "Triangle":
                        {
                            var a = new Vector2(obj.PointAVec.X, obj.PointAVec.Y);
                            var b = new Vector2(obj.PointBVec.X, obj.PointBVec.Y);
                            var c = new Vector2(obj.PointCVec.X, obj.PointCVec.Y);
                            if (obj.Filled)
                                sd.AddTriangleFilled(a, b, c, col);
                            else
                                sd.AddTriangle(a, b, c, col, obj.Thickness);
                            break;
                        }
                        case "Image":
                        {
                            var imageBytes = obj.ImageBytes;
                            var imageKey = obj.ImageDataKey;
                            if (imageBytes == null || imageBytes.Length == 0 || imageKey == null) break;
                            try
                            {
                                using var ms = new System.IO.MemoryStream(imageBytes);
                                using var image = ISImage.Load<Rgba32>(ms);
                                this.AddOrGetImagePointer(imageKey, image, false, out var handle);
                                if (handle != IntPtr.Zero)
                                {
                                    var min = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                                    var max = new Vector2(min.X + obj.SizeVec.X, min.Y + obj.SizeVec.Y);
                                    sd.AddImage(handle, min, max);
                                }
                            }
                            catch { }
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private static void RenderScriptCapture(List<LuaDrawingObject> objects)
        {
            if (ScriptEngine.RobloxInputEnabled)
                return;

            if (!TryGetScriptCaptureBounds(objects, out var min, out var max))
                return;

            var size = new Vector2(Math.Max(1f, max.X - min.X), Math.Max(1f, max.Y - min.Y));
            ImGui.SetNextWindowPos(min, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0f);

            if (ImGui.Begin("##ScriptInputCapture", ScriptCaptureFlags))
            {
                ImGui.InvisibleButton("##ScriptInputRegion", size);
            }

            ImGui.End();
        }

        private static bool TryGetScriptCaptureBounds(List<LuaDrawingObject> objects, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;
            bool found = false;

            foreach (var obj in objects)
            {
                if (!obj.Visible || obj.ZIndex >= 0)
                    continue;

                if (!TryGetDrawingBounds(obj, out var objMin, out var objMax))
                    continue;

                if (!found)
                {
                    min = objMin;
                    max = objMax;
                    found = true;
                    continue;
                }

                min = Vector2.Min(min, objMin);
                max = Vector2.Max(max, objMax);
            }

            return found;
        }

        private static bool TryGetDrawingBounds(LuaDrawingObject obj, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;

            switch (obj.DrawType)
            {
                case "Square":
                    min = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                    max = min + new Vector2(obj.SizeVec.X, obj.SizeVec.Y);
                    return true;

                case "Circle":
                {
                    var center = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                    var radius = new Vector2(obj.Radius, obj.Radius);
                    min = center - radius;
                    max = center + radius;
                    return true;
                }

                case "Line":
                {
                    var from = new Vector2(obj.FromVec.X, obj.FromVec.Y);
                    var to = new Vector2(obj.ToVec.X, obj.ToVec.Y);
                    min = Vector2.Min(from, to);
                    max = Vector2.Max(from, to);
                    return true;
                }

                case "Text":
                {
                    float width = (obj.Text?.Length ?? 0) * obj.FontSize * 0.55f;
                    float height = obj.FontSize + 4f;
                    var pos = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                    if (obj.Center)
                        pos.X -= width / 2f;
                    min = pos;
                    max = pos + new Vector2(width, height);
                    return true;
                }

                case "Triangle":
                {
                    var a = new Vector2(obj.PointAVec.X, obj.PointAVec.Y);
                    var b = new Vector2(obj.PointBVec.X, obj.PointBVec.Y);
                    var c = new Vector2(obj.PointCVec.X, obj.PointCVec.Y);
                    min = Vector2.Min(a, Vector2.Min(b, c));
                    max = Vector2.Max(a, Vector2.Max(b, c));
                    return true;
                }

                case "Image":
                    min = new Vector2(obj.PositionVec.X, obj.PositionVec.Y);
                    max = min + new Vector2(obj.SizeVec.X, obj.SizeVec.Y);
                    return true;
            }

            return false;
        }

        private static bool is_rbx()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;

            var sb = new StringBuilder(256);
            if (GetWindowTextW(h, sb, 256) > 0 && sb.ToString().Contains("Roblox")) return true;

            GetWindowThreadProcessId(h, out uint pid);
            try { return pid != 0 && Process.GetProcessById((int)pid).ProcessName.Contains("Roblox"); }
            catch { return false; }
        }

        public static void start()
        {
            lock (l)
            {
                if (running) return;
                running = true;
            }

            new Thread(() =>
            {
                try
                {
                    var app = new Program();
                    app.Start();

                    watcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

                    try
                    {
                        if (Application.Current != null)
                        {
                            onAppExit = (s, e) => kill();
                            Application.Current.Exit += onAppExit;
                            onDispatcherShutdown = (s, e) => kill();
                            Application.Current.Dispatcher.ShutdownStarted += onDispatcherShutdown;
                        }

                        onProcessExit = (s, e) => kill();
                        AppDomain.CurrentDomain.ProcessExit += onProcessExit;
                    }
                    catch { }

                    new Thread(() =>
                    {
                        IntPtr win = IntPtr.Zero;
                        while (running)
                        {
                            if (win == IntPtr.Zero) win = FindWindow("Overlay", null);
                            if (win != IntPtr.Zero)
                                SetWindowPos(win, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);

                            Thread.Sleep(200);
                        }
                    })
                    { IsBackground = true }.Start();

                    System.Windows.Threading.Dispatcher.Run();
                }
                catch { }
                finally
                {
                    lock (l) running = false;
                }
            })
            { IsBackground = true }.Start();
        }

        public static void kill()
        {
            lock (l)
            {
                if (!running) return;
                running = false;
            }

            try
            {
                try { if (Application.Current != null && onAppExit != null) Application.Current.Exit -= onAppExit; } catch { }
                try { if (Application.Current != null && onDispatcherShutdown != null) Application.Current.Dispatcher.ShutdownStarted -= onDispatcherShutdown; } catch { }
                try { if (onProcessExit != null) AppDomain.CurrentDomain.ProcessExit -= onProcessExit; } catch { }

                if (watcher != null && !watcher.HasShutdownStarted && !watcher.HasShutdownFinished)
                {
                    watcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send);
                }

                try
                {
                    IntPtr win = FindWindow("Overlay", null);
                    if (win != IntPtr.Zero)
                        PostMessage(win, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }
            catch { }
        }

        private static uint u32(System.Windows.Media.Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);

        // Single-overlay entry point. Uses start() so the static `running` guard
        // is set — any later visuals.Start()/IMGUI.Program.start() from attach
        // will return early instead of spawning a second overlay (which crashes).
        public static void Main(string[] args)
        {
            start();
            while (running) Thread.Sleep(100);
        }
    }
}