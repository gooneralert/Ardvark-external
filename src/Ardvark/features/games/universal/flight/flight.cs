using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using FoulzExternal.SDK;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using Offsets;
using Options;

namespace FoulzExternal.features.games.universal.flight
{
    internal static class flight
    {
        private static bool running;
        private static Thread? thread;
        private static readonly object locker = new();

        private static volatile bool flyActive;
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
            // refresh cache every ~2 seconds
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
                flyActive = false;
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
                flyActive = false;
                thread?.Join();
            }
        }

        static bool Key(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static void Loop()
        {
            Vector3 flyPos = new();
            bool hasFlyPos = false;

            var clock = Stopwatch.StartNew();
            double prev = clock.Elapsed.TotalSeconds;

            while (running)
            {
                try
                {
                    if (!Options.Settings.Flight.VFlight)
                    {
                        flyActive = false;
                        bindWasDown = false;
                        hasFlyPos = false;
                        Thread.Sleep(50);
                        continue;
                    }

                    bool down = Options.Settings.Flight.VFlightBind.IsPressed();
                    if (down && !bindWasDown)
                    {
                        flyActive = !flyActive;
                        hasFlyPos = false;
                    }
                    bindWasDown = down;

                    if (!flyActive) { prev = clock.Elapsed.TotalSeconds; hasFlyPos = false; Thread.Sleep(5); continue; }

                    if (!Storage.IsInitialized || SDK.Instance.Mem == null)
                    { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(5); continue; }

                    var lp = Storage.LocalPlayerInstance;
                    if (!lp.IsValid) { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(2); continue; }

                    var chr = lp.GetCharacter();
                    if (!chr.IsValid) { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(2); continue; }

                    var hrp = chr.FindFirstChild("HumanoidRootPart");
                    if (!hrp.IsValid) { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(2); continue; }

                    var cam = Storage.CameraInstance;
                    if (!cam.IsValid) { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(2); continue; }

                    long prim = SDK.Instance.Mem.ReadPtr(hrp.Address + Offsets.BasePart.Primitive);
                    if (prim == 0) { prev = clock.Elapsed.TotalSeconds; Thread.Sleep(2); continue; }

                    var mem = SDK.Instance.Mem;

                    // --- timing ---
                    double now = clock.Elapsed.TotalSeconds;
                    float dt = Math.Clamp((float)(now - prev), 0.0001f, 0.05f);
                    prev = now;

                    // --- camera rotation matrix ---
                    var rot = mem.Read<Matrix3x3>(cam.Address + Offsets.Camera.Rotation);

                    // Extract camera look direction projected onto XZ plane (yaw only).
                    // This prevents glitches when looking straight up or down.
                    float lookX = -rot.r02;
                    float lookZ = -rot.r22;
                    float yawLen = (float)Math.Sqrt(lookX * lookX + lookZ * lookZ);

                    Vector3 forward, right;
                    if (yawLen > 0.001f)
                    {
                        forward = new Vector3 { x = lookX / yawLen, y = 0, z = lookZ / yawLen };
                        // right = forward cross worldUp = (-fz, 0, fx)
                        right = new Vector3 { x = -forward.z, y = 0, z = forward.x };
                    }
                    else
                    {
                        forward = new Vector3 { x = 0, y = 0, z = -1 };
                        right = new Vector3 { x = 1, y = 0, z = 0 };
                    }

                    var worldUp = new Vector3 { x = 0, y = 1, z = 0 };

                    // --- initialise fly position from game on first frame ---
                    if (!hasFlyPos)
                    {
                        flyPos = mem.Read<Vector3>(prim + Offsets.Primitive.Position);
                        hasFlyPos = true;
                    }

                    // --- input -> movement direction (only when Roblox is focused) ---
                    float speed = Options.Settings.Flight.VFlightSpeed;
                    Vector3 moveDir = new() { x = 0, y = 0, z = 0 };
                    bool focused = IsRobloxFocused();
                    if (focused)
                    {
                        if (Key(0x57)) moveDir = moveDir + forward;   // W  - forward (XZ plane)
                        if (Key(0x53)) moveDir = moveDir - forward;   // S  - backward
                        if (Key(0x41)) moveDir = moveDir - right;     // A  - strafe left
                        if (Key(0x44)) moveDir = moveDir + right;     // D  - strafe right
                        if (Key(0x20)) moveDir = moveDir + worldUp;   // Space  - up
                        if (Key(0xA0)) moveDir = moveDir - worldUp;   // LShift - down
                    }

                    bool moving = moveDir.Magnitude() > 0.001f;
                    Vector3 velocity;

                    if (moving)
                    {
                        moveDir = moveDir.Normalize();
                        velocity = moveDir * speed;
                        flyPos = flyPos + velocity * dt;
                    }
                    else
                    {
                        velocity = new Vector3 { x = 0, y = 0, z = 0 };
                    }

                    // --- write position + velocity every frame ---
                    // Position = authoritative placement (no drift/rubber-band)
                    // Velocity = tells physics engine our intent (smooth interpolation between writes)
                    mem.Write(prim + Offsets.Primitive.Position, flyPos);
                    mem.Write(prim + Offsets.Primitive.AssemblyLinearVelocity, velocity);
                    mem.Write(prim + Offsets.Primitive.AssemblyAngularVelocity, new Vector3 { x = 0, y = 0, z = 0 });

                    // --- fake shift lock: rotate character to face camera yaw ---
                    // Builds a yaw-only rotation matrix so the character always faces
                    // where the camera looks, without requiring shift lock to be enabled.
                    if (yawLen > 0.001f)
                    {
                        // CFrame columns: col0=Right, col1=Up, col2=-Look
                        var charRot = new Matrix3x3
                        {
                            r00 = -forward.z, r01 = 0, r02 = -forward.x,
                            r10 = 0,          r11 = 1, r12 = 0,
                            r20 = forward.x,  r21 = 0, r22 = -forward.z,
                        };
                        mem.Write(prim + Offsets.Primitive.Rotation, charRot);
                    }

                    // --- noclip ---
                    byte flags = mem.Read<byte>(prim + Offsets.Primitive.Flags);
                    byte clean = (byte)(flags & ~(byte)Offsets.PrimitiveFlags.CanCollide);
                    if (flags != clean)
                        mem.Write(prim + Offsets.Primitive.Flags, clean);
                }
                catch { }

                Thread.SpinWait(100);
            }
        }
    }
}
