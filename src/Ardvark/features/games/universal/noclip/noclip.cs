using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Offsets;
using Options;

namespace FoulzExternal.features.games.universal.noclip
{
    internal static class noclip
    {
        private static bool running;
        private static Thread? thread;
        private static readonly object locker = new();

        private static volatile bool noclipActive;
        private static bool bindWasDown;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static uint cachedRobloxPid;
        private static double pidCacheTime;

        static bool IsRobloxFocused()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowThreadProcessId(fg, out uint pid);
            if (cachedRobloxPid != 0 && pid == cachedRobloxPid) return true;
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (now - pidCacheTime > 2.0)
            {
                pidCacheTime = now;
                try
                {
                    var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                    if (procs.Length > 0) cachedRobloxPid = (uint)procs[0].Id;
                }
                catch { }
            }
            return pid == cachedRobloxPid;
        }

        public static void Start()
        {
            lock (locker)
            {
                if (running) return;
                running = true;
                noclipActive = false;
                bindWasDown = false;
                thread = new Thread(Loop) { IsBackground = true };
                thread.Start();
            }
        }

        public static void Stop()
        {
            lock (locker)
            {
                running = false;
                noclipActive = false;
                thread?.Join();
            }
        }

        private static void Loop()
        {
            while (running)
            {
                try
                {
                    if (!Settings.Misc.NoclipEnabled)
                    {
                        noclipActive = false;
                        bindWasDown = false;
                        Thread.Sleep(50);
                        continue;
                    }

                    bool down = Settings.Misc.NoclipBind.IsPressed();
                    if (down && !bindWasDown)
                        noclipActive = !noclipActive;
                    bindWasDown = down;

                    if (!noclipActive) { Thread.Sleep(5); continue; }

                    if (!Storage.IsInitialized || SDK.Instance.Mem == null)
                    { Thread.Sleep(5); continue; }

                    var lp = Storage.LocalPlayerInstance;
                    if (!lp.IsValid) { Thread.Sleep(2); continue; }

                    var chr = lp.GetCharacter();
                    if (!chr.IsValid) { Thread.Sleep(2); continue; }

                    var mem = SDK.Instance.Mem;

                    foreach (var part in chr.GetChildren())
                    {
                        string cls = part.GetClass();
                        if (cls != "Part" && cls != "MeshPart" && cls != "BasePart")
                            continue;

                        try
                        {
                            long prim = mem.ReadPtr(part.Address + BasePart.Primitive);
                            if (prim == 0) continue;

                            byte flags = mem.Read<byte>(prim + Primitive.Flags);
                            byte cleared = (byte)(flags & ~(byte)PrimitiveFlags.CanCollide);
                            if (flags != cleared)
                                mem.Write(prim + Primitive.Flags, cleared);
                        }
                        catch { }
                    }
                }
                catch { }

                Thread.Sleep(2);
            }
        }
    }
}
