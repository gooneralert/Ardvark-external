using FoulzExternal.SDK;
using FoulzExternal.SDK.caches;
using FoulzExternal.SDK.worldtoscreen;
using FoulzExternal.storage;
using ImGuiNET;
using Offsets;
using Options;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FoulzExternal.features.games.universal.checks.teamcheck;

// ── Type aliases ─────────────────────────────────────────────────────────────
// SDK world-space structs (from FoulzExternal.SDK.structures) live alongside
// System.Numerics (ImGui screen-space). Aliasing keeps them unambiguous.
using SVector2 = FoulzExternal.SDK.structures.Vector2;
using SVector3 = FoulzExternal.SDK.structures.Vector3;
using SMatrix4 = FoulzExternal.SDK.structures.Matrix4;
using SMatrix3x3 = FoulzExternal.SDK.structures.Matrix3x3;
using SDKInstance = FoulzExternal.SDK.Instance;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace FoulzExternal.games.universal.visuals
{
    // ────────────────────────────────────────────────────────────────────────
    //  Ported rendering model from jew-dick-hack C++ external:
    //  - No worker thread, no scene snapshots, no locks, no sleeps.
    //  - RenderImGui() runs inline on the ImGui render thread each frame and
    //    draws the ESP directly (box / skeleton / convex-hull chams) exactly
    //    like the C++ ESP::Render() inside Renderer::MainLoop().
    //  - The render loop is unthrottled (Present(0,0) equivalent), so there
    //    is nothing to cap the fps the way Thread.Sleep throttles did.
    // ────────────────────────────────────────────────────────────────────────
    public static class visuals
    {
        private const uint Black = 0xFF000000;
        private const uint White = 0xFFFFFFFF;
        private const uint Yellow = 0xFF00FFFF; // ABGR

        // OBB edge index pairs (C++ k_box_edges).
        private static readonly int[,] BoxEdges =
        {
            {0,1},{0,2},{0,4},{1,3},{1,5},{2,3},
            {2,6},{3,7},{4,5},{4,6},{5,7},{6,7}
        };

        private static uint rgba(float r, float g, float b, float a) =>
            ((uint)(a * 255) << 24) | ((uint)(b * 255) << 16) | ((uint)(g * 255) << 8) | (uint)(r * 255);

        // ── World → Screen (identical math to C++ ESP.cpp) ──────────────────
        private static bool WorldToScreen(SMatrix4 vmtx, SVector2 viewport, SVector3 pos, out SVector2 screen)
        {
            screen = default;
            float[] m = vmtx.data;
            float w = pos.x * m[12] + pos.y * m[13] + pos.z * m[14] + m[15];
            if (w < 0.01f) return false;

            float x = pos.x * m[0] + pos.y * m[1] + pos.z * m[2] + m[3];
            float y = pos.x * m[4] + pos.y * m[5] + pos.z * m[6] + m[7];

            float invw = 1.0f / w;
            x *= invw;
            y *= invw;

            screen.x = viewport.x / 2 + x * viewport.x / 2;
            screen.y = viewport.y / 2 - y * viewport.y / 2;
            return true;
        }

        private static float ClipW(SMatrix4 vmtx, SVector3 p)
        {
            float[] m = vmtx.data;
            return p.x * m[12] + p.y * m[13] + p.z * m[14] + m[15];
        }

        // ── Pixel-snapped box (C++ EspBox::SnapEspBox + DrawBox) ────────────
        private static void SnapBox(float minX, float minY, float maxX, float maxY,
            out float x1, out float y1, out float x2, out float y2)
        {
            x1 = (float)Math.Floor(minX);
            y1 = (float)Math.Floor(minY);
            x2 = (float)Math.Ceiling(maxX);
            y2 = (float)Math.Ceiling(maxY);
            if (x2 <= x1) x2 = x1 + 1f;
            if (y2 <= y1) y2 = y1 + 1f;
        }

        private static void DrawBox(ImDrawListPtr dl, float tlX, float tlY, float brX, float brY,
            uint color, float thick, bool outline)
        {
            SnapBox(tlX, tlY, brX, brY, out float x1, out float y1, out float x2, out float y2);
            if (thick < 0.5f) thick = 0.5f;

            if (outline)
            {
                if (thick <= 1.01f)
                {
                    dl.AddRect(new NVector2(x1 - 1, y1 - 1), new NVector2(x2 + 1, y2 + 1), Black, 0f, 0, 1f);
                    dl.AddRect(new NVector2(x1 + 1, y1 + 1), new NVector2(x2 - 1, y2 - 1), Black, 0f, 0, 1f);
                }
                else
                {
                    dl.AddRect(new NVector2(x1, y1), new NVector2(x2, y2), Black, 0f, 0, thick + 2f);
                }
            }
            dl.AddRect(new NVector2(x1, y1), new NVector2(x2, y2), color, 0f, 0, thick);
        }

        private static void DrawCornerBox(ImDrawListPtr dl, float tlX, float tlY, float brX, float brY,
            uint color, float thick, bool outline)
        {
            SnapBox(tlX, tlY, brX, brY, out float x1, out float y1, out float x2, out float y2);
            if (thick < 0.5f) thick = 0.5f;
            float lw = (float)Math.Floor((x2 - x1) * 0.25f);
            float lh = (float)Math.Floor((y2 - y1) * 0.25f);
            if (lw < 2f) lw = 2f;
            if (lh < 2f) lh = 2f;

            Span<NVector2> segs = stackalloc NVector2[16];
            segs[0] = new NVector2(x1, y1); segs[1] = new NVector2(x1 + lw, y1);
            segs[2] = new NVector2(x1, y1); segs[3] = new NVector2(x1, y1 + lh);
            segs[4] = new NVector2(x2 - lw, y1); segs[5] = new NVector2(x2, y1);
            segs[6] = new NVector2(x2, y1); segs[7] = new NVector2(x2, y1 + lh);
            segs[8] = new NVector2(x1, y2 - lh); segs[9] = new NVector2(x1, y2);
            segs[10] = new NVector2(x1, y2); segs[11] = new NVector2(x1 + lw, y2);
            segs[12] = new NVector2(x2, y2 - lh); segs[13] = new NVector2(x2, y2);
            segs[14] = new NVector2(x2 - lw, y2); segs[15] = new NVector2(x2, y2);

            if (outline)
            {
                float ot = thick + 2f;
                for (int i = 0; i < 16; i += 2)
                    dl.AddLine(segs[i], segs[i + 1], Black, ot);
            }
            for (int i = 0; i < 16; i += 2)
                dl.AddLine(segs[i], segs[i + 1], color, thick);
        }

        private static void DrawSkeletonLine(ImDrawListPtr dl, NVector2 a, NVector2 b, uint color, float thick, bool outline)
        {
            if (thick < 1f) thick = 1f;
            if (outline)
                dl.AddLine(a, b, Black, thick + 2f);
            dl.AddLine(a, b, color, thick);
        }

        // ── Convex hull (C++ ConvexHull) ────────────────────────────────────
        private static List<NVector2> ConvexHull(List<NVector2> pts)
        {
            if (pts.Count < 3) return pts;

            pts.Sort((a, b) => a.X < b.X ? -1 : (a.X > b.X ? 1 : a.Y.CompareTo(b.Y)));

            static float Cross(NVector2 o, NVector2 a, NVector2 b) =>
                (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

            var hull = new List<NVector2>(pts.Count * 2);
            int k = 0;
            for (int i = 0; i < pts.Count; ++i)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], pts[i]) <= 0f) k--;
                if (k < hull.Count) hull[k] = pts[i]; else hull.Add(pts[i]);
                k++;
            }
            for (int i = pts.Count - 2, t = k + 1; i >= 0; --i)
            {
                while (k >= t && Cross(hull[k - 2], hull[k - 1], pts[i]) <= 0f) k--;
                if (k < hull.Count) hull[k] = pts[i]; else hull.Add(pts[i]);
                k++;
            }
            if (k > 0) hull.RemoveRange(k - 1, hull.Count - (k - 1));
            else hull.Clear();
            return hull;
        }

        // ── Segment-poly inside test (C++ SegInsidePoly) ────────────────────
        private static bool SegInsidePoly(NVector2 a, NVector2 b, List<NVector2> poly, out float t0, out float t1)
        {
            t0 = 0f; t1 = 1f;
            int n = poly.Count;
            if (n < 3) return false;

            NVector2 c = NVector2.Zero;
            for (int i = 0; i < n; ++i) { c.X += poly[i].X; c.Y += poly[i].Y; }
            c.X /= n; c.Y /= n;

            const float eps = 0.25f;
            for (int i = 0; i < n; ++i)
            {
                NVector2 p = poly[i], q = poly[(i + 1) % n];
                float ex = q.X - p.X, ey = q.Y - p.Y;
                float side(NVector2 v) => ex * (v.Y - p.Y) - ey * (v.X - p.X);

                float s = -1f;
                if (side(c) >= 0f) s = 1f;

                float f0 = s * side(a);
                float f1 = s * side(b);
                float df = f1 - f0;

                if (MathF.Abs(df) < 1e-6f)
                {
                    if (f0 < -eps) return false;
                    continue;
                }

                float tc = (-eps - f0) / df;
                if (df > 0f) { if (tc > t0) t0 = tc; }
                else { if (tc < t1) t1 = tc; }

                if (t0 >= t1) return false;
            }

            t0 = MathF.Max(0f, t0);
            t1 = MathF.Min(1f, t1);
            return t1 > t0;
        }

        // ── Clip half plane (C++ ClipHalfPlane) ─────────────────────────────
        private static List<NVector2> ClipHalfPlane(List<NVector2> poly, NVector2 p, NVector2 q, float s)
        {
            var outp = new List<NVector2>(poly.Count + 2);
            int n = poly.Count;
            if (n < 3) return outp;

            float F(NVector2 v) => s * ((q.X - p.X) * (v.Y - p.Y) - (q.Y - p.Y) * (v.X - p.X));

            for (int i = 0; i < n; ++i)
            {
                NVector2 a = poly[i], b = poly[(i + 1) % n];
                float fa = F(a), fb = F(b);
                if (fa >= 0f) outp.Add(a);
                if ((fa < 0f) != (fb < 0f))
                {
                    float t = fa / (fa - fb);
                    outp.Add(new NVector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
            }

            if (outp.Count < 3) outp.Clear();
            return outp;
        }

        // ── Subtract poly (C++ SubtractPoly) — cham pieces ──────────────────
        private static void SubtractPoly(List<NVector2> piece, List<NVector2> b, List<List<NVector2>> outp)
        {
            int n = b.Count;
            if (n < 3)
            {
                if (piece.Count >= 3) outp.Add(piece);
                return;
            }

            NVector2 c = NVector2.Zero;
            for (int i = 0; i < n; ++i) { c.X += b[i].X; c.Y += b[i].Y; }
            c.X /= n; c.Y /= n;

            for (int i = 0; i < n && piece.Count >= 3; ++i)
            {
                NVector2 p = b[i], q = b[(i + 1) % n];
                float cs = (q.X - p.X) * (c.Y - p.Y) - (q.Y - p.Y) * (c.X - p.X);
                float s = -1f;
                if (cs >= 0f) s = 1f;

                var outside = ClipHalfPlane(piece, p, q, -s);
                if (outside.Count >= 3) outp.Add(outside);
                piece = ClipHalfPlane(piece, p, q, s);
            }
        }

        // ── Outline segment outside part-union (C++ DrawSegmentOutsideUnion) ─
        private static void DrawSegmentOutsideUnion(ImDrawListPtr dl, NVector2 a, NVector2 b,
            List<List<NVector2>> polys, int skip, uint color)
        {
            var covered = new List<(float t0, float t1)>();
            for (int i = 0; i < polys.Count; ++i)
            {
                if (i == skip) continue;
                if (SegInsidePoly(a, b, polys[i], out float t0, out float t1))
                    covered.Add((t0, t1));
            }

            if (covered.Count == 0)
            {
                dl.AddLine(a, b, color, 1f);
                return;
            }

            covered.Sort((x, y) => x.t0.CompareTo(y.t0));

            const float minPiece = 0.002f;
            float cursor = 0f;
            foreach (var iv in covered)
            {
                if (iv.t0 > cursor + minPiece)
                {
                    var p0 = NVector2.Lerp(a, b, cursor);
                    var p1 = NVector2.Lerp(a, b, iv.t0);
                    dl.AddLine(p0, p1, color, 1f);
                }
                if (iv.t1 > cursor) cursor = iv.t1;
                if (cursor >= 1f) break;
            }

            if (cursor < 1f - minPiece)
            {
                var p0 = NVector2.Lerp(a, b, cursor);
                dl.AddLine(p0, b, color, 1f);
            }
        }

        // ── Part corner: 8 OBB corners + near-plane screen expand ───────────
        private static void ExpandFromObbCorners(SMatrix4 vm, SVector2 viewport,
            SVector3[] world, ref float minX, ref float maxX, ref float minY, ref float maxY,
            ref bool anyVisible)
        {
            Span<float> cw = stackalloc float[8];
            for (int i = 0; i < 8; ++i)
            {
                cw[i] = ClipW(vm, world[i]);
                if (cw[i] >= 0.01f && WorldToScreen(vm, viewport, world[i], out SVector2 sp))
                {
                    minX = MathF.Min(minX, sp.x); maxX = MathF.Max(maxX, sp.x);
                    minY = MathF.Min(minY, sp.y); maxY = MathF.Max(maxY, sp.y);
                    anyVisible = true;
                }
            }

            Span<(int a, int b)> edges = stackalloc (int, int)[]
            {
                (0,1),(1,3),(3,2),(2,0),
                (4,5),(5,7),(7,6),(6,4),
                (0,4),(1,5),(2,6),(3,7)
            };
            const float near = 0.01f;
            for (int e = 0; e < 12; ++e)
            {
                int a = edges[e].a, b = edges[e].b;
                bool aIn = cw[a] >= near, bIn = cw[b] >= near;
                if (aIn == bIn) continue;
                float t = (near - cw[a]) / (cw[b] - cw[a]);
                if (t < 0f || t > 1f) continue;
                var p = new SVector3
                {
                    x = world[a].x + (world[b].x - world[a].x) * t,
                    y = world[a].y + (world[b].y - world[a].y) * t,
                    z = world[a].z + (world[b].z - world[a].z) * t,
                };
                if (WorldToScreen(vm, viewport, p, out SVector2 sp))
                {
                    minX = MathF.Min(minX, sp.x); maxX = MathF.Max(maxX, sp.x);
                    minY = MathF.Min(minY, sp.y); maxY = MathF.Max(maxY, sp.y);
                    anyVisible = true;
                }
            }
        }

        // ── Position/size/rotation reads (memory) ───────────────────────────
        // Read fresh every frame like the C++ external does (BasePart::GetPos/
        // GetSize/GetRotation per player per frame). No persistent caching —
        // caching caused the ESP to freeze in time at the first-read position.
        private static void ReadPartData(SDKInstance i, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot)
        {
            pos = default; size = default; rot = default;
            if (!i.IsValid) return;
            try
            {
                long ptr = SDKInstance.Mem.ReadPtr(i.Address + Offsets.BasePart.Primitive);
                if (ptr == 0) return;
                pos = SDKInstance.Mem.Read<SVector3>(ptr + Offsets.Primitive.Position);
                size = SDKInstance.Mem.Read<SVector3>(ptr + Offsets.Primitive.Size);
                rot = SDKInstance.Mem.Read<SMatrix3x3>(ptr + Offsets.Primitive.Rotation);
            }
            catch { }
        }

        private static SVector3 RotateVec(in SMatrix3x3 r, float lx, float ly, float lz) => new SVector3
        {
            x = r.r00 * lx + r.r01 * ly + r.r02 * lz,
            y = r.r10 * lx + r.r11 * ly + r.r12 * lz,
            z = r.r20 * lx + r.r21 * ly + r.r22 * lz,
        };

        // ── Main inline ESP render (called every frame from ImGui render) ────
        public static void RenderImGui(SMatrix4 vm, SVector2 viewport)
        {
            var dl = ImGui.GetBackgroundDrawList();
            var settings = Settings.Visuals;
            if (!settings.Enabled) return;

            var guys = playerobjects.CachedPlayerObjects;
            if (guys == null || guys.Count == 0) return;

            float overlayW = viewport.x;
            float overlayH = viewport.y;

            var lp = Storage.LocalPlayerInstance;

            foreach (var p in guys)
            {
                try
                {
                    if (p.address == 0) continue;
                    if (p.address == lp.Address && !settings.LocalPlayerESP) continue;
                    if (TeamCheck.isteammate(p)) continue;

                    if (p.IsPF && p.Bones != null && p.Bones.Count > 0)
                        RenderPFPlayer(dl, vm, viewport, p, settings);
                    else
                        RenderPlayer(dl, vm, viewport, p, settings);
                }
                catch { }
            }
        }

        // ── PF: box via bone OBBs + skeleton via bones + hull chams ────────
        private static void RenderPFPlayer(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            RobloxPlayer p, Options.Visuals settings)
        {
            float minX = 1e6f, maxX = -1e6f, minY = 1e6f, maxY = -1e6f;
            bool any = false;

            for (int i = 0; i < p.Bones.Count; ++i)
            {
                var bone = p.Bones[i];
                if (!bone.IsValid) continue;
                ReadPartData(bone, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot);
                if (pos.x == 0 && pos.y == 0 && pos.z == 0) continue;

                SVector3 half = new SVector3 { x = size.x / 2f, y = size.y / 2f, z = size.z / 2f };
                if (half.x < 0.01f && half.y < 0.01f && half.z < 0.01f)
                    half = new SVector3 { x = 0.5f, y = 0.5f, z = 0.5f };

                var world = new SVector3[8];
                int idx = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var off = RotateVec(in rot, half.x * sx, half.y * sy, half.z * sz);
                            world[idx++] = new SVector3 { x = pos.x + off.x, y = pos.y + off.y, z = pos.z + off.z };
                        }

                ExpandFromObbCorners(vm, viewport, world, ref minX, ref maxX, ref minY, ref maxY, ref any);
            }

            if (!any || minX > 5000f) return;
            SnapBox(minX, minY, maxX, maxY, out float bx1, out float by1, out float bx2, out float by2);

            uint boxColor = White;
            const float thick = 1f;
            const bool outline = true;

            if (settings.BoxESP)
            {
                if (settings.BoxMode == 1)
                    DrawCornerBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
                else
                    DrawBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
            }
            else if (settings.CornerESP)
            {
                DrawCornerBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
            }

            if (settings.Name && !string.IsNullOrEmpty(p.Name))
                DrawCenteredText(dl, p.Name, (bx1 + bx2) * 0.5f, by1 - 15f, 12f, White);

            if (settings.Skeleton)
            {
                if (p.Head.IsValid && p.HumanoidRootPart.IsValid)
                {
                    ReadPartData(p.Head, out var hp, out _, out _);
                    ReadPartData(p.HumanoidRootPart, out var hrp, out _, out _);
                    DrawBone(dl, vm, viewport, hp, hrp, thick, outline);
                }
                if (p.HumanoidRootPart.IsValid)
                {
                    ReadPartData(p.HumanoidRootPart, out var hrp, out _, out _);
                    foreach (var bone in p.Bones)
                    {
                        if (!bone.IsValid) continue;
                        if (bone.Address == p.HumanoidRootPart.Address) continue;
                        if (p.Head.IsValid && bone.Address == p.Head.Address) continue;
                        ReadPartData(bone, out var bp, out _, out _);
                        DrawBone(dl, vm, viewport, hrp, bp, thick, outline);
                    }
                }
            }

            if (settings.Chams)
                DrawPlayerChams(dl, vm, viewport, p.Bones, settings);
        }

        // ── Standard: OBB box + head/feet clamp + text + skeleton + chams ──
        private static void RenderPlayer(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            RobloxPlayer p, Options.Visuals settings)
        {
            Span<SDKInstance> parts = stackalloc SDKInstance[]
            {
                p.Head, p.HumanoidRootPart,
                p.Upper_Torso.IsValid ? p.Upper_Torso : p.Torso,
                p.Lower_Torso.IsValid ? p.Lower_Torso : p.HumanoidRootPart,
                p.Left_Arm, p.Right_Arm, p.Left_Leg, p.Right_Leg,
                p.Left_Foot, p.Right_Foot, p.Left_Hand, p.Right_Hand,
                p.Left_Upper_Arm, p.Right_Upper_Arm, p.Left_Upper_Leg, p.Right_Upper_Leg
            };

            float minX = 1e6f, maxX = -1e6f, minY = 1e6f, maxY = -1e6f;
            bool any = false;

            // Per-part world data for skeleton + clamping reuse.
            var partPositions = new Dictionary<long, SVector3>();
            var partSizes = new Dictionary<long, SVector3>();
            var partRots = new Dictionary<long, SMatrix3x3>();

            for (int i = 0; i < parts.Length; ++i)
            {
                var part = parts[i];
                if (!part.IsValid) continue;

                ReadPartData(part, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot);
                if (pos.x == 0 && pos.y == 0 && pos.z == 0) continue;

                partPositions[part.Address] = pos;
                partSizes[part.Address] = size;
                partRots[part.Address] = rot;

                SVector3 half = new SVector3 { x = size.x / 2f, y = size.y / 2f, z = size.z / 2f };
                if (half.x < 0.01f && half.y < 0.01f && half.z < 0.01f)
                    half = new SVector3 { x = 0.5f, y = 0.5f, z = 0.5f };

                // Skip HRP for the bounding box (inflates box at distance — C++).
                if (part.Address == p.HumanoidRootPart.Address) continue;

                var world = new SVector3[8];
                int idx = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var off = RotateVec(in rot, half.x * sx, half.y * sy, half.z * sz);
                            world[idx++] = new SVector3 { x = pos.x + off.x, y = pos.y + off.y, z = pos.z + off.z };
                        }

                ExpandFromObbCorners(vm, viewport, world, ref minX, ref maxX, ref minY, ref maxY, ref any);
            }

            if (!any || minX > 5000f) return;

            // Clamp to head apex / feet bottom like C++.
            float topY = minY, botY = maxY;
            if (p.Head.IsValid && partPositions.TryGetValue(p.Head.Address, out var hPos))
            {
                if (WorldToScreen(vm, viewport, new SVector3 { x = hPos.x, y = hPos.y + 0.5f, z = hPos.z }, out SVector2 hs))
                    topY = MathF.Min(topY, hs.y);
            }
            if (p.Left_Foot.IsValid && partPositions.TryGetValue(p.Left_Foot.Address, out var lfPos))
            {
                if (WorldToScreen(vm, viewport, new SVector3 { x = lfPos.x, y = lfPos.y - 0.5f, z = lfPos.z }, out SVector2 fs))
                    botY = MathF.Max(botY, fs.y);
            }
            if (p.Right_Foot.IsValid && partPositions.TryGetValue(p.Right_Foot.Address, out var rfPos))
            {
                if (WorldToScreen(vm, viewport, new SVector3 { x = rfPos.x, y = rfPos.y - 0.5f, z = rfPos.z }, out SVector2 fs))
                    botY = MathF.Max(botY, fs.y);
            }
            minY = MathF.Min(minY, topY);
            maxY = MathF.Max(maxY, botY);

            SnapBox(minX, minY, maxX, maxY, out float bx1, out float by1, out float bx2, out float by2);

            uint boxColor = White;
            uint skeletonColor = rgba(0.75f, 0.82f, 1f, 1f);
            const float thick = 1f;
            const bool outline = true;

            if (settings.BoxESP)
            {
                if (settings.BoxMode == 1)
                    DrawCornerBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
                else
                    DrawBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
            }
            else if (settings.CornerESP)
            {
                DrawCornerBox(dl, bx1, by1, bx2, by2, boxColor, thick, outline);
            }

            if (settings.Name && !string.IsNullOrEmpty(p.Name))
                DrawCenteredText(dl, p.Name, (bx1 + bx2) * 0.5f, by1 - 15f, 12f, White);

            if (settings.Health)
            {
                float frac = p.MaxHealth > 0f ? Math.Clamp(p.Health / p.MaxHealth, 0f, 1f) : 1f;
                uint hpCol = rgba(1f - frac, frac, 0f, 1f);
                dl.AddRectFilled(new NVector2(bx1 - 5f, by2), new NVector2(bx1 - 3f, by1), Black);
                if (frac > 0.001f)
                    dl.AddRectFilled(new NVector2(bx1 - 5f, by2 - (by2 - by1) * frac), new NVector2(bx1 - 3f, by2), hpCol);
            }

            if (settings.HealthText)
                DrawCenteredText(dl, $"{(int)p.Health}", bx1 - 18f, by1 + 2f, 10f, White);

            if (settings.Distance)
            {
                SVector3 hrpPos = default;
                if (p.HumanoidRootPart.IsValid) hrpPos = partPositions.TryGetValue(p.HumanoidRootPart.Address, out var hp) ? hp : default;
                else if (p.Head.IsValid) hrpPos = partPositions.TryGetValue(p.Head.Address, out var hp2) ? hp2 : default;
                float dist = Distance2D(localPos, hrpPos);
                DrawCenteredText(dl, $"{(int)dist}m", (bx1 + bx2) * 0.5f, by2 + 2f, 12f, White);
            }

            if (p.IsPF && !string.IsNullOrEmpty(p.ToolName))
                DrawCenteredText(dl, p.ToolName, (bx1 + bx2) * 0.5f, by2 + 14f, 12f, Yellow);

            // ── Skeleton (C++ skeleton logic — R6/R15) ──────────────────────
            if (settings.Skeleton)
            {
                SVector3 PartPos(SDKInstance inst, out bool ok)
                {
                    SVector3 v = default;
                    ok = inst.IsValid && partPositions.TryGetValue(inst.Address, out v);
                    return ok ? v : default;
                }

                void BoneLine(SVector3 a, SVector3 b)
                {
                    if (WorldToScreen(vm, viewport, a, out SVector2 sa) && WorldToScreen(vm, viewport, b, out SVector2 sb))
                        DrawSkeletonLine(dl, new NVector2(sa.x, sa.y), new NVector2(sb.x, sb.y), skeletonColor, thick, outline);
                }

                if (p.RigType == 1)
                {
                    var h = PartPos(p.Head, out bool haveHead);
                    var u = PartPos(p.Upper_Torso, out bool haveUT);
                    var l = PartPos(p.Lower_Torso, out bool haveLT);

                    if (haveHead && haveUT) BoneLine(h, u);
                    if (haveUT && haveLT) BoneLine(u, l);

                    BoneLine(u, PartPos(p.Left_Upper_Arm, out _));
                    BoneLine(PartPos(p.Left_Upper_Arm, out _), PartPos(p.Left_Hand, out _));
                    BoneLine(u, PartPos(p.Right_Upper_Arm, out _));
                    BoneLine(PartPos(p.Right_Upper_Arm, out _), PartPos(p.Right_Hand, out _));
                    BoneLine(l, PartPos(p.Left_Upper_Leg, out _));
                    BoneLine(PartPos(p.Left_Upper_Leg, out _), PartPos(p.Left_Foot, out _));
                    BoneLine(l, PartPos(p.Right_Upper_Leg, out _));
                    BoneLine(PartPos(p.Right_Upper_Leg, out _), PartPos(p.Right_Foot, out _));
                }
                else
                {
                    var h = PartPos(p.Head, out bool haveHead2);
                    var t = PartPos(p.Torso, out bool haveTorso);
                    if (!haveTorso) t = PartPos(p.HumanoidRootPart, out haveTorso);

                    if (haveHead2 && haveTorso) BoneLine(h, t);
                    BoneLine(t, PartPos(p.Left_Arm, out _));
                    BoneLine(t, PartPos(p.Right_Arm, out _));
                    BoneLine(t, PartPos(p.Left_Leg, out _));
                    BoneLine(t, PartPos(p.Right_Leg, out _));
                }
            }

            // ── Non-engine chams: convex-hull fill per part + outline ───────
            if (settings.Chams)
            {
                var hullParts = new List<SDKInstance>();
                for (int i = 0; i < parts.Length; ++i)
                    if (parts[i].IsValid && parts[i].Address != p.HumanoidRootPart.Address)
                        hullParts.Add(parts[i]);
                DrawPlayerChams(dl, vm, viewport, hullParts, settings);
            }
        }

        // ── Chams dispatcher (C++ chams modes) ─────────────────────────────
        // mode 0 = solid box chams (filled + outline)
        // mode 1 = shader-style animated scan fill (plasma bands)
        // mode 2 = wireframe mesh-style (edges only, no fill)
        private static void DrawPlayerChams(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            IList<SDKInstance> parts, Options.Visuals settings)
        {
            if (parts == null || parts.Count == 0) return;

            float fillA = Math.Clamp(settings.ChamsFillAlpha, 0f, 1f);
            float outA = Math.Clamp(settings.ChamsOutlineAlpha, 0f, 1f);
            uint fill = rgba(0.25f, 0.45f, 1f, fillA);
            uint outline = rgba(0.75f, 0.82f, 1f, outA);

            if (settings.ChamsMode == 2)
            {
                // Wireframe: just the OBB edges per part.
                var pts = new NVector2[8];
                for (int pi = 0; pi < parts.Count; ++pi)
                {
                    var part = parts[pi];
                    if (!part.IsValid) continue;
                    ReadPartData(part, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot);
                    if (pos.x == 0 && pos.y == 0 && pos.z == 0) continue;

                    SVector3 half = new SVector3 { x = size.x / 2f, y = size.y / 2f, z = size.z / 2f };
                    if (half.x < 0.01f && half.y < 0.01f && half.z < 0.01f)
                        half = new SVector3 { x = 0.5f, y = 0.5f, z = 0.5f };

                    int idx = 0;
                    bool ok = true;
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                            {
                                var off = RotateVec(in rot, half.x * sx, half.y * sy, half.z * sz);
                                var w = new SVector3 { x = pos.x + off.x, y = pos.y + off.y, z = pos.z + off.z };
                                if (WorldToScreen(vm, viewport, w, out SVector2 sp))
                                    pts[idx++] = new NVector2(sp.x, sp.y);
                                else
                                    ok = false;
                            }
                    if (!ok) continue;

                    for (int e = 0; e < 12; ++e)
                    {
                        int a = BoxEdges[e, 0], b = BoxEdges[e, 1];
                        dl.AddLine(pts[a], pts[b], Black, 2.5f);
                        dl.AddLine(pts[a], pts[b], outline, 1.2f);
                    }
                }
                return;
            }

            // Solid + shader both use the hull fill pipeline.
            uint fillCol = fill;
            if (settings.ChamsMode == 1)
            {
                // Shader-style: animated scan band over the body silhouette.
                float t = (float)(Environment.TickCount64 & 0xFFFF) / 65535f;
                DrawHullChams(dl, vm, viewport, parts, fillCol, outline);
                DrawScanBand(dl, vm, viewport, parts, t, fillCol);
            }
            else
            {
                DrawHullChams(dl, vm, viewport, parts, fillCol, outline);
            }
        }

        // ── Animated shader scan band (ported from C++ ShaderChams ScanY) ──
        private static void DrawScanBand(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            IList<SDKInstance> parts, float phase, uint color)
        {
            if (parts == null || parts.Count == 0) return;

            // Body bounding box from all part screens (screen-space).
            bool any = false;
            float minY = 1e9f, maxY = -1e9f, minX = 1e9f, maxX = -1e9f;
            for (int pi = 0; pi < parts.Count; ++pi)
            {
                var part = parts[pi];
                if (!part.IsValid) continue;
                ReadPartData(part, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot);
                if (pos.x == 0 && pos.y == 0 && pos.z == 0) continue;

                SVector3 half = new SVector3 { x = size.x / 2f, y = size.y / 2f, z = size.z / 2f };
                if (half.x < 0.01f && half.y < 0.01f && half.z < 0.01f)
                    half = new SVector3 { x = 0.5f, y = 0.5f, z = 0.5f };

                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var off = RotateVec(in rot, half.x * sx, half.y * sy, half.z * sz);
                            var w = new SVector3 { x = pos.x + off.x, y = pos.y + off.y, z = pos.z + off.z };
                            if (WorldToScreen(vm, viewport, w, out SVector2 sp))
                            {
                                minX = MathF.Min(minX, sp.x); maxX = MathF.Max(maxX, sp.x);
                                minY = MathF.Min(minY, sp.y); maxY = MathF.Max(maxY, sp.y);
                                any = true;
                            }
                        }
            }
            if (!any || maxX - minX < 1f || maxY - minY < 1f) return;

            float h = maxY - minY;
            float bh = h * 0.18f;
            float cy = minY + (h + bh) * phase - bh * 0.5f;
            dl.AddRectFilled(new NVector2(minX - 2f, cy - bh * 0.5f), new NVector2(maxX + 2f, cy + bh * 0.5f), color);
        }

        // ── Convex-hull chams fill + outside-union outline (C++ mode 2) ─────
        private static void DrawHullChams(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            IList<SDKInstance> parts, uint fillColor, uint outlineColor)
        {
            if (parts == null || parts.Count == 0) return;

            var hulls = new List<List<NVector2>>();
            for (int pi = 0; pi < parts.Count; ++pi)
            {
                var part = parts[pi];
                if (!part.IsValid) continue;
                ReadPartData(part, out SVector3 pos, out SVector3 size, out SMatrix3x3 rot);
                if (pos.x == 0 && pos.y == 0 && pos.z == 0) continue;

                SVector3 half = new SVector3 { x = size.x / 2f, y = size.y / 2f, z = size.z / 2f };
                if (half.x < 0.01f && half.y < 0.01f && half.z < 0.01f)
                    half = new SVector3 { x = 0.5f, y = 0.5f, z = 0.5f };

                var pts = new List<NVector2>(8);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var off = RotateVec(in rot, half.x * sx, half.y * sy, half.z * sz);
                            var w = new SVector3 { x = pos.x + off.x, y = pos.y + off.y, z = pos.z + off.z };
                            if (WorldToScreen(vm, viewport, w, out SVector2 sp))
                                pts.Add(new NVector2(sp.x, sp.y));
                        }

                if (pts.Count >= 3)
                    hulls.Add(ConvexHull(pts));
            }
            if (hulls.Count == 0) return;

            // Clip pieces so fills don't overlap (C++ SubtractPoly).
            var clipped = new List<List<NVector2>>();
            clipped.Capacity = hulls.Count * 2;
            for (int i = 0; i < hulls.Count; ++i)
            {
                if (hulls[i].Count < 3) continue;
                var pieces = new List<List<NVector2>> { hulls[i] };
                for (int j = 0; j < i && pieces.Count > 0; ++j)
                {
                    if (hulls[j].Count < 3) continue;
                    var next = new List<List<NVector2>>();
                    foreach (var piece in pieces)
                        SubtractPoly(piece, hulls[j], next);
                    pieces = next;
                }
                foreach (var piece in pieces)
                    if (piece.Count >= 3) clipped.Add(piece);
            }

            // Fill
            ImDrawListFlags backup = dl.Flags;
            dl.Flags &= ~ImDrawListFlags.AntiAliasedFill;
            for (int i = 0; i < clipped.Count; ++i)
            {
                if (clipped[i].Count < 3) continue;
                var arr = clipped[i].ToArray();
                dl.AddConvexPolyFilled(ref arr[0], arr.Length, fillColor);
            }
            dl.Flags = backup;

            // Outline — outside union of other parts (C++ DrawSegmentOutsideUnion)
            for (int i = 0; i < hulls.Count; ++i)
            {
                var hull = hulls[i];
                int n = hull.Count;
                if (n < 2) continue;
                for (int e = 0; e < n; ++e)
                    DrawSegmentOutsideUnion(dl, hull[e], hull[(e + 1) % n], hulls, i, outlineColor);
            }
        }

        private static void DrawBone(ImDrawListPtr dl, SMatrix4 vm, SVector2 viewport,
            SVector3 a, SVector3 b, float thick, bool outline)
        {
            if (WorldToScreen(vm, viewport, a, out SVector2 sa) && WorldToScreen(vm, viewport, b, out SVector2 sb))
                DrawSkeletonLine(dl, new NVector2(sa.x, sa.y), new NVector2(sb.x, sb.y), rgba(0.75f, 0.82f, 1f, 1f), thick, outline);
        }

        private static void DrawCenteredText(ImDrawListPtr dl, string text, float x, float y, float size, uint color)
        {
            var full = ImGui.CalcTextSize(text);
            float scale = size / ImGui.GetFontSize();
            var ts = new NVector2(full.X * scale, full.Y * scale);

            var p = new NVector2(x - ts.X * 0.5f, y - ts.Y * 0.5f);
            float px = (float)Math.Floor(p.X);
            float py = (float)Math.Floor(p.Y);

            for (int i = -1; i <= 1; ++i)
                for (int j = -1; j <= 1; ++j)
                    if (i != 0 || j != 0)
                        dl.AddText(new NVector2(px + i, py + j), Black, text);
            dl.AddText(new NVector2(px, py), color, text);
        }

        private static SVector3 localPos;

        internal static void UpdateLocalPlayerPos(SVector3 pos) => localPos = pos;

        private static float Distance2D(SVector3 a, SVector3 b) =>
            MathF.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

        // Kept for API compatibility (unused by the new inline renderer).
        public static void Start() { }
        public static void Stop() { }
        public static Scene GetSceneSnapshot() => new Scene();
        public class Scene
        {
            public List<Box> boxes = new();
            public List<Line> lines = new();
            public List<Text> texts = new();
            public List<Dot> dots = new();
        }
        public struct Box { public System.Windows.Rect r; public bool f; }
        public struct Line { public System.Windows.Point a, b; public System.Windows.Media.Color c; public double w; }
        public struct Text { public string t; public System.Windows.Point p; public System.Windows.Media.Color c; public double s; public bool ctr; }
        public struct Dot { public System.Windows.Point p; public double r; public System.Windows.Media.Color c; }

        public static SVector3 GetPos(SDKInstance i, bool refresh)
        {
            if (!i.IsValid) return new SVector3();
            try
            {
                long ptr = SDKInstance.Mem.ReadPtr(i.Address + Offsets.BasePart.Primitive);
                if (ptr == 0) return new SVector3();
                return SDKInstance.Mem.Read<SVector3>(ptr + Offsets.Primitive.Position);
            }
            catch { return new SVector3(); }
        }
    }
}