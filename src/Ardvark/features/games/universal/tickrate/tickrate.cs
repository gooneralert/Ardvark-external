using System;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.tickrate
{
    internal static class tickrate
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

                    bool enabled = Settings.Tickrate.Enabled;

                    if (enabled)
                    {
                        var lp = Storage.LocalPlayerInstance;
                        if (!lp.IsValid) { Thread.Sleep(50); continue; }

                        var chr = lp.GetCharacter();
                        if (!chr.IsValid) { Thread.Sleep(50); continue; }

                        var hum = chr.FindFirstChildOfClass("Humanoid");
                        if (!hum.IsValid) { Thread.Sleep(50); continue; }

                        // Write to HeartbeatFPS on RunService to control tickrate
                        // RunService.HeartbeatFPS = 0xb8 (exists in offsets)
                        // The DataModel has ScriptContext at 0x440 which contains RunService info
                        // Alternatively, write to Humanoid's internal tickrate
                        // Offset 0x1d4 = WalkspeedCheck is used for walkspeed sync,
                        // HeartbeatFPS affects overall render/heartbeat rate
                        // For Humanoid tickrate specifically, we can write to worldStepsPerSec
                        var dm = SDKInstance.GetDataModel();
                        if (dm.IsValid)
                        {
                            long ws = SDKInstance.Mem.ReadPtr(Storage.WorkspaceInstance.Address + Offsets.Workspace.World);
                            if (ws != 0)
                            {
                                // worldStepsPerSec at World.0x680
                                SDKInstance.Mem.Write(ws + Offsets.World.worldStepsPerSec, Settings.Tickrate.Value);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }
                }
                catch { }

                Thread.Sleep(10);
            }
        }
    }
}