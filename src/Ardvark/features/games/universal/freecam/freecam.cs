using System;
using System.Runtime.InteropServices;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.freecam
{
    // ────────────────────────────────────────────────────────────────────────
    //  freecam — C# port of the geeg lad freecam_tick (Misc.cpp). Moves the
    //  camera via Humanoid.CameraOffset + PlatformStand, WASD/Space/Ctrl to
    //  fly, Shift to boost. Restores on disable.
    // ────────────────────────────────────────────────────────────────────────
    public static class freecam
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        private static Thread? t;
        private static bool active;

        private static bool on;
        private static bool prev;
        private static bool toggled;
        private static Vector3 savedOff;
        private static bool savedPlat;
        private static bool savedAutoRot;
        private static Vector3 off;
        private static Vector3 world;

        public static void Start()
        {
            if (active) return;
            active = true;
            t = new Thread(Tick) { IsBackground = true };
            t.Start();
        }

        public static void Stop()
        {
            active = false;
            Restore();
        }

        private static void Restore()
        {
            try
            {
                if (on && SDKInstance.Mem != null)
                {
                    long hum = FindHumanoid();
                    if (hum != 0)
                    {
                        SDKInstance.Mem.Write(hum + Offsets.Humanoid.CameraOffset, savedOff);
                        SDKInstance.Mem.Write(hum + Offsets.Humanoid.PlatformStand, savedPlat);
                        SDKInstance.Mem.Write(hum + Offsets.Humanoid.AutoRotate, savedAutoRot);
                    }
                }
            }
            catch { }
            on = false;
        }

        private static long FindHumanoid()
        {
            try
            {
                if (!Storage.IsInitialized || !Storage.LocalPlayerInstance.IsValid) return 0;
                var chara = Storage.LocalPlayerInstance.GetCharacter();
                if (!chara.IsValid) return 0;
                foreach (var c in chara.GetChildren())
                {
                    if (c.GetClass() == "Humanoid") return c.Address;
                }
            }
            catch { }
            return 0;
        }

        private static void Tick()
        {
            while (active)
            {
                try
                {
                    if (!Storage.IsInitialized || SDKInstance.Mem == null) { Thread.Sleep(200); continue; }

                    var fc = Settings.Freecam;
                    if (!fc.Enabled) { Restore(); Thread.Sleep(50); continue; }

                    long hum = FindHumanoid();
                    if (hum == 0) { Restore(); Thread.Sleep(100); continue; }

                    // Determine want state
                    bool want = false;
                    if (fc.Mode == 2)
                    {
                        want = true;
                        prev = false;
                    }
                    else if (fc.Key != 0)
                    {
                        bool down = (GetAsyncKeyState(fc.Key) & 0x8000) != 0;
                        if (fc.Mode == 1)
                        {
                            if (down && !prev) toggled = !toggled;
                            want = toggled;
                        }
                        else
                        {
                            want = down;
                        }
                        prev = down;
                    }
                    else
                    {
                        toggled = false;
                    }

                    if (!want)
                    {
                        Restore();
                        Thread.Sleep(10);
                        continue;
                    }

                    if (!on)
                    {
                        on = true;
                        savedOff = SDKInstance.Mem.Read<Vector3>(hum + Offsets.Humanoid.CameraOffset);
                        savedPlat = SDKInstance.Mem.Read<bool>(hum + Offsets.Humanoid.PlatformStand);
                        savedAutoRot = SDKInstance.Mem.Read<bool>(hum + Offsets.Humanoid.AutoRotate);
                        off = new Vector3 { x = 0, y = 0, z = 0 };
                        world = new Vector3 { x = 0, y = 0, z = 0 };
                    }

                    SDKInstance.Mem.Write(hum + Offsets.Humanoid.PlatformStand, true);
                    SDKInstance.Mem.Write(hum + Offsets.Humanoid.AutoRotate, false);

                    // Camera basis from Workspace.Camera rotation
                    Vector3 fwd, right, up;
                    if (!GetCamBasis(out fwd, out right, out up))
                    {
                        fwd = new Vector3 { x = 0, y = 0, z = -1 };
                        right = new Vector3 { x = 1, y = 0, z = 0 };
                        up = new Vector3 { x = 0, y = 1, z = 0 };
                    }

                    Vector3 dir = new Vector3 { x = 0, y = 0, z = 0 };
                    if ((GetAsyncKeyState('W') & 0x8000) != 0) dir = Add(dir, fwd);
                    if ((GetAsyncKeyState('S') & 0x8000) != 0) dir = Sub(dir, fwd);
                    if ((GetAsyncKeyState('D') & 0x8000) != 0) dir = Add(dir, right);
                    if ((GetAsyncKeyState('A') & 0x8000) != 0) dir = Sub(dir, right);
                    if ((GetAsyncKeyState(0x20) & 0x8000) != 0) dir = Add(dir, up);
                    if ((GetAsyncKeyState(0x11) & 0x8000) != 0) dir = Sub(dir, up);

                    float spd = fc.Speed;
                    if ((GetAsyncKeyState(0x10) & 0x8000) != 0) spd *= 3f;

                    float len = LengthSq(dir);
                    if (len > 0.01f)
                    {
                        var norm = Normalize(dir);
                        world = Add(world, Scale(norm, spd * 0.016f));
                    }

                    // Convert world offset to local (root rotation)
                    Vector3 local = new Vector3 { x = world.x, y = world.y, z = world.z };
                    off = Add(savedOff, local);
                    SDKInstance.Mem.Write(hum + Offsets.Humanoid.CameraOffset, off);
                }
                catch { }
                Thread.Sleep(16);
            }
        }

        private static bool GetCamBasis(out Vector3 fwd, out Vector3 right, out Vector3 up)
        {
            fwd = right = up = default;
            try
            {
                if (!Storage.CameraInstance.IsValid) return false;
                var rot = SDKInstance.Mem.Read<Matrix4>(Storage.CameraInstance.Address + Offsets.Camera.Rotation);
                if (rot.data == null || rot.data.Length < 16) return false;
                // data is column-major: [col*4+row]
                fwd = new Vector3 { x = -rot.data[8 + 0], y = -rot.data[8 + 1], z = -rot.data[8 + 2] };
                right = new Vector3 { x = rot.data[0], y = rot.data[1], z = rot.data[2] };
                up = new Vector3 { x = rot.data[4], y = rot.data[5], z = rot.data[6] };
                return true;
            }
            catch { return false; }
        }

        private static Vector3 Add(Vector3 a, Vector3 b) => new Vector3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };
        private static Vector3 Sub(Vector3 a, Vector3 b) => new Vector3 { x = a.x - b.x, y = a.y - b.y, z = a.z - b.z };
        private static Vector3 Scale(Vector3 a, float s) => new Vector3 { x = a.x * s, y = a.y * s, z = a.z * s };
        private static float LengthSq(Vector3 a) => a.x * a.x + a.y * a.y + a.z * a.z;
        private static Vector3 Normalize(Vector3 a)
        {
            float len = (float)Math.Sqrt(LengthSq(a));
            if (len < 1e-4f) return new Vector3 { x = 0, y = 0, z = 0 };
            return new Vector3 { x = a.x / len, y = a.y / len, z = a.z / len };
        }
    }
}