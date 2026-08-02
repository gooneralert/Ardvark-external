using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FoulzExternal.SDK;
using FoulzExternal.SDK.caches;
using FoulzExternal.SDK.gamedetector;
using FoulzExternal.SDK.structures;
using FoulzExternal.SDK.worldtoscreen;
using FoulzExternal.storage;
using Offsets;
using Options;
using FoulzExternal.features.games.universal.checks.teamcheck;
using FoulzExternal.features.games.universal.checks.downedcheck;
using FoulzExternal.features.games.universal.checks.transparencycheck;
using SDKInstance = FoulzExternal.SDK.Instance;

namespace FoulzExternal.features.games.universal.aiming.silent
{
    /// <summary>
    /// Phantom Forces silent aim. Works by continuously writing the camera's
    /// rotation to look at the target (LookAt matrix). In PF (a first-person
    /// shooter) this makes bullets hit the target without moving the mouse.
    ///
    /// Only active while in the Phantom Forces place (292439477). When active
    /// it replaces the other silent aim methods.
    /// </summary>
    internal static class phantomsilent
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LBUTTON = 0x01;

        // ── State ──────────────────────────────────────────────────
        private static bool _writerRun = false;
        private static bool _writerStarted = false;
        private static volatile bool _active = false;
        private static long _camAddr = 0;
        private static bool _camIsPart = false;
        private static Matrix3x3 _lastMatrix;
        private static Thread? _writer;
        private static bool _running = false;
        private static Thread? _loop;

        // ── Public API ─────────────────────────────────────────────
        public static bool Active() => _active;

        public static void Start()
        {
            if (_running) return;
            _running = true;
            _loop = new Thread(Loop) { IsBackground = true };
            _loop.Start();
        }

        public static void Stop()
        {
            _running = false;
            _writerRun = false;
            _active = false;
            try { _loop?.Join(200); } catch { }
            try { _writer?.Join(200); } catch { }
            _loop = null; _writer = null;
            _writerStarted = false;
        }

        public static bool IsActivePlace()
        {
            try { return finder.whatgame() == GameType.pf; }
            catch { return false; }
        }

        public static void SetActive(bool on, Vector3 worldTarget)
        {
            if (!on)
            {
                _active = false;
                return;
            }
            if (!IsActivePlace())
            {
                _active = false;
                return;
            }
            float lenSq = worldTarget.x * worldTarget.x + worldTarget.y * worldTarget.y + worldTarget.z * worldTarget.z;
            if (lenSq < 1e-6f) return;

            Start();
            _active = true;
            Run(worldTarget);
        }

        // ── Core logic (ported from PhantomSilent.cpp) ─────────────
        private static Matrix3x3 LookAtToMatrix(Vector3 cameraPosition, Vector3 targetPosition)
        {
            Vector3 forward = (targetPosition - cameraPosition).Normalize();
            Vector3 right = new Vector3 { x = 0, y = 1, z = 0 }.Cross(forward).Normalize();
            Vector3 up = forward.Cross(right);

            return new Matrix3x3
            {
                r00 = -right.x, r01 = up.x, r02 = -forward.x,
                r10 = right.y,  r11 = up.y,  r12 = -forward.y,
                r20 = -right.z, r21 = up.z,  r22 = -forward.z
            };
        }

        private static long PartPrimitive(long part)
        {
            if (part <= 0x1000) return 0;
            try { return SDKInstance.Mem.ReadPtr(part + Offsets.BasePart.Primitive); }
            catch { return 0; }
        }

        private static Vector3 GetPartPos(long part)
        {
            long prim = PartPrimitive(part);
            if (prim <= 0x1000) return new Vector3();
            try { return SDKInstance.Mem.Read<Vector3>(prim + Offsets.Primitive.Position); }
            catch { return new Vector3(); }
        }

        private static void SetRotation(long partOrCam, Matrix3x3 m, bool isPart)
        {
            if (partOrCam <= 0x1000) return;
            try
            {
                if (isPart)
                {
                    long prim = PartPrimitive(partOrCam);
                    if (prim <= 0x1000) return;
                    SDKInstance.Mem.Write(prim + Offsets.Primitive.Rotation, m);
                }
                else
                {
                    SDKInstance.Mem.Write(partOrCam + Offsets.Camera.Rotation, m);
                }
            }
            catch { }
        }

        private static void Run(Vector3 targetpos)
        {
            try
            {
                var ws = Storage.WorkspaceInstance;
                if (!ws.IsValid) return;

                // FindFirstChildOfClass("Camera")
                long cameraparent = 0;
                try
                {
                    var camChild = ws.FindFirstChildOfClass("Camera");
                    if (camChild.IsValid) cameraparent = camChild.Address;
                }
                catch { }
                if (cameraparent <= 0x1000)
                {
                    try { cameraparent = SDKInstance.Mem.ReadPtr(ws.Address + Offsets.Workspace.CurrentCamera); }
                    catch { }
                }
                if (cameraparent <= 0x1000) return;

                // camera = cameraparent.FindFirstChild("Part"); if 0 → cameraparent
                long camera = 0;
                bool cameraIsPart = false;
                try
                {
                    var camInst = new SDKInstance(cameraparent);
                    var partChild = camInst.FindFirstChild("Part");
                    if (partChild.IsValid)
                    {
                        camera = partChild.Address;
                        cameraIsPart = true;
                    }
                }
                catch { }
                if (camera <= 0x1000)
                {
                    camera = cameraparent;
                    cameraIsPart = false;
                }
                if (camera <= 0x1000) return;

                Vector3 pos = cameraIsPart
                    ? GetPartPos(camera)
                    : SDKInstance.Mem.Read<Vector3>(camera + Offsets.Camera.Position);

                Matrix3x3 targetmatrix = LookAtToMatrix(pos, targetpos);
                // Force r11 = 0.01f (as in source)
                targetmatrix.r11 = 0.01f;

                SetRotation(camera, targetmatrix, cameraIsPart);

                _lastMatrix = targetmatrix;
                _camAddr = camera;
                _camIsPart = cameraIsPart;
            }
            catch { }
        }

        private static void WriterLoop()
        {
            while (_writerRun)
            {
                try
                {
                    if (_active)
                    {
                        long addr = _camAddr;
                        bool isPart = _camIsPart;
                        Matrix3x3 m = _lastMatrix;
                        if (addr > 0x1000)
                        {
                            SetRotation(addr, m, isPart);
                            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                                SetRotation(addr, m, isPart);
                        }
                    }
                    bool lmb = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                    Thread.Sleep((_active && lmb) ? 1 : 10);
                }
                catch { Thread.Sleep(10); }
            }
        }

        // ── Main loop ──────────────────────────────────────────────
        private static void Loop()
        {
            var cache = new Dictionary<long, long>();
            while (_running)
            {
                try
                {
                    if (SDKInstance.Mem == null) { Thread.Sleep(50); continue; }
                    var s = Options.Settings.Silent;
                    bool key = s.SilentAimbotKey.IsPressed();
                    bool active = s.SilentAimbot && (s.AlwaysOn || key);
                    if (!IsActivePlace()) { if (_active) _active = false; Thread.Sleep(50); continue; }
                    if (!active) { if (_active) _active = false; Thread.Sleep(10); continue; }
                    if (!Storage.IsInitialized) { Thread.Sleep(50); continue; }
                    var target = FindTarget(s, cache);
                    if (target.address != 0)
                    {
                        var worldPos = GetBonePos(target, Settings.Aiming.TargetBone, cache);
                        SetActive(true, worldPos);
                    }
                    else
                        SetActive(false, new Vector3());
                }
                catch { }
                Thread.Sleep(5);
            }
            _active = false;
        }

        [DllImport("user32.dll", EntryPoint = "GetCursorPos")]
        private static extern bool get_pos(out WorldToScreenHelper.POINT p);

        private static RobloxPlayer FindTarget(Options.Silent s, Dictionary<long, long> cache)
        {
            RobloxPlayer best = default;
            float closest = float.MaxValue;
            get_pos(out var mouse);
            var lp = Storage.LocalPlayerInstance;
            if (!lp.IsValid) return best;
            var targets = playerobjects.CachedPlayerObjects;
            if (targets == null) return best;
            foreach (var p in targets)
            {
                if (p.address == 0 || p.address == lp.Address || p.Health <= 0) continue;
                if (Settings.Checks.TeamCheck && TeamCheck.isteammate(p)) continue;
                if (Settings.Checks.DownedCheck && DownedCheck.is_downed(p)) continue;
                if (Settings.Checks.TransparencyCheck && TransparencyCheck.is_clear(p)) continue;
                var pred = GetPred(p, s, cache);
                var screen = WorldToScreenHelper.WorldToScreen(pred);
                if (screen.x == -1) continue;
                float dist = (float)Math.Sqrt(Math.Pow(screen.x - mouse.x, 2) + Math.Pow(screen.y - mouse.y, 2));
                if (dist < closest && dist <= s.SFOV) { closest = dist; best = p; }
            }
            return best;
        }

        private static Vector3 GetBonePos(RobloxPlayer p, int id, Dictionary<long, long> cache)
        {
            SDKInstance part = new SDKInstance(0);
            if (id == 0 && p.Head.IsValid) part = p.Head;
            else if (p.HumanoidRootPart.IsValid) part = p.HumanoidRootPart;
            else if (p.Head.IsValid) part = p.Head;
            if (!part.IsValid && p.Head.IsValid) part = p.Head;
            return GetXyz(part, cache);
        }

        private static Vector3 GetXyz(SDKInstance p, Dictionary<long, long> cache)
        {
            if (!p.IsValid) return new Vector3();
            if (!cache.TryGetValue(p.Address, out long ptr))
            {
                ptr = SDKInstance.Mem.ReadPtr(p.Address + Offsets.BasePart.Primitive);
                if (ptr != 0) cache[p.Address] = ptr;
            }
            return ptr != 0 ? SDKInstance.Mem.Read<Vector3>(ptr + Offsets.Primitive.Position) : new Vector3();
        }

        private static Vector3 GetPred(RobloxPlayer p, Options.Silent s, Dictionary<long, long> cache)
        {
            var pos = GetBonePos(p, Settings.Aiming.TargetBone, cache);
            if (s.SPrediction)
            {
                var root = p.HumanoidRootPart.IsValid ? p.HumanoidRootPart : p.Head;
                long prim = SDKInstance.Mem.ReadPtr(root.Address + Offsets.BasePart.Primitive);
                if (prim != 0)
                {
                    var vel = SDKInstance.Mem.Read<Vector3>(prim + Offsets.Primitive.AssemblyLinearVelocity);
                    float px = s.PredictionX != 0 ? (2.1f - s.PredictionX) : 0.0f;
                    float py = s.PredictionY != 0 ? (2.1f - s.PredictionY) : 0.0f;
                    pos.x += vel.x * px; pos.y += vel.y * py; pos.z += vel.z * px;
                }
            }
            return pos;
        }
    }
}
