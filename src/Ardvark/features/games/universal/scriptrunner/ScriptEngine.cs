using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Windows;
using NLua;
using FoulzExternal.logging.notifications;
using FoulzExternal.storage;
using FoulzExternal.SDK.structures;
using FoulzExternal.SDK.worldtoscreen;
using SDKInst = FoulzExternal.SDK.Instance;

namespace FoulzExternal.features.games.universal.scriptrunner
{
    public static class ScriptEngine
    {
        // Shared across ScriptEngine + LuaInstance during execution
        internal static Lua? ActiveState;

        private static CancellationTokenSource? _cts;
        private static Thread? _thread;
        private static readonly object _lock = new();
        private static int _luaTableSequence;
        private const long ScriptSourceOffset = 0x174;
        private static readonly ConcurrentDictionary<string, string> FastFlags = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsRunning { get; private set; }
        public static bool RobloxInputEnabled { get; private set; }

        // Output queue polled by the UI: (message, level) where level = "print"|"warn"|"error"
        public static readonly ConcurrentQueue<(string text, string level)> Output = new();

        public static string ScriptsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "FoulzExternal", "scripts");

        internal static object WrapVector2(float x, float y)
        {
            return WrapLuaValue("_wrap_v2", new LuaVector2(x, y), (double)x, (double)y);
        }

        internal static object WrapVector3(float x, float y, float z)
        {
            return WrapLuaValue("_wrap_v3", new LuaVector3(x, y, z), (double)x, (double)y, (double)z);
        }

        internal static LuaVector2 CoerceVector2(object? value, LuaVector2? fallback = null)
        {
            if (value is LuaVector2 vec2)
                return vec2;

            if (value is LuaTable table)
            {
                return new LuaVector2(
                    ReadTableFloat(table, "X", "x", "_x", fallback?.X ?? 0f),
                    ReadTableFloat(table, "Y", "y", "_y", fallback?.Y ?? 0f));
            }

            return fallback ?? new LuaVector2();
        }

        internal static LuaVector3 CoerceVector3(object? value, LuaVector3? fallback = null)
        {
            if (value is LuaVector3 vec3)
                return vec3;

            if (value is LuaTable table)
            {
                return new LuaVector3(
                    ReadTableFloat(table, "X", "x", "_x", fallback?.X ?? 0f),
                    ReadTableFloat(table, "Y", "y", "_y", fallback?.Y ?? 0f),
                    ReadTableFloat(table, "Z", "z", "_z", fallback?.Z ?? 0f));
            }

            return fallback ?? new LuaVector3();
        }

        internal static LuaCFrame CoerceCFrame(object? value, LuaCFrame? fallback = null)
        {
            if (value is LuaCFrame cframe)
                return cframe;

            if (value is LuaTable table)
            {
                try
                {
                    object? position = table["Position"] ?? table["position"];
                    if (position != null)
                        return LuaCFrame.FromPosition(CoerceVector3(position));
                }
                catch { }
            }

            return fallback ?? LuaCFrame.Identity();
        }

        private static object WrapLuaValue(string functionName, object fallback, params object[] args)
        {
            var lua = ActiveState;
            if (lua == null)
                return fallback;

            try
            {
                var fn = lua.GetFunction(functionName);
                if (fn == null)
                    return fallback;

                var result = fn.Call(args);
                if (result != null && result.Length > 0 && result[0] != null)
                    return result[0]!;
            }
            catch { }

            return fallback;
        }

        private static float ReadTableFloat(LuaTable table, string upper, string lower, string hidden, float fallback)
        {
            try
            {
                if (TryConvertToFloat(table[upper], out var value) ||
                    TryConvertToFloat(table[lower], out value) ||
                    TryConvertToFloat(table[hidden], out value))
                {
                    return value;
                }
            }
            catch { }

            return fallback;
        }

        private static bool TryConvertToFloat(object? value, out float result)
        {
            try
            {
                if (value != null)
                {
                    result = Convert.ToSingle(value);
                    return true;
                }
            }
            catch { }

            result = 0f;
            return false;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public static void Run(string script)
        {
            Stop();
            Output.Clear();
            Directory.CreateDirectory(ScriptsDir);
            RobloxInputEnabled = false;
            LuaUserInputService.Shared.Reset();
            LuaRunService.Shared.Reset();
            ScriptPlayersEvents.Shared.Reset(clearHandlers: true);
            lock (_lock)
            {
                _cts = new CancellationTokenSource();
                IsRunning = true;
                var cts = _cts;
                _thread = new Thread(() => Execute(script, cts))
                    { IsBackground = true, Name = "LuaScript" };
                _thread.Start();
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                IsRunning = false;
                RobloxInputEnabled = false;
                LuaUserInputService.Shared.Reset();
                LuaRunService.Shared.Reset();
                ScriptPlayersEvents.Shared.Reset(clearHandlers: true);
                ScriptDrawingLayer.Clear();
            }
        }

        // ── Core execution ────────────────────────────────────────────────────

        private static void Execute(string script, CancellationTokenSource cts)
        {
            try
            {
                using var lua = new Lua();
                lua.State.Encoding = Encoding.UTF8;
                ActiveState = lua;

                RegisterGlobals(lua, cts.Token);
                lua.DoString(Bootstrap, "bootstrap");
                // Wrap the user script in spawn() so that task.wait() yields cooperatively
                // instead of blocking the thread. This allows __pump_input and __step_threads
                // to run between iterations of any while loops in the user script.
                var preprocessed = PreprocessLua(script);
                lua.DoString($"spawn(function()\n{preprocessed}\nend)", "script");

                // Cooperatively pump spawned coroutines on the same Lua state.
                // This avoids concurrent NLua access, which is unstable.
                var pumpInput = lua.GetFunction("__pump_input");
                var pumpRunService = lua.GetFunction("__pump_runservice");
                var stepThreads = lua.GetFunction("__step_threads");
                var lastTick = DateTime.UtcNow;
                while (!cts.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var deltaSeconds = Math.Max(0.0, (now - lastTick).TotalSeconds);
                    lastTick = now;

                    try { pumpInput?.Call(); }
                    catch (Exception ex)
                    {
                        Output.Enqueue(($"[error] input: {ex.Message}", "error"));
                        break;
                    }

                    try { pumpRunService?.Call(deltaSeconds); }
                    catch (Exception ex)
                    {
                        Output.Enqueue(($"[error] runservice: {ex.Message}", "error"));
                        break;
                    }

                    try { ScriptPlayersEvents.Shared.Pump(); }
                    catch (Exception ex)
                    {
                        Output.Enqueue(($"[error] players: {ex.Message}", "error"));
                        break;
                    }

                    int active = 0;
                    if (stepThreads != null)
                    {
                        object[]? result;
                        try
                        {
                            result = stepThreads.Call(now.Subtract(DateTime.UnixEpoch).TotalSeconds);
                        }
                        catch (Exception ex)
                        {
                            Output.Enqueue(($"[error] scheduler: {ex.Message}", "error"));
                            break;
                        }

                        if (result != null && result.Length > 0)
                        {
                            try { active = Convert.ToInt32(result[0]); } catch { active = 0; }
                        }
                    }

                    if (active <= 0 &&
                        !ScriptDrawingLayer.HasObjects &&
                        !HasActiveLuaEventConnections(lua) &&
                        !ScriptPlayersEvents.Shared.HasConnections)
                    {
                        break;
                    }

                    Thread.Sleep(16); // ~60fps to match Roblox heartbeat rate
                }
            }
            catch (Exception ex) when (ex is not ThreadAbortException)
            {
                if (!cts.IsCancellationRequested)
                    Output.Enqueue(($"[error] {ex.Message}", "error"));
            }
            finally
            {
                ActiveState = null;
                IsRunning   = false;
            }
        }

        // ── Globals registration ──────────────────────────────────────────────

        private static void RegisterGlobals(Lua lua, CancellationToken token)
        {
            // ── Output ───────────────────────────────────────────────────────
            // _rawprint(str) is the C# sink; bootstrap overrides print/warn/error
            lua["_rawprint"] = new Action<string>(s => Output.Enqueue((s ?? "nil", "print")));
            lua["_rawwarn"]  = new Action<string>(s => Output.Enqueue(($"⚠ {s}", "warn")));
            lua["_rawerr"]   = new Action<string>(s => Output.Enqueue(($"✗ {s}", "error")));

            lua["identifyexecutor"] = new Func<string>(() => "Matcha compatibility 1.0 (FoulzExternal)");
            lua["getgetname"] = new Func<string>(GetGameName);
            lua["setclipboard"] = new Action<string>(SetClipboardText);
            lua["getclipboard"] = new Func<string>(GetClipboardText);
            lua["setrobloxinput"] = new Action<bool>(state => RobloxInputEnabled = state);
            lua["isrbxactive"] = new Func<bool>(ScriptInput.IsRobloxFocused);
            lua["keypress"] = new Action<int>(ScriptInput.KeyPress);
            lua["keyrelease"] = new Action<int>(ScriptInput.KeyRelease);
            lua["iskeypressed"] = new Func<int, bool>(ScriptInput.IsKeyDown);
            lua["ismouse1pressed"] = new Func<bool>(() => ScriptInput.IsMouseButtonDown(0x01));
            lua["ismouse2pressed"] = new Func<bool>(() => ScriptInput.IsMouseButtonDown(0x02));
            lua["mouse1press"] = new Action(() => ScriptInput.MouseButtonPress(0x01));
            lua["mouse1release"] = new Action(() => ScriptInput.MouseButtonRelease(0x01));
            lua["mouse1click"] = new Action(() => ScriptInput.MouseButtonClick(0x01));
            lua["mouse2press"] = new Action(() => ScriptInput.MouseButtonPress(0x02));
            lua["mouse2release"] = new Action(() => ScriptInput.MouseButtonRelease(0x02));
            lua["mouse2click"] = new Action(() => ScriptInput.MouseButtonClick(0x02));
            lua["mousemoveabs"] = new Action<int, int>(ScriptInput.MoveMouseAbsolute);
            lua["mousemoverel"] = new Action<int, int>(ScriptInput.MoveMouseRelative);
            lua["mousescroll"] = new Action<int>(ScriptInput.MouseScroll);
            lua["getscripts"] = new Func<LuaTable>(() => BuildScriptTable(lua));
            lua["getscriptbytecode"] = new Func<object?, string>(GetScriptBytecode);
            lua["decompile"] = new Func<object?, string>(DecompileScript);
            lua["getscripthash"] = new Func<object?, string>(GetScriptHash);
            lua["base64encode"] = new Func<string, string>(data => Convert.ToBase64String(Encoding.UTF8.GetBytes(data ?? string.Empty)));
            lua["base64decode"] = new Func<string, string>(DecodeBase64);
            lua["setfflag"] = new Action<string, string>((name, value) =>
            {
                if (!string.IsNullOrWhiteSpace(name))
                    FastFlags[name] = value ?? string.Empty;
            });
            lua["getfflag"] = new Func<string, object?>(name =>
            {
                if (string.IsNullOrWhiteSpace(name))
                    return null;
                return FastFlags.TryGetValue(name, out var value) ? value : null;
            });

            // ── HttpService / httpget ─────────────────────────────────────────
            var httpServiceInstance = new LuaHttpService();
            lua["HttpService"] = httpServiceInstance;
            lua["httpget"] = new Func<string, object?, string>((url, content) => HttpGetRequest(url));
            lua["HttpGet"] = new Func<string, object?, string>((url, content) => HttpGetRequest(url));

            // ── notify(message, title, duration_seconds) ─────────────────────
            lua["notify"] = new Action<string, string, double>((msg, title, dur) =>
                notify.Notify(title ?? "Script", msg, (int)(dur * 1000)));
            lua["_input_get_mouse_pos"] = new Func<object>(() =>
            {
                var mouse = ScriptInput.GetMousePosition();
                return WrapVector2(mouse.X, mouse.Y);
            });
            lua["_input_is_key_down"] = new Func<object, bool>(key => ScriptInput.IsKeyDown(ScriptInput.ResolveVirtualKey(key)));
            lua["_input_is_mouse1_down"] = new Func<bool>(() => ScriptInput.IsMouseButtonDown(0x01));
            lua["_input_is_mouse2_down"] = new Func<bool>(() => ScriptInput.IsMouseButtonDown(0x02));

            // ── blocking wait(secs) primitive used by the main thread and the
            // coroutine scheduler fallback.
            lua["_wait_blocking"] = new Action<double>(secs =>
            {
                var until = DateTime.UtcNow.AddSeconds(secs);
                while (!token.IsCancellationRequested && DateTime.UtcNow < until)
                    Thread.Sleep(16);
                token.ThrowIfCancellationRequested();
            });

            // ── Memory ───────────────────────────────────────────────────────
            lua["memory_read"]  = new Func<string, long, object?>(MemRead);
            lua["memory_write"] = new Action<string, long, object?>(MemWrite);
            lua["getbase"]      = new Func<long>(() => SDKInst.Mem?.Base ?? 0);
            // ── WorldToScreen ──────────────────────────────────────────────────
            lua["WorldToScreen"] = new Func<object, object[]>(value =>
            {
                try
                {
                    var v3 = CoerceVector3(value);
                    var sdk = new Vector3 { x = v3.X, y = v3.Y, z = v3.Z };
                    var s = WorldToScreenHelper.WorldToScreen(sdk);
                    bool on = s.x != -1;
                    return new object[] { WrapVector2(s.x, s.y), on };
                }
                catch { return new object[] { WrapVector2(0, 0), false }; }
            });
            // ── game / workspace ─────────────────────────────────────────────
            try
            {
                var dataModel = SDKInst.GetDataModel();
                if (dataModel.IsValid)
                {
                    lua["game"] = new LuaInstance(dataModel);
                }
                else
                {
                    lua["game"] = null;
                    Output.Enqueue(("[warn] DataModel is invalid", "warn"));
                }
            }
            catch (Exception ex)
            {
                lua["game"] = null;
                Output.Enqueue(($"[warn] Failed to set up game object: {ex.Message}", "warn"));
            }

            try
            {
                if (Storage.WorkspaceInstance.IsValid)
                {
                    lua["workspace"] = new LuaInstance(Storage.WorkspaceInstance);
                }
                else
                {
                    lua["workspace"] = null;
                }
            }
            catch (Exception ex)
            {
                lua["workspace"] = null;
                Output.Enqueue(($"[warn] Failed to set up workspace object: {ex.Message}", "warn"));
            }

            // ── _getService helper function (more reliable than method calls) ──
            lua["_getService"] = new Func<object, string, object?>((_gameObj, serviceName) =>
            {
                try
                {
                    if (_gameObj is LuaInstance luaInst && luaInst.IsValid)
                    {
                        return luaInst.GetService(serviceName);
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Output.Enqueue(($"[_getService] Error: {ex.Message}", "error"));
                    return null;
                }
            });

            // ── Vector3 / Vector2 / Color3 factories ─────────────────────────
            lua["_v3new"]      = new Func<float, float, float, LuaVector3>(LuaVector3.New);
            lua["_v2new"]      = new Func<float, float, LuaVector2>(LuaVector2.New);
            lua["_c3new"]      = new Func<float, float, float, LuaColor3>(LuaColor3.New);
            lua["_c3fromRGB"]  = new Func<float, float, float, LuaColor3>(LuaColor3.FromRGB);
            lua["_cfnew"]      = new Func<object?, object?, object?, LuaCFrame>(CreateCFrame);
            lua["_cflookat"]   = new Func<object?, object?, object?, LuaCFrame>(CreateLookAtCFrame);

            // ── Drawing ──────────────────────────────────────────────────────
            lua["_drawing_new"] = new Func<string, LuaDrawingObject>(new LuaDrawingFactory().New);

            // ── loadstring ───────────────────────────────────────────────────
            lua["loadstring"] = new Func<string, LuaFunction?>(chunk =>
            {
                try   { return lua.LoadString(PreprocessLua(chunk), "chunk") as LuaFunction; }
                catch (Exception ex) { Output.Enqueue(($"[error] loadstring: {ex.Message}", "error")); return null; }
            });

            // ── require ──────────────────────────────────────────────────────
            lua["require"] = new Func<string, object?>(path =>
            {
                try
                {
                    if (!path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
                        path += ".lua";
                    string full = Path.IsPathRooted(path) ? path : Path.Combine(ScriptsDir, path);
                    if (!File.Exists(full)) throw new FileNotFoundException($"require: {path} not found");
                    var src = File.ReadAllText(full, Encoding.UTF8);
                    var results = lua.DoString(PreprocessLua(src), path);
                    return results?.Length > 0 ? results[0] : true;
                }
                catch (Exception ex) { Output.Enqueue(($"[error] require: {ex.Message}", "error")); return null; }
            });
            lua["run_secure"] = new Func<string, object?>(code =>
            {
                try
                {
                    var result = lua.DoString(PreprocessLua(code ?? string.Empty), "run_secure");
                    return result != null && result.Length > 0 ? result[0] : true;
                }
                catch (Exception ex)
                {
                    Output.Enqueue(($"[error] run_secure: {ex.Message}", "error"));
                    return null;
                }
            });

            // ── Roblox globals missing from standard Lua ──────────────────────
            // tick() returns Unix epoch time as double (Roblox global)
            lua["tick"] = new Func<double>(() =>
                (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds);
        }

        private static bool HasActiveLuaEventConnections(Lua lua)
        {
            try
            {
                var value = lua["__has_active_connections"];
                return value != null && Convert.ToBoolean(value);
            }
            catch { return false; }
        }

        internal static LuaTable CreateLuaSequenceTable(Lua lua, IReadOnlyList<object?> values)
        {
            string key = "_t" + Interlocked.Increment(ref _luaTableSequence);
            lua.NewTable(key);
            var table = lua.GetTable(key);
            for (int i = 0; i < values.Count; i++)
                table[i + 1] = values[i];
            lua[key] = null;
            return table;
        }

        internal static LuaTable CreateLuaMapTable(Lua lua, IReadOnlyDictionary<string, object?> values)
        {
            string key = "_t" + Interlocked.Increment(ref _luaTableSequence);
            lua.NewTable(key);
            var table = lua.GetTable(key);
            foreach (var pair in values)
                table[pair.Key] = pair.Value;
            lua[key] = null;
            return table;
        }

        private static LuaTable BuildScriptTable(Lua lua)
        {
            var values = new List<object?>();
            foreach (var script in EnumerateScripts())
                values.Add(new LuaInstance(script));
            return CreateLuaSequenceTable(lua, values);
        }

        private static List<SDKInst> EnumerateScripts()
        {
            var scripts = new List<SDKInst>();
            try
            {
                var root = Storage.DataModelInstance;
                if (!root.IsValid)
                    root = SDKInst.GetDataModel();

                if (root.IsValid)
                    CollectScripts(root, scripts, 0);
            }
            catch { }
            return scripts;
        }

        private static void CollectScripts(SDKInst instance, List<SDKInst> output, int depth)
        {
            if (!instance.IsValid || depth > 12)
                return;

            try
            {
                string className = instance.GetClass();
                if (className == "Script" || className == "LocalScript" || className == "ModuleScript")
                    output.Add(instance);

                foreach (var child in instance.GetChildren())
                    CollectScripts(child, output, depth + 1);
            }
            catch { }
        }

        private static string GetGameName()
        {
            try
            {
                if (Storage.DataModelInstance.IsValid)
                    return Storage.DataModelInstance.GetName();
                return SDKInst.GetDataModel().GetName();
            }
            catch { return ""; }
        }

        private static readonly System.Net.Http.HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        private static string HttpGetRequest(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL cannot be empty");
                var bytes = _httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                return System.Text.Encoding.Latin1.GetString(bytes);
            }
            catch (Exception ex)
            {
                Output.Enqueue(($"[error] httpget: {ex.Message}", "error"));
                throw new InvalidOperationException($"HttpGet failed: {ex.Message}");
            }
        }

        private static string DecodeBase64(string data)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(data ?? string.Empty)); }
            catch { return string.Empty; }
        }

        private static LuaCFrame CreateCFrame(object? a, object? b, object? c)
        {
            if (a == null && b == null && c == null)
                return LuaCFrame.Identity();

            if (a is LuaCFrame cframe)
                return cframe;

            if (b == null && c == null)
                return LuaCFrame.FromPosition(CoerceVector3(a));

            if (b != null && c == null && LooksLikeVector(b))
                return LuaCFrame.LookAt(CoerceVector3(a), CoerceVector3(b));

            return LuaCFrame.FromPosition(new LuaVector3(
                ConvertToFloat(a),
                ConvertToFloat(b),
                ConvertToFloat(c)));
        }

        private static LuaCFrame CreateLookAtCFrame(object? at, object? lookAt, object? up)
        {
            return LuaCFrame.LookAt(
                CoerceVector3(at),
                CoerceVector3(lookAt),
                LooksLikeVector(up) ? CoerceVector3(up) : null);
        }

        private static bool LooksLikeVector(object? value)
        {
            if (value is LuaVector2 or LuaVector3)
                return true;

            if (value is LuaTable table)
            {
                try
                {
                    return table["X"] != null || table["x"] != null;
                }
                catch { return false; }
            }

            return false;
        }

        private static float ConvertToFloat(object? value)
        {
            try { return Convert.ToSingle(value); }
            catch { return 0f; }
        }

        private static void SetClipboardText(string value)
        {
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    app.Dispatcher.Invoke(() => Clipboard.SetText(value ?? string.Empty));
                    return;
                }

                var done = new ManualResetEvent(false);
                Exception? failure = null;
                var thread = new Thread(() =>
                {
                    try { Clipboard.SetText(value ?? string.Empty); }
                    catch (Exception ex) { failure = ex; }
                    finally { done.Set(); }
                });
                if (OperatingSystem.IsWindows())
                    thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                done.WaitOne();
                if (failure != null)
                    throw failure;
            }
            catch (Exception ex)
            {
                Output.Enqueue(($"[error] setclipboard: {ex.Message}", "error"));
            }
        }

        private static string GetClipboardText()
        {
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    string? result = null;
                    app.Dispatcher.Invoke(() => result = Clipboard.GetText());
                    return result ?? string.Empty;
                }

                var done = new ManualResetEvent(false);
                string? text = null;
                Exception? failure = null;
                var thread = new Thread(() =>
                {
                    try { text = Clipboard.GetText(); }
                    catch (Exception ex) { failure = ex; }
                    finally { done.Set(); }
                });
                if (OperatingSystem.IsWindows())
                    thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                done.WaitOne();
                if (failure != null)
                    throw failure;
                return text ?? string.Empty;
            }
            catch (Exception ex)
            {
                Output.Enqueue(($"[error] getclipboard: {ex.Message}", "error"));
                return string.Empty;
            }
        }

        private static string GetScriptBytecode(object? scriptObject)
        {
            var script = UnwrapScriptInstance(scriptObject);
            if (!script.IsValid)
                return string.Empty;

            try
            {
                int offset = GetBytecodeOffset(script.GetClass());
                if (offset != 0)
                {
                    long bytecodeObject = SDKInst.Mem.ReadPtr(script.Address + offset);
                    if (bytecodeObject != 0)
                    {
                        long data = SDKInst.Mem.ReadPtr(bytecodeObject + Offsets.ByteCode.Pointer);
                        int size = SDKInst.Mem.Read<int>(bytecodeObject + Offsets.ByteCode.Size);
                        if (data != 0 && size > 0 && size <= 1024 * 1024)
                            return Convert.ToBase64String(ReadBytes(data, size));
                    }
                }
            }
            catch { }

            return ReadScriptSource(script);
        }

        private static string DecompileScript(object? scriptObject)
        {
            var script = UnwrapScriptInstance(scriptObject);
            if (!script.IsValid)
                return string.Empty;

            var source = ReadScriptSource(script);
            return !string.IsNullOrEmpty(source) ? source : GetScriptBytecode(scriptObject);
        }

        private static string GetScriptHash(object? scriptObject)
        {
            var bytecode = GetScriptBytecode(scriptObject);
            var hash = ComputeFnv1a64(Encoding.UTF8.GetBytes(bytecode ?? string.Empty));
            return hash.ToString("x16");
        }

        private static SDKInst UnwrapScriptInstance(object? scriptObject)
        {
            if (scriptObject is LuaInstance luaInstance)
                return luaInstance._inst;
            if (scriptObject is long address && address > 0x1000)
                return new SDKInst(address);
            return default;
        }

        private static int GetBytecodeOffset(string className)
        {
            return className switch
            {
                "ModuleScript" => (int)Offsets.ModuleScript.ByteCode,
                "LocalScript" => (int)Offsets.LocalScript.ByteCode,
                "Script" => (int)Offsets.Script.ByteCode,
                _ => 0,
            };
        }

        private static string ReadScriptSource(SDKInst script)
        {
            try { return ReadRobloxString(script.Address + ScriptSourceOffset); }
            catch { return string.Empty; }
        }

        private static string ReadRobloxString(long address)
        {
            if (address == 0 || SDKInst.Mem == null)
                return string.Empty;

            try
            {
                int length = SDKInst.Mem.Read<int>(address + 0x18);
                if (length <= 0 || length > 1024 * 1024)
                    return string.Empty;

                long textAddress = length >= 16 ? SDKInst.Mem.ReadPtr(address) : address;
                if (textAddress == 0)
                    return string.Empty;

                return Encoding.UTF8.GetString(ReadBytes(textAddress, length));
            }
            catch { return string.Empty; }
        }

        private static byte[] ReadBytes(long address, int count)
        {
            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
                bytes[i] = SDKInst.Mem.Read<byte>(address + i);
            return bytes;
        }

        private static ulong ComputeFnv1a64(byte[] bytes)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= prime;
            }

            return hash;
        }

        // ── Memory helpers ────────────────────────────────────────────────────

        private static object? MemRead(string type, long addr)
        {
            if (SDKInst.Mem == null) return null;
            try
            {
                // Normalize type names (support aliases)
                type = type?.ToLowerInvariant() ?? string.Empty;
                return type switch
                {
                    // Standard types
                    "byte" or "i8"     => (object)(double)SDKInst.Mem.Read<byte>(addr),
                    "short" or "i16"   => (double)SDKInst.Mem.Read<short>(addr),
                    "int" or "i32"     => (double)SDKInst.Mem.Read<int>(addr),
                    "int64" or "i64" or "long" => (double)SDKInst.Mem.Read<long>(addr),
                    "float" or "f32"   => (double)SDKInst.Mem.Read<float>(addr),
                    "double" or "f64"  => (double)SDKInst.Mem.Read<double>(addr),
                    "bool"             => (object)SDKInst.Mem.Read<bool>(addr),
                    "string"           => SDKInst.Mem.ReadString(addr),
                    // Pointer types
                    "pointer" or "ptr" => (double)(ulong)SDKInst.Mem.ReadPtr(addr),
                    // Vector types - return as wrapped Lua values
                    "vector3" => ReadVector3(addr),
                    "vector2" => ReadVector2(addr),
                    _         => null
                };
            }
            catch { return null; }
        }

        private static object ReadVector3(long addr)
        {
            try
            {
                float x = SDKInst.Mem.Read<float>(addr);
                float y = SDKInst.Mem.Read<float>(addr + 4);
                float z = SDKInst.Mem.Read<float>(addr + 8);
                return new LuaVector3(x, y, z);
            }
            catch { return new LuaVector3(); }
        }

        private static object ReadVector2(long addr)
        {
            try
            {
                float x = SDKInst.Mem.Read<float>(addr);
                float y = SDKInst.Mem.Read<float>(addr + 4);
                return new LuaVector2(x, y);
            }
            catch { return new LuaVector2(); }
        }

        private static void MemWrite(string type, long addr, object? value)
        {
            if (SDKInst.Mem == null || value == null) return;
            try
            {
                type = type?.ToLowerInvariant() ?? string.Empty;
                switch (type)
                {
                    // Standard types
                    case "byte":
                    case "i8":
                        SDKInst.Mem.Write(addr, Convert.ToByte(value));
                        break;
                    case "short":
                    case "i16":
                        SDKInst.Mem.Write(addr, Convert.ToInt16(value));
                        break;
                    case "int":
                    case "i32":
                        SDKInst.Mem.Write(addr, Convert.ToInt32(value));
                        break;
                    case "int64":
                    case "i64":
                    case "long":
                        SDKInst.Mem.Write(addr, Convert.ToInt64(value));
                        break;
                    case "float":
                    case "f32":
                        SDKInst.Mem.Write(addr, Convert.ToSingle(value));
                        break;
                    case "double":
                    case "f64":
                        SDKInst.Mem.Write(addr, Convert.ToDouble(value));
                        break;
                    case "bool":
                        SDKInst.Mem.Write(addr, Convert.ToBoolean(value));
                        break;
                    // Pointer types
                    case "pointer":
                    case "ptr":
                        SDKInst.Mem.Write(addr, (long)Convert.ToUInt64(value));
                        break;
                    // Vector types
                    case "vector3":
                        WriteVector3(addr, value);
                        break;
                    case "vector2":
                        WriteVector2(addr, value);
                        break;
                }
            }
            catch { }
        }

        private static void WriteVector3(long addr, object? value)
        {
            try
            {
                var vec = CoerceVector3(value);
                SDKInst.Mem.Write(addr, vec.X);
                SDKInst.Mem.Write(addr + 4, vec.Y);
                SDKInst.Mem.Write(addr + 8, vec.Z);
            }
            catch { }
        }

        private static void WriteVector2(long addr, object? value)
        {
            try
            {
                var vec = CoerceVector2(value);
                SDKInst.Mem.Write(addr, vec.X);
                SDKInst.Mem.Write(addr + 4, vec.Y);
            }
            catch { }
        }

        // ── Lua 'continue' preprocessor ─────────────────────────────────────────────

        // Converts Luau's 'continue' keyword to Lua 5.4 goto-based equivalent.
        private static string PreprocessLua(string src)
        {
            // Rewrite compound ops first (+=, -=, *=, /=, //=, %=, ^=, ..=)
            src = PreprocessCompoundOps(src);
            src = PreprocessGenericFor(src);

            // ── Pass 1: find which loops contain 'continue' ──────────────────────
            var continued = new HashSet<int>();
            var stk = new Stack<(bool loop, int id)>();
            int seq = 0; bool expDo = false;
            foreach (var t in LuaTokenize(src))
            {
                switch (t.Text)
                {
                    case "for": case "while": stk.Push((true,  ++seq)); expDo = true;  break;
                    case "repeat":           stk.Push((true,  ++seq)); expDo = false; break;
                    case "if": case "function": stk.Push((false, 0)); expDo = false; break;
                    case "do":
                        if (!expDo) stk.Push((false, 0));
                        expDo = false; break;
                    case "then": expDo = false; break;
                    case "end": case "until":
                        if (stk.Count > 0) stk.Pop(); break;
                    case "continue":
                        foreach (var f in stk)
                            if (f.loop) { continued.Add(f.id); break; }
                        break;
                }
            }
            if (continued.Count == 0) return src;   // nothing to do

            // ── Pass 2: rewrite continue → goto + inject labels ────────────────
            var sb  = new StringBuilder(src.Length + 64 * continued.Count);
            var stk2 = new Stack<(bool loop, int id)>();
            int seq2 = 0; bool expDo2 = false;
            int prev = 0;
            foreach (var t in LuaTokenize(src))
            {
                sb.Append(src, prev, t.Start - prev);
                prev = t.End;
                switch (t.Text)
                {
                    case "for": case "while":
                        stk2.Push((true, ++seq2)); expDo2 = true;
                        sb.Append(t.Text); break;
                    case "repeat":
                        stk2.Push((true, ++seq2)); expDo2 = false;
                        sb.Append(t.Text); break;
                    case "if": case "function":
                        stk2.Push((false, 0)); expDo2 = false;
                        sb.Append(t.Text); break;
                    case "do":
                        if (!expDo2) stk2.Push((false, 0));
                        expDo2 = false; sb.Append("do"); break;
                    case "then": expDo2 = false; sb.Append("then"); break;
                    case "end":
                    {
                        (bool loop, int id) f = stk2.Count > 0 ? stk2.Pop() : (false, 0);
                        if (f.loop && continued.Contains(f.id))
                            sb.Append($" ::__cont_{f.id}__:: ");
                        sb.Append("end"); break;
                    }
                    case "until":
                    {
                        (bool loop, int id) f = stk2.Count > 0 ? stk2.Pop() : (false, 0);
                        if (f.loop && continued.Contains(f.id))
                            sb.Append($" ::__cont_{f.id}__:: ");
                        sb.Append("until"); break;
                    }
                    case "continue":
                    {
                        int id = 0;
                        foreach ((bool loop, int lid) f in stk2) if (f.loop) { id = f.lid; break; }
                        sb.Append(id > 0 ? $"goto __cont_{id}__" : "do end --[[continue]]");
                        break;
                    }
                    default: sb.Append(t.Text); break;
                }
            }
            if (prev < src.Length) sb.Append(src, prev, src.Length - prev);
            return sb.ToString();
        }

        private record struct LuaTok(string Text, int Start, int End);

        // Yields identifier/keyword tokens with source positions; skips strings/comments.
        private static IEnumerable<LuaTok> LuaTokenize(string src)
        {
            int i = 0, n = src.Length;
            static int EqLv(string s, int p) { int c = 0; while (p < s.Length && s[p] == '=') { c++; p++; } return c; }

            while (i < n)
            {
                char c = src[i];
                if (c <= ' ') { i++; continue; }

                // Line / block comment
                if (c == '-' && i + 1 < n && src[i + 1] == '-')
                {
                    i += 2;
                    if (i < n && src[i] == '[')
                    {
                        int eq = EqLv(src, i + 1);
                        if (i + 1 + eq < n && src[i + 1 + eq] == '[')
                        {
                            i += 2 + eq;
                            while (i < n) { if (src[i] == ']') { int e2 = EqLv(src, i + 1); if (e2 == eq && i + 2 + eq <= n && src[i + 1 + eq] == ']') { i += 2 + eq; break; } } i++; }
                            continue;
                        }
                    }
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }

                // Long string
                if (c == '[')
                {
                    int eq = EqLv(src, i + 1);
                    if (eq > 0 && i + 1 + eq < n && src[i + 1 + eq] == '[')
                    {
                        i += 2 + eq;
                        while (i < n) { if (src[i] == ']') { int e2 = EqLv(src, i + 1); if (e2 == eq && i + 2 + eq <= n && src[i + 1 + eq] == ']') { i += 2 + eq; break; } } i++; }
                        continue;
                    }
                }

                // Short strings
                if (c == '"' || c == '\'')
                {
                    char q = c; i++;
                    while (i < n && src[i] != q) { if (src[i] == '\\') i++; i++; }
                    if (i < n) i++;
                    continue;
                }

                // Identifiers / keywords
                if (char.IsLetter(c) || c == '_')
                {
                    int s = i;
                    while (i < n && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                    yield return new LuaTok(src[s..i], s, i);
                    continue;
                }

                i++;
            }
        }

        // Rewrites Luau compound assignment ops outside strings/comments:
        //   x += e  →  x = x + (e)
        //   x -= e  →  x = x - (e)   etc.
        // Handles multi-token LHS like foo.bar, foo[expr], method:calls(...).
        private static string PreprocessCompoundOps(string src)
        {
            // We scan character-by-character. When outside strings/comments we
            // look for the pattern:  <lhs_expr> <op>= <rhs>
            // where op is one of: + - * / // % ^ ..

            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0, n = src.Length;

            while (i < n)
            {
                // ── Skip line comment ────────────────────────────────────────
                if (i + 1 < n && src[i] == '-' && src[i + 1] == '-')
                {
                    // long comment?
                    int j = i + 2;
                    if (j < n && src[j] == '[')
                    {
                        int eq = 0; int k = j + 1;
                        while (k < n && src[k] == '=') { eq++; k++; }
                        if (k < n && src[k] == '[')
                        {
                            sb.Append(src, i, k - i + 1); i = k + 1;
                            // scan to ]=...=]
                            while (i < n)
                            {
                                if (src[i] == ']')
                                {
                                    int e2 = 0; int m = i + 1;
                                    while (m < n && src[m] == '=') { e2++; m++; }
                                    if (e2 == eq && m < n && src[m] == ']')
                                    { sb.Append(src, i, m - i + 1); i = m + 1; break; }
                                }
                                sb.Append(src[i]); i++;
                            }
                            continue;
                        }
                    }
                    // line comment
                    while (i < n && src[i] != '\n') { sb.Append(src[i]); i++; }
                    continue;
                }

                // ── Skip long string ─────────────────────────────────────────
                if (src[i] == '[')
                {
                    int eq = 0; int k = i + 1;
                    while (k < n && src[k] == '=') { eq++; k++; }
                    if (eq > 0 && k < n && src[k] == '[')
                    {
                        sb.Append(src, i, k - i + 1); i = k + 1;
                        while (i < n)
                        {
                            if (src[i] == ']')
                            {
                                int e2 = 0; int m = i + 1;
                                while (m < n && src[m] == '=') { e2++; m++; }
                                if (e2 == eq && m < n && src[m] == ']')
                                { sb.Append(src, i, m - i + 1); i = m + 1; break; }
                            }
                            sb.Append(src[i]); i++;
                        }
                        continue;
                    }
                }

                // ── Skip short string ─────────────────────────────────────────
                if (src[i] == '"' || src[i] == '\'')
                {
                    char q = src[i]; sb.Append(q); i++;
                    while (i < n && src[i] != q)
                    {
                        if (src[i] == '\\') { sb.Append(src[i]); i++; }
                        if (i < n) { sb.Append(src[i]); i++; }
                    }
                    if (i < n) { sb.Append(src[i]); i++; }
                    continue;
                }

                // ── Detect compound op ────────────────────────────────────────
                // We need to look back for an LHS that ends at current position.
                // Strategy: emit chars one by one, but if we see op= scan back.

                // Try to match <op>= where op ∈ { + - * / // % ^ .. }
                (string op, int opLen) GetCompoundOp()
                {
                    // floor div //=
                    if (i + 2 < n && src[i] == '/' && src[i+1] == '/' && src[i+2] == '=')
                        return ("//", 3);
                    // concat ..=
                    if (i + 2 < n && src[i] == '.' && src[i+1] == '.' && src[i+2] == '=')
                        return ("..", 3);
                    // single-char ops +=  -=  *=  /=  %=  ^=
                    if (i + 1 < n && src[i+1] == '=')
                    {
                        char op = src[i];
                        if (op == '+' || op == '-' || op == '*' ||
                            op == '/' || op == '%' || op == '^')
                            return (op.ToString(), 2);
                    }
                    return ("", 0);
                }

                var (opStr, opLen) = GetCompoundOp();
                if (opLen > 0)
                {
                    // Grab the LHS from what we've already emitted.
                    // Walk backwards through sb skipping whitespace, then grab
                    // the LHS token (identifier, dot-chains, bracket calls).
                    string built = sb.ToString();
                    int end = built.Length - 1;
                    while (end >= 0 && built[end] == ' ') end--;

                    // find start of LHS: walk back past idents, '.', '[...]', calls
                    int start = end;
                    while (start > 0)
                    {
                        char ch = built[start - 1];
                        if (char.IsLetterOrDigit(ch) || ch == '_' ||
                            ch == '.' || ch == ']' || ch == ')') { start--; }
                        else if (ch == '[')
                        {
                            // skip matched brackets going left
                            int depth = 1; start--;
                            while (start > 0 && depth > 0)
                            {
                                start--;
                                if (built[start] == ']') depth++;
                                else if (built[start] == '[') depth--;
                            }
                        }
                        else break;
                    }

                    string lhs = built.Substring(start, end - start + 1).Trim();
                    if (lhs.Length == 0) lhs = "_"; // safety

                    // Trim lhs off sb
                    sb.Remove(start, sb.Length - start);

                    // Collect RHS to end of logical line
                    i += opLen; // skip the compound op token
                    // skip whitespace
                    while (i < n && (src[i] == ' ' || src[i] == '\t')) i++;

                    // Read RHS: everything up to newline / statement separator,
                    // but also stop before top-level control keywords like
                    // `until`, so `x -= 1 until cond` becomes
                    // `x = x - (1) until cond`.
                    var rhs = new System.Text.StringBuilder();
                    int depth2 = 0;
                    while (i < n)
                    {
                        char ch = src[i];
                        if (ch == '(' || ch == '[') depth2++;
                        else if (ch == ')' || ch == ']') depth2--;
                        if (depth2 < 0) break;
                        if (depth2 == 0 && (ch == '\n' || ch == ';')) break;

                        if (depth2 == 0 && char.IsLetter(ch))
                        {
                            bool IsKeywordAt(string keyword)
                            {
                                if (i + keyword.Length > n) return false;
                                if (!string.Equals(src.Substring(i, keyword.Length), keyword, StringComparison.Ordinal))
                                    return false;

                                bool leftOk = i == 0 || !char.IsLetterOrDigit(src[i - 1]) && src[i - 1] != '_';
                                bool rightOk = i + keyword.Length >= n || (!char.IsLetterOrDigit(src[i + keyword.Length]) && src[i + keyword.Length] != '_');
                                return leftOk && rightOk;
                            }

                            if (IsKeywordAt("until") || IsKeywordAt("do") || IsKeywordAt("then") || IsKeywordAt("else") || IsKeywordAt("elseif") || IsKeywordAt("end"))
                                break;
                        }

                        rhs.Append(ch); i++;
                    }

                    sb.Append($"{lhs} = {lhs} {opStr} ({rhs.ToString().TrimEnd()})");
                    continue;
                }

                sb.Append(src[i]); i++;
            }

            return sb.ToString();
        }

        // Rewrites Luau generic loops:
        //   for k, v in t do        -> for k, v in pairs(t) do
        //   for v in list do        -> for v in pairs(list) do
        // Leaves explicit iterator calls intact.
        private static string PreprocessGenericFor(string src)
        {
            var lines = src.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("for ", StringComparison.Ordinal))
                    continue;

                int inIdx = trimmed.IndexOf(" in ", StringComparison.Ordinal);
                int doIdx = trimmed.LastIndexOf(" do", StringComparison.Ordinal);
                if (inIdx <= 0 || doIdx <= inIdx)
                    continue;

                string expr = trimmed.Substring(inIdx + 4, doIdx - (inIdx + 4)).Trim();
                // Skip if already using a proper Lua iterator (pairs, ipairs, next, or multi-return like "next, tbl")
                if (expr.Contains(",") || expr.StartsWith("pairs(") || expr.StartsWith("ipairs(") || expr.StartsWith("next"))
                    continue;

                string prefix = line.Substring(0, line.Length - trimmed.Length);
                string vars = trimmed.Substring(4, inIdx - 4).Trim();
                string tail = trimmed.Substring(doIdx + 3); // preserve anything after " do" (inline loop bodies)
                // Use __smart_iter so both plain tables AND method calls that return tables work.
                // e.g. "for i,v in players:GetChildren() do" -> "for i,v in __smart_iter(players:GetChildren()) do"
                lines[i] = $"{prefix}for {vars} in __smart_iter({expr}) do{tail}";
            }

            return string.Join("\n", lines);
        }

        // ── Bootstrap Lua (runs before every user script) ─────────────────────

        private const string Bootstrap = """
-- Pretty-print (supports multiple args, any type)
function print(...)
    local args = table.pack(...)
    local t = {}
    for i = 1, args.n do t[i] = tostring(args[i]) end
    _rawprint(table.concat(t, "\t"))
end
function warn(...)
    local args = table.pack(...)
    local t = {}
    for i = 1, args.n do t[i] = tostring(args[i]) end
    _rawwarn(table.concat(t, "\t"))
end
function error(msg, _)
    _rawerr(tostring(msg))
end

-- Cooperative scheduler for Matcha-style spawn/task.wait.
__threads = {}

local function __now_seconds()
    return os.clock()
end

local function __spawn_impl(fn, ...)
    if type(fn) ~= "function" then
        error("spawn expects a function")
    end

    local co = coroutine.create(fn)
    table.insert(__threads, {
        co = co,
        wake = 0,
        args = table.pack(...),
        started = false,
    })

    return { Disconnect = function() end }
end

function __step_threads(now)
    local active = 0
    local i = 1
    while i <= #__threads do
        local th = __threads[i]
        if now >= (th.wake or 0) then
            local ok, yielded
            if not th.started then
                th.started = true
                ok, yielded = coroutine.resume(th.co, table.unpack(th.args, 1, th.args.n or 0))
            else
                ok, yielded = coroutine.resume(th.co)
            end

            if not ok then
                _rawerr("spawn: " .. tostring(yielded))
                table.remove(__threads, i)
            elseif coroutine.status(th.co) == "dead" then
                table.remove(__threads, i)
            else
                th.wake = now + (tonumber(yielded) or 0)
                active = active + 1
                i = i + 1
            end
        else
            active = active + 1
            i = i + 1
        end
    end

    return active
end

-- Minimum wait matches Roblox heartbeat (~1/60s). Without this floor,
-- task.wait() / wait() with 0 would pump at ~100Hz and cause scripts like
-- velocity multipliers to compound exponentially instead of per-frame.
local __MIN_WAIT = 1 / 60

function wait(secs)
    secs = tonumber(secs) or 0
    secs = math.max(secs, __MIN_WAIT)
    local _, isMain = coroutine.running()
    if isMain then
        _wait_blocking(secs)
        return secs
    end
    return coroutine.yield(secs)
end

function spawn(fn, ...)
    return __spawn_impl(fn, ...)
end

-- task.defer: schedules fn to run on the next scheduler tick (wake = now so it runs ASAP)
local function __defer_impl(fn, ...)
    if type(fn) ~= "function" then
        error("task.defer expects a function")
    end
    local co = coroutine.create(fn)
    local entry = {
        co = co,
        wake = 0,
        args = table.pack(...),
        started = false,
    }
    table.insert(__threads, entry)
    return co  -- return coroutine handle so task.cancel can reference it
end

-- task.cancel: removes a coroutine from the scheduler
local function __cancel_impl(co)
    if type(co) ~= "thread" then return end
    for i, th in ipairs(__threads) do
        if th.co == co then
            table.remove(__threads, i)
            return
        end
    end
end

task = { wait = wait, spawn = spawn, defer = __defer_impl, cancel = __cancel_impl }

math.clamp = math.clamp or function(v, minv, maxv)
    if v < minv then return minv end
    if v > maxv then return maxv end
    return v
end

math.log10 = math.log10 or function(v)
    return math.log(v) / math.log(10)
end

table.find = table.find or function(t, needle)
    for i, v in pairs(t) do
        if v == needle then
            return i
        end
    end
    return nil
end

table.clear = table.clear or function(t)
    for k in pairs(t) do
        t[k] = nil
    end
end

-- __smart_iter: Luau-style "for k,v in tbl" works on both plain tables and
-- function iterators (e.g. string.gmatch). The preprocessor rewrites all
-- generic-for expressions to go through here.
function __smart_iter(e)
    if type(e) == "table" then
        return next, e, nil
    end
    -- Already an iterator function (coroutine.wrap, string.gmatch, etc.)
    return e
end

getgenv = getgenv or function()
    return _G
end

getrenv = getrenv or function()
    return _G
end

getfenv = getfenv or function(target)
    if target == nil or target == 0 or target == 1 then
        return _G
    end
    if type(target) == "function" then
        return _G
    end
    return _G
end

setfenv = setfenv or function(target, env)
    env = env or _G
    if type(target) == "table" then
        return target
    end
    return target
end

bit32 = bit32 or {}

bit32.band = bit32.band or function(...)
    local args = table.pack(...)
    local result = 0xFFFFFFFF
    for i = 1, args.n do
        result = result & (tonumber(args[i]) or 0)
    end
    return result & 0xFFFFFFFF
end

bit32.bor = bit32.bor or function(...)
    local args = table.pack(...)
    local result = 0
    for i = 1, args.n do
        result = result | (tonumber(args[i]) or 0)
    end
    return result & 0xFFFFFFFF
end

bit32.bxor = bit32.bxor or function(...)
    local args = table.pack(...)
    local result = 0
    for i = 1, args.n do
        result = result ~ (tonumber(args[i]) or 0)
    end
    return result & 0xFFFFFFFF
end

bit32.bnot = bit32.bnot or function(value)
    return (~(tonumber(value) or 0)) & 0xFFFFFFFF
end

bit32.lshift = bit32.lshift or function(value, shift)
    return ((tonumber(value) or 0) << (tonumber(shift) or 0)) & 0xFFFFFFFF
end

bit32.rshift = bit32.rshift or function(value, shift)
    return (((tonumber(value) or 0) & 0xFFFFFFFF) >> (tonumber(shift) or 0)) & 0xFFFFFFFF
end

bit32.arshift = bit32.arshift or function(value, shift)
    value = tonumber(value) or 0
    shift = tonumber(shift) or 0
    return (value >> shift) & 0xFFFFFFFF
end

bit32.lrotate = bit32.lrotate or function(value, shift)
    value = (tonumber(value) or 0) & 0xFFFFFFFF
    shift = (tonumber(shift) or 0) & 31
    return ((value << shift) | (value >> (32 - shift))) & 0xFFFFFFFF
end

bit32.rrotate = bit32.rrotate or function(value, shift)
    value = (tonumber(value) or 0) & 0xFFFFFFFF
    shift = (tonumber(shift) or 0) & 31
    return ((value >> shift) | (value << (32 - shift))) & 0xFFFFFFFF
end

bit32.extract = bit32.extract or function(value, field, width)
    value = (tonumber(value) or 0) & 0xFFFFFFFF
    field = tonumber(field) or 0
    width = tonumber(width) or 1
    local mask = ((1 << width) - 1) & 0xFFFFFFFF
    return (value >> field) & mask
end

bit32.replace = bit32.replace or function(value, replacement, field, width)
    value = (tonumber(value) or 0) & 0xFFFFFFFF
    replacement = (tonumber(replacement) or 0) & 0xFFFFFFFF
    field = tonumber(field) or 0
    width = tonumber(width) or 1
    local mask = (((1 << width) - 1) << field) & 0xFFFFFFFF
    return ((value & (~mask)) | ((replacement << field) & mask)) & 0xFFFFFFFF
end

local function __read_vector(self, key)
    if key == "X" or key == "x" then return rawget(self, "_x") or 0 end
    if key == "Y" or key == "y" then return rawget(self, "_y") or 0 end
    if key == "Z" or key == "z" then return rawget(self, "_z") or 0 end
    if key == "Magnitude" or key == "magnitude" then
        local x = rawget(self, "_x") or 0
        local y = rawget(self, "_y") or 0
        local z = rawget(self, "_z") or 0
        return math.sqrt(x * x + y * y + z * z)
    end
    if key == "Unit" or key == "unit" then
        local x = rawget(self, "_x") or 0
        local y = rawget(self, "_y") or 0
        local z = rawget(self, "_z") or 0
        local m = math.sqrt(x * x + y * y + z * z)
        if m == 0 then
            return rawget(self, "_z") ~= nil and _wrap_v3(0, 0, 1) or _wrap_v2(0, 0)
        end
        return rawget(self, "_z") ~= nil and _wrap_v3(x / m, y / m, z / m) or _wrap_v2(x / m, y / m)
    end
end

local function __write_vector(self, key, value)
    value = tonumber(value) or 0
    if key == "X" or key == "x" then rawset(self, "_x", value); return end
    if key == "Y" or key == "y" then rawset(self, "_y", value); return end
    if key == "Z" or key == "z" then rawset(self, "_z", value); return end
    rawset(self, key, value)
end

local function __v2_binary(a, b, op)
    local ax, ay = a.X, a.Y
    if type(b) == "number" then
        return _wrap_v2(op(ax, b), op(ay, b))
    end
    return _wrap_v2(op(ax, b.X), op(ay, b.Y))
end

local function __v3_binary(a, b, op)
    local ax, ay, az = a.X, a.Y, a.Z
    if type(b) == "number" then
        return _wrap_v3(op(ax, b), op(ay, b), op(az, b))
    end
    return _wrap_v3(op(ax, b.X), op(ay, b.Y), op(az, b.Z))
end

local __v2mt = {
    __index = __read_vector,
    __newindex = __write_vector,
    __add = function(a, b) return __v2_binary(a, b, function(x, y) return x + y end) end,
    __sub = function(a, b) return __v2_binary(a, b, function(x, y) return x - y end) end,
    __mul = function(a, b) return __v2_binary(a, b, function(x, y) return x * y end) end,
    __div = function(a, b) return __v2_binary(a, b, function(x, y) return x / y end) end,
    __unm = function(a) return _wrap_v2(-a.X, -a.Y) end,
    __tostring = function(a) return string.format("(%s, %s)", tostring(a.X), tostring(a.Y)) end,
}

local __v3mt = {
    __index = __read_vector,
    __newindex = __write_vector,
    __add = function(a, b) return __v3_binary(a, b, function(x, y) return x + y end) end,
    __sub = function(a, b) return __v3_binary(a, b, function(x, y) return x - y end) end,
    __mul = function(a, b) return __v3_binary(a, b, function(x, y) return x * y end) end,
    __div = function(a, b) return __v3_binary(a, b, function(x, y) return x / y end) end,
    __unm = function(a) return _wrap_v3(-a.X, -a.Y, -a.Z) end,
    __tostring = function(a) return string.format("(%s, %s, %s)", tostring(a.X), tostring(a.Y), tostring(a.Z)) end,
}

function _wrap_v2(x, y)
    return setmetatable({ _x = tonumber(x) or 0, _y = tonumber(y) or 0 }, __v2mt)
end

function _wrap_v3(x, y, z)
    return setmetatable({ _x = tonumber(x) or 0, _y = tonumber(y) or 0, _z = tonumber(z) or 0 }, __v3mt)
end

-- Type constructors
Vector3 = { new = _wrap_v3 }
Vector2 = { new = _wrap_v2 }
CFrame  = {
    new = function(a, b, c)
        return _cfnew(a, b, c)
    end,
    lookAt = function(at, target, up)
        return _cflookat(at, target, up)
    end,
}
Color3  = { new = _c3new, fromRGB = _c3fromRGB }
Color3.fromHSV = function(h, s, v)
    h = tonumber(h) or 0
    s = tonumber(s) or 0
    v = tonumber(v) or 0

    local i = math.floor(h * 6)
    local f = h * 6 - i
    local p = v * (1 - s)
    local q = v * (1 - f * s)
    local t = v * (1 - (1 - f) * s)
    local m = i % 6

    if m == 0 then return Color3.new(v, t, p) end
    if m == 1 then return Color3.new(q, v, p) end
    if m == 2 then return Color3.new(p, v, t) end
    if m == 3 then return Color3.new(p, q, v) end
    if m == 4 then return Color3.new(t, p, v) end
    return Color3.new(v, p, q)
end
Color3.fromHex = function(hex)
    hex = tostring(hex or ""):gsub("#", "")
    if #hex == 3 then
        hex = hex:sub(1,1) .. hex:sub(1,1) .. hex:sub(2,2) .. hex:sub(2,2) .. hex:sub(3,3) .. hex:sub(3,3)
    end
    if #hex ~= 6 then return Color3.new(0, 0, 0) end
    local r = tonumber(hex:sub(1, 2), 16) or 0
    local g = tonumber(hex:sub(3, 4), 16) or 0
    local b = tonumber(hex:sub(5, 6), 16) or 0
    return Color3.fromRGB(r, g, b)
end
Drawing = { new = _drawing_new }
Drawing.Fonts = {
    UI = 0,
    System = 1,
    SystemBold = 2,
    Minecraft = 3,
    Monospace = 4,
    Pixel = 5,
    Fortnite = 6,
}

Enum = {
    KeyCode = {
        MouseButton1 = 1,
        MouseButton2 = 2,
        MouseMovement = 0,
        Backspace = 8,
        Tab = 9,
        Enter = 13,
        Shift = 16,
        Control = 17,
        Alt = 18,
        Pause = 19,
        CapsLock = 20,
        Escape = 27,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Insert = 45,
        Delete = 46,
        Zero = 48,
        One = 49,
        Two = 50,
        Three = 51,
        Four = 52,
        Five = 53,
        Six = 54,
        Seven = 55,
        Eight = 56,
        Nine = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        W = 87,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        X = 88,
        Y = 89,
        Z = 90,
        LeftWindows = 91,
        RightWindows = 92,
        NumPad0 = 96,
        NumPad1 = 97,
        NumPad2 = 98,
        NumPad3 = 99,
        NumPad4 = 100,
        NumPad5 = 101,
        NumPad6 = 102,
        NumPad7 = 103,
        NumPad8 = 104,
        NumPad9 = 105,
        Multiply = 106,
        Add = 107,
        Subtract = 109,
        Decimal = 110,
        Divide = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        NumLock = 144,
        ScrollLock = 145,
        LeftShift = 160,
        RightShift = 161,
        LeftControl = 162,
        RightControl = 163,
        LeftAlt = 164,
        RightAlt = 165,
        Semicolon = 186,
        Equals = 187,
        Comma = 188,
        Minus = 189,
        Period = 190,
        Slash = 191,
        Backquote = 192,
        LeftBracket = 219,
        Backslash = 220,
        RightBracket = 221,
        Quote = 222,
    },
    UserInputType = {
        MouseMovement = 0,
        MouseButton1 = 1,
        MouseButton2 = 2,
        Keyboard = 3,
    },
}

-- Luau vector library
vector = {
    create = function(x, y, z) return _wrap_v3(x or 0, y or 0, z or 0) end,
    magnitude = function(v)
        local z = v.z or 0
        return math.sqrt(v.x*v.x + v.y*v.y + z*z)
    end,
    normalize = function(v)
        local z = v.z or 0
        local m = math.sqrt(v.x*v.x + v.y*v.y + z*z)
        if m == 0 then return {x=0, y=0, z=1} end
        return {x=v.x/m, y=v.y/m, z=z/m}
    end,
}

local function __make_event()
    local handlers = {}
    return {
        Connect = function(self, fn)
            table.insert(handlers, fn)
            return {
                Disconnect = function()
                    for i, handler in ipairs(handlers) do
                        if handler == fn then
                            table.remove(handlers, i)
                            break
                        end
                    end
                end
            }
        end,
        Fire = function(self, ...)
            for _, fn in ipairs(handlers) do
                local ok, err = pcall(fn, ...)
                if not ok then
                    _rawerr("event: " .. tostring(err))
                end
            end
        end,
        HandlerCount = function(self)
            return #handlers
        end,
        Clear = function(self)
            table.clear(handlers)
        end,
    }
end

local function __make_input_object(keyCode, userInputType, position)
    return {
        KeyCode = keyCode,
        UserInputType = userInputType,
        Position = position,
    }
end

local __input_state = {
    mouse = _input_get_mouse_pos(),
    mouse1 = _input_is_mouse1_down(),
    mouse2 = _input_is_mouse2_down(),
    keys = {},
}

for name, code in pairs(Enum.KeyCode) do
    if type(code) == "number" and code > 2 then
        __input_state.keys[code] = _input_is_key_down(code)
    end
end

UserInputService = {
    InputBegan = __make_event(),
    InputEnded = __make_event(),
    InputChanged = __make_event(),
    IsKeyDown = function(self, key) return _input_is_key_down(key) end,
    GetMouseLocation = function(self) return _input_get_mouse_pos() end,
}

RunService = {
    RenderStepped = __make_event(),
    Heartbeat = __make_event(),
    Stepped = __make_event(),
}

__has_active_connections = false

function __pump_input()
    local mouse = _input_get_mouse_pos()
    if mouse.X ~= __input_state.mouse.X or mouse.Y ~= __input_state.mouse.Y then
        UserInputService.InputChanged:Fire(__make_input_object(Enum.KeyCode.MouseMovement, Enum.UserInputType.MouseMovement, mouse), false)
        __input_state.mouse = mouse
    end

    local mouse1 = _input_is_mouse1_down()
    if mouse1 ~= __input_state.mouse1 then
        local input = __make_input_object(Enum.KeyCode.MouseButton1, Enum.UserInputType.MouseButton1, mouse)
        if mouse1 then UserInputService.InputBegan:Fire(input, false) else UserInputService.InputEnded:Fire(input, false) end
        __input_state.mouse1 = mouse1
    end

    local mouse2 = _input_is_mouse2_down()
    if mouse2 ~= __input_state.mouse2 then
        local input = __make_input_object(Enum.KeyCode.MouseButton2, Enum.UserInputType.MouseButton2, mouse)
        if mouse2 then UserInputService.InputBegan:Fire(input, false) else UserInputService.InputEnded:Fire(input, false) end
        __input_state.mouse2 = mouse2
    end

    for name, code in pairs(Enum.KeyCode) do
        if type(code) == "number" and code > 2 then
            local down = _input_is_key_down(code)
            if down ~= __input_state.keys[code] then
                local input = __make_input_object(code, Enum.UserInputType.Keyboard, mouse)
                if down then UserInputService.InputBegan:Fire(input, false) else UserInputService.InputEnded:Fire(input, false) end
                __input_state.keys[code] = down
            end
        end
    end

    __has_active_connections =
        UserInputService.InputBegan:HandlerCount() > 0 or
        UserInputService.InputEnded:HandlerCount() > 0 or
        UserInputService.InputChanged:HandlerCount() > 0 or
        RunService.RenderStepped:HandlerCount() > 0 or
        RunService.Heartbeat:HandlerCount() > 0 or
        RunService.Stepped:HandlerCount() > 0
end

function __pump_runservice(delta)
    RunService.RenderStepped:Fire(delta)
    RunService.Heartbeat:Fire(delta)
    RunService.Stepped:Fire(delta)
    __has_active_connections = __has_active_connections or
        RunService.RenderStepped:HandlerCount() > 0 or
        RunService.Heartbeat:HandlerCount() > 0 or
        RunService.Stepped:HandlerCount() > 0
end

-- Helper function to wrap C# instance objects for property access only
local function wrap_instance(inst)
    if inst == nil then return nil end
    -- If it's a primitive, return as-is
    if type(inst) ~= "userdata" then return inst end
    
    -- For C# objects, return them unwrapped to preserve NLua method binding
    -- NLua will handle method calls through the C# object itself
    return inst
end

-- Only proxy for FindFirstChild fallback - don't interfere with methods
local function make_instance_children_accessor(inst)
    if inst == nil then return inst end
    if type(inst) ~= "userdata" then return inst end
    
    -- Don't wrap - return instances directly
    -- Methods will work through NLua's native binding
    -- For child access, users must use :FindFirstChild()
    return inst
end

-- Simple helper wrapper: if game is available, allow script to use it directly
-- The C# GetService is already exposed via NLua binding
if game ~= nil then
    print("[bootstrap] Game object initialized successfully")
    
    -- Create a pure Lua table proxy to intercept property access
    local game_csharp = game
    local game_mt = {}
    
    function game_mt.__index(self, key)
        -- Try key "GetService" to return a bound method
        if key == "GetService" then
            return function(self, name)
                if game_csharp.GetService == nil then
                    warn("[proxy] GetService method not found on game_csharp")
                    return nil
                end
                local result = game_csharp:GetService(name)
                return make_instance_children_accessor(result)
            end
        end

        -- HttpGet must be forwarded as a bound call on the C# instance
        if key == "HttpGet" then
            return function(self, url, content)
                return game_csharp:HttpGet(url)
            end
        end
        
        -- Try direct property first
        local direct = game_csharp[key]
        if direct ~= nil then 
            return make_instance_children_accessor(direct)
        end
        
        -- Fallback to GetService for convenience (game.Players instead of game:GetService("Players"))
        if game_csharp.GetService == nil then
            warn("[proxy] GetService method not found, key=" .. tostring(key))
            return nil
        end
        
        local result = game_csharp:GetService(key)
        
        if result == nil then
            warn("[proxy] GetService(" .. tostring(key) .. ") returned nil")
        else
            print("[proxy] GetService(" .. tostring(key) .. ") returned: " .. tostring(result))
        end
        
        return make_instance_children_accessor(result)
    end
    
    function game_mt.__newindex(self, key, value)
        game_csharp[key] = value
    end
    
    -- Create proxy table
    local game_proxy = setmetatable({}, game_mt)
    
    -- Replace game with the proxy
    game = game_proxy
    
    print("[bootstrap] Game proxy setup complete")
else
    warn("[bootstrap] Game object is nil - some services may not be available")
end

-- Convenience wrappers
function getplayers()
    if game == nil then return {} end
    local svc = game:GetService("Players")
    if svc == nil then return {} end
    return svc:GetPlayers()
end

function getlocalplayer()
    if game == nil then return nil end
    local svc = game:GetService("Players")
    if svc == nil then return nil end
    return svc.LocalPlayer
end

printl = print
load = loadstring

function run_secure(text)
    local fn = loadstring(text)
    if fn == nil then
        return nil
    end
    return pcall(fn)
end

-- typeof: Roblox-style type introspection (not in standard Lua)
-- Returns the __type metamethod name for userdata, otherwise type()
function typeof(v)
    local t = type(v)
    if t == "userdata" or t == "table" then
        local mt = getmetatable(v)
        if mt then
            local name = rawget(mt, "__type") or rawget(mt, "__name")
            if name then return tostring(name) end
        end
    end
    -- Map Lua primitive type names to Roblox equivalents
    if t == "number" then return "number" end
    if t == "string" then return "string" end
    if t == "boolean" then return "boolean" end
    if t == "nil" then return "nil" end
    if t == "function" then return "function" end
    if t == "thread" then return "thread" end
    return t
end
""";
    }
}
