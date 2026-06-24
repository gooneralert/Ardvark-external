using System;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.infinitejump
{
    internal static class infinitejump
    {
        private static Thread? t;
        private static bool active;

        public static void Start()
        {
            if (active) return;
            active = true;
            t = new Thread(tick) { IsBackground = true };
            t.Start();
        }

        public static void Stop() => active = false;

        private static void tick()
        {
            while (active)
            {
                try
                {
                    if (!Storage.IsInitialized || SDKInstance.Mem == null) { Thread.Sleep(200); continue; }

                    var lp = Storage.LocalPlayerInstance;
                    if (!lp.IsValid) { Thread.Sleep(50); continue; }

                    var chr = lp.GetCharacter();
                    if (!chr.IsValid) { Thread.Sleep(50); continue; }

                    var hum = chr.FindFirstChildOfClass("Humanoid");
                    if (!hum.IsValid) { Thread.Sleep(50); continue; }

                    if (Settings.InfiniteJump.Enabled)
                    {
                        float power = Settings.InfiniteJump.CustomPower ? Settings.InfiniteJump.PowerValue : hum.GetJumpPower();
                        SDKInstance.Mem.Write(hum.Address + Offsets.Humanoid.JumpPower, power);
                        SDKInstance.Mem.Write(hum.Address + Offsets.Humanoid.UseJumpPower, (byte)1);
                    }

                    Thread.Sleep(10);
                }
                catch { Thread.Sleep(10); }
            }
        }
    }
}