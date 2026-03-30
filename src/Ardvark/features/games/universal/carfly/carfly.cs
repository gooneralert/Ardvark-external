using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using FoulzExternal.SDK;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using FoulzExternal.logging.notifications;
using Offsets;
using Options;
using SDKInstance = FoulzExternal.SDK.Instance;

// Jailbreak car fly.
// Keys: W/A/S/D move (camera-relative, yaw only), Z up, X down.
// Toggle bind activates while CarFlyEnabled is on.
//
// Uses velocity-only movement — never writes Position so Roblox collision detection is preserved.
// Writing AssemblyLinearVelocity = {0,0,0} when no keys are held counteracts gravity (hover).
// Camera is not touched, so mouse-look works normally in vehicle seat mode.

namespace FoulzExternal.features.games.universal.carfly
{
    internal static class carfly
    {
        private static bool running;
        private static Thread? thread;
        private static readonly object locker = new();

        private static volatile bool flyActive;
        private static bool bindWasDown;

        private static long _cachedCarAddr;
        private static long _cachedRootPrim;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static uint _cachedRobloxPid;
        private static double _pidCacheTime;

        static bool IsRobloxFocused()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowThreadProcessId(fg, out uint pid);
            if (_cachedRobloxPid != 0 && pid == _cachedRobloxPid) return true;
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (now - _pidCacheTime > 2.0)
            {
                _pidCacheTime = now;
                try { var p = Process.GetProcessesByName("RobloxPlayerBeta"); if (p.Length > 0) _cachedRobloxPid = (uint)p[0].Id; } catch { }
            }
            return pid == _cachedRobloxPid;
        }

        public static void Start()
        {
            lock (locker)
            {
                if (running) return;
                running = true;
                flyActive = false;
                bindWasDown = false;
                ResetCache();
                thread = new Thread(Loop) { IsBackground = true };
                thread.Start();
            }
        }

        public static void Stop()
        {
            lock (locker) { running = false; flyActive = false; }
        }

        static bool Key(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        static void ResetCache()
        {
            _cachedCarAddr = 0;
            _cachedRootPrim = 0;
        }

        private static string ReadStringValue(SDKInstance inst)
        {
            try
            {
                long addr = inst.Address + Offsets.Misc.Value;
                int len = SDKInstance.Mem.Read<int>(addr + 0x18);
                if (len <= 0 || len > 256) return "";
                if (len >= 16)
                {
                    long ptr = SDKInstance.Mem.ReadPtr(addr);
                    return ptr != 0 ? SDKInstance.Mem.ReadString(ptr) : "";
                }
                return SDKInstance.Mem.ReadString(addr);
            }
            catch { return ""; }
        }

        private static SDKInstance FindCar()
        {
            var lp = Storage.LocalPlayerInstance;
            if (!lp.IsValid) return new SDKInstance(0);

            string localName = "";
            try { localName = lp.GetName(); } catch { }
            if (string.IsNullOrEmpty(localName)) return new SDKInstance(0);

            var ws = Storage.WorkspaceInstance;
            if (!ws.IsValid) return new SDKInstance(0);

            var vehiclesFolder = ws.FindFirstChild("Vehicles");
            if (!vehiclesFolder.IsValid) return new SDKInstance(0);

            List<SDKInstance>? vehicles = null;
            try { vehicles = vehiclesFolder.GetChildren(); } catch { }
            if (vehicles == null) return new SDKInstance(0);

            string stateKey = "_VehicleState_" + localName;
            foreach (var v in vehicles)
            {
                try
                {
                    if (v.FindFirstChild(stateKey).IsValid) return v;
                    var seat = v.FindFirstChild("Seat");
                    if (seat.IsValid)
                    {
                        var pnVal = seat.FindFirstChild("PlayerName");
                        if (pnVal.IsValid && ReadStringValue(pnVal) == localName) return v;
                    }
                }
                catch { }
            }
            return new SDKInstance(0);
        }

        // Finds the Primitive pointer for the car root part.
        // Tries Model.PrimaryPart first, then falls back to the first valid BasePart child.
        private static long FindRootPrimitive(SDKInstance car)
        {
            try
            {
                long ppAddr = SDKInstance.Mem.ReadPtr(car.Address + Offsets.Model.PrimaryPart);
                if (ppAddr != 0)
                {
                    long prim = SDKInstance.Mem.ReadPtr(ppAddr + Offsets.BasePart.Primitive);
                    if (prim != 0) return prim;
                }
            }
            catch { }

            List<SDKInstance>? kids = null;
            try { kids = car.GetChildren(); } catch { }
            if (kids == null) return 0;

            foreach (var kid in kids)
            {
                try
                {
                    string cls = kid.GetClass();
                    if (cls == "Part" || cls == "MeshPart" || cls == "UnionOperation"
                        || cls == "TrussPart" || cls == "WedgePart" || cls == "CornerWedgePart")
                    {
                        long prim = SDKInstance.Mem.ReadPtr(kid.Address + Offsets.BasePart.Primitive);
                        if (prim != 0) return prim;
                    }
                }
                catch { }
            }
            return 0;
        }

        private static void Loop()
        {
            var clock = Stopwatch.StartNew();
            double nextCarSearch = 0;
            bool prevFlyActive = false;

            while (running)
            {
                try
                {
                    if (!Settings.CarFly.CarFlyEnabled)
                    {
                        // Zero velocity before going fully idle so the car doesn't rocket off.
                        if (prevFlyActive && _cachedRootPrim != 0 && SDKInstance.Mem != null)
                        {
                            var z = new Vector3 { x = 0f, y = 0f, z = 0f };
                            try { SDKInstance.Mem.Write(_cachedRootPrim + Offsets.Primitive.AssemblyLinearVelocity,  z); } catch { }
                            try { SDKInstance.Mem.Write(_cachedRootPrim + Offsets.Primitive.AssemblyAngularVelocity, z); } catch { }
                        }
                        flyActive = false;
                        prevFlyActive = false;
                        bindWasDown = false;
                        ResetCache();
                        nextCarSearch = 0;
                        Thread.Sleep(50);
                        continue;
                    }

                    bool down = Settings.CarFly.CarFlyBind.IsPressed();
                    if (down && !bindWasDown)
                    {
                        flyActive = !flyActive;
                        notify.Notify("CarFly", flyActive ? "ON" : "OFF", 2000);
                    }
                    bindWasDown = down;

                    double now = clock.Elapsed.TotalSeconds;

                    // On the exact frame fly is toggled off, write zero velocity so the car
                    // stops cleanly instead of continuing at the last written speed.
                    bool justDeactivated = prevFlyActive && !flyActive;
                    prevFlyActive = flyActive;

                    if (!flyActive || !Storage.IsInitialized || SDKInstance.Mem == null)
                    {
                        if (justDeactivated && _cachedRootPrim != 0 && SDKInstance.Mem != null)
                        {
                            var z = new Vector3 { x = 0f, y = 0f, z = 0f };
                            try { SDKInstance.Mem.Write(_cachedRootPrim + Offsets.Primitive.AssemblyLinearVelocity,  z); } catch { }
                            try { SDKInstance.Mem.Write(_cachedRootPrim + Offsets.Primitive.AssemblyAngularVelocity, z); } catch { }
                        }
                        Thread.Sleep(5);
                        continue;
                    }

                    // Only search for the car periodically to avoid per-frame tree traversal stutter.
                    // Re-check every 2 s while sitting in a car, 0.3 s while not in one.
                    if (_cachedCarAddr == 0 || now >= nextCarSearch)
                    {
                        var car = FindCar();
                        if (car.IsValid)
                        {
                            if (car.Address != _cachedCarAddr)
                            {
                                _cachedCarAddr = car.Address;
                                _cachedRootPrim = FindRootPrimitive(car);
                            }
                            nextCarSearch = now + 2.0;
                        }
                        else
                        {
                            _cachedCarAddr = 0;
                            _cachedRootPrim = 0;
                            nextCarSearch = now + 0.3;
                        }
                    }

                    if (_cachedCarAddr == 0 || _cachedRootPrim == 0)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    var mem = SDKInstance.Mem;

                    // Camera-relative directions (yaw only) — same approach as flight.cs.
                    var forward = new Vector3 { x = 0, y = 0, z = -1 };
                    var right   = new Vector3 { x = 1, y = 0, z =  0 };
                    var cam = Storage.CameraInstance;
                    if (cam.IsValid)
                    {
                        var rot = mem.Read<Matrix3x3>(cam.Address + Offsets.Camera.Rotation);
                        float lookX = -rot.r02;
                        float lookZ = -rot.r22;
                        float yawLen = (float)Math.Sqrt(lookX * lookX + lookZ * lookZ);
                        if (yawLen > 0.001f)
                        {
                            forward = new Vector3 { x = lookX / yawLen, y = 0, z = lookZ / yawLen };
                            right   = new Vector3 { x = -forward.z,     y = 0, z = forward.x      };
                        }
                    }

                    float speed = Settings.CarFly.CarFlySpeed;
                    var moveDir = new Vector3 { x = 0, y = 0, z = 0 };
                    var worldUp = new Vector3 { x = 0, y = 1, z = 0 };

                    if (IsRobloxFocused())
                    {
                        if (Key(0x57)) moveDir = moveDir + forward;  // W - forward
                        if (Key(0x53)) moveDir = moveDir - forward;  // S - backward
                        if (Key(0x41)) moveDir = moveDir - right;    // A - left
                        if (Key(0x44)) moveDir = moveDir + right;    // D - right
                        if (Key(0x5A)) moveDir = moveDir + worldUp;  // Z - up
                        if (Key(0x58)) moveDir = moveDir - worldUp;  // X - down
                    }

                    // Velocity-only: no Position write so collision detection is preserved.
                    // Writing zero velocity when idle counteracts gravity → car hovers in place.
                    Vector3 velocity;
                    if (moveDir.Magnitude() > 0.001f)
                        velocity = moveDir.Normalize() * speed;
                    else
                        velocity = new Vector3 { x = 0f, y = 0f, z = 0f };

                    mem.Write(_cachedRootPrim + Offsets.Primitive.AssemblyLinearVelocity, velocity);
                }
                catch { }
                Thread.SpinWait(100);
            }
        }
    }
}
