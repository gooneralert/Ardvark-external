using System;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.fps
{
    internal static class fps
    {
        private static Thread? t;
        private static bool active;
        private static bool valueWritten;
        private static double originalFps;

        public static void Start()
        {
            if (active) return;
            active = true;
            valueWritten = false;
            originalFps = 0;
            t = new Thread(tick) { IsBackground = true };
            t.Start();
        }

        public static void Stop()
        {
            active = false;
            try
            {
                if (valueWritten && SDKInstance.Mem != null)
                {
                    long ptr = SDKInstance.Mem.ReadPtr(SDKInstance.Mem.Base + Offsets.TaskScheduler.Pointer);
                    if (ptr != 0)
                        SDKInstance.Mem.Write(ptr + Offsets.TaskScheduler.MaxFPS, originalFps);
                }
            }
            catch { }
            valueWritten = false;
        }

        private static void tick()
        {
            while (active)
            {
                try
                {
                    if (!Storage.IsInitialized || SDKInstance.Mem == null) { Thread.Sleep(200); continue; }

                    if (Settings.FPS.FPSEnabled)
                    {
                        long ptr = SDKInstance.Mem.ReadPtr(SDKInstance.Mem.Base + Offsets.TaskScheduler.Pointer);
                        if (ptr == 0) { Thread.Sleep(100); continue; }

                        if (!valueWritten)
                        {
                            originalFps = SDKInstance.Mem.Read<double>(ptr + Offsets.TaskScheduler.MaxFPS);
                            valueWritten = true;
                        }

                        // Geeg-lad style: write the configured cap (0 = unlimited).
                        // Roblox stores either a delay (0..1) or an fps value.
                        double cur = SDKInstance.Mem.Read<double>(ptr + Offsets.TaskScheduler.MaxFPS);
                        bool isDelay = cur > 0.0 && cur <= 1.0;
                        double target = isDelay ? 0.0 : 0.0; // 0 = uncapped
                        if (Settings.FPS.Value > 0f)
                        {
                            float cap = Settings.FPS.Value;
                            target = isDelay ? (1.0 / cap) : cap;
                        }
                        SDKInstance.Mem.Write(ptr + Offsets.TaskScheduler.MaxFPS, target);
                    }
                    else
                    {
                        if (valueWritten && SDKInstance.Mem != null)
                        {
                            long ptr = SDKInstance.Mem.ReadPtr(SDKInstance.Mem.Base + Offsets.TaskScheduler.Pointer);
                            if (ptr != 0)
                                SDKInstance.Mem.Write(ptr + Offsets.TaskScheduler.MaxFPS, originalFps);
                            valueWritten = false;
                        }
                        Thread.Sleep(200);
                    }
                }
                catch { }

                Thread.Sleep(50);
            }
        }
    }
}