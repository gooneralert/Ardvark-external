using System;
using System.Collections.Generic;
using System.Threading;
using Offsets;
using SDKInstance = FoulzExternal.SDK.Instance;
using FoulzExternal.SDK.structures;
using FoulzExternal.storage;
using Options;

namespace FoulzExternal.features.games.universal.world
{
    // ────────────────────────────────────────────────────────────────────────
    //  world — C# port of the geeg lad WorldSlots/WorldEdit modules.
    //  Tick loop writes Lighting/Sky/Atmosphere/Effects properties directly
    //  through memory (same offsets as the C++ version). No engine chams.
    // ────────────────────────────────────────────────────────────────────────
    public static class world
    {
        private static Thread? t;
        private static bool active;

        public static void Start()
        {
            if (active) return;
            active = true;
            t = new Thread(Tick) { IsBackground = true };
            t.Start();
        }

        public static void Stop() => active = false;

        private static long FindLighting()
        {
            try
            {
                if (!Storage.IsInitialized || !Storage.DataModelInstance.IsValid) return 0;
                foreach (var c in Storage.DataModelInstance.GetChildren())
                {
                    if (c.GetClass() == "Lighting") return c.Address;
                }
            }
            catch { }
            return 0;
        }

        private static long FindChild(long parent, string cls)
        {
            try
            {
                if (parent <= 0x1000) return 0;
                foreach (var c in new SDKInstance(parent).GetChildren())
                {
                    if (c.GetClass() == cls) return c.Address;
                }
            }
            catch { }
            return 0;
        }

        private static void WriteVec3(long addr, Vector3 v)
        {
            try { if (addr != 0) SDKInstance.Mem.Write(addr, v); } catch { }
        }

        private static void WriteFloat(long addr, float v)
        {
            try { if (addr != 0) SDKInstance.Mem.Write(addr, v); } catch { }
        }

        private static void WriteByte(long addr, byte v)
        {
            try { if (addr != 0) SDKInstance.Mem.Write(addr, v); } catch { }
        }

        private static void WriteInt(long addr, int v)
        {
            try { if (addr != 0) SDKInstance.Mem.Write(addr, v); } catch { }
        }

        private static Vector3 Col3(System.Numerics.Vector4 c) => new Vector3 { x = c.X, y = c.Y, z = c.Z };

        private static void WriteColor3(long addr, System.Numerics.Vector4 c)
        {
            try
            {
                if (addr == 0) return;
                float r = Math.Clamp(c.X, 0f, 1f);
                float g = Math.Clamp(c.Y, 0f, 1f);
                float b = Math.Clamp(c.Z, 0f, 1f);
                byte[] rgb = { (byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f) };
                SDKInstance.Mem.WriteRaw(addr, rgb, rgb.Length);
            }
            catch { }
        }

        private static void Tick()
        {
            var cache = new Dictionary<long, long>();
            long lighting = 0;

            while (active)
            {
                try
                {
                    if (!Storage.IsInitialized || SDKInstance.Mem == null) { Thread.Sleep(200); continue; }

                    // Refresh lighting every ~400ms
                    long now = Environment.TickCount64;
                    long last = cache.TryGetValue(0, out long l) ? l : 0;
                    if (now - last >= 400 || lighting == 0)
                    {
                        lighting = FindLighting();
                        cache[0] = now;
                    }

                    if (lighting == 0) { Thread.Sleep(100); continue; }

                    var w = Settings.World;

                    // ── No Shadow ────────────────────────────────────────
                    if (w.NoShadow)
                        WriteByte(lighting + Offsets.Lighting.GlobalShadows, 0);

                    // ── Time Changer ─────────────────────────────────────
                    if (w.TimeChanger)
                    {
                        float t = Math.Clamp(w.ClockTime, 0f, 24f);
                        WriteFloat(lighting + Offsets.Lighting.ClockTime, t);

                        const float PI = 3.14159265f;
                        float sunAngle = (t / 24f - 0.25f) * 2f * PI;
                        float sunY = (float)Math.Sin(sunAngle);
                        float sunX = 0f;
                        float sunZ = -(float)Math.Cos(sunAngle);
                        var sunPos = new Vector3 { x = sunX, y = sunY, z = sunZ };
                        var moonPos = new Vector3 { x = -sunX, y = -sunY, z = -sunZ };
                        WriteVec3(lighting + Offsets.Lighting.SunPosition, sunPos);
                        WriteVec3(lighting + Offsets.Lighting.MoonPosition, moonPos);

                        float h = sunY;
                        Vector3 gradTop, gradBot;
                        if (h > 0.15f)
                        {
                            gradTop = new Vector3 { x = 0.35f, y = 0.65f, z = 0.95f };
                            gradBot = new Vector3 { x = 0.75f, y = 0.85f, z = 0.95f };
                        }
                        else if (h > 0f)
                        {
                            float st = h / 0.15f;
                            gradTop = new Vector3 { x = 0.15f + st * 0.2f, y = 0.15f + st * 0.5f, z = 0.25f + st * 0.7f };
                            gradBot = new Vector3 { x = 0.95f, y = 0.45f + st * 0.4f, z = 0.25f + st * 0.7f };
                        }
                        else
                        {
                            float nt = Math.Min((-h) / 0.3f, 1f);
                            float inv = 1f - nt;
                            gradTop = new Vector3 { x = 0.05f + inv * 0.1f, y = 0.05f + inv * 0.1f, z = 0.12f + inv * 0.13f };
                            gradBot = new Vector3 { x = 0.08f + inv * 0.07f, y = 0.08f + inv * 0.07f, z = 0.15f + inv * 0.1f };
                        }
                        WriteVec3(lighting + Offsets.Lighting.GradientTop, gradTop);
                        WriteVec3(lighting + Offsets.Lighting.GradientBottom, gradBot);
                    }

                    // ── Ambient / Outdoor ────────────────────────────────
                    if (w.Ambient)
                        WriteColor3(lighting + Offsets.Lighting.Ambient, w.AmbientCol);
                    if (w.Outdoor)
                        WriteColor3(lighting + Offsets.Lighting.OutdoorAmbient, w.OutdoorCol);

                    // ── Brightness / Exposure ─────────────────────────────
                    if (w.Brightness)
                        WriteFloat(lighting + Offsets.Lighting.Brightness, Math.Clamp(w.BrightnessVal, 0f, 20f));
                    if (w.ExposureOn)
                        WriteFloat(lighting + Offsets.Lighting.ExposureCompensation, Math.Clamp(w.Exposure, -5f, 5f));

                    // ── Light ─────────────────────────────────────────────
                    if (w.Light)
                    {
                        WriteColor3(lighting + Offsets.Lighting.LightColor, w.LightCol);
                        WriteVec3(lighting + Offsets.Lighting.LightDirection, new Vector3 { x = w.LightDirX, y = w.LightDirY, z = w.LightDirZ });
                    }

                    // ── Fog ───────────────────────────────────────────────
                    if (w.Fog)
                    {
                        float start = Math.Clamp(w.FogStart, 0f, 100f);
                        float end = Math.Clamp(w.FogEnd, start + 1f, 2000f);
                        WriteFloat(lighting + Offsets.Lighting.FogStart, start);
                        WriteFloat(lighting + Offsets.Lighting.FogEnd, end);
                        WriteColor3(lighting + Offsets.Lighting.FogColor, w.FogColor);
                    }

                    // ── Env Scale ─────────────────────────────────────────
                    if (w.Env)
                    {
                        WriteFloat(lighting + Offsets.Lighting.EnvironmentDiffuseScale, Math.Clamp(w.EnvDiffuse, 0f, 2f));
                        WriteFloat(lighting + Offsets.Lighting.EnvironmentSpecularScale, Math.Clamp(w.EnvSpecular, 0f, 2f));
                    }

                    // ── Color Shift ───────────────────────────────────────
                    if (w.ColorShift)
                    {
                        WriteColor3(lighting + Offsets.Lighting.ColorShift_Top, w.ShiftTop);
                        WriteColor3(lighting + Offsets.Lighting.ColorShift_Bottom, w.ShiftBot);
                    }

                    // ── Atmosphere (child of Lighting) ────────────────────
                    if (w.Atmosphere)
                    {
                        long atmo = FindChild(lighting, "Atmosphere");
                        if (atmo != 0)
                        {
                            WriteFloat(atmo + Offsets.Atmosphere.Density, Math.Clamp(w.AtmoDensity, 0f, 1f));
                            WriteFloat(atmo + Offsets.Atmosphere.Haze, Math.Clamp(w.AtmoHaze, 0f, 10f));
                            WriteFloat(atmo + Offsets.Atmosphere.Glare, Math.Clamp(w.AtmoGlare, 0f, 10f));
                            WriteFloat(atmo + Offsets.Atmosphere.Offset, Math.Clamp(w.AtmoOffset, 0f, 1f));
                            WriteColor3(atmo + Offsets.Atmosphere.Color, w.AtmoColor);
                            WriteColor3(atmo + Offsets.Atmosphere.Decay, w.AtmoDecay);
                        }
                    }

                    // ── Sky (child of Lighting) ───────────────────────────
                    if (w.Sky)
                    {
                        long sky = SDKInstance.Mem.ReadPtr(lighting + Offsets.Lighting.Sky);
                        if (sky == 0) sky = FindChild(lighting, "Sky");
                        if (sky != 0)
                        {
                            WriteFloat(sky + Offsets.Sky.SunAngularSize, Math.Clamp(w.SunAngular, 0f, 60f));
                            WriteFloat(sky + Offsets.Sky.MoonAngularSize, Math.Clamp(w.MoonAngular, 0f, 60f));
                            WriteVec3(sky + Offsets.Sky.SkyboxOrientation, new Vector3 { x = w.SkyOrientX, y = w.SkyOrientY, z = w.SkyOrientZ });
                        }
                    }

                    // ── Bloom / ColorCorr / ColorGrade / Dof ──────────────
                    if (w.Bloom)
                    {
                        long bloom = FindChild(lighting, "BloomEffect");
                        if (bloom != 0)
                        {
                            WriteByte(bloom + Offsets.BloomEffect.Enabled, 1);
                            WriteFloat(bloom + Offsets.BloomEffect.Intensity, Math.Clamp(w.BloomIntensity, 0f, 5f));
                            WriteFloat(bloom + Offsets.BloomEffect.Size, Math.Clamp(w.BloomSize, 0f, 56f));
                            WriteFloat(bloom + Offsets.BloomEffect.Threshold, Math.Clamp(w.BloomThreshold, 0f, 3f));
                        }
                    }

                    if (w.ColorCorr)
                    {
                        long cc = FindChild(lighting, "ColorCorrectionEffect");
                        if (cc != 0)
                        {
                            WriteByte(cc + Offsets.ColorCorrectionEffect.Enabled, 1);
                            WriteFloat(cc + Offsets.ColorCorrectionEffect.Brightness, w.CcBri);
                            WriteFloat(cc + Offsets.ColorCorrectionEffect.Contrast, w.CcCon);
                            WriteColor3(cc + Offsets.ColorCorrectionEffect.TintColor, w.CcTint);
                        }
                    }

                    if (w.ColorGrade)
                    {
                        long cg = FindChild(lighting, "ColorGradingEffect");
                        if (cg != 0)
                        {
                            WriteByte(cg + Offsets.ColorGradingEffect.Enabled, 1);
                            WriteInt(cg + Offsets.ColorGradingEffect.TonemapperPreset, Math.Clamp(w.Tonemapper, 0, 1));
                        }
                    }

                    if (w.Dof)
                    {
                        long dof = FindChild(lighting, "DepthOfFieldEffect");
                        if (dof != 0)
                        {
                            WriteByte(dof + Offsets.DepthOfFieldEffect.Enabled, 1);
                            WriteFloat(dof + 0xb8, w.DofFar);
                            WriteFloat(dof + 0xbc, w.DofNear);
                            WriteFloat(dof + 0xc0, w.DofFocus);
                            WriteFloat(dof + 0xc4, w.DofRadius);
                        }
                    }

                    // ── Terrain (Workspace.Terrain) ───────────────────────
                    if (w.Terrain && Storage.WorkspaceInstance.IsValid)
                    {
                        long terrain = Storage.WorkspaceInstance.FindFirstChildOfClass("Terrain").Address;
                        if (terrain != 0)
                        {
                            WriteFloat(terrain + Offsets.Terrain.GrassLength, Math.Clamp(w.GrassLen, 0f, 1f));
                            WriteColor3(terrain + Offsets.Terrain.WaterColor, w.WaterCol);
                            WriteFloat(terrain + Offsets.Terrain.WaterReflectance, Math.Clamp(w.WaterRefl, 0f, 1f));
                            WriteFloat(terrain + Offsets.Terrain.WaterTransparency, Math.Clamp(w.WaterTrans, 0f, 1f));
                        }
                    }

                    // ── Skybox Changer (Sky texture ids) ──────────────────
                    if (w.SkyboxChanger)
                    {
                        long sky = SDKInstance.Mem.ReadPtr(lighting + Offsets.Lighting.Sky);
                        if (sky == 0) sky = FindChild(lighting, "Sky");
                        if (sky != 0 && w.SkyboxPreset >= 0 && w.SkyboxPreset < SkyboxPresets.Length)
                        {
                            var p = SkyboxPresets[w.SkyboxPreset];
                            WriteInt64(sky + Offsets.Sky.SkyboxBk, p.Bk);
                            WriteInt64(sky + Offsets.Sky.SkyboxDn, p.Dn);
                            WriteInt64(sky + Offsets.Sky.SkyboxFt, p.Ft);
                            WriteInt64(sky + Offsets.Sky.SkyboxLf, p.Lf);
                            WriteInt64(sky + Offsets.Sky.SkyboxRt, p.Rt);
                            WriteInt64(sky + Offsets.Sky.SkyboxUp, p.Up);
                        }
                    }
                }
                catch { }

                bool busy = Settings.World.IsBusy;
                Thread.Sleep(busy ? 1 : 24);
            }
        }

        private static void WriteInt64(long addr, long v)
        {
            try { if (addr != 0) SDKInstance.Mem.Write(addr, v); } catch { }
        }

        // ── Skybox presets (same asset ids as geeg lad WorldSlots.cpp) ────
        public struct SkyboxPresetInfo
        {
            public string Name;
            public long Bk, Dn, Ft, Lf, Rt, Up;
        }

        public static readonly SkyboxPresetInfo[] SkyboxPresets = {
            new() { Name = "Night",        Bk = 15536110634, Dn = 15536112543, Ft = 15536116141, Lf = 15536114370, Rt = 15536118762, Up = 15536117282 },
            new() { Name = "Evening",      Bk = 16136021536, Dn = 16136025360, Ft = 16136021536, Lf = 16136021536, Rt = 16136021536, Up = 16136023362 },
            new() { Name = "Morning",      Bk = 6444884337,  Dn = 6444884785,  Ft = 6444884337,  Lf = 6444884337,  Rt = 6444884337,  Up = 6412503613  },
            new() { Name = "Galaxy",       Bk = 159454299,   Dn = 159454296,   Ft = 159454293,   Lf = 159454286,   Rt = 159454300,   Up = 159454288   },
            new() { Name = "Pink Sky",     Bk = 12635309703, Dn = 12635311686, Ft = 12635312870, Lf = 12635313718, Rt = 12635315817, Up = 12635316856 },
            new() { Name = "Vaporwave",    Bk = 8631780182,  Dn = 8631784904,  Ft = 8631769834,  Lf = 8631777199,  Rt = 8631735555,  Up = 8631782345  },
            new() { Name = "Red Night",    Bk = 401664839,   Dn = 401664862,   Ft = 401664960,   Lf = 401664881,   Rt = 401664901,   Up = 401664936   },
            new() { Name = "Sunset",       Bk = 323494035,   Dn = 323494368,   Ft = 323494130,   Lf = 323494252,   Rt = 323494067,   Up = 323493360   },
            new() { Name = "Blue",         Bk = 135483466,   Dn = 135483484,   Ft = 135483461,   Lf = 135483495,   Rt = 135483499,   Up = 135483475   },
            new() { Name = "Purple Haze",  Bk = 8107841671,  Dn = 6444884785,  Ft = 8107841671,  Lf = 8107841671,  Rt = 8107841671,  Up = 8107849791  },
            new() { Name = "Pink Fade",    Bk = 11427769401, Dn = 11427770685, Ft = 11427769401, Lf = 11427769401, Rt = 11427769401, Up = 11427771954 },
        };
    }
}