using FoulzExternal.helpers.keybind;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static Options.Aiming;

// daddy

namespace Options
{
    public class Humanoid
    {
        public bool WalkspeedEnabled = false;
        public bool JumpPowerEnabled = false;

        public float Walkspeed = 16f;
        public float JumpPower = 50f;

        public Humanoid()
        {
        }
    }

    public class Camera
    {
        public bool FOVEnabled = false;
        public float FOV = 70f;

        public Camera()
        {
        }
    }
    public class Visuals
    {
        public bool Enabled = false;   // master ESP toggle
        public bool BoxESP = false;
        public bool FilledBox = false;
        public bool Box = false;
        public bool BoxFill = false;
        public bool Tracers = false;
        public bool Skeleton = false;
        public bool Name = false;
        public bool Distance = false;
        public bool Health = false;
        public bool ESP3D = false;
        public bool HeadCircle = false;
        public bool CornerESP = false;
        public bool RemoveBorders = false;
        public bool ChinaHat = false;
        public bool LocalPlayerESP = false;
        public int TracersStart = 0;
        public float NameSize = 12f;
        public float DistanceSize = 15f;
        public float TracerThickness = 1.5f;
        public float HeadCircleMaxScale = 2.5f;

        // ── geeg lad ESP additions (external-feasible, no engine chams) ──
        public bool OffscreenArrows = false;   // arrow when target is off screen
        public float ArrowSize = 14f;
        public float ArrowRadius = 200f;
        public bool DistanceCheck = false;     // only show players within max distance
        public float MaxDistance = 1000f;
        public bool DeadCheck = false;         // skip dead players
        public bool HealthText = false;        // numeric health above bar
        public int BoxMode = 0;                // 0 = regular, 1 = corner-only
        public int TracerOrigin = 0;           // 0 bottom, 1 center, 2 mouse, 3 top

        // ── Chams (non-engine overlay chams, ported from C++ external) ─────
        public bool Chams = false;             // master chams toggle
        public int ChamsMode = 1;              // 0 = solid box, 1 = shader, 2 = wireframe mesh-style
        public int ChamsShaderStyle = 0;       // shader style index (0 = plasma scan)
        public float ChamsFillAlpha = 0.5f;    // fill opacity (0..1)
        public float ChamsOutlineAlpha = 1f;   // outline opacity (0..1)

        public Visuals()
        {
        }
    }

    public class Aiming
    {
        public KeyBind AimbotKey = new KeyBind("Aimbot");
        public int AimingType;
        public int ToggleType;
        public bool Aimbot = false;
        public bool StickyAim = false;
        public float Sensitivity = 1.0f;
        public bool Smoothness = false;
        public float SmoothnessX = 0.0f;
        public float SmoothnessY = 0.05f;
        public bool Prediction = false;
        public float PredictionY = 2f;
        public float PredictionX = 2f;
        public float FOV = 100f;
        public bool ShowFOV = false;
        public bool FillFOV = false;
        public bool AnimatedFOV = false;
        public float Range = 100f;
        public int TargetBone = 0;

        public Aiming() { }
    }
    public class Silent
    {
        public KeyBind SilentAimbotKey = new KeyBind("SilentAimbotKey");
        public bool SilentAimbot = false;
        public bool AlwaysOn = false;
        public bool SilentVisualizer = false;
        public bool ShowSilentFOV = false;
        public bool SPrediction = false;
        public float SilentFOV = 100f;
        public float PredictionY = 2f;
        public float PredictionX = 2f;
        public float SFOV = 150f;
        public bool RaycastSilent = false;
        public bool MagicBullet = false;
        public int SilentMethod = 0; // 0=Off, 1=Rivals, 2=Raycast, 3=Magic Bullet

        public Silent() { }
    }

    public class Checks
    {
        public bool TeamCheck = false;
        public bool PFTeamCheck = false;
        public bool PFSwitchTeam = false;
        public bool DownedCheck = false;
        public bool TransparencyCheck = false;
        public bool WallCheck = false;
        public Checks()
        {
        }
    }
    public class Network
    {
        public KeyBind DeSyncBind = new KeyBind("DeSyncBind");
        public bool DeSync = false;
        public bool DeSyncVisualizer = false;

        public Network()
        {
        }
    }
    public class Flight
    {
        public KeyBind VFlightBind = new KeyBind("VFlightBind");
        public bool VFlight = false;
        public float VFlightSpeed = 50f;
        public int VFlightMethod = 0; // 0 = Position, 1 = Velocity

        public Flight()
        {
        }
    }
    public class CarFly
    {
        public KeyBind CarFlyBind = new KeyBind("CarFlyBind");
        public bool CarFlyEnabled = false;
        public float CarFlySpeed = 600f;

        public CarFly() { }
    }
    public class FPS
    {
        public bool FPSEnabled = false;
        public float Value = 60f;
        public FPS() { }
    }
    public class Tickrate
    {
        public bool Enabled = false;
        public float Value = 60f;
        public Tickrate() { }
    }
    public class Gravity
    {
        public bool Enabled = false;
        public float Value = 196.2f;
        public Gravity() { }
    }
    public class ThirdPerson
    {
        public bool Enabled = false;
        public float Distance = 8f;
        public ThirdPerson() { }
    }
    public class InfiniteJump
    {
        public bool Enabled = false;
        public bool CustomPower = false;
        public float PowerValue = 75f;
        public InfiniteJump() { }
    }

    // ── Freecam (geeg lad Misc.cpp freecam_tick) ───────────────────────────
    public class Freecam
    {
        public bool Enabled = false;
        public int Key = 0;          // 0 = always on
        public int Mode = 0;         // 0 hold, 1 toggle, 2 always
        public float Speed = 60f;
        public Freecam() { }
    }

    // ── World settings (geeg lad world tab) ─────────────────────────────────
    public class World
    {
        public bool NoShadow = false;

        public bool TimeChanger = false;
        public float ClockTime = 14f;

        public bool Ambient = false;
        public System.Numerics.Vector4 AmbientCol = new(1f, 1f, 1f, 1f);
        public bool Outdoor = false;
        public System.Numerics.Vector4 OutdoorCol = new(1f, 1f, 1f, 1f);

        public bool Brightness = false;
        public float BrightnessVal = 2f;
        public bool ExposureOn = false;
        public float Exposure = 0f;

        public bool Light = false;
        public System.Numerics.Vector4 LightCol = new(1f, 1f, 1f, 1f);
        public float LightDirX = 0f, LightDirY = -1f, LightDirZ = 0f;

        public bool Fog = false;
        public float FogStart = 0f;
        public float FogEnd = 2000f;
        public System.Numerics.Vector4 FogColor = new(1f, 1f, 1f, 1f);

        public bool Env = false;
        public float EnvDiffuse = 1f;
        public float EnvSpecular = 1f;

        public bool ColorShift = false;
        public System.Numerics.Vector4 ShiftTop = new(1f, 1f, 1f, 1f);
        public System.Numerics.Vector4 ShiftBot = new(1f, 1f, 1f, 1f);

        public bool Atmosphere = false;
        public float AtmoDensity = 0.3f;
        public float AtmoHaze = 0f;
        public float AtmoGlare = 0f;
        public float AtmoOffset = 0.25f;
        public System.Numerics.Vector4 AtmoColor = new(1f, 1f, 1f, 1f);
        public System.Numerics.Vector4 AtmoDecay = new(1f, 1f, 1f, 1f);

        public bool Sky = false;
        public float SunAngular = 21f;
        public float MoonAngular = 11f;
        public float SkyOrientX = 0f, SkyOrientY = 0f, SkyOrientZ = 0f;

        public bool SkyboxChanger = false;
        public int SkyboxPreset = 0;

        public bool Bloom = false;
        public float BloomIntensity = 1f;
        public float BloomSize = 24f;
        public float BloomThreshold = 0.95f;

        public bool ColorCorr = false;
        public float CcBri = 0f;
        public float CcCon = 0f;
        public System.Numerics.Vector4 CcTint = new(1f, 1f, 1f, 1f);

        public bool ColorGrade = false;
        public int Tonemapper = 0;

        public bool Dof = false;
        public float DofFar = 0.1f;
        public float DofNear = 0.1f;
        public float DofFocus = 20f;
        public float DofRadius = 50f;

        public bool Terrain = false;
        public float GrassLen = 0.2f;
        public System.Numerics.Vector4 GrassCol = new(1f, 1f, 1f, 1f);
        public System.Numerics.Vector4 WaterCol = new(1f, 1f, 1f, 1f);
        public float WaterRefl = 1f;
        public float WaterTrans = 0.3f;

        public bool IsBusy =>
            NoShadow || TimeChanger || Ambient || Outdoor || Brightness || ExposureOn ||
            Light || Fog || Env || ColorShift || Atmosphere || Sky || SkyboxChanger ||
            Bloom || ColorCorr || ColorGrade || Dof || Terrain;

        public World() { }
    }

    public static class Settings
    {
        public static Humanoid Humanoid = new Humanoid();
        public static Camera Camera = new Camera();
        public static Visuals Visuals = new Visuals();
        public static Aiming Aiming = new Aiming();
        public static Checks Checks = new Checks();
        public static Network Network = new Network();
        public static Flight Flight = new Flight();
        public static CarFly CarFly = new CarFly();
        public static Silent Silent = new Silent();
        public static FPS FPS = new FPS();
        public static Tickrate Tickrate = new Tickrate();
        public static Gravity Gravity = new Gravity();
        public static ThirdPerson ThirdPerson = new ThirdPerson();
        public static InfiniteJump InfiniteJump = new InfiniteJump();
        public static Options.World World = new Options.World();
        public static Freecam Freecam = new Freecam();
    }
}
