using System;
using System.Threading;
using FoulzExternal.helpers.keybind;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Offsets;

namespace FoulzExternal.features.games.universal.noclip
{
    internal static class noclip
    {
        public static bool Enabled = false;
        public static KeyBind Bind = new KeyBind("NoclipBind");
        // true = keybind toggles noclip on/off, false = always active when Enabled
        public static bool BindMode = true;

        private static bool running;
        private static Thread? thread;
        private static readonly object locker = new();

        private static volatile bool noclipActive;
        private static bool bindWasDown;

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
                    if (!Enabled)
                    {
                        noclipActive = false;
                        bindWasDown = false;
                        Thread.Sleep(50);
                        continue;
                    }

                    if (BindMode)
                    {
                        bool down = Bind.IsPressed();
                        if (down && !bindWasDown)
                            noclipActive = !noclipActive;
                        bindWasDown = down;
                    }
                    else
                    {
                        noclipActive = true;
                    }

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
