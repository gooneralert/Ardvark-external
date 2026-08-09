using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using FoulzExternal.SDK;
using FoulzExternal.SDK.caches;
using FoulzExternal.storage;
using Offsets;
using Options;
using SDKInstance = FoulzExternal.SDK.Instance;
using Point = System.Windows.Point;
using FoulzExternal.SDK.structures;
using FoulzExternal.features.games.universal.checks.teamcheck;
using FoulzExternal.features.games.universal.checks.downedcheck;
using FoulzExternal.features.games.universal.checks.transparencycheck;

// ────────────────────────────────────────────────────────────────────────────
// Aimbot (non-silent) — ported from the C++ external module.
// Supports BOTH camera aim (write camera CFrame) and mouse aim (SendInput),
// the easing/smoothing family, closest-to-cursor / cursor-point aiming parts,
// prediction, DPI-aware sensitivity, and all the target checks from the module.
// ────────────────────────────────────────────────────────────────────────────

namespace FoulzExternal.games.universal.aiming
{
    public static class aiming
    {
        [DllImport("user32.dll", EntryPoint = "GetCursorPos")] private static extern bool get_pos(out POINT p);
        [DllImport("user32.dll", EntryPoint = "ScreenToClient")] private static extern bool screen_to_client(IntPtr h, ref POINT p);
        [DllImport("user32.dll", EntryPoint = "FindWindowW")] private static extern IntPtr find_window(string? c, string? n);
        [DllImport("user32.dll", EntryPoint = "GetDC")] private static extern IntPtr get_dc(IntPtr h);
        [DllImport("user32.dll", EntryPoint = "ReleaseDC")] private static extern int release_dc(IntPtr h, IntPtr hdc);
        [DllImport("user32.dll", EntryPoint = "GetDeviceCaps")] private static extern int get_device_caps(IntPtr hdc, int index);
        [DllImport("user32.dll", EntryPoint = "SendInput")] private static extern uint send_input(uint c, INPUT[] i, int s);
        [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void mouse_go(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

        private const int LOGPIXELSX = 88;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT
        {
            public int dx; public int dy; public uint mouseData; public uint dwFlags;
            public uint time; public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)] private struct INPUT
        {
            public uint type; public MOUSEINPUT mi;
        }

        // ── public API (unchanged so the UI / overlay keeps working) ─────
        private static bool vibing = false;
        private static Thread? brain;
        private static readonly object safety = new();
        private static Scene view = new();
        private static RobloxPlayer locked;
        private static RobloxPlayer current;
        private static bool is_on = false;
        private static bool old_key = false;
        private static bool was_active = false;

        // target-state (mirrors the module's found_target / last_closest_point)
        private static bool found_target = false;
        private static Vector3 last_closest_point = new Vector3();
        private static bool has_last_closest_point = false;

        // fractional mouse accumulator so sub-pixel movement isn't lost
        private static float acc_x = 0f;
        private static float acc_y = 0f;

        public struct FOVCircle { public Point center; public float radius; public Color outline; public Color fillColor; public int type; public bool fill; }
        public class Scene { public List<FOVCircle> circles = new(); }

        public static void Start()
        {
            if (vibing) return;
            vibing = true;
            brain = new Thread(go_crazy) { IsBackground = true };
            brain.Start();
        }

        public static void Stop() => vibing = false;

        public static Scene GetSceneSnapshot()
        {
            lock (safety) return new Scene { circles = new List<FOVCircle>(view.circles) };
        }
// ── MouseSettings / DPI (from the module) ─────────────────────────
        private class MouseSettings
        {
            public float base_dpi = 800.0f;
            public float current_dpi = 800.0f;
            public float dpi_scale_factor = 1.0f;
            public bool dpi_auto_detected = false;

            public void update_dpi_scale() => dpi_scale_factor = base_dpi / current_dpi;
            public float get_dpi_adjusted_sensitivity() => dpi_scale_factor;
        }

        private static readonly MouseSettings mouse_settings = new MouseSettings();

        private static void detect_mouse_dpi()
        {
            try
            {
                IntPtr hwnd = find_window(null, "Roblox");
                if (hwnd == IntPtr.Zero) return;

                IntPtr hdc = get_dc(hwnd);
                if (hdc == IntPtr.Zero) return;
                int dpi_x = get_device_caps(hdc, LOGPIXELSX);
                release_dc(hwnd, hdc);

                if (dpi_x <= 0) return;
                float estimated_dpi = 800.0f * (dpi_x / 96.0f);
                mouse_settings.current_dpi = Math.Max(400.0f, Math.Min(3200.0f, estimated_dpi));
                mouse_settings.update_dpi_scale();
                mouse_settings.dpi_auto_detected = true;
            }
            catch { }
        }

        // ── vector helpers ─────────────────────────────────────────────────
        private static Vector3 LerpV3(Vector3 a, Vector3 b, float t)
            => new Vector3 { x = a.x + (b.x - a.x) * t, y = a.y + (b.y - a.y) * t, z = a.z + (b.z - a.z) * t };

        private static Vector3 AddV3(Vector3 a, Vector3 b)
            => new Vector3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };

        private static Vector3 DivV3(Vector3 a, Vector3 b)
            => new Vector3 { x = a.x / b.x, y = a.y / b.y, z = a.z / b.z };

        private static Vector3 NormalizeDev(Vector3 v)
        {
            float len = (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return len != 0 ? new Vector3 { x = v.x / len, y = v.y / len, z = v.z / len } : v;
        }

        // ── easing family (from the module) ────────────────────────────────
        private static float applyEasing(float t, Aiming s)
        {
            t = Math.Max(0.0f, Math.Min(1.0f, t));
            float result = t;
            int style = s.SmoothingStyle;

            switch (style)
            {
                case 1:
                    {
                        float speed = Math.Max(0.10f, Math.Min(5.00f, s.LinearSpeed));
                        result = Math.Max(0.0f, Math.Min(1.0f, t * speed));
                    }
                    break;
                case 2:
                    {
                        float power = Math.Max(1.0f, Math.Min(5.0f, s.QuadPower));
                        result = (float)Math.Pow(t, power);
                    }
                    break;
                case 3:
                    {
                        float power = Math.Max(1.0f, Math.Min(5.0f, s.QuadPower));
                        result = 1.0f - (float)Math.Pow(1.0f - t, power);
                    }
                    break;
                case 4:
                    {
                        float power = Math.Max(1.0f, Math.Min(5.0f, s.QuadPower));
                        if (t < 0.5f) result = (float)Math.Pow(2.0f * t, power) / 2.0f;
                        else result = 1.0f - (float)Math.Pow(-2.0f * t + 2.0f, power) / 2.0f;
                    }
                    break;
                case 5:
                    {
                        float sharp = Math.Max(1.0f, Math.Min(5.0f, s.CubicSharpness));
                        result = (float)Math.Pow(t, sharp);
                    }
                    break;
                case 6:
                    {
                        float sharp = Math.Max(1.0f, Math.Min(5.0f, s.CubicSharpness));
                        result = 1.0f - (float)Math.Pow(1.0f - t, sharp);
                    }
                    break;
                case 7:
                    {
                        float sharp = Math.Max(1.0f, Math.Min(5.0f, s.CubicSharpness));
                        if (t < 0.5f) result = (float)Math.Pow(2.0f * t, sharp) / 2.0f;
                        else result = 1.0f - (float)Math.Pow(-2.0f * t + 2.0f, sharp) / 2.0f;
                    }
                    break;
                case 8:
                    {
                        float mult = Math.Max(0.5f, Math.Min(3.0f, s.SineMultiplier));
                        result = 1.0f - (float)Math.Cos((t * 3.14159265f * mult) / 2.0f);
                    }
                    break;
                case 9:
                    {
                        float mult = Math.Max(0.5f, Math.Min(3.0f, s.SineMultiplier));
                        result = (float)Math.Sin((t * 3.14159265f * mult) / 2.0f);
                    }
                    break;
                case 10:
                    {
                        float mult = Math.Max(0.5f, Math.Min(3.0f, s.SineMultiplier));
                        result = -((float)Math.Cos(3.14159265f * t * mult) - 1.0f) / 2.0f;
                    }
                    break;
                default:
                    result = t;
                    break;
            }

            if (float.IsNaN(result) || float.IsInfinity(result)) result = t;
            return Math.Max(0.0f, Math.Min(1.0f, result));
        }
// ── part access helpers ───────────────────────────────────────────
        private static Vector3 get_xyz(SDKInstance p, Dictionary<long, long> cache)
        {
            if (!p.IsValid) return new Vector3();
            if (!cache.TryGetValue(p.Address, out long ptr))
            {
                ptr = SDKInstance.Mem.ReadPtr(p.Address + Offsets.BasePart.Primitive);
                if (ptr != 0) cache[p.Address] = ptr;
            }
            return ptr != 0 ? SDKInstance.Mem.Read<Vector3>(ptr + Offsets.Primitive.Position) : new Vector3();
        }

        private static Vector3 get_velocity(SDKInstance p)
        {
            if (!p.IsValid || SDKInstance.Mem == null) return new Vector3();
            try
            {
                long ptr = SDKInstance.Mem.ReadPtr(p.Address + Offsets.BasePart.Primitive);
                return ptr != 0 ? SDKInstance.Mem.Read<Vector3>(ptr + Offsets.Primitive.AssemblyLinearVelocity) : new Vector3();
            }
            catch { return new Vector3(); }
        }

        private static bool is_valid_part(SDKInstance p) => p.IsValid && SDKInstance.Mem != null;

        private static Dictionary<long, long> fresh_cache() => new Dictionary<long, long>();

        private struct SDD_PART { public SDKInstance inst; public string name; }

        // pick the part of a locked player for the configured AimPart
        private static SDD_PART get_aim_part(RobloxPlayer p, int aimpart, POINT cursor)
        {
            var head = p.Head;
            switch (aimpart)
            {
                case 1:
                    if (is_valid_part(p.Upper_Torso)) return new SDD_PART { inst = p.Upper_Torso, name = "UpperTorso" };
                    if (is_valid_part(p.Torso)) return new SDD_PART { inst = p.Torso, name = "Torso" };
                    break;
                case 2:
                    if (is_valid_part(p.Lower_Torso)) return new SDD_PART { inst = p.Lower_Torso, name = "LowerTorso" };
                    if (is_valid_part(p.Torso)) return new SDD_PART { inst = p.Torso, name = "Torso" };
                    break;
                case 3:
                    if (is_valid_part(p.HumanoidRootPart)) return new SDD_PART { inst = p.HumanoidRootPart, name = "HumanoidRootPart" };
                    break;
                case 4:
                    return new SDD_PART { inst = get_closest_part_to_cursor(p, cursor), name = "Closest" };
                case 5:
                    return new SDD_PART { inst = get_closest_part_to_cursor(p, cursor), name = "CursorPoint" };
                case 6:
                    if (is_valid_part(p.Left_Lower_Leg)) return new SDD_PART { inst = p.Left_Lower_Leg, name = "Calf" };
                    if (is_valid_part(p.Right_Lower_Leg)) return new SDD_PART { inst = p.Right_Lower_Leg, name = "Calf" };
                    if (is_valid_part(p.Left_Leg)) return new SDD_PART { inst = p.Left_Leg, name = "Calf" };
                    if (is_valid_part(p.Right_Leg)) return new SDD_PART { inst = p.Right_Leg, name = "Calf" };
                    break;
                default:
                    break;
            }
            return new SDD_PART { inst = head, name = "Head" };
        }

        // find the body part nearest the cursor (module: get_closest_part_to_cursor_aimbot)
        private static SDKInstance get_closest_part_to_cursor(RobloxPlayer p, POINT cursor)
        {
            var parts = new List<(SDKInstance part, string name)>
            {
                (p.Head, "Head"),
                (p.Upper_Torso.IsValid ? p.Upper_Torso : p.Torso, "UpperTorso"),
                (p.Lower_Torso.IsValid ? p.Lower_Torso : p.Torso, "LowerTorso"),
                (p.HumanoidRootPart, "HumanoidRootPart"),
                (p.Left_Upper_Arm.IsValid ? p.Left_Upper_Arm : p.Left_Arm, "LeftUpperArm"),
                (p.Right_Upper_Arm.IsValid ? p.Right_Upper_Arm : p.Right_Arm, "RightUpperArm"),
                (p.Left_Lower_Arm.IsValid ? p.Left_Lower_Arm : p.Left_Arm, "LeftLowerArm"),
                (p.Right_Lower_Arm.IsValid ? p.Right_Lower_Arm : p.Right_Arm, "RightLowerArm"),
                (p.Left_Hand, "LeftHand"),
                (p.Right_Hand, "RightHand"),
                (p.Left_Upper_Leg.IsValid ? p.Left_Upper_Leg : p.Left_Leg, "LeftUpperLeg"),
                (p.Right_Upper_Leg.IsValid ? p.Right_Upper_Leg : p.Right_Leg, "RightUpperLeg"),
                (p.Left_Lower_Leg.IsValid ? p.Left_Lower_Leg : p.Left_Leg, "LeftLowerLeg"),
                (p.Right_Lower_Leg.IsValid ? p.Right_Lower_Leg : p.Right_Leg, "RightLowerLeg"),
                (p.Left_Foot, "LeftFoot"),
                (p.Right_Foot, "RightFoot")
            };

            SDKInstance best = p.Head;
            float bestDist = float.MaxValue;
            var cache = fresh_cache();
            foreach (var (part, _) in parts)
            {
                if (!is_valid_part(part)) continue;
                Vector2 sc = FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.WorldToScreen(get_xyz(part, cache));
                if (sc.x == -1.0f || sc.y == -1.0f) continue;
                float dx = cursor.x - sc.x;
                float dy = cursor.y - sc.y;
                float d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; best = part; }
            }
            return best;
        }
// smooth a target point toward the cursor across the part's volume (module: get_closest_point_on_part_aimbot)
        private static Vector3 get_closest_point_on_part(SDKInstance part, POINT cursor)
        {
            Vector3 part_pos = get_xyz(part, fresh_cache());
            const float radius = 1.0f;

            Vector2 sc = FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.WorldToScreen(part_pos);
            if (sc.x == -1.0f || sc.y == -1.0f) return part_pos;

            float offsetX = Math.Max(-radius, Math.Min(radius, ((cursor.x - sc.x) / 50.0f) * radius * 0.95f));
            float offsetY = Math.Max(-radius, Math.Min(radius, ((cursor.y - sc.y) / 50.0f) * radius * 0.95f));
            float zOff = (((float)(DateTime.UtcNow.Ticks % 1000) / 1000.0f) - 0.5f) * radius * 0.3f;

            Vector3 point = new Vector3 { x = part_pos.x + offsetX, y = part_pos.y + offsetY, z = part_pos.z + zOff };

            if (has_last_closest_point)
                point = LerpV3(last_closest_point, point, 0.2f);
            last_closest_point = point;
            has_last_closest_point = true;

            return point;
        }

        // prediction (module: velocity / predictionaxis, added to target pos)
        private static Vector3 get_predicted(SDKInstance target_part, Vector3 target_pos, Aiming s)
        {
            if (!s.Prediction) return target_pos;

            float px = s.PredictionX != 0 ? (2.1f - s.PredictionX) : 0.0f;
            float py = s.PredictionY != 0 ? (2.1f - s.PredictionY) : 0.0f;
            if (px == 0 && py == 0) return target_pos;

            Vector3 vel = get_velocity(target_part);
            Vector3 pred = new Vector3
            {
                x = target_pos.x + vel.x * px,
                y = target_pos.y + vel.y * py,
                z = target_pos.z + vel.z * px
            };
            return pred;
        }
// ── camera aimbot (module: perform_camera_aimbot) ──────────────────
        private static void perform_camera_aim(Vector3 target, Aiming s)
        {
            try
            {
                long cam = Storage.CameraInstance.Address;
                if (cam == 0 || SDKInstance.Mem == null) return;

                if (!float.IsFinite(target.x) || !float.IsFinite(target.y) || !float.IsFinite(target.z)) return;

                var curRot = SDKInstance.Mem.Read<Matrix3x3>(cam + Offsets.Camera.Rotation);
                var camPos = SDKInstance.Mem.Read<Vector3>(cam + Offsets.Camera.Position);

                var lookAt = sCFrame.LookAt(camPos, target, new Vector3 { x = 0, y = 1, z = 0 });
                Matrix3x3 targetMat = new Matrix3x3
                {
                    r00 = lookAt.r00, r01 = lookAt.r01, r02 = lookAt.r02,
                    r10 = lookAt.r10, r11 = lookAt.r11, r12 = lookAt.r12,
                    r20 = lookAt.r20, r21 = lookAt.r21, r22 = lookAt.r22
                };

                // camera shake (from the module)
                if (s.CamlockShake)
                {
                    var rnd = new Random(Environment.TickCount & 0x7fffffff);
                    float ox = (float)(rnd.NextDouble() * 2.0 - 1.0) * s.CamlockShakeX * 0.01f;
                    float oy = (float)(rnd.NextDouble() * 2.0 - 1.0) * s.CamlockShakeY * 0.01f;
                    targetMat.r00 += ox; targetMat.r01 += oy;
                    targetMat.r10 += oy; // keep it tiny so the matrix stays sane
                }

                if (!float.IsFinite(targetMat.r00) || float.IsNaN(targetMat.r00)) return;

                Vector4 curQ = Vector4.FromMatrix(curRot);
                Vector4 tarQ = Vector4.FromMatrix(targetMat);
                float t = 1.0f;

                if (s.Smoothness)
                {
                    float sx = Math.Max(0.0f, Math.Min(0.99f, s.SmoothnessX));
                    float sy = Math.Max(0.0f, Math.Min(0.99f, s.SmoothnessY));
                    float slow = (sx + sy) * 0.5f;
                    float eased = applyEasing(Math.Max(0.01f, 1.0f - slow), s);
                    t = eased;
                    if (t < 0.01f) t = 0.01f;
                }

                var slerped = Vector4.Slerp(curQ, tarQ, t).ToMatrix();

                SDKInstance.Mem.Write(cam + Offsets.Camera.Position, camPos);
                SDKInstance.Mem.Write(cam + Offsets.Camera.Rotation, slerped);

                long camPrim = SDKInstance.Mem.ReadPtr(cam + Offsets.BasePart.Primitive);
                if (camPrim != 0)
                {
                    SDKInstance.Mem.Write(camPrim + Offsets.Primitive.Position, camPos);
                    SDKInstance.Mem.Write(camPrim + Offsets.Primitive.Rotation, slerped);
                }
            }
            catch { }
        }

        // ── mouse aimbot (module: perform_mouse_aimbot) ───────────────────
        // NOTE: this external's WorldToScreen returns ABSOLUTE screen coords
        // (window client-origin offset added), so we compare against GetCursorPos.
        private static void perform_mouse_aim(Vector2 screen, IntPtr window, Aiming s)
        {
            if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y)) return;
            if (window == IntPtr.Zero) return;

            POINT cursor;
            if (!get_pos(out cursor)) return;

            float deltaX = screen.x - cursor.x;
            float deltaY = screen.y - cursor.y;

            // proven movement path (mouse_event + fractional accumulator, same as the
            // original external). Roblox can ignore SendInput relative moves, so we
            // use mouse_event which this codebase is verified against.
            float smooth = s.Smoothness ? s.SmoothnessY : 0.05f;
            float t = Math.Clamp(1.0f - smooth, 0.01f, 1.0f);

            float sens = 1.0f;
            try { sens = SDKInstance.Mem.Read<float>(SDKInstance.Mem.Base + Offsets.MouseService.SensitivityPointer); } catch { }
            if (sens <= 0.0f) sens = 1.0f;

            float scale = s.Sensitivity / (sens + 0.2f);

            // easing + DPI from the module, blended into the per-frame factor
            float dpi = mouse_settings.get_dpi_adjusted_sensitivity();
            if (!float.IsFinite(dpi) || dpi <= 0.0f) dpi = 1.0f;
            float eased = applyEasing(t, s) * dpi;

            const float speed = 0.2f;
            acc_x += deltaX * eased * scale * speed;
            acc_y += deltaY * eased * scale * speed;

            if (Math.Abs(deltaX) < 1.0f && Math.Abs(deltaY) < 1.0f) { acc_x = 0; acc_y = 0; return; }

            int mx = (int)acc_x;
            int my = (int)acc_y;
            acc_x -= mx;
            acc_y -= my;

            if (mx != 0 || my != 0) mouse_go(MOUSEEVENTF_MOVE, mx, my, 0, IntPtr.Zero);
        }

        // ── target picking (module: get_target_closest_to_mouse/camera) ────
        private static bool is_dead_or_knocked(RobloxPlayer p, Aiming s)
        {
            if (p.Health <= 0) return true;
            if (s.KnockCheck && Settings.Checks.DownedCheck && DownedCheck.is_downed(p)) return true;
            return false;
        }

        private static RobloxPlayer find_victim(Aiming s, POINT cursor, bool useCamera, Dictionary<long, long> cache)
        {
            RobloxPlayer best = default;
            float closest = float.MaxValue;

            var lp = Storage.LocalPlayerInstance;
            if (!lp.IsValid || SDKInstance.Mem == null) return best;

            var targets = playerobjects.CachedPlayerObjects;
            if (targets == null) return best;

            Vector3 camPos = new Vector3();
            try { camPos = SDKInstance.Mem.Read<Vector3>(Storage.CameraInstance.Address + Offsets.Camera.Position); } catch { }

            // FOV centre: cursor (mouse method / fov anchored to cursor) or screen centre (camera method)
            float cx = cursor.x;
            float cy = cursor.y;
            if (useCamera || s.FOVType == 1)
            {
                try
                {
                    if (FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.GetWindowInfo(out var size, out var p))
                    {
                        cx = p.x + size.x / 2.0f;
                        cy = p.y + size.y / 2.0f;
                    }
                }
                catch { }
            }
            float fovSq = s.FOV * s.FOV;
            float range = s.Range;

            foreach (var p in targets)
            {
                if (p.address == 0 || p.address == lp.Address || p.Health <= 0) continue;
                if (Settings.Checks.TeamCheck && TeamCheck.isteammate(p)) continue;
                if (Settings.Checks.DownedCheck && DownedCheck.is_downed(p)) continue;
                if (Settings.Checks.TransparencyCheck && TransparencyCheck.is_clear(p)) continue;
                if (s.HealthCheck && p.Health <= s.HealthThreshold) continue;
                if (s.KnockCheck && Settings.Checks.DownedCheck && DownedCheck.is_downed(p)) continue;

                SDD_PART part = get_aim_part(p, s.AimPart, cursor);
                if (!is_valid_part(part.inst) && !p.Head.IsValid) continue;
                Vector3 worldPos = is_valid_part(part.inst) ? get_xyz(part.inst, cache) : get_xyz(p.Head, cache);

                // range check (world distance from camera)
                if (s.RangeCheck)
                {
                    float dx = worldPos.x - camPos.x, dy = worldPos.y - camPos.y, dz = worldPos.z - camPos.z;
                    if ((dx * dx + dy * dy + dz * dz) > (range * range)) continue;
                }

                Vector2 screenPos = FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.WorldToScreen(worldPos);
                if (screenPos.x == -1.0f || screenPos.y == -1.0f) continue;

                float ddx = cx - screenPos.x;
                float ddy = cy - screenPos.y;
                float distSq = ddx * ddx + ddy * ddy;

                if (s.UseFOV && distSq > fovSq) continue;

                if (distSq < closest)
                {
                    closest = distSq;
                    best = p;
                }
            }

            return best;
        }
// ── main loop (module: hooks::aimbot) ──────────────────────────────
        private static void go_crazy()
        {
            IntPtr window = IntPtr.Zero;
            var cache = new Dictionary<long, long>();

            while (vibing)
            {
                try
                {
                    if (SDKInstance.Mem == null) { Thread.Sleep(50); continue; }

                    if (window == IntPtr.Zero) window = find_window(null, "Roblox");
                    if (window == IntPtr.Zero) { Thread.Sleep(200); continue; }

                    if (!mouse_settings.dpi_auto_detected) detect_mouse_dpi();

                    var s = Settings.Aiming;
                    bool key = s.AimbotKey.IsPressed();
                    if (s.ToggleType == 1 && key && !old_key) is_on = !is_on;
                    old_key = key;

                    // if no key is bound, the "aimbot" checkbox alone enables it
                    bool hasBind = s.AimbotKey.Key > 0 || s.AimbotKey.MouseButton >= 0 || s.AimbotKey.ControllerButton >= 0;
                    bool active = s.Aimbot && (hasBind ? (s.ToggleType == 1 ? is_on : key) : true);

                    get_pos(out var cursor);
                    var next = new Scene();

                    Color outl = Colors.White;
                    Color fill = Color.FromArgb(128, 255, 255, 255);
                    if (s.AnimatedFOV)
                    {
                        float t = (float)Environment.TickCount / 1000f;
                        outl = get_rainbow(t);
                        fill = Color.FromArgb(50, outl.R, outl.G, outl.B);
                    }
                    if (s.ShowFOV)
                        next.circles.Add(new FOVCircle { center = new Point(cursor.x, cursor.y), radius = s.FOV, outline = outl, fillColor = fill, fill = s.FillFOV });

                    if (!active)
                    {
                        locked = default; current = default; was_active = false;
                        found_target = false; has_last_closest_point = false;
                        lock (safety) view = next;
                        Thread.Sleep(10);
                        continue;
                    }

                    bool needsFresh = !was_active || !found_target || locked.address == 0 || locked.Health <= 0 ||
                        (Settings.Checks.DownedCheck && DownedCheck.is_downed(locked)) ||
                        (Settings.Checks.TransparencyCheck && TransparencyCheck.is_clear(locked));

                    if (s.Autoswitch || needsFresh)
                    {
                        locked = find_victim(s, cursor, s.AimingType == 0, cache);
                        found_target = locked.address != 0;
                    }

                    if (locked.address == 0)
                    {
                        current = default; was_active = false; found_target = false;
                        lock (safety) view = next;
                        Thread.Sleep(10);
                        continue;
                    }

                    // sticky aim keeps the current locked target until it dies/vanishes
                    if (s.StickyAim)
                    {
                        if (current.address == 0 || current.Health <= 0 ||
                            (Settings.Checks.DownedCheck && DownedCheck.is_downed(current)) ||
                            (Settings.Checks.TransparencyCheck && TransparencyCheck.is_clear(current)))
                            current = find_victim(s, cursor, s.AimingType == 0, cache);
                        if (current.address != 0) locked = current;
                    }

                    // unlock on death
                    if (s.UnlockOnDeath && is_dead_or_knocked(locked, s))
                    {
                        locked = default; current = default; was_active = false;
                        found_target = false; has_last_closest_point = false;
                        lock (safety) view = next;
                        Thread.Sleep(10);
                        continue;
                    }

                    SDD_PART ap = get_aim_part(locked, s.AimPart, cursor);
                    if (!is_valid_part(ap.inst) && !locked.Head.IsValid)
                    {
                        locked = default; current = default; was_active = false;
                        found_target = false; has_last_closest_point = false;
                        lock (safety) view = next;
                        Thread.Sleep(10);
                        continue;
                    }
Vector3 target_pos = s.AimPart == 5
                        ? get_closest_point_on_part(ap.inst, cursor)
                        : get_xyz(is_valid_part(ap.inst) ? ap.inst : locked.Head, cache);

                    Vector3 pred = get_predicted(is_valid_part(ap.inst) ? ap.inst : locked.Head, target_pos, s);

                    Vector2 screen = FoulzExternal.SDK.worldtoscreen.WorldToScreenHelper.WorldToScreen(pred);
                    if (screen.x == -1.0f || screen.y == -1.0f)
                    {
                        if (s.StickyAim && !s.Autoswitch)
                        {
                            lock (safety) view = next;
                            Thread.Sleep(20);
                            continue;
                        }
                        locked = default; current = default; was_active = false;
                        found_target = false; has_last_closest_point = false;
                        lock (safety) view = next;
                        Thread.Sleep(10);
                        continue;
                    }

                    if (s.AimingType == 0) perform_camera_aim(pred, s);
                    else if (s.AimingType == 1) perform_mouse_aim(screen, window, s);

                    was_active = true;
                    lock (safety) view = next;

                    // light pacing so we don't spin the CPU on a busy loop
                    Thread.Sleep(5);
                }
                catch { }
                Thread.Sleep(1);
            }
        }

        private static Color get_rainbow(float t)
        {
            byte r = (byte)((Math.Sin(t) * 0.5 + 0.5) * 255);
            byte g = (byte)((Math.Sin(t + 2.094) * 0.5 + 0.5) * 255);
            byte b = (byte)((Math.Sin(t + 4.188) * 0.5 + 0.5) * 255);
            return Color.FromRgb(r, g, b);
        }
    }
}
