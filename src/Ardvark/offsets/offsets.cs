/* =============================================================
/*                       Ardvark Offsets
/* -------------------------------------------------------------
<<<<<<< Updated upstream
/*  Dumped With     : RbxDumperV2                               
/*  Roblox Version  : version-8884371d30284041
/*  Dumper Version  : 2.1.7
/*  Dumped At       : 10:20 23/06/2026 (GMT)
/*  Total Offsets   : 392
/* -------------------------------------------------------------
/*  Join the discord!                                           
/*  https://offsets.imtheo.lol/discord                          
=======
/*  Primary source  : https://imtheo.lol/Offsets/Offsets.cs
/*  Fallback source : https://awaky1337.github.io/jewsploit-offsets/Offsets.h
/*  Fallback offsets are only used to fill in values that are
/*  not present in the primary source.
>>>>>>> Stashed changes
/* =============================================================
*/

namespace Offsets {
    public static class Info {
        public static string ClientVersion = "version-8884371d30284041";
    }
    public static class AirProperties {
        public const long AirDensity = 0x18;
        public const long GlobalWind = 0x3c;
    }
    public static class AnimationTrack {
<<<<<<< Updated upstream
         public const long Animation = 0xd0;
         public const long Animator = 0x118;
         public const long IsPlaying = 0xa10;
         public const long Looped = 0xf5;
         public const long Speed = 0xe4;
         public const long TimePosition = 0xe8;
=======
        public const long Animation = 0xb8;
        public const long Animator = 0x108;
        public const long IsPlaying = 0x5ef;
        public const long Looped = 0xe5;
        public const long Speed = 0xd4;
        public const long TimePosition = 0xd8;
>>>>>>> Stashed changes
    }
    public static class Animator {
<<<<<<< Updated upstream
         public const long ActiveAnimations = 0x888;
=======
        public const long ActiveAnimations = 0xb80;
>>>>>>> Stashed changes
    }
    public static class Atmosphere {
<<<<<<< Updated upstream
         public const long Color = 0xd0;
         public const long Decay = 0xdc;
         public const long Density = 0xe8;
         public const long Glare = 0xec;
         public const long Haze = 0xf0;
         public const long Offset = 0xf4;
=======
        public const long Color = 0xb8;
        public const long Decay = 0xc4;
        public const long Density = 0xd0;
        public const long Glare = 0xd4;
        public const long Haze = 0xd8;
        public const long Offset = 0xdc;
>>>>>>> Stashed changes
    }
    public static class Attachment {
<<<<<<< Updated upstream
         public const long Position = 0xdc;
=======
        public const long Position = 0xc4;
>>>>>>> Stashed changes
    }
    public static class Attribute {
        public const long Key = 0x0;
        public const long Size = 0x58;
        public const long Value = 0x18;
        public const long TypeIdRva = 0x7e41a2c;
    }
    public static class AttributesMap {
        public const long Attributes = 0x10;
        public const long Length = 0x0;
    }
    public static class BasePart {
<<<<<<< Updated upstream
         public const long CastShadow = 0xf5;
         public const long Color3 = 0x194;
         public const long Locked = 0xf6;
         public const long Massless = 0xf7;
         public const long Primitive = 0x148;
         public const long Reflectance = 0xec;
         public const long Shape = 0x1b1;
         public const long Transparency = 0xf0;
=======
        public const long CastShadow = 0xd5;
        public const long Color3 = 0x148;
        public const long Locked = 0xd6;
        public const long Massless = 0xd7;
        public const long Primitive = 0x128;
        public const long Reflectance = 0xcc;
        public const long Shape = 0x159;
        public const long Transparency = 0xd0;
>>>>>>> Stashed changes
    }
    public static class Beam {
<<<<<<< Updated upstream
         public const long Attachment0 = 0x178;
         public const long Attachment1 = 0x188;
         public const long Brightness = 0x198;
         public const long CurveSize0 = 0x19c;
         public const long CurveSize1 = 0x1a0;
         public const long LightEmission = 0x1a4;
         public const long LightInfluence = 0x1a8;
         public const long Texture = 0x158;
         public const long TextureLength = 0x1b4;
         public const long TextureSpeed = 0x1bc;
         public const long Width0 = 0x1c0;
         public const long Width1 = 0x1c4;
         public const long ZOffset = 0x1c8;
=======
        public const long Attachment0 = 0x160;
        public const long Attachment1 = 0x170;
        public const long Brightness = 0x180;
        public const long CurveSize0 = 0x184;
        public const long CurveSize1 = 0x188;
        public const long LightEmission = 0x18c;
        public const long LightInfluence = 0x190;
        public const long Texture = 0x140;
        public const long TextureLength = 0x19c;
        public const long TextureSpeed = 0x1a4;
        public const long Width0 = 0x1a8;
        public const long Width1 = 0x1ac;
        public const long ZOffset = 0x1b0;
>>>>>>> Stashed changes
    }
    public static class BloomEffect {
<<<<<<< Updated upstream
         public const long Enabled = 0xc8;
         public const long Intensity = 0xd0;
         public const long Size = 0xd4;
         public const long Threshold = 0xd8;
=======
        public const long Enabled = 0xb0;
        public const long Intensity = 0xb8;
        public const long Size = 0xbc;
        public const long Threshold = 0xc0;
>>>>>>> Stashed changes
    }
    public static class BlurEffect {
<<<<<<< Updated upstream
         public const long Enabled = 0xc8;
         public const long Size = 0xd0;
=======
        public const long Enabled = 0xb0;
        public const long Size = 0xb8;
>>>>>>> Stashed changes
    }
    public static class ByteCode {
        public const long Pointer = 0x10;
        public const long Size = 0x20;
    }
    public static class Camera {
<<<<<<< Updated upstream
         public const long CameraSubject = 0xe8;
         public const long CameraType = 0x158;
         public const long FieldOfView = 0x160;
         public const long ImagePlaneDepth = 0x2f0;
         public const long Position = 0x11c;
         public const long Rotation = 0xf8;
         public const long Viewport = 0x2ac;
         public const long ViewportSize = 0x2e8;
=======
        public const long CameraSubject = 0xc8;
        public const long CameraType = 0x138;
        public const long FieldOfView = 0x140;
        public const long ImagePlaneDepth = 0x2d0;
        public const long Position = 0xfc;
        public const long Rotation = 0xd8;
        public const long Viewport = 0x28c;
        public const long ViewportSize = 0x2c8;
>>>>>>> Stashed changes
    }
    public static class CharacterMesh {
<<<<<<< Updated upstream
         public const long BaseTextureId = 0xe0;
         public const long BodyPart = 0x160;
         public const long MeshId = 0x110;
         public const long OverlayTextureId = 0x140;
=======
        public const long BaseTextureId = 0xc8;
        public const long BodyPart = 0x148;
        public const long MeshId = 0xf8;
        public const long OverlayTextureId = 0x128;
>>>>>>> Stashed changes
    }
    public static class ClickDetector {
<<<<<<< Updated upstream
         public const long MaxActivationDistance = 0x100;
         public const long MouseIcon = 0xe0;
=======
        public const long MaxActivationDistance = 0xe8;
        public const long MouseIcon = 0xc8;
>>>>>>> Stashed changes
    }
    public static class Clothing {
<<<<<<< Updated upstream
         public const long Color3 = 0x138;
         public const long Template = 0x118;
=======
        public const long Color3 = 0x120;
        public const long Template = 0x100;
>>>>>>> Stashed changes
    }
    public static class ColorCorrectionEffect {
<<<<<<< Updated upstream
         public const long Brightness = 0xdc;
         public const long Contrast = 0xe0;
         public const long Enabled = 0xc8;
         public const long TintColor = 0xd0;
=======
        public const long Brightness = 0xc4;
        public const long Contrast = 0xc8;
        public const long Enabled = 0xb0;
        public const long TintColor = 0xb8;
>>>>>>> Stashed changes
    }
    public static class ColorGradingEffect {
<<<<<<< Updated upstream
         public const long Enabled = 0xc8;
         public const long TonemapperPreset = 0xd0;
=======
        public const long Enabled = 0xb0;
        public const long TonemapperPreset = 0xb8;
>>>>>>> Stashed changes
    }
    public static class DataModel {
<<<<<<< Updated upstream
         public const long CreatorId = 0x198;
         public const long GameId = 0x1a0;
         public const long GameLoaded = 0x678;
         public const long JobId = 0x138;
         public const long PlaceId = 0x1a8;
         public const long PlaceVersion = 0x1c4;
         public const long PrimitiveCount = 0x4a8;
         public const long ScriptContext = 0x440;
         public const long ServerIP = 0x660;
         public const long ToRenderView1 = 0x1e0;
         public const long ToRenderView2 = 0x8;
         public const long ToRenderView3 = 0x28;
         public const long Workspace = 0x178;
=======
        public const long CreatorId = 0x180;
        public const long GameId = 0x188;
        public const long GameLoaded = 0x578;
        public const long JobId = 0x120;
        public const long PlaceId = 0x190;
        public const long PlaceVersion = 0x1ac;
        public const long PrimitiveCount = 0x3c0;
        public const long ScriptContext = 0x440;
        public const long ServerIP = 0x560;
        public const long ToRenderView1 = 0x1c8;
        public const long ToRenderView2 = 0x8;
        public const long ToRenderView3 = 0x28;
        public const long Workspace = 0x160;
>>>>>>> Stashed changes
    }
    public static class DepthOfFieldEffect {
<<<<<<< Updated upstream
         public const long Enabled = 0xc8;
         public const long FarIntensity = 0xd0;
         public const long FocusDistance = 0xd4;
         public const long InFocusRadius = 0xd8;
         public const long NearIntensity = 0xdc;
=======
        public const long Enabled = 0xb0;
        public const long FarIntensity = 0xb8;
        public const long FocusDistance = 0xbc;
        public const long InFocusRadius = 0xc0;
        public const long NearIntensity = 0xc4;
>>>>>>> Stashed changes
    }
    public static class DragDetector {
<<<<<<< Updated upstream
         public const long ActivatedCursorIcon = 0x1d8;
         public const long CursorIcon = 0xe0;
         public const long MaxActivationDistance = 0x100;
         public const long MaxDragAngle = 0x2c0;
         public const long MaxDragTranslation = 0x284;
         public const long MaxForce = 0x2c4;
         public const long MaxTorque = 0x2c8;
         public const long MinDragAngle = 0x2cc;
         public const long MinDragTranslation = 0x290;
         public const long ReferenceInstance = 0x208;
         public const long Responsiveness = 0x2d8;
=======
        public const long ActivatedCursorIcon = 0x1c0;
        public const long CursorIcon = 0xc8;
        public const long MaxActivationDistance = 0xe8;
        public const long MaxDragAngle = 0x2a8;
        public const long MaxDragTranslation = 0x26c;
        public const long MaxForce = 0x2ac;
        public const long MaxTorque = 0x2b0;
        public const long MinDragAngle = 0x2b4;
        public const long MinDragTranslation = 0x278;
        public const long ReferenceInstance = 0x1f0;
        public const long Responsiveness = 0x2c0;
>>>>>>> Stashed changes
    }
    public static class FakeDataModel {
<<<<<<< Updated upstream
         public const long Pointer = 0x7bcf6a8;
         public const long RealDataModel = 0x1d8;
=======
        public const long Pointer = 0x7e26978;
        public const long RealDataModel = 0x1d0;
>>>>>>> Stashed changes
    }
    public static class GuiBase2D {
<<<<<<< Updated upstream
         public const long AbsolutePosition = 0x110;
         public const long AbsoluteRotation = 0x188;
         public const long AbsoluteSize = 0x118;
=======
        public const long AbsolutePosition = 0xf8;
        public const long AbsoluteRotation = 0x178;
        public const long AbsoluteSize = 0x100;
>>>>>>> Stashed changes
    }
    public static class GuiObject {
<<<<<<< Updated upstream
         public const long BackgroundColor3 = 0x540;
         public const long BackgroundTransparency = 0x54c;
         public const long BorderColor3 = 0x54c;
         public const long Image = 0x988;
         public const long LayoutOrder = 0x580;
         public const long Position = 0x510;
         public const long RichText = 0xb50;
         public const long Rotation = 0x188;
         public const long ScreenGui_Enabled = 0x4c4;
         public const long Size = 0x530;
         public const long Text = 0xda0;
         public const long TextColor3 = 0xe50;
         public const long Visible = 0x5ad;
         public const long ZIndex = 0x19b;
=======
        public const long BackgroundColor3 = 0x540;
        public const long BackgroundTransparency = 0x54c;
        public const long BorderColor3 = 0x54c;
        public const long Image = 0x988;
        public const long LayoutOrder = 0x580;
        public const long Position = 0x510;
        public const long RichText = 0xb78;
        public const long Rotation = 0x178;
        public const long ScreenGui_Enabled = 0x4c4;
        public const long Size = 0x530;
        public const long Text = 0xde8;
        public const long TextColor3 = 0xe98;
        public const long Visible = 0x5ad;
        public const long ZIndex = 0x18b;
>>>>>>> Stashed changes
    }
    public static class Humanoid {
<<<<<<< Updated upstream
         public const long AutoJumpEnabled = 0x1e0;
         public const long AutoRotate = 0x1e1;
         public const long AutomaticScalingEnabled = 0x1e2;
         public const long BreakJointsOnDeath = 0x1e3;
         public const long CameraOffset = 0x140;
         public const long DisplayDistanceType = 0x18c;
         public const long DisplayName = 0xd0;
         public const long EvaluateStateMachine = 0x1e4;
         public const long FloorMaterial = 0x190;
         public const long Health = 0x194;
         public const long HealthDisplayDistance = 0x198;
         public const long HealthDisplayType = 0x19c;
         public const long HipHeight = 0x1a0;
         public const long HumanoidRootPart = 0x480;
         public const long HumanoidState = 0x8a0;
         public const long HumanoidStateID = 0x20;
         public const long IsWalking = 0x91f;
         public const long Jump = 0x1e6;
         public const long JumpHeight = 0x1ac;
         public const long JumpPower = 0x1b0;
         public const long MaxHealth = 0x1b4;
         public const long MaxSlopeAngle = 0x1b8;
         public const long MoveDirection = 0x158;
         public const long MoveToPart = 0x130;
         public const long MoveToPoint = 0x17c;
         public const long NameDisplayDistance = 0x1bc;
         public const long NameOcclusion = 0x1c0;
         public const long PlatformStand = 0x1e8;
         public const long RequiresNeck = 0x1e9;
         public const long RigType = 0x1cc;
         public const long SeatPart = 0x120;
         public const long Sit = 0x1e9;
         public const long TargetPoint = 0x164;
         public const long UseJumpPower = 0x1ec;
         public const long WalkTimer = 0x410;
         public const long Walkspeed = 0x1dc;
         public const long WalkspeedCheck = 0x3c4;
=======
        public const long AutoJumpEnabled = 0x1d4;
        public const long AutoRotate = 0x1d5;
        public const long AutomaticScalingEnabled = 0x1d6;
        public const long BreakJointsOnDeath = 0x1d7;
        public const long CameraOffset = 0x128;
        public const long DisplayDistanceType = 0x180;
        public const long DisplayName = 0xb8;
        public const long EvaluateStateMachine = 0x1d8;
        public const long FloorMaterial = 0x184;
        public const long Health = 0x190;
        public const long HealthDisplayDistance = 0x188;
        public const long HealthDisplayType = 0x18c;
        public const long HipHeight = 0x194;
        public const long HumanoidRootPart = 0x478;
        public const long HumanoidState = 0x898;
        public const long HumanoidStateID = 0x20;
        public const long IsWalking = 0x93f;
        public const long Jump = 0x1da;
        public const long JumpHeight = 0x1a0;
        public const long JumpPower = 0x1a4;
        public const long MaxHealth = 0x1a8;
        public const long MaxSlopeAngle = 0x1ac;
        public const long MoveDirection = 0x140;
        public const long MoveToPart = 0x118;
        public const long MoveToPoint = 0x164;
        public const long NameDisplayDistance = 0x1b0;
        public const long NameOcclusion = 0x1b4;
        public const long PlatformStand = 0x1dc;
        public const long RequiresNeck = 0x1dd;
        public const long RigType = 0x1c0;
        public const long SeatPart = 0x108;
        public const long Sit = 0x1dd;
        public const long TargetPoint = 0x14c;
        public const long UseJumpPower = 0x1e0;
        public const long WalkTimer = 0x408;
        public const long Walkspeed = 0x1d0;
        public const long WalkspeedCheck = 0x3bc;
        public const long PlatformStatePointer = 0x0;
>>>>>>> Stashed changes
    }
    public static class Instance {
<<<<<<< Updated upstream
         public const long ChildrenEnd = 0x8;
         public const long ChildrenStart = 0x78;
         public const long ClassBase = 0x230;
         public const long ClassDescriptor = 0x18;
         public const long ClassName = 0x8;
         public const long ComponentMap = 0x38;
         public const long Name = 0xb0;
         public const long Parent = 0x70;
         public const long This = 0x8;
=======
        public const long ChildrenEnd = 0x8;
        public const long ChildrenStart = 0x70;
        public const long ClassBase = 0x1b0;
        public const long ClassDescriptor = 0x18;
        public const long ClassName = 0x8;
        public const long ComponentMap = 0x38;
        public const long Name = 0x98;
        public const long Parent = 0x68;
        public const long This = 0x8;
        public const long AttributeContainer = 0x48;
        public const long AttributeList = 0x18;
        public const long AttributeToNext = 0x58;
        public const long AttributeToValue = 0x18;
        public const long ClassByName = 0x507a230;
        public const long Creator_create = 0x0;
        public const long Creator_isCreatable = 0x10;
        public const long CreatorFromClass = 0x4c32e60;
        public const long FromExisting = 0x1e013c0;
        public const long New = 0x1e00d00;
        public const long PushToLua = 0x1d48ca0;
        public const long SetParent = 0x4a8cf20;
        public const long WhJobNopSlot = 0x10;
        public const long WhJobVftable = 0x60424d8;
>>>>>>> Stashed changes
    }
    public static class Lighting {
<<<<<<< Updated upstream
         public const long Ambient = 0xe0;
         public const long Brightness = 0x128;
         public const long ClockTime = 0x1c0;
         public const long ColorShift_Bottom = 0xf8;
         public const long ColorShift_Top = 0xec;
         public const long EnvironmentDiffuseScale = 0x12c;
         public const long EnvironmentSpecularScale = 0x130;
         public const long ExposureCompensation = 0x134;
         public const long FogColor = 0x104;
         public const long FogEnd = 0x13c;
         public const long FogStart = 0x140;
         public const long GeographicLatitude = 0x198;
         public const long GlobalShadows = 0x150;
         public const long GradientBottom = 0x19c;
         public const long GradientTop = 0x158;
         public const long LightColor = 0x164;
         public const long LightDirection = 0x170;
         public const long MoonPosition = 0x18c;
         public const long OutdoorAmbient = 0x110;
         public const long Sky = 0x1e0;
         public const long Source = 0x17c;
         public const long SunPosition = 0x180;
=======
        public const long Ambient = 0xd0;
        public const long Brightness = 0x118;
        public const long ClockTime = 0xc8;
        public const long ColorShift_Bottom = 0xe8;
        public const long ColorShift_Top = 0xdc;
        public const long EnvironmentDiffuseScale = 0x11c;
        public const long EnvironmentSpecularScale = 0x120;
        public const long ExposureCompensation = 0x124;
        public const long FogColor = 0xf4;
        public const long FogEnd = 0x12c;
        public const long FogStart = 0x130;
        public const long GeographicLatitude = 0x134;
        public const long GlobalShadows = 0x144;
        public const long GradientBottom = 0x188;
        public const long GradientTop = 0x148;
        public const long LightColor = 0x154;
        public const long LightDirection = 0x160;
        public const long MoonPosition = 0x17c;
        public const long OutdoorAmbient = 0x100;
        public const long Sky = 0x1c0;
        public const long Source = 0x16c;
        public const long SunPosition = 0x170;
>>>>>>> Stashed changes
    }
    public static class LocalScript {
<<<<<<< Updated upstream
         public const long ByteCode = 0x1a8;
         public const long GUID = 0xe8;
         public const long Hash = 0x1b8;
=======
        public const long ByteCode = 0x190;
        public const long GUID = 0xd0;
        public const long Hash = 0x1a0;
>>>>>>> Stashed changes
    }
    public static class MaterialColors {
        public const long Asphalt = 0x30;
        public const long Basalt = 0x27;
        public const long Brick = 0xf;
        public const long Cobblestone = 0x33;
        public const long Concrete = 0xc;
        public const long CrackedLava = 0x2d;
        public const long Glacier = 0x1b;
        public const long Grass = 0x6;
        public const long Ground = 0x2a;
        public const long Ice = 0x36;
        public const long LeafyGrass = 0x39;
        public const long Limestone = 0x3f;
        public const long Mud = 0x24;
        public const long Pavement = 0x42;
        public const long Rock = 0x18;
        public const long Salt = 0x3c;
        public const long Sand = 0x12;
        public const long Sandstone = 0x21;
        public const long Slate = 0x9;
        public const long Snow = 0x1e;
        public const long WoodPlanks = 0x15;
    }
    public static class MeshContentProvider {
        public const long AssetID = 0x10;
        public const long Cache = 0xf0;
        public const long LRUCache = 0x20;
        public const long MeshData = 0x40;
        public const long ToMeshData = 0x40;
    }
    public static class MeshData {
        public const long FaceEnd = 0x38;
        public const long FaceStart = 0x30;
        public const long VertexEnd = 0x8;
        public const long VertexStart = 0x0;
    }
    public static class MeshPart {
<<<<<<< Updated upstream
         public const long MeshId = 0x2f8;
         public const long Texture = 0x328;
=======
        public const long MeshId = 0x2a8;
        public const long Texture = 0x2d8;
>>>>>>> Stashed changes
    }
    public static class Misc {
<<<<<<< Updated upstream
         public const long Adornee = 0x108;
         public const long AnimationId = 0xd8;
         public const long StringLength = 0x10;
         public const long Value = 0xd0;
=======
        public const long Adornee = 0xf0;
        public const long AnimationId = 0xc0;
        public const long StringLength = 0x10;
        public const long Value = 0xb8;
>>>>>>> Stashed changes
    }
    public static class Model {
<<<<<<< Updated upstream
         public const long PrimaryPart = 0x278;
         public const long Scale = 0x164;
=======
        public const long PrimaryPart = 0x258;
        public const long Scale = 0x144;
>>>>>>> Stashed changes
    }
    public static class ModuleScript {
<<<<<<< Updated upstream
         public const long ByteCode = 0x150;
         public const long GUID = 0xe8;
         public const long Hash = 0x160;
         public const long IsCoreScript = 0x0;
=======
        public const long ByteCode = 0x138;
        public const long GUID = 0xd0;
        public const long Hash = 0x148;
        public const long IsCoreScript = 0x0;
>>>>>>> Stashed changes
    }
    public static class MouseService {
<<<<<<< Updated upstream
         public const long InputObject = 0x108;
         public const long InputObject2 = 0x118;
         public const long MousePosition = 0xec;
         public const long SensitivityPointer = 0x7d92898;
=======
        public const long InputObject = 0xf0;
        public const long InputObject2 = 0x100;
        public const long MousePosition = 0xd4;
        public const long SensitivityPointer = 0x7fd51b8;
>>>>>>> Stashed changes
    }
    public static class ParticleEmitter {
<<<<<<< Updated upstream
         public const long Acceleration = 0x1f8;
         public const long Brightness = 0x234;
         public const long Drag = 0x238;
         public const long Lifetime = 0x20c;
         public const long LightEmission = 0x250;
         public const long LightInfluence = 0x254;
         public const long Rate = 0x260;
         public const long RotSpeed = 0x214;
         public const long Rotation = 0x21c;
         public const long Speed = 0x224;
         public const long SpreadAngle = 0x22c;
         public const long Texture = 0x1d8;
         public const long TimeScale = 0x274;
         public const long VelocityInheritance = 0x278;
         public const long ZOffset = 0x27c;
=======
        public const long Acceleration = 0x1e0;
        public const long Brightness = 0x21c;
        public const long Drag = 0x220;
        public const long Lifetime = 0x1f4;
        public const long LightEmission = 0x238;
        public const long LightInfluence = 0x23c;
        public const long Rate = 0x248;
        public const long RotSpeed = 0x1fc;
        public const long Rotation = 0x204;
        public const long Speed = 0x20c;
        public const long SpreadAngle = 0x214;
        public const long Texture = 0x1c0;
        public const long TimeScale = 0x25c;
        public const long VelocityInheritance = 0x260;
        public const long ZOffset = 0x264;
>>>>>>> Stashed changes
    }
    public static class Player {
<<<<<<< Updated upstream
         public const long AccountAge = 0x34c;
         public const long CameraMode = 0x35c;
         public const long DisplayName = 0x150;
         public const long HealthDisplayDistance = 0x37c;
         public const long LocalPlayer = 0x148;
         public const long LocaleId = 0x130;
         public const long MaxZoomDistance = 0x354;
         public const long MinZoomDistance = 0x358;
         public const long ModelInstance = 0x3d0;
         public const long Mouse = 0x1188;
         public const long NameDisplayDistance = 0x38c;
         public const long Team = 0x2d0;
         public const long TeamColor = 0x398;
         public const long UserId = 0x2f8;
=======
        public const long AccountAge = 0x35c;
        public const long CameraMode = 0x370;
        public const long DisplayName = 0x138;
        public const long HealthDisplayDistance = 0x390;
        public const long LocalPlayer = 0x130;
        public const long LocaleId = 0x118;
        public const long MaxZoomDistance = 0x368;
        public const long MinZoomDistance = 0x36c;
        public const long ModelInstance = 0x298;
        public const long Mouse = 0x11e0;
        public const long NameDisplayDistance = 0x3a0;
        public const long Team = 0x2d8;
        public const long TeamColor = 0x3ac;
        public const long UserId = 0xd0;
>>>>>>> Stashed changes
    }
    public static class PlayerConfigurer {
        public const long Pointer = 0x0;
    }
    public static class PlayerMouse {
<<<<<<< Updated upstream
         public const long Icon = 0xe0;
         public const long Workspace = 0x168;
=======
        public const long Icon = 0xc8;
        public const long Workspace = 0x150;
>>>>>>> Stashed changes
    }
    public static class Primitive {
        public const long AssemblyAngularVelocity = 0x104;
        public const long AssemblyLinearVelocity = 0xf8;
        public const long Flags = 0x1b6;
        public const long Material = 0x0;
        public const long Owner = 0x208;
        public const long Position = 0xec;
        public const long Rotation = 0xc8;
        public const long Size = 0x1b8;
        public const long Validate = 0x6;
        public const long Properties = 0xa0;
        public const long PropertyPosition = 0x90;
    }
    public static class PrimitiveFlags {
        public const long Anchored = 0x2;
        public const long CanCollide = 0x8;
        public const long CanQuery = 0x20;
        public const long CanTouch = 0x10;
    }
    public static class ProximityPrompt {
<<<<<<< Updated upstream
         public const long ActionText = 0xc8;
         public const long Enabled = 0x14e;
         public const long GamepadKeyCode = 0x134;
         public const long HoldDuration = 0x138;
         public const long KeyCode = 0x13c;
         public const long MaxActivationDistance = 0x140;
         public const long ObjectText = 0xe8;
         public const long RequiresLineOfSight = 0x14f;
=======
        public const long ActionText = 0xb0;
        public const long Enabled = 0x136;
        public const long GamepadKeyCode = 0x11c;
        public const long HoldDuration = 0x120;
        public const long KeyCode = 0x124;
        public const long MaxActivationDistance = 0x128;
        public const long ObjectText = 0xd0;
        public const long RequiresLineOfSight = 0x137;
>>>>>>> Stashed changes
    }
    public static class RenderJob {
        public const long FakeDataModel = 0x38;
        public const long RealDataModel = 0x1c8;
        public const long RenderView = 0x1d0;
    }
    public static class RenderView {
        public const long DeviceD3D11 = 0x8;
        public const long LightingValid = 0x150;
        public const long SkyValid = 0x28d;
        public const long VisualEngine = 0x10;
    }
    public static class RunService {
<<<<<<< Updated upstream
         public const long HeartbeatFPS = 0xb8;
         public const long HeartbeatTask = 0xf8;
=======
        public const long HeartbeatFPS = 0xf4;
        public const long HeartbeatTask = 0x3b8;
>>>>>>> Stashed changes
    }
    public static class Script {
<<<<<<< Updated upstream
         public const long ByteCode = 0x1a8;
         public const long GUID = 0xe8;
         public const long Hash = 0x1b8;
=======
        public const long ByteCode = 0x190;
        public const long GUID = 0xd0;
        public const long Hash = 0x1a0;
>>>>>>> Stashed changes
    }
    public static class ScriptContext {
        public const long RequireBypass = 0x0;
        public const long LuaState = 0x28;
        public const long LuaState2 = 0x28;
        public const long LuaStateAlt = 0xe8;
        public const long VmEncryptedLuaState = 0xd0;
        public const long VmWrapper = 0x220;
        public const long VmWrapper2 = 0x528;
        public const long VmWrapperBig = 0x440;
    }
    public static class Seat {
<<<<<<< Updated upstream
         public const long Occupant = 0x218;
=======
        public const long Occupant = 0x1b0;
>>>>>>> Stashed changes
    }
    public static class Sky {
<<<<<<< Updated upstream
         public const long MoonAngularSize = 0x25c;
         public const long MoonTextureId = 0xe0;
         public const long SkyboxBk = 0x110;
         public const long SkyboxDn = 0x140;
         public const long SkyboxFt = 0x170;
         public const long SkyboxLf = 0x1a0;
         public const long SkyboxOrientation = 0x250;
         public const long SkyboxRt = 0x1d0;
         public const long SkyboxUp = 0x200;
         public const long StarCount = 0x260;
         public const long SunAngularSize = 0x254;
         public const long SunTextureId = 0x230;
=======
        public const long MoonAngularSize = 0x244;
        public const long MoonTextureId = 0xc8;
        public const long SkyboxBk = 0xf8;
        public const long SkyboxDn = 0x128;
        public const long SkyboxFt = 0x158;
        public const long SkyboxLf = 0x188;
        public const long SkyboxOrientation = 0x238;
        public const long SkyboxRt = 0x1b8;
        public const long SkyboxUp = 0x1e8;
        public const long StarCount = 0x248;
        public const long SunAngularSize = 0x23c;
        public const long SunTextureId = 0x218;
>>>>>>> Stashed changes
    }
    public static class Sound {
<<<<<<< Updated upstream
         public const long Looped = 0x155;
         public const long PlaybackSpeed = 0x134;
         public const long RollOffMaxDistance = 0x138;
         public const long RollOffMinDistance = 0x13c;
         public const long SoundGroup = 0x100;
         public const long SoundId = 0xe0;
         public const long Volume = 0x148;
=======
        public const long IsPlaying = 0x140;
        public const long Looped = 0x13d;
        public const long PlaybackSpeed = 0x11c;
        public const long RollOffMaxDistance = 0x120;
        public const long RollOffMinDistance = 0x124;
        public const long SoundGroup = 0xe8;
        public const long SoundId = 0xc8;
        public const long Volume = 0x130;
>>>>>>> Stashed changes
    }
    public static class SpawnLocation {
<<<<<<< Updated upstream
         public const long AllowTeamChangeOnTouch = 0x3d;
         public const long Enabled = 0x1f1;
         public const long ForcefieldDuration = 0x1e8;
         public const long Neutral = 0x1f2;
         public const long TeamColor = 0x1ec;
=======
        public const long AllowTeamChangeOnTouch = 0x3d;
        public const long Enabled = 0x189;
        public const long ForcefieldDuration = 0x180;
        public const long Neutral = 0xad;
        public const long TeamColor = 0x184;
>>>>>>> Stashed changes
    }
    public static class SpecialMesh {
<<<<<<< Updated upstream
         public const long MeshId = 0x110;
         public const long Scale = 0xdc;
=======
        public const long MeshId = 0xf8;
        public const long Scale = 0xc4;
        public const long Offset = 0xb8;
>>>>>>> Stashed changes
    }
    public static class StatsItem {
        public const long Value = 0xc8;
    }
    public static class SunRaysEffect {
<<<<<<< Updated upstream
         public const long Enabled = 0xc8;
         public const long Intensity = 0xd0;
         public const long Spread = 0xd4;
=======
        public const long Enabled = 0xb0;
        public const long Intensity = 0xb8;
        public const long Spread = 0xbc;
>>>>>>> Stashed changes
    }
    public static class SurfaceAppearance {
<<<<<<< Updated upstream
         public const long AlphaMode = 0x2a0;
         public const long Color = 0x288;
         public const long ColorMap = 0xe0;
         public const long EmissiveMaskContent = 0x110;
         public const long EmissiveStrength = 0x2a4;
         public const long EmissiveTint = 0x294;
         public const long MetalnessMap = 0x140;
         public const long NormalMap = 0x170;
         public const long RoughnessMap = 0x1a0;
=======
        public const long AlphaMode = 0x290;
        public const long Color = 0x278;
        public const long ColorMap = 0xc8;
        public const long EmissiveMaskContent = 0xf8;
        public const long EmissiveStrength = 0x294;
        public const long EmissiveTint = 0x284;
        public const long MetalnessMap = 0x128;
        public const long NormalMap = 0x158;
        public const long RoughnessMap = 0x188;
>>>>>>> Stashed changes
    }
    public static class TaskScheduler {
<<<<<<< Updated upstream
         public const long JobEnd = 0xd0;
         public const long JobName = 0x18;
         public const long JobStart = 0xc8;
         public const long MaxFPS = 0xb0;
         public const long Pointer = 0x815c668;
=======
        public const long JobEnd = 0xd0;
        public const long JobName = 0x18;
        public const long JobStart = 0xc8;
        public const long MaxFPS = 0xb0;
        public const long Pointer = 0x84a58e0;
>>>>>>> Stashed changes
    }
    public static class Team {
<<<<<<< Updated upstream
         public const long BrickColor = 0xd0;
=======
        public const long BrickColor = 0xb8;
>>>>>>> Stashed changes
    }
    public static class Terrain {
<<<<<<< Updated upstream
         public const long GrassLength = 0x1f0;
         public const long MaterialColors = 0x4a0;
         public const long WaterColor = 0x1e0;
         public const long WaterReflectance = 0x1f8;
         public const long WaterTransparency = 0x1fc;
         public const long WaterWaveSize = 0x200;
         public const long WaterWaveSpeed = 0x204;
=======
        public const long GrassLength = 0x188;
        public const long MaterialColors = 0x430;
        public const long WaterColor = 0x178;
        public const long WaterReflectance = 0x190;
        public const long WaterTransparency = 0x194;
        public const long WaterWaveSize = 0x198;
        public const long WaterWaveSpeed = 0x19c;
>>>>>>> Stashed changes
    }
    public static class Textures {
<<<<<<< Updated upstream
         public const long Decal_Texture = 0x198;
         public const long Texture_Texture = 0x198;
=======
        public const long Decal_Texture = 0x180;
        public const long Texture_Texture = 0x180;
>>>>>>> Stashed changes
    }
    public static class Tool {
<<<<<<< Updated upstream
         public const long CanBeDropped = 0x1f5;
         public const long Enabled = 0x4c1;
         public const long Grip = 0x4b4;
         public const long ManualActivationOnly = 0x4c2;
         public const long RequiresHandle = 0x4c3;
         public const long TextureId = 0x368;
         public const long Tooltip = 0x470;
=======
        public const long CanBeDropped = 0x4b8;
        public const long Enabled = 0x165;
        public const long Grip = 0x4ac;
        public const long ManualActivationOnly = 0x4ba;
        public const long RequiresHandle = 0x4bb;
        public const long TextureId = 0x360;
        public const long Tooltip = 0x468;
>>>>>>> Stashed changes
    }
    public static class UnionOperation {
<<<<<<< Updated upstream
         public const long AssetId = 0x2f0;
=======
        public const long AssetId = 0x2a8;
>>>>>>> Stashed changes
    }
    public static class UserInputService {
<<<<<<< Updated upstream
         public const long WindowInputState = 0x2d8;
=======
        public const long WindowInputState = 0x2c0;
>>>>>>> Stashed changes
    }
    public static class VehicleSeat {
<<<<<<< Updated upstream
         public const long MaxSpeed = 0x230;
         public const long SteerFloat = 0x238;
         public const long ThrottleFloat = 0x240;
         public const long Torque = 0x244;
         public const long TurnSpeed = 0x248;
=======
        public const long MaxSpeed = 0x1c8;
        public const long SteerFloat = 0x1d0;
        public const long ThrottleFloat = 0x1d8;
        public const long Torque = 0x1dc;
        public const long TurnSpeed = 0x1e0;
>>>>>>> Stashed changes
    }
    public static class VisualEngine {
<<<<<<< Updated upstream
         public const long Dimensions = 0xab0;
         public const long FakeDataModel = 0xa90;
         public const long Pointer = 0x82ea3f8;
         public const long RenderView = 0xbb8;
         public const long ViewMatrix = 0x150;
=======
        public const long Dimensions = 0xae0;
        public const long FakeDataModel = 0xac0;
        public const long Pointer = 0x8818f60;
        public const long RenderView = 0xbf0;
        public const long ViewMatrix = 0x180;
>>>>>>> Stashed changes
    }
    public static class Weld {
<<<<<<< Updated upstream
         public const long Part0 = 0x130;
         public const long Part1 = 0x140;
=======
        public const long Part0 = 0x118;
        public const long Part1 = 0x128;
>>>>>>> Stashed changes
    }
    public static class WeldConstraint {
<<<<<<< Updated upstream
         public const long Part0 = 0xd0;
         public const long Part1 = 0xe0;
=======
        public const long Part0 = 0xb8;
        public const long Part1 = 0xc8;
>>>>>>> Stashed changes
    }
    public static class WindowInputState {
        public const long CapsLock = 0x40;
        public const long CurrentTextBox = 0x48;
    }
    public static class Workspace {
<<<<<<< Updated upstream
         public const long CurrentCamera = 0x4a8;
         public const long DistributedGameTime = 0x4c8;
         public const long ReadOnlyGravity = 0x9e8;
         public const long World = 0x400;
=======
        public const long CurrentCamera = 0x498;
        public const long DistributedGameTime = 0x4b8;
        public const long ReadOnlyGravity = 0x9b0;
        public const long World = 0x3f0;
>>>>>>> Stashed changes
    }
    public static class World {
<<<<<<< Updated upstream
         public const long AirProperties = 0x218;
         public const long FallenPartsDestroyHeight = 0x208;
         public const long Gravity = 0x210;
         public const long Primitives = 0x288;
         public const long worldStepsPerSec = 0x680;
=======
        public const long AirProperties = 0x218;
        public const long FallenPartsDestroyHeight = 0x208;
        public const long Gravity = 0x210;
        public const long Primitives = 0x288;
        public const long worldStepsPerSec = 0x700;
    }
    public static class Alloc {
        public const long Free = 0x5123fe0;
        public const long Malloc = 0x5123f60;
    }
    public static class Chat {
        public const long IsFocused = 0x154;
    }
    public static class FastClusterEntity {
        public const long AlphaByte = 0x14;
        public const long BBoxMaxX = 0xA4;
        public const long BBoxMaxY = 0xA8;
        public const long BBoxMaxZ = 0xAC;
        public const long BBoxMinX = 0x98;
        public const long BBoxMinY = 0x9C;
        public const long BBoxMinZ = 0xA0;
        public const long ContextPtr = 0x08;
        public const long DecalMaterialPtr = 0x48;
        public const long MaterialPtr = 0x20;
        public const long PrimitiveIndexArrayPtr = 0x80;
        public const long RenderQueueId = 0x10;
        public const long TechniqueArrayPtr = 0x70;
        public const long VTableRva = 0x5f381c8;
        public static class Context {
            public const long PrimitivePoolPtr = 0x1A0;
        }
        public static class PrimitivePool {
            public const long ArrayBase = 0x20;
        }
        public static class PrimitiveRecord {
            public const long Stride = 48;
            public const long Translation = 36;
        }
    }
    public static class Context {
        public const long PrimitivePoolPtr = 0x1A0;
    }
    public static class PrimitivePool {
        public const long ArrayBase = 0x20;
    }
    public static class PrimitiveRecord {
        public const long Stride = 48;
        public const long Translation = 36;
    }
    public static class LuaState {
        public const long Base = 0x40;
        public const long Global = 0x28;
        public const long Top = 0x38;
        public const long TypeTag = 0x0;
    }
    public static class Luau {
        public const long collectgarbage_wrap = 0x1e76340;
        public const long loadstring = 0x1e76ad0;
        public const long lua_gc = 0x4b61f60;
        public const long lua_getfield = 0x4b622b0;
        public const long lua_getglobal = 0x0;
        public const long lua_getmetatable = 0x4b623c0;
        public const long lua_gettop = 0x4b625f0;
        public const long lua_pcall = 0x4b69c60;
        public const long lua_pushboolean = 0x4b62fb0;
        public const long lua_pushlstring = 0x4b63370;
        public const long lua_pushnil = 0x4b63430;
        public const long lua_pushnumber = 0x4b63490;
        public const long lua_pushstring = 0x4b63510;
        public const long lua_pushvalue = 0x4b63740;
        public const long lua_rawget = 0x4b63a40;
        public const long lua_rawgetfield = 0x4b63ae0;
        public const long lua_rawset = 0x4b63f90;
        public const long lua_setfield = 0x4b64870;
        public const long lua_settop = 0x4b64bc0;
        public const long lua_tolstring = 0x4b64ff0;
        public const long luaC_fullgc = 0x4b6cb50;
        public const long luaC_step = 0x4b6cd20;
        public const long luaC_step_work = 0x4b6c800;
        public const long luaL_getmetafield = 0x4b69650;
        public const long pcall_wrap = 0x4b7e4b0;
        public const long print_wrap = 0x1e771f0;
        public const long require = 0x1e772e0;
        public const long require_impl = 0x0;
    }
    public static class LuauGlobal {
        public const long currentwhite = 0x48;
        public const long gcopages = 0x90;
        public const long gcopages_end = 0x78;
        public const long gcopages_large = 0xa0;
        public const long gcopages_sizeclass = 0xa8;
        public const long gcpause = 0x18;
        public const long gcstate = 0x49;
        public const long gcstepmul = 0x14;
        public const long gcstepsize = 0x10;
        public const long GCthreshold = 0x0;
        public const long gray = 0x28;
        public const long grayagain = 0x30;
        public const long page_next_all = 0x8;
        public const long page_next_free = 0x18;
        public const long strt_hash = 0x40;
        public const long strt_size = 0x38;
        public const long totalbytes = 0x8;
        public const long weak = 0x20;
    }
    public static class MaterialLayer {
        public const long ColorData = 0x24;
        public const long FillModeByte = 0x11;
        public const long Flags2 = 0x20;
        public const long MatFlags = 0x18;
        public const long Param = 0x1C;
        public const long Stride = 136;
    }
    public static class Reflection {
        public const long ClassDescCreatable = 0x10;
        public const long ClassDescFlags = 0x1bc;
        public const long CreatorTable = 0x8392d10;
        public const long EntryValue = 0x8;
        public const long NameRegistry = 0x85c7398;
        public const long NameTable = 0x50;
        public const long TableEmpty = 0x20;
        public const long TableEnd = 0x8;
        public const long TableStart = 0x0;
        public const long TableStride = 0x10;
    }
    public static class RenderQueue {
        public const long AlwaysOnTop = 13;
        public const long AlwaysOnTopAdorns = 14;
        public const long Decals = 2;
        public const long Glass = 8;
        public const long GlassTint = 7;
        public const long OnTopReadOnlyDepth = 12;
        public const long OnTopWithDepth = 11;
        public const long Opaque = 0;
        public const long OpaqueAdorns = 4;
        public const long OpaqueCasters = 3;
        public const long OpaqueWithAlpha = 5;
        public const long Screen = 15;
        public const long ScreenOnTopOfBlur = 16;
        public const long Terrain = 1;
        public const long Transparent = 9;
        public const long TransparentCasters = 10;
        public const long Water = 6;
    }
    public static class TechniqueArray {
        public const long BeginOffset = 0x0;
        public const long EndOffset = 0x8;
    }
    public static class WorldRoot {
        public const long RaycastBoundDesc = 0x8091390;
        public const long RaycastBoundFn = 0x80;
>>>>>>> Stashed changes
    }
}
