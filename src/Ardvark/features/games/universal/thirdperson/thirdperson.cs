using System;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.thirdperson
{
    internal static class thirdperson
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

                    bool enabled = Settings.ThirdPerson.Enabled;

                    if (enabled)
                    {
                        var camCache = Storage.CameraInstance;
                        if (!camCache.IsValid) { Thread.Sleep(200); continue; }

                        var cam = new SDKInstance(camCache.Address);
                        if (!cam.IsValid) { Thread.Sleep(50); continue; }

                        // Set camera type to Scriptable (6) to allow manual control
                        SDKInstance.Mem.Write(cam.Address + Offsets.Camera.CameraType, 6);

                        // Get the local player's character HRP position to offset behind
                        var lp = Storage.LocalPlayerInstance;
                        if (!lp.IsValid) { Thread.Sleep(50); continue; }

                        var chr = lp.GetCharacter();
                        if (!chr.IsValid) { Thread.Sleep(50); continue; }

                        var hrp = chr.FindFirstChild("HumanoidRootPart");
                        if (!hrp.IsValid) { Thread.Sleep(50); continue; }

                        // Read HRP CFrame
                        var cframe = hrp.GetCFrame();

                        float dist = Settings.ThirdPerson.Distance;

                        // LookVector is -column2 (since column2 is Back vector in Roblox)
                        // Camera offset = position - lookVector * distance
                        float lookX = -cframe.r02;
                        float lookY = -cframe.r12;
                        float lookZ = -cframe.r22;

                        // Normalize
                        float len = (float)Math.Sqrt(lookX * lookX + lookY * lookY + lookZ * lookZ);
                        if (len > 0.001f)
                        {
                            lookX /= len;
                            lookY /= len;
                            lookZ /= len;
                        }

                        // Set camera position behind the player
                        SDKInstance.Mem.Write(cam.Address + Offsets.Camera.Position,
                            new Vector3
                            {
                                x = cframe.x - lookX * dist,
                                y = cframe.y - lookY * dist + 2f, // slight upward offset
                                z = cframe.z - lookZ * dist
                            });

                        // Copy HRP rotation to camera so it faces the same direction
                        // Column 0 = Right, Column 1 = Up, Column 2 = Back
                        var camRot = new Matrix3x3
                        {
                            r00 = cframe.r00, r01 = cframe.r01, r02 = cframe.r02,
                            r10 = cframe.r10, r11 = cframe.r11, r12 = cframe.r12,
                            r20 = cframe.r20, r21 = cframe.r21, r22 = cframe.r22
                        };
                        SDKInstance.Mem.Write(cam.Address + Offsets.Camera.Rotation, camRot);
                    }
                    else
                    {
                        // Restore camera type to Custom (0) when disabled so the engine controls it again
                        var camCache = Storage.CameraInstance;
                        if (camCache.IsValid)
                        {
                            var cam = new SDKInstance(camCache.Address);
                            if (cam.IsValid)
                            {
                                int currentType = SDKInstance.Mem.Read<int>(cam.Address + Offsets.Camera.CameraType);
                                if (currentType == 6) // Scriptable
                                    SDKInstance.Mem.Write(cam.Address + Offsets.Camera.CameraType, 0);
                            }
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