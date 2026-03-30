using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using NLua;
using Offsets;
using FoulzExternal.SDK;
using FoulzExternal.SDK.structures;
using FoulzExternal.SDK.worldtoscreen;
using FoulzExternal.storage;
using SDKInst = FoulzExternal.SDK.Instance;

namespace FoulzExternal.features.games.universal.scriptrunner
{
    // ─────────────────────────────────────────────────────────────────────────
    // Value types exposed to Lua
    // ─────────────────────────────────────────────────────────────────────────

    public class LuaVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public LuaVector3(float x = 0, float y = 0, float z = 0) { X = x; Y = y; Z = z; }
        public static LuaVector3 New(float x = 0, float y = 0, float z = 0) => new(x, y, z);

        public float Magnitude() => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public LuaVector3 Normalize() { var m = Magnitude(); return m > 0 ? new(X / m, Y / m, Z / m) : new(0, 0, 1); }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    public class LuaVector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public LuaVector2(float x = 0, float y = 0) { X = x; Y = y; }
        public static LuaVector2 New(float x = 0, float y = 0) => new(x, y);

        public override string ToString() => $"({X}, {Y})";
    }

    public class LuaColor3
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }

        public LuaColor3(float r = 1, float g = 1, float b = 1) { R = r; G = g; B = b; }
        public static LuaColor3 New(float r, float g, float b) => new(r, g, b);
        public static LuaColor3 FromRGB(float r, float g, float b) => new(r / 255f, g / 255f, b / 255f);

        public override string ToString() => $"({R}, {G}, {B})";
    }

    public class LuaCFrame
    {
        private readonly LuaVector3 _position;
        private readonly LuaVector3 _right;
        private readonly LuaVector3 _up;
        private readonly LuaVector3 _look;

        public LuaCFrame(
            LuaVector3 position,
            LuaVector3? right = null,
            LuaVector3? up = null,
            LuaVector3? look = null)
        {
            _position = position;
            _right = right ?? new LuaVector3(1, 0, 0);
            _up = up ?? new LuaVector3(0, 1, 0);
            _look = look ?? new LuaVector3(0, 0, -1);
        }

        public object Position => ScriptEngine.WrapVector3(_position.X, _position.Y, _position.Z);
        public object RightVector => ScriptEngine.WrapVector3(_right.X, _right.Y, _right.Z);
        public object UpVector => ScriptEngine.WrapVector3(_up.X, _up.Y, _up.Z);
        public object LookVector => ScriptEngine.WrapVector3(_look.X, _look.Y, _look.Z);
        public float X => _position.X;
        public float Y => _position.Y;
        public float Z => _position.Z;

        internal static LuaCFrame Identity() => new(new LuaVector3());

        internal static LuaCFrame FromPosition(LuaVector3 position) => new(position);

        internal static LuaCFrame LookAt(LuaVector3 position, LuaVector3 target, LuaVector3? up = null)
        {
            var native = sCFrame.LookAt(
                new Vector3 { x = position.X, y = position.Y, z = position.Z },
                new Vector3 { x = target.X, y = target.Y, z = target.Z },
                new Vector3 { x = up?.X ?? 0f, y = up?.Y ?? 1f, z = up?.Z ?? 0f });
            return FromNative(native);
        }

        internal static LuaCFrame FromNative(sCFrame native)
        {
            return new LuaCFrame(
                new LuaVector3(native.x, native.y, native.z),
                new LuaVector3(native.r00, native.r10, native.r20),
                new LuaVector3(native.r01, native.r11, native.r21),
                new LuaVector3(-native.r02, -native.r12, -native.r22));
        }

        internal sCFrame ToNative()
        {
            return new sCFrame
            {
                r00 = _right.X,
                r10 = _right.Y,
                r20 = _right.Z,
                r01 = _up.X,
                r11 = _up.Y,
                r21 = _up.Z,
                r02 = -_look.X,
                r12 = -_look.Y,
                r22 = -_look.Z,
                x = _position.X,
                y = _position.Y,
                z = _position.Z,
            };
        }

        public override string ToString() => $"CFrame({X}, {Y}, {Z})";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drawing layer
    // ─────────────────────────────────────────────────────────────────────────

    public class LuaDrawingObject
    {
        private static int _idGen;
        internal readonly int Id = System.Threading.Interlocked.Increment(ref _idGen);

        private LuaVector2 _position = new(0, 0);
        private LuaVector2 _from = new(0, 0);
        private LuaVector2 _to = new(100, 100);
        private LuaVector2 _pointA = new(0, 0);
        private LuaVector2 _pointB = new(50, 100);
        private LuaVector2 _pointC = new(100, 0);

        public string DrawType { get; }
        public string Type => DrawType;
        public bool   Visible     { get; set; } = false;
        public LuaColor3 Color    { get; set; } = new LuaColor3(1, 1, 1);
        public float  Transparency { get; set; } = 0f;  // 0 = fully opaque
        public float  Thickness   { get; set; } = 1f;
        public int    ZIndex      { get; set; } = 0;
        public int    Font        { get; set; } = 0;

        // Square / Text / Circle — shared position (Circle uses this as its center)
        public object Position
        {
            get => ScriptEngine.WrapVector2(_position.X, _position.Y);
            set => _position = ScriptEngine.CoerceVector2(value, _position);
        }
        internal LuaVector2 PositionVec => _position;

        // Size: LuaVector2 for Square, float for Text font size
        private LuaVector2 _sizeVec = new LuaVector2(100, 100);
        internal LuaVector2 SizeVec => _sizeVec;
        public object? Size
        {
            get => DrawType == "Text" ? (object)FontSize : ScriptEngine.WrapVector2(_sizeVec.X, _sizeVec.Y);
            set
            {
                if (DrawType == "Text") FontSize = Convert.ToSingle(value ?? 13f);
                else _sizeVec = ScriptEngine.CoerceVector2(value, _sizeVec);
            }
        }

        public bool  Filled        { get; set; } = false;
        public float Corner        { get; set; } = 0f;

        // Circle
        public float Radius        { get; set; } = 50f;
        public int   NumSides      { get; set; } = 32;

        // Line
        public object From
        {
            get => ScriptEngine.WrapVector2(_from.X, _from.Y);
            set => _from = ScriptEngine.CoerceVector2(value, _from);
        }
        internal LuaVector2 FromVec => _from;

        public object To
        {
            get => ScriptEngine.WrapVector2(_to.X, _to.Y);
            set => _to = ScriptEngine.CoerceVector2(value, _to);
        }
        internal LuaVector2 ToVec => _to;

        // Text
        public string Text         { get; set; } = "";
        public float  FontSize     { get; set; } = 13f;
        public bool   Center       { get; set; } = false;   // center-align horizontally
        public bool   Outline      { get; set; } = false;
        public LuaColor3 OutlineColor { get; set; } = new LuaColor3(0, 0, 0);

        // Triangle
        public object PointA
        {
            get => ScriptEngine.WrapVector2(_pointA.X, _pointA.Y);
            set => _pointA = ScriptEngine.CoerceVector2(value, _pointA);
        }
        internal LuaVector2 PointAVec => _pointA;

        public object PointB
        {
            get => ScriptEngine.WrapVector2(_pointB.X, _pointB.Y);
            set => _pointB = ScriptEngine.CoerceVector2(value, _pointB);
        }
        internal LuaVector2 PointBVec => _pointB;

        public object PointC
        {
            get => ScriptEngine.WrapVector2(_pointC.X, _pointC.Y);
            set => _pointC = ScriptEngine.CoerceVector2(value, _pointC);
        }
        internal LuaVector2 PointCVec => _pointC;

        public LuaDrawingObject(string drawType) { DrawType = drawType; }

        public void Remove()
        {
            Visible = false;
            ScriptDrawingLayer.Remove(Id);
        }
    }

    // Factory that Lua calls as Drawing.new("Square")
    public class LuaDrawingFactory
    {
        public LuaDrawingObject New(string drawType)
        {
            var obj = new LuaDrawingObject(drawType);
            ScriptDrawingLayer.Add(obj);
            return obj;
        }
    }

    public static class ScriptDrawingLayer
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, LuaDrawingObject> _objs = new();

        internal static void Add(LuaDrawingObject o) => _objs[o.Id] = o;
        internal static void Remove(int id)          => _objs.TryRemove(id, out _);
        public   static void Clear()                 => _objs.Clear();
        public   static bool HasObjects              => !_objs.IsEmpty;
        public   static IEnumerable<LuaDrawingObject> Snapshot() => _objs.Values;

        // ABGR uint for ImGui
        public static uint ToImColor(LuaColor3 c, float alpha)
        {
            byte r = (byte)Math.Clamp(c.R * 255f, 0, 255);
            byte g = (byte)Math.Clamp(c.G * 255f, 0, 255);
            byte b = (byte)Math.Clamp(c.B * 255f, 0, 255);
            byte a = (byte)Math.Clamp((1f - alpha) * 255f, 0, 255);
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Instance wrapper — mirrors the Matcha LuaVM surface
    // ─────────────────────────────────────────────────────────────────────────

    public class LuaInstance
    {
        private static int _tblSeq;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _attributes = new();
        internal readonly SDKInst _inst;

        public LuaInstance(SDKInst inst)  { _inst = inst; }
        public LuaInstance(long address)  { _inst = new SDKInst(address); }

        // ── Core properties ──────────────────────────────────────────────────

        public bool   IsValid   => _inst.IsValid;
        public long   Address   => _inst.Address;
        public string Name      { get { try { return _inst.GetName();  } catch { return ""; } } }
        public string ClassName { get { try { return _inst.GetClass(); } catch { return ""; } } }
        public string JobId     { get { try { return LuaHelpers.ReadRobloxString(_inst.Address + Offsets.DataModel.JobId); } catch { return ""; } } }

        public LuaInstance? Parent
        {
            get
            {
                try
                {
                    long a = SDKInst.Mem.ReadPtr(_inst.Address + Offsets.Instance.Parent);
                    return a > 0x1000 ? new LuaInstance(a) : null;
                }
                catch { return null; }
            }
        }

        // ── DataModel / service shortcuts ────────────────────────────────────

        public long GameId  { get { try { return _inst.GetGameID();  } catch { return 0; } } }
        public long PlaceId { get { try { return _inst.GetPlaceID(); } catch { return 0; } } }

        public object? GetService(string name)
        {
            try
            {
                // Validate state before proceeding
                if (!_inst.IsValid)
                {
                    ScriptEngine.Output.Enqueue(($"[GetService] Instance is invalid when getting service: {name}", "error"));
                    return null;
                }

                switch (name)
                {
                    case "Players":
                    {
                        var players = Storage.PlayersInstance;
                        if (players.IsValid) return new LuaInstance(players);
                        var svc = _inst.FindFirstChildOfClass("Players");
                        if (!svc.IsValid) svc = _inst.FindFirstChild("Players");
                        return svc.IsValid ? new LuaInstance(svc) : null;
                    }
                    case "Workspace":
                    {
                        var ws = Storage.WorkspaceInstance;
                        if (ws.IsValid) return new LuaInstance(ws);
                        var svc = _inst.FindFirstChildOfClass("Workspace");
                        if (!svc.IsValid) svc = _inst.FindFirstChild("Workspace");
                        return svc.IsValid ? new LuaInstance(svc) : null;
                    }
                    case "ReplicatedStorage":
                    {
                        var svc = _inst.FindFirstChildOfClass("ReplicatedStorage");
                        if (!svc.IsValid) svc = _inst.FindFirstChild("ReplicatedStorage");
                        return svc.IsValid ? new LuaInstance(svc) : null;
                    }
                    case "Lighting":
                    {
                        var svc = _inst.FindFirstChildOfClass("Lighting");
                        if (!svc.IsValid) svc = _inst.FindFirstChild("Lighting");
                        return svc.IsValid ? new LuaInstance(svc) : null;
                    }
                    case "UserInputService":
                    {
                        var lua = ScriptEngine.ActiveState;
                        if (lua != null)
                        {
                            var service = lua["UserInputService"];
                            if (service != null) return service;
                        }
                        return LuaUserInputService.Shared;
                    }
                    case "RunService":
                    {
                        var lua = ScriptEngine.ActiveState;
                        if (lua != null)
                        {
                            var service = lua["RunService"];
                            if (service != null) return service;
                        }
                        return LuaRunService.Shared;
                    }
                    case "HttpService":
                        return new LuaHttpService();
                    default: return FindFirstChild(name);
                }
            }
            catch (Exception ex)
            {
                ScriptEngine.Output.Enqueue(($"[GetService] Error getting service '{name}': {ex.Message}", "error"));
                return null;
            }
        }

        // ── Instance-tree navigation ─────────────────────────────────────────

        public LuaInstance? FindFirstChild(string name)
        {
            try { var f = _inst.FindFirstChild(name); return f.IsValid ? new LuaInstance(f) : null; }
            catch { return null; }
        }

        // Dynamic property access via indexer - supports Matcha-style property notation
        // Returns child instances by name, enabling code like instance.CarCollection
        public object? this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return null;
                
                // Try to find as a child instance
                try
                {
                    return FindFirstChild(key);
                }
                catch { return null; }
            }
        }

        public LuaInstance? FindFirstChildOfClass(string cls)
        {
            try { var f = _inst.FindFirstChildOfClass(cls); return f.IsValid ? new LuaInstance(f) : null; }
            catch { return null; }
        }

        public LuaInstance? FindFirstChildWhichIsA(string cls) => FindFirstChildOfClass(cls);

        public bool IsA(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var className = ClassName;
            if (string.Equals(className, name, StringComparison.OrdinalIgnoreCase))
                return true;

            return name switch
            {
                "Instance" => true,
                "BasePart" => className is "Part" or "MeshPart" or "TrussPart" or "WedgePart" or "UnionOperation" or "Seat" or "VehicleSeat",
                "GuiObject" => className.Contains("Gui", StringComparison.OrdinalIgnoreCase) || className.Contains("Label", StringComparison.OrdinalIgnoreCase) || className.Contains("Button", StringComparison.OrdinalIgnoreCase),
                "ValueBase" => className is "ObjectValue" or "Color3Value" or "NumberValue" or "IntValue" or "FloatValue" or "BoolValue" or "StringValue",
                _ => false,
            };
        }

        public string GetFullName()
        {
            try
            {
                var names = new List<string>();
                LuaInstance? current = this;
                while (current != null && current.Address > 0x1000)
                {
                    names.Add(current.Name);
                    current = current.Parent;
                }
                names.Reverse();
                return string.Join('.', names);
            }
            catch { return Name; }
        }

        // Returns a proper 1-based Lua table so ipairs() works
        public LuaTable GetChildren()
        {
            var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
            List<SDKInst> list; try { list = _inst.GetChildren(); } catch { list = new(); }
            return BuildTable(lua, list, c => new LuaInstance(c));
        }

        public LuaTable GetDescendants()
        {
            var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
            var all = new List<LuaInstance>();
            try { Recurse(_inst, all, 0); } catch { }
            return BuildTable(lua, all, x => x);
        }

        public LuaTable GetAttributes()
        {
            var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
            var values = new Dictionary<string, object?>();

            try
            {
                long attrContainer = SDKInst.Mem.ReadPtr(_inst.Address + Offsets.Instance.AttributeContainer);
                long attrList = attrContainer != 0 ? SDKInst.Mem.ReadPtr(attrContainer + Offsets.Instance.AttributeList) : 0;
                if (attrList != 0)
                {
                    for (int i = 0; i < 0x1000; i += (int)Offsets.Instance.AttributeToNext)
                    {
                        long namePtr = SDKInst.Mem.ReadPtr(attrList + i);
                        string attrName = LuaHelpers.ReadRobloxString(namePtr);
                        if (string.IsNullOrEmpty(attrName))
                            break;
                        values[attrName] = LuaHelpers.ParseAttributeValue(_inst.GetAttribute(attrName));
                    }
                }
            }
            catch { }

            return ScriptEngine.CreateLuaMapTable(lua, values);
        }

        private static void Recurse(SDKInst inst, List<LuaInstance> list, int depth)
        {
            if (depth > 8) return;
            List<SDKInst> kids; try { kids = inst.GetChildren(); } catch { return; }
            foreach (var k in kids) { list.Add(new LuaInstance(k)); Recurse(k, list, depth + 1); }
        }

        public bool IsDescendantOf(LuaInstance parent)
        {
            try
            {
                var cur = Parent;
                while (cur != null && cur.Address > 0x1000)
                {
                    if (cur.Address == parent.Address) return true;
                    cur = cur.Parent;
                }
                return false;
            }
            catch { return false; }
        }

        public LuaInstance? WaitForChild(string name, double timeout = 5.0)
        {
            var until = DateTime.UtcNow.AddSeconds(timeout);
            while (DateTime.UtcNow < until)
            {
                var f = FindFirstChild(name);
                if (f != null) return f;
                Thread.Sleep(50);
            }
            return null;
        }

        // ── Players service ───────────────────────────────────────────────────

        public LuaInstance? LocalPlayer
        {
            get
            {
                try { var lp = Storage.LocalPlayerInstance; return lp.IsValid ? new LuaInstance(lp) : null; }
                catch { return null; }
            }
        }

        public LuaTable GetPlayers()
        {
            var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
            List<SDKInst> kids; try { kids = _inst.GetChildren(); } catch { kids = new(); }
            var players = new List<SDKInst>();
            foreach (var k in kids) { try { if (k.GetClass() == "Player") players.Add(k); } catch { } }
            return BuildTable(lua, players, p => new LuaInstance(p));
        }

        public LuaEvent PlayerAdded => ScriptPlayersEvents.Shared.PlayerAdded;
        public LuaEvent PlayerRemoving => ScriptPlayersEvents.Shared.PlayerRemoving;

        // ── Player ────────────────────────────────────────────────────────────

        public LuaInstance? Character
        {
            get
            {
                try { var c = _inst.GetCharacter(); return c.IsValid ? new LuaInstance(c) : null; }
                catch { return null; }
            }
        }

        public LuaInstance? Team
        {
            get
            {
                try
                {
                    long addr = SDKInst.Mem.ReadPtr(_inst.Address + Offsets.Player.Team);
                    return addr > 0x1000 ? new LuaInstance(addr) : null;
                }
                catch { return null; }
            }
        }

        public long UserId { get { try { return _inst.GetUserID(); } catch { return 0; } } }
        public LuaInstance? PrimaryPart
        {
            get
            {
                try
                {
                    long addr = SDKInst.Mem.ReadPtr(_inst.Address + Offsets.Model.PrimaryPart);
                    return addr > 0x1000 ? new LuaInstance(addr) : null;
                }
                catch { return null; }
            }
        }

        // Common child-style access used by Matcha scripts / UI libs.
        public LuaInstance? CurrentCamera
        {
            get
            {
                try
                {
                    var cam = Storage.CameraInstance;
                    return cam.IsValid ? new LuaInstance(cam) : null;
                }
                catch { return null; }
            }
        }

        public LuaInstance? Vehicles => FindFirstChild("Vehicles");
        public LuaInstance? Root => FindFirstChild("Root");
        public LuaInstance? Engine => FindFirstChild("Engine");
        public LuaInstance? Weight => FindFirstChild("Weight");
        public LuaInstance? Seat => FindFirstChild("Seat");
        public LuaInstance? PlayerName => FindFirstChild("PlayerName");
        public LuaInstance? HumanoidRootPart => FindFirstChild("HumanoidRootPart");

        // ── BasePart (primitive) properties ──────────────────────────────────

        private long Prim()
        {
            long p = SDKInst.Mem.ReadPtr(_inst.Address + Offsets.BasePart.Primitive);
            if (p == 0) throw new InvalidOperationException($"{Name} is not a BasePart");
            return p;
        }

        public object Position
        {
            get
            {
                if (ClassName == "Camera")
                {
                    try
                    {
                        var v = SDKInst.Mem.Read<Vector3>(_inst.Address + Offsets.Camera.Position);
                        return ScriptEngine.WrapVector3(v.x, v.y, v.z);
                    }
                    catch { return ScriptEngine.WrapVector3(0, 0, 0); }
                }

                var pv = SDKInst.Mem.Read<Vector3>(Prim() + Offsets.Primitive.Position);
                return ScriptEngine.WrapVector3(pv.x, pv.y, pv.z);
            }
            set
            {
                var v = ScriptEngine.CoerceVector3(value);
                if (ClassName == "Camera")
                {
                    try { SDKInst.Mem.Write(_inst.Address + Offsets.Camera.Position, new Vector3 { x = v.X, y = v.Y, z = v.Z }); }
                    catch { }
                    return;
                }

                SDKInst.Mem.Write(Prim() + Offsets.Primitive.Position, new Vector3 { x = v.X, y = v.Y, z = v.Z });
            }
        }

        public LuaCFrame CFrame
        {
            get
            {
                try
                {
                    if (ClassName == "Camera")
                    {
                        var rotation = SDKInst.Mem.Read<Matrix3x3>(_inst.Address + Offsets.Camera.Rotation);
                        var position = SDKInst.Mem.Read<Vector3>(_inst.Address + Offsets.Camera.Position);
                        return LuaCFrame.FromNative(new sCFrame
                        {
                            r00 = rotation.r00,
                            r01 = rotation.r01,
                            r02 = rotation.r02,
                            r10 = rotation.r10,
                            r11 = rotation.r11,
                            r12 = rotation.r12,
                            r20 = rotation.r20,
                            r21 = rotation.r21,
                            r22 = rotation.r22,
                            x = position.x,
                            y = position.y,
                            z = position.z,
                        });
                    }

                    return LuaCFrame.FromNative(_inst.GetCFrame());
                }
                catch { return LuaCFrame.Identity(); }
            }
            set
            {
                try
                {
                    var native = (value ?? LuaCFrame.Identity()).ToNative();
                    if (ClassName == "Camera")
                    {
                        SDKInst.Mem.Write(_inst.Address + Offsets.Camera.Position, new Vector3 { x = native.x, y = native.y, z = native.z });
                        SDKInst.Mem.Write(_inst.Address + Offsets.Camera.Rotation, new Matrix3x3
                        {
                            r00 = native.r00,
                            r01 = native.r01,
                            r02 = native.r02,
                            r10 = native.r10,
                            r11 = native.r11,
                            r12 = native.r12,
                            r20 = native.r20,
                            r21 = native.r21,
                            r22 = native.r22,
                        });
                        return;
                    }

                    SDKInst.Mem.Write(Prim() + 0x11C, native);
                }
                catch { }
            }
        }

        public LuaColor3 Color
        {
            get
            {
                try
                {
                    uint raw = SDKInst.Mem.Read<uint>(_inst.Address + Offsets.BasePart.Color3);
                    return LuaHelpers.DecodeColor(raw);
                }
                catch { return new LuaColor3(); }
            }
            set
            {
                try { SDKInst.Mem.Write(_inst.Address + Offsets.BasePart.Color3, LuaHelpers.EncodeColor(value)); }
                catch { }
            }
        }

        public object Velocity
        {
            get  { var v = SDKInst.Mem.Read<Vector3>(Prim() + Offsets.Primitive.AssemblyLinearVelocity); return ScriptEngine.WrapVector3(v.x, v.y, v.z); }
            set  { var v = ScriptEngine.CoerceVector3(value); SDKInst.Mem.Write(Prim() + Offsets.Primitive.AssemblyLinearVelocity, new Vector3 { x = v.X, y = v.Y, z = v.Z }); }
        }

        public object AssemblyLinearVelocity { get => Velocity; set => Velocity = value; }

        public object Size
        {
            get  { try { var v = SDKInst.Mem.Read<Vector3>(Prim() + Offsets.Primitive.Size); return ScriptEngine.WrapVector3(v.x, v.y, v.z); } catch { return ScriptEngine.WrapVector3(0, 0, 0); } }
            set  { try { var v = ScriptEngine.CoerceVector3(value); SDKInst.Mem.Write(Prim() + Offsets.Primitive.Size, new Vector3 { x = v.X, y = v.Y, z = v.Z }); } catch { } }
        }

        public bool CanCollide
        {
            get
            {
                try
                {
                    byte f = SDKInst.Mem.Read<byte>(Prim() + Offsets.Primitive.Flags);
                    return (f & (byte)Offsets.PrimitiveFlags.CanCollide) != 0;
                }
                catch { return true; }
            }
            set
            {
                try
                {
                    long p = Prim();
                    byte f = SDKInst.Mem.Read<byte>(p + Offsets.Primitive.Flags);
                    f = value ? (byte)(f | (byte)Offsets.PrimitiveFlags.CanCollide)
                              : (byte)(f & ~(byte)Offsets.PrimitiveFlags.CanCollide);
                    SDKInst.Mem.Write(p + Offsets.Primitive.Flags, f);
                }
                catch { }
            }
        }

        public float Transparency
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.BasePart.Transparency); } catch { return 0f; } }
            set { try { SDKInst.Mem.Write(_inst.Address + Offsets.BasePart.Transparency, value); } catch { } }
        }

        // ── Humanoid ──────────────────────────────────────────────────────────

        public float Health
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.Humanoid.Health); } catch { return 0f; } }
            set { try { SDKInst.Mem.Write(_inst.Address + Offsets.Humanoid.Health, value); } catch { } }
        }

        public float MaxHealth
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.Humanoid.MaxHealth); } catch { return 100f; } }
        }

        public float WalkSpeed
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.Humanoid.Walkspeed); } catch { return 16f; } }
            set { try { SDKInst.Mem.Write(_inst.Address + Offsets.Humanoid.Walkspeed, value); SDKInst.Mem.Write(_inst.Address + Offsets.Humanoid.WalkspeedCheck, value); } catch { } }
        }

        public float JumpPower
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.Humanoid.JumpPower); } catch { return 50f; } }
            set { try { SDKInst.Mem.Write(_inst.Address + Offsets.Humanoid.JumpPower, value); } catch { } }
        }

        // ── Camera ────────────────────────────────────────────────────────────

        public object CameraPosition
        {
            get
            {
                try
                {
                    var cam = Storage.CameraInstance;
                    if (!cam.IsValid) return ScriptEngine.WrapVector3(0, 0, 0);
                    long prim = SDKInst.Mem.ReadPtr(cam.Address + Offsets.BasePart.Primitive);
                    var v = SDKInst.Mem.Read<Vector3>(prim + Offsets.Primitive.Position);
                    return ScriptEngine.WrapVector3(v.x, v.y, v.z);
                }
                catch { return ScriptEngine.WrapVector3(0, 0, 0); }
            }
        }

        public object ViewportSize
        {
            get
            {
                try
                {
                    long cameraAddr = _inst.Address;
                    var size = SDKInst.Mem.Read<Vector2>(cameraAddr + Offsets.Camera.ViewportSize);
                    return ScriptEngine.WrapVector2(size.x, size.y);
                }
                catch { return ScriptEngine.WrapVector2(1920, 1080); }
            }
        }

        public float FieldOfView
        {
            get { try { return SDKInst.Mem.Read<float>(_inst.Address + Offsets.Camera.FieldOfView); } catch { return 70f; } }
            set { try { SDKInst.Mem.Write(_inst.Address + Offsets.Camera.FieldOfView, value); } catch { } }
        }

        // Camera:lookAt(at, lookAt) — Matcha API: mutates the camera CFrame in-place
        public void LookAt(object? at, object? lookAt)
        {
            try
            {
                var atVec = ScriptEngine.CoerceVector3(at);
                var lookAtVec = ScriptEngine.CoerceVector3(lookAt);
                var native = sCFrame.LookAt(
                    new Vector3 { x = atVec.X, y = atVec.Y, z = atVec.Z },
                    new Vector3 { x = lookAtVec.X, y = lookAtVec.Y, z = lookAtVec.Z },
                    new Vector3 { x = 0f, y = 1f, z = 0f });
                SDKInst.Mem.Write(_inst.Address + Offsets.Camera.Position, new Vector3 { x = native.x, y = native.y, z = native.z });
                SDKInst.Mem.Write(_inst.Address + Offsets.Camera.Rotation, new Matrix3x3
                {
                    r00 = native.r00, r01 = native.r01, r02 = native.r02,
                    r10 = native.r10, r11 = native.r11, r12 = native.r12,
                    r20 = native.r20, r21 = native.r21, r22 = native.r22,
                });
            }
            catch { }
        }

        public object AbsolutePosition
        {
            get
            {
                try
                {
                    var v = SDKInst.Mem.Read<Vector2>(_inst.Address + Offsets.GuiBase2D.AbsolutePosition);
                    return ScriptEngine.WrapVector2(v.x, v.y);
                }
                catch { return ScriptEngine.WrapVector2(0, 0); }
            }
        }

        public object AbsoluteSize
        {
            get
            {
                try
                {
                    var v = SDKInst.Mem.Read<Vector2>(_inst.Address + Offsets.GuiBase2D.AbsoluteSize);
                    return ScriptEngine.WrapVector2(v.x, v.y);
                }
                catch { return ScriptEngine.WrapVector2(0, 0); }
            }
        }

        public string MeshId
        {
            get
            {
                try
                {
                    if (ClassName == "MeshPart")
                        return LuaHelpers.ReadRobloxString(_inst.Address + Offsets.MeshPart.MeshId);
                    return string.Empty;
                }
                catch { return string.Empty; }
            }
        }

        public string TextureId
        {
            get
            {
                try
                {
                    if (ClassName == "MeshPart")
                        return LuaHelpers.ReadRobloxString(_inst.Address + Offsets.MeshPart.Texture);
                    return string.Empty;
                }
                catch { return string.Empty; }
            }
        }

        public string Text
        {
            get
            {
                try
                {
                    if (ClassName is "TextLabel" or "TextButton" or "TextBox")
                    {
                        string value = LuaHelpers.ReadRobloxString(_inst.Address + Offsets.GuiObject.Text);
                        if (!string.IsNullOrEmpty(value))
                            return value;
                    }

                    return _attributes.TryGetValue(Address + ":Text", out var cached) ? cached?.ToString() ?? string.Empty : string.Empty;
                }
                catch { return string.Empty; }
            }
            set
            {
                _attributes[Address + ":Text"] = value ?? string.Empty;
            }
        }

        // ── Value objects (StringValue, IntValue, BoolValue, NumberValue) ─────

        public object? Value
        {
            get
            {
                try
                {
                    string cls = ClassName;
                    if (cls == "StringValue")
                    {
                        long addr = _inst.Address + Offsets.Misc.Value;
                        int len = SDKInst.Mem.Read<int>(addr + 0x18);
                        if (len > 0 && len <= 256)
                        {
                            if (len >= 16)
                            {
                                long ptr = SDKInst.Mem.ReadPtr(addr);
                                if (ptr != 0) return SDKInst.Mem.ReadString(ptr);
                            }
                            else
                            {
                                return SDKInst.Mem.ReadString(addr);
                            }
                        }

                        return Name;
                    }
                    if (cls == "IntValue")     return (double)SDKInst.Mem.Read<int>(_inst.Address + 0x60);
                    if (cls == "NumberValue" || cls == "FloatValue")  return (double)SDKInst.Mem.Read<double>(_inst.Address + 0x60);
                    if (cls == "BoolValue")    return SDKInst.Mem.Read<bool>(_inst.Address + 0x60);
                    if (cls == "ObjectValue")  { long addr = SDKInst.Mem.ReadPtr(_inst.Address + 0x60); return addr > 0x1000 ? new LuaInstance(addr) : null; }
                    return null;
                }
                catch { return ClassName == "StringValue" ? Name : null; }
            }
            set
            {
                try
                {
                    TrySetValueObject(this, value);
                    _attributes[Address + ":Value"] = value;
                }
                catch { }
            }
        }

        public object? GetAttribute(string name)
        {
            try
            {
                var child = FindFirstChild(name);
                if (child != null)
                {
                    var childValue = child.Value;
                    if (childValue != null) return childValue;
                }

                string raw = _inst.GetAttribute(name);
                if (!string.IsNullOrEmpty(raw))
                {
                    if (bool.TryParse(raw, out var boolValue)) return boolValue;
                    if (double.TryParse(raw, out var numberValue)) return numberValue;
                    return raw;
                }

                return _attributes.TryGetValue(Address + ":" + name, out var value) ? value : null;
            }
            catch { return null; }
        }

        public void SetAttribute(string name, object? value)
        {
            try
            {
                // Always attempt to write to actual Roblox memory first so that
                // game LocalScripts reading :GetAttribute() see the updated value.
                // The attribute must already exist in memory (server has set it at
                // least once); SetAttributeValue walks the live attribute list.
                if (value != null)
                {
                    try
                    {
                        // Try as float first — covers numbers, multipliers, etc.
                        _inst.SetAttributeValue(name, Convert.ToSingle(value));
                    }
                    catch
                    {
                        // Fallback: try as int for whole-number attributes
                        try { _inst.SetAttributeValue(name, Convert.ToInt32(value)); } catch { }
                    }
                }

                // Also propagate to any child ValueObject with the same name
                var child = FindFirstChild(name);
                if (child != null)
                    TrySetValueObject(child, value);

                // Keep local cache in sync for our own GetAttribute reads
                _attributes[Address + ":" + name] = value;
            }
            catch { }
        }

        private static void TrySetValueObject(LuaInstance instance, object? value)
        {
            try
            {
                switch (instance.ClassName)
                {
                    case "IntValue":
                        SDKInst.Mem.Write(instance._inst.Address + 0x60, Convert.ToInt32(value ?? 0));
                        break;
                    case "NumberValue":
                    case "FloatValue":
                        SDKInst.Mem.Write(instance._inst.Address + 0x60, Convert.ToDouble(value ?? 0d));
                        break;
                    case "BoolValue":
                        SDKInst.Mem.Write(instance._inst.Address + 0x60, Convert.ToBoolean(value ?? false));
                        break;
                    case "ObjectValue":
                        if (value is LuaInstance obj)
                            SDKInst.Mem.Write(instance._inst.Address + 0x60, obj.Address);
                        break;
                }
            }
            catch { }
        }

        // ── HttpService ──────────────────────────────────────────────────────

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        public string HttpGet(string url)
        {
            try { return _http.GetStringAsync(url).GetAwaiter().GetResult(); }
            catch (Exception ex) { throw new InvalidOperationException($"HttpGet failed: {ex.Message}"); }
        }

        public string HttpGet(string url, object? content) => HttpGet(url);

        // ── GetMouse (stub) ───────────────────────────────────────────────────

        public LuaMouse GetMouse() => new();

        // ── Misc ──────────────────────────────────────────────────────────────

        public override string ToString() => $"Instance<{ClassName}>({Name})";

        // ── Helpers ───────────────────────────────────────────────────────────

        private static LuaTable BuildTable<T>(Lua lua, List<T> list, Func<T, LuaInstance> selector)
        {
            string key = "_t" + System.Threading.Interlocked.Increment(ref _tblSeq);
            lua.NewTable(key);
            var t = lua.GetTable(key);
            for (int i = 0; i < list.Count; i++)
                t[i + 1] = selector(list[i]);
            lua[key] = null;
            return t;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stub Mouse object returned by Player:GetMouse()
    // ─────────────────────────────────────────────────────────────────────────

    internal static class ScriptInput
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll", EntryPoint = "mouse_event")] private static extern void MouseEvent(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);
        [DllImport("user32.dll", EntryPoint = "keybd_event")] private static extern void KeybdEvent(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? windowName);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, out POINT point);

        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;
        private const uint MouseRightDown = 0x0008;
        private const uint MouseRightUp = 0x0010;
        private const uint MouseWheel = 0x0800;
        private const uint KeyEventKeyUp = 0x0002;

        public static LuaVector2 GetMousePosition()
        {
            try
            {
                if (!GetCursorPos(out var point))
                    return new LuaVector2();

                return new LuaVector2(point.X, point.Y);
            }
            catch { return new LuaVector2(); }
        }

        public static bool IsMouseButtonDown(int button) => (GetAsyncKeyState(button) & 0x8000) != 0;
        public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        public static void KeyPress(int virtualKey) => KeybdEvent((byte)virtualKey, 0, 0, UIntPtr.Zero);
        public static void KeyRelease(int virtualKey) => KeybdEvent((byte)virtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);

        public static void MouseButtonPress(int button)
        {
            MouseEvent(button == 0x01 ? MouseLeftDown : MouseRightDown, 0, 0, 0, UIntPtr.Zero);
        }

        public static void MouseButtonRelease(int button)
        {
            MouseEvent(button == 0x01 ? MouseLeftUp : MouseRightUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void MouseButtonClick(int button)
        {
            MouseButtonPress(button);
            Thread.Sleep(10);
            MouseButtonRelease(button);
        }

        public static void MoveMouseAbsolute(int x, int y)
        {
            try { SetCursorPos(x, y); }
            catch { }
        }

        public static void MoveMouseRelative(int x, int y)
        {
            try
            {
                var current = GetMousePosition();
                SetCursorPos((int)current.X + x, (int)current.Y + y);
            }
            catch { }
        }

        public static void MouseScroll(int amount)
        {
            try { MouseEvent(MouseWheel, 0, 0, (uint)(amount * 120), UIntPtr.Zero); }
            catch { }
        }

        public static bool IsRobloxFocused()
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero) return false;
                IntPtr roblox = FindWindow(null, "Roblox");
                if (roblox != IntPtr.Zero && foreground == roblox) return true;
                foreach (var process in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    if (process.MainWindowHandle != IntPtr.Zero && process.MainWindowHandle == foreground)
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static int ResolveVirtualKey(object? key)
        {
            try
            {
                if (key == null) return 0;
                return Convert.ToInt32(key);
            }
            catch { return 0; }
        }
    }

    public class LuaMouse
    {
        public float X => ScriptInput.GetMousePosition().X;
        public float Y => ScriptInput.GetMousePosition().Y;
        public object Hit => ScriptEngine.WrapVector3(0, 0, 0);
        public LuaInstance? Target { get; } = null;
        public bool Button1Down => ScriptInput.IsMouseButtonDown(0x01);
        public bool Button2Down => ScriptInput.IsMouseButtonDown(0x02);

        public override string ToString() => $"Mouse({X}, {Y})";
    }

    public class LuaConnection
    {
        private Action? _disconnect;

        public LuaConnection(Action disconnect) => _disconnect = disconnect;

        public void Disconnect()
        {
            try { _disconnect?.Invoke(); }
            finally { _disconnect = null; }
        }
    }

    public class LuaEvent
    {
        private readonly List<LuaFunction> _handlers = new();
        public bool HasConnections => _handlers.Count > 0;

        public LuaConnection Connect(LuaFunction fn)
        {
            _handlers.Add(fn);
            return new LuaConnection(() => _handlers.Remove(fn));
        }

        public void Clear() => _handlers.Clear();

        public void Fire(params object?[] args)
        {
            if (_handlers.Count == 0)
                return;

            foreach (var handler in _handlers.ToArray())
            {
                try { handler.Call(args); }
                catch (Exception ex) { ScriptEngine.Output.Enqueue(($"✗ event: {ex.Message}", "error")); }
            }
        }
    }

    public class LuaInputObject
    {
        public object? KeyCode { get; set; }
        public object? UserInputType { get; set; }
        public object? Position { get; set; }
    }

    public class LuaUserInputService
    {
        private LuaVector2 _lastMouse = new();
        private bool _lastMouse1;
        private bool _lastMouse2;

        public static LuaUserInputService Shared { get; } = new();

        public LuaEvent InputBegan { get; } = new();
        public LuaEvent InputEnded { get; } = new();
        public LuaEvent InputChanged { get; } = new();

        public bool HasConnections => InputBegan.HasConnections || InputEnded.HasConnections || InputChanged.HasConnections;

        public void Reset()
        {
            _lastMouse = ScriptInput.GetMousePosition();
            _lastMouse1 = ScriptInput.IsMouseButtonDown(0x01);
            _lastMouse2 = ScriptInput.IsMouseButtonDown(0x02);
            InputBegan.Clear();
            InputEnded.Clear();
            InputChanged.Clear();
        }

        public void Pump()
        {
            var mouse = ScriptInput.GetMousePosition();
            if (mouse.X != _lastMouse.X || mouse.Y != _lastMouse.Y)
            {
                InputChanged.Fire(new LuaInputObject
                {
                    KeyCode = 0,
                    UserInputType = 0,
                    Position = ScriptEngine.WrapVector2(mouse.X, mouse.Y),
                }, false);
                _lastMouse = mouse;
            }

            PumpMouseButton(0x01, 1, ref _lastMouse1);
            PumpMouseButton(0x02, 2, ref _lastMouse2);
        }

        private void PumpMouseButton(int virtualKey, int inputType, ref bool lastState)
        {
            bool currentState = ScriptInput.IsMouseButtonDown(virtualKey);
            if (currentState == lastState)
                return;

            var input = new LuaInputObject
            {
                KeyCode = inputType,
                UserInputType = inputType,
                Position = ScriptEngine.WrapVector2(_lastMouse.X, _lastMouse.Y),
            };

            if (currentState) InputBegan.Fire(input, false);
            else InputEnded.Fire(input, false);

            lastState = currentState;
        }

        public bool IsKeyDown(object key) => ScriptInput.IsKeyDown(ScriptInput.ResolveVirtualKey(key));

        public object GetMouseLocation()
        {
            var mouse = ScriptInput.GetMousePosition();
            return ScriptEngine.WrapVector2(mouse.X, mouse.Y);
        }
    }

    public class LuaRunService
    {
        public static LuaRunService Shared { get; } = new();

        public LuaEvent RenderStepped { get; } = new();
        public LuaEvent Heartbeat { get; } = new();
        public LuaEvent Stepped { get; } = new();

        public bool HasConnections => RenderStepped.HasConnections || Heartbeat.HasConnections || Stepped.HasConnections;

        public void Reset()
        {
            RenderStepped.Clear();
            Heartbeat.Clear();
            Stepped.Clear();
        }

        public void Pump(double deltaSeconds)
        {
            RenderStepped.Fire(deltaSeconds);
            Heartbeat.Fire(deltaSeconds);
            Stepped.Fire(deltaSeconds);
        }
    }

    public class ScriptPlayersEvents
    {
        private readonly HashSet<long> _knownPlayers = new();

        public static ScriptPlayersEvents Shared { get; } = new();

        public LuaEvent PlayerAdded { get; } = new();
        public LuaEvent PlayerRemoving { get; } = new();

        public bool HasConnections => PlayerAdded.HasConnections || PlayerRemoving.HasConnections;

        public void Reset(bool clearHandlers = false)
        {
            _knownPlayers.Clear();
            foreach (var player in SnapshotPlayers())
                _knownPlayers.Add(player.Address);

            if (clearHandlers)
            {
                PlayerAdded.Clear();
                PlayerRemoving.Clear();
            }
        }

        public void Pump()
        {
            var players = SnapshotPlayers();
            var current = new HashSet<long>();

            foreach (var player in players)
            {
                current.Add(player.Address);
                if (!_knownPlayers.Contains(player.Address))
                    PlayerAdded.Fire(new LuaInstance(player));
            }

            foreach (var address in _knownPlayers)
            {
                if (!current.Contains(address))
                    PlayerRemoving.Fire(new LuaInstance(address));
            }

            _knownPlayers.Clear();
            foreach (var address in current)
                _knownPlayers.Add(address);
        }

        private static List<SDKInst> SnapshotPlayers()
        {
            var result = new List<SDKInst>();
            try
            {
                var players = Storage.PlayersInstance;
                if (!players.IsValid)
                    return result;

                foreach (var child in players.GetChildren())
                {
                    try
                    {
                        if (child.IsValid && child.GetClass() == "Player")
                            result.Add(child);
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }
    }

    public class LuaHttpService
    {
        private static readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public string HttpGet(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL cannot be empty");
                return _http.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ScriptEngine.Output.Enqueue(($"[error] HttpGet: {ex.Message}", "error"));
                throw new InvalidOperationException($"HttpGet failed: {ex.Message}");
            }
        }

        public string HttpGet(string url, object? content) => HttpGet(url);

        public string JSONEncode(object value)
        {
            var plain = NormalizeValue(value);
            return JsonSerializer.Serialize(plain);
        }

        public object? JSONDecode(string value)
        {
            using var document = JsonDocument.Parse(value);
            return ConvertJsonElement(document.RootElement);
        }

        public string GenerateGUID() => Guid.NewGuid().ToString("D");

        private static object? NormalizeValue(object? value)
        {
            if (value == null)
                return null;

            switch (value)
            {
                case LuaTable table:
                    return NormalizeLuaTable(table);
                case LuaVector2 vec2:
                    return new Dictionary<string, object?> { ["x"] = vec2.X, ["y"] = vec2.Y };
                case LuaVector3 vec3:
                    return new Dictionary<string, object?> { ["x"] = vec3.X, ["y"] = vec3.Y, ["z"] = vec3.Z };
                case LuaColor3 color:
                    return new Dictionary<string, object?> { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B };
                case LuaInstance instance:
                    return new Dictionary<string, object?>
                    {
                        ["name"] = instance.Name,
                        ["className"] = instance.ClassName,
                        ["address"] = instance.Address,
                    };
                default:
                    return value;
            }
        }

        private static object NormalizeLuaTable(LuaTable table)
        {
            var numeric = new SortedDictionary<int, object?>();
            var keyed = new Dictionary<string, object?>();
            bool hasStringKey = false;

            foreach (var key in table.Keys)
            {
                var raw = table[key];
                if (TryGetIntegerKey(key, out int index))
                {
                    numeric[index] = NormalizeValue(raw);
                }
                else
                {
                    hasStringKey = true;
                    keyed[key?.ToString() ?? string.Empty] = NormalizeValue(raw);
                }
            }

            if (!hasStringKey && numeric.Count > 0)
            {
                var list = new List<object?>();
                foreach (var pair in numeric)
                {
                    while (list.Count < pair.Key - 1)
                        list.Add(null);
                    list.Add(pair.Value);
                }
                return list;
            }

            foreach (var pair in numeric)
                keyed[pair.Key.ToString()] = pair.Value;

            return keyed;
        }

        private static bool TryGetIntegerKey(object? key, out int index)
        {
            try
            {
                if (key != null)
                {
                    double numeric = Convert.ToDouble(key);
                    index = (int)numeric;
                    return Math.Abs(numeric - index) < double.Epsilon;
                }
            }
            catch { }

            index = 0;
            return false;
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
                    var map = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                        map[property.Name] = ConvertJsonElement(property.Value);
                    return ScriptEngine.CreateLuaMapTable(lua, map);
                }
                case JsonValueKind.Array:
                {
                    var lua = ScriptEngine.ActiveState ?? throw new InvalidOperationException("No active Lua state");
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(ConvertJsonElement(item));
                    return ScriptEngine.CreateLuaSequenceTable(lua, list);
                }
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
    }

    internal static class LuaHelpers
    {
        internal static LuaColor3 DecodeColor(uint raw)
        {
            float r = ((raw >> 16) & 0xFF) / 255f;
            float g = ((raw >> 8) & 0xFF) / 255f;
            float b = (raw & 0xFF) / 255f;
            return new LuaColor3(r, g, b);
        }

        internal static uint EncodeColor(LuaColor3 color)
        {
            uint r = (uint)Math.Clamp((int)Math.Round(color.R * 255f), 0, 255);
            uint g = (uint)Math.Clamp((int)Math.Round(color.G * 255f), 0, 255);
            uint b = (uint)Math.Clamp((int)Math.Round(color.B * 255f), 0, 255);
            return (r << 16) | (g << 8) | b;
        }

        internal static object? ParseAttributeValue(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;
            if (bool.TryParse(raw, out var boolValue))
                return boolValue;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var numberValue))
                return numberValue;
            return raw;
        }

        internal static string ReadRobloxString(long address)
        {
            if (address == 0 || SDKInst.Mem == null)
                return string.Empty;

            try
            {
                int length = SDKInst.Mem.Read<int>(address + 0x18);
                if (length <= 0 || length > 1024 * 1024)
                    return string.Empty;

                long stringAddress = length >= 16 ? SDKInst.Mem.ReadPtr(address) : address;
                if (stringAddress == 0)
                    return string.Empty;

                var bytes = new byte[length];
                for (int i = 0; i < length; i++)
                    bytes[i] = SDKInst.Mem.Read<byte>(stringAddress + i);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch { return string.Empty; }
        }
    }
}
