using System;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.gravity
{
    internal static class gravity
    {
        private static Thread? t;
        private static bool active;
        private static bool valueWritten;
        private static float originalGravity;

        public static void Start()
        {
            if (active) return;
            active = true;
            valueWritten = false;
            originalGravity = 196.2f;
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
                    long world = SDKInstance.Mem.ReadPtr(Storage.WorkspaceInstance.Address + Offsets.Workspace.World);
                    if (world != 0)
                        SDKInstance.Mem.Write(world + Offsets.World.Gravity, originalGravity);
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

                    bool enabled = Settings.Gravity.Enabled;

                    if (enabled)
                    {
                    long world = SDKInstance.Mem.ReadPtr(Storage.WorkspaceInstance.Address + Offsets.Workspace.World);
                        if (world != 0)
                        {
                            if (!valueWritten)
                            {
                                originalGravity = SDKInstance.Mem.Read<float>(world + Offsets.World.Gravity);
                                valueWritten = true;
                            }
                            SDKInstance.Mem.Write(world + Offsets.World.Gravity, Settings.Gravity.Value);
                        }
                    }
                    else
                    {
                        if (valueWritten && SDKInstance.Mem != null)
                        {
                            long world = SDKInstance.Mem.ReadPtr(Storage.WorkspaceInstance.Address + Offsets.Workspace.World);
                            if (world != 0)
                                SDKInstance.Mem.Write(world + Offsets.World.Gravity, originalGravity);
                            valueWritten = false;
                        }
                        Thread.Sleep(200);
                    }
                }
                catch { }

                Thread.Sleep(10);
            }
        }
    }
}