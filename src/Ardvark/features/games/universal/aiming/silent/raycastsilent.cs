using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FoulzExternal.SDK;
using FoulzExternal.SDK.caches;
using FoulzExternal.SDK.structures;
using FoulzExternal.SDK.worldtoscreen;
using FoulzExternal.storage;
using Offsets;
using Options;
using FoulzExternal.features.games.universal.checks.teamcheck;
using FoulzExternal.features.games.universal.checks.downedcheck;
using FoulzExternal.features.games.universal.checks.transparencycheck;
using ManualOffsets;
using SDKInstance = FoulzExternal.SDK.Instance;

namespace FoulzExternal.features.games.universal.aiming.silent
{
    /// <summary>
    /// Raycast-based silent aim. Hooks the WorldRoot::Raycast BoundFuncDesc
    /// function pointer so that raycasts are redirected toward a target.
    ///
    /// Requires ManualOffsets.WorldRoot.RaycastBoundDesc (RVA) to be filled in
    /// after finding it via the IDA guide (see offsets_tutorial.md).
    /// </summary>
    internal static class raycastsilent
    {
        // ── P/Invoke ───────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, UIntPtr dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandleExA(uint dwFlags, string lpModuleName, out IntPtr phModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", EntryPoint = "ReadProcessMemory")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;   // BYTE* — 8 bytes on x64!
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DOS_HEADER
        {
            public ushort e_magic;
            public ushort e_cblp;
            public ushort e_cp;
            public ushort e_crlc;
            public ushort e_cparhdr;
            public ushort e_minalloc;
            public ushort e_maxalloc;
            public ushort e_ss;
            public ushort e_sp;
            public ushort e_csum;
            public ushort e_ip;
            public ushort e_cs;
            public ushort e_lfarlc;
            public ushort e_ovno;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] e_res1;
            public ushort e_oemid;
            public ushort e_oeminfo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public ushort[] e_res2;
            public int e_lfanew;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_FILE_HEADER
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_OPTIONAL_HEADER64
        {
            public ushort Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public uint SizeOfCode;
            public uint SizeOfInitializedData;
            public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint;
            public uint BaseOfCode;
            public ulong ImageBase;
            public uint SectionAlignment;
            public uint FileAlignment;
            public ushort MajorOperatingSystemVersion;
            public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion;
            public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion;
            public ushort MinorSubsystemVersion;
            public uint Win32VersionValue;
            public uint SizeOfImage;
            public uint SizeOfHeaders;
            public uint CheckSum;
            public ushort Subsystem;
            public ushort DllCharacteristics;
            public ulong SizeOfStackReserve;
            public ulong SizeOfStackCommit;
            public ulong SizeOfHeapReserve;
            public ulong SizeOfHeapCommit;
            public uint LoaderFlags;
            public uint NumberOfRvaAndSizes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public IMAGE_DATA_DIRECTORY[] DataDirectory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Name;
            public uint VirtualSize;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint PointerToRelocations;
            public uint PointerToLinenumbers;
            public ushort NumberOfRelocations;
            public ushort NumberOfLinenumbers;
            public uint Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DATA_DIRECTORY
        {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CFG_INFO
        {
            public UIntPtr Offset;
            public uint Flags;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool SetProcessValidCallTargetsFn(IntPtr hProcess, IntPtr baseAddress, UIntPtr regionSize, uint count, ref CFG_INFO info);

        // ── Constants ──────────────────────────────────────────────
        private const uint PAGE_EXECUTE = 0x10;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_GUARD = 0x100;
        private const uint PAGE_NOACCESS = 0x01;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_PRIVATE = 0x20000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint CFG_CALL_TARGET_VALID = 0x00000001;

        // ── Offsets ────────────────────────────────────────────────
        private static readonly long DescRva = WorldRoot.RaycastBoundDesc;
        private static readonly long BoundFnOffset = WorldRoot.RaycastBoundFn;
        private const int StubBytes = 0x200;

        // ── RaycastState (matches C++ layout) ──────────────────────
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct RaycastState
        {
            public uint active;
            public uint reserved;
            public float target_x;
            public float target_y;
            public float target_z;
            public float scale;
            public ulong calls;
            public float cam_x; // 0x20 — skip wallbang if origin ~ cam
            public float cam_y;
            public float cam_z;
        }

        // ── Hook state ─────────────────────────────────────────────
        private class HookState
        {
            public long thunk = 0;
            public long state = 0;
            public long originalFunction = 0;
            public long moduleBase = 0;
            public bool thunkOwned = false;
            public bool installed = false;
            public bool active = false;
        }

        private static readonly HookState g_hook = new();
        private static bool g_wallbang = false;
        private static DateTime g_lastFail = DateTime.MinValue;

        // ── Threading ──────────────────────────────────────────────
        private static bool running = false;
        private static Thread? thread;
        private static readonly object locker = new();

        // ── Public API ─────────────────────────────────────────────
        public static bool Ready() => g_hook.installed;
        public static bool Aiming() => g_hook.active;
        public static bool WallbangMode() => g_wallbang;
        public static long OriginalHandler() => g_hook.originalFunction;

        public static void Start()
        {
            lock (locker)
            {
                if (running) return;
                running = true;
                thread = new Thread(Loop) { IsBackground = true };
                thread.Start();
            }
        }

        public static void Stop()
        {
            lock (locker)
            {
                running = false;
                thread?.Join();
            }
        }

        // ── Main loop ──────────────────────────────────────────────
        private static void Loop()
        {
            var cache = new Dictionary<long, long>();

            while (running)
            {
                try
                {
                    if (SDKInstance.Mem == null) { Thread.Sleep(50); continue; }

                    var s = Options.Settings.Silent;
                    bool key = s.SilentAimbotKey.IsPressed();
                    bool active = s.SilentAimbot && (s.AlwaysOn || key);

                    // Determine if raycast silent or magic bullet is enabled
                    // Suppress when in Phantom Forces (PF uses its own silent aim)
                    bool raycastOn = active && (s.SilentMethod == 2 || s.SilentMethod == 3) && !phantomsilent.IsActivePlace();
                    bool magicOn = active && s.SilentMethod == 3 && !phantomsilent.IsActivePlace();

                    if (!raycastOn && !magicOn)
                    {
                        if (g_hook.active) SetActive(false, new Vector3(), false);
                        Ensure(false);
                        Thread.Sleep(10);
                        continue;
                    }

                    if (!Storage.IsInitialized) { Thread.Sleep(50); continue; }

                    // Ensure hook is installed
                    Ensure(true);
                    if (!g_hook.installed) { Thread.Sleep(100); continue; }

                    // Find target
                    var target = FindTarget(s, cache);
                    if (target.address != 0)
                    {
                        var worldPos = GetBonePos(target, Settings.Aiming.TargetBone, cache);
                        bool wallbang = magicOn; // magic bullet = wallbang mode
                        SetActive(true, worldPos, wallbang);
                    }
                    else
                    {
                        SetActive(false, new Vector3(), false);
                    }
                }
                catch { }
                Thread.Sleep(5);
            }

            // Cleanup on exit
            SetActive(false, new Vector3(), false);
            Ensure(false);
        }

        // ── Target finding (shared logic with silentaiming) ────────
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
                if (dist < closest && dist <= s.SFOV)
                {
                    closest = dist;
                    best = p;
                }
            }
            return best;
        }

        private static Vector3 GetBonePos(RobloxPlayer p, int id, Dictionary<long, long> cache)
        {
            bool r15 = p.RigType == 1;
            SDKInstance part = new SDKInstance(0);

            switch (id)
            {
                case 0: part = p.Head; break;
                case 1: part = p.HumanoidRootPart.IsValid ? p.HumanoidRootPart : (r15 ? p.Upper_Torso : p.Torso); break;
                case 2: part = r15 ? (p.Left_Hand.IsValid ? p.Left_Hand : p.Left_Lower_Arm) : p.Left_Arm; break;
                case 3: part = r15 ? (p.Right_Hand.IsValid ? p.Right_Hand : p.Right_Lower_Arm) : p.Right_Arm; break;
                case 4: part = r15 ? (p.Left_Foot.IsValid ? p.Left_Foot : p.Left_Lower_Leg) : p.Left_Leg; break;
                case 5: part = r15 ? (p.Right_Foot.IsValid ? p.Right_Foot : p.Right_Lower_Leg) : p.Right_Leg; break;
                default: part = p.Head; break;
            }

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

                    pos.x += vel.x * px;
                    pos.y += vel.y * py;
                    pos.z += vel.z * px;
                }
            }
            return pos;
        }

        // ── Hook installation / removal ────────────────────────────

        private static bool OkAddr(long a) => a >= 0x10000 && a < 0x00007FFFFFFFFFFF;

        private static bool WMem(long a, byte[] d)
        {
            if (!OkAddr(a) || d == null || d.Length == 0) return false;
            return SDKInstance.Mem.WriteRaw(a, d, d.Length);
        }

        private static bool ReadVal(long a, byte[] d)
        {
            if (!OkAddr(a) || d == null) return false;
            return SDKInstance.Mem.ReadRaw(a, d, d.Length);
        }

        private static uint QueryProtect(long a)
        {
            MEMORY_BASIC_INFORMATION mbi;
            if (VirtualQueryEx(SDKInstance.Mem.Handle, (IntPtr)a, out mbi, (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                return 0;
            return mbi.Protect;
        }

        private static bool IsExecutableProtect(uint p)
        {
            uint x = p & 0xFF;
            return x == PAGE_EXECUTE || x == PAGE_EXECUTE_READ ||
                   x == PAGE_EXECUTE_READWRITE || x == PAGE_EXECUTE_WRITECOPY;
        }

        private static int PageSize()
        {
            return 0x1000;
        }

        private static bool ProtectRemote(long address, int size, uint protection, out uint oldProtect)
        {
            oldProtect = 0;
            if (!OkAddr(address) || size == 0) return false;

            int pg = PageSize();
            long pageMask = ~(long)(pg - 1);
            long baseAddr = address & pageMask;
            long end = (address + size + pg - 1) & pageMask;
            int span = (int)(end - baseAddr);

            if (VirtualProtectEx(SDKInstance.Mem.Handle, (IntPtr)baseAddr, (UIntPtr)span, protection, out oldProtect))
                return true;

            // Fallback: try writecopy variants
            if (protection == PAGE_EXECUTE_READWRITE)
                return VirtualProtectEx(SDKInstance.Mem.Handle, (IntPtr)baseAddr, (UIntPtr)span, PAGE_EXECUTE_WRITECOPY, out oldProtect);
            if (protection == PAGE_READWRITE)
                return VirtualProtectEx(SDKInstance.Mem.Handle, (IntPtr)baseAddr, (UIntPtr)span, PAGE_WRITECOPY, out oldProtect);

            return false;
        }

        private static bool WriteProtected(long address, byte[] data)
        {
            if (!OkAddr(address) || data == null || data.Length == 0) return false;
            bool changed = ProtectRemote(address, data.Length, PAGE_EXECUTE_READWRITE, out uint oldProt);
            bool wrote = WMem(address, data);
            if (changed)
                ProtectRemote(address, data.Length, oldProt, out _);
            return wrote;
        }

        private static void AppendU64(List<byte> c, ulong v)
        {
            byte[] b = BitConverter.GetBytes(v);
            c.AddRange(b);
        }

        private static void PatchRel32(List<byte> c, int o, int t)
        {
            int v = t - (o + 4);
            byte[] b = BitConverter.GetBytes(v);
            c[o] = b[0]; c[o + 1] = b[1]; c[o + 2] = b[2]; c[o + 3] = b[3];
        }

        /// <summary>
        /// Builds the hook thunk (x86-64). Redirects raycast origin/direction
        /// toward the target when active, then calls the original function.
        /// </summary>
        private static byte[] MakeHookThunk(long state, long orig)
        {
            var c = new List<byte>(384);
            var inactive = new List<int>();

            void JeInactive()
            {
                c.Add(0x0F); c.Add(0x84);
                inactive.Add(c.Count);
                c.Add(0); c.Add(0); c.Add(0); c.Add(0);
            }
            void JbeInactive()
            {
                c.Add(0x0F); c.Add(0x86);
                inactive.Add(c.Count);
                c.Add(0); c.Add(0); c.Add(0); c.Add(0);
            }

            // sub rsp, 68h
            c.Add(0x48); c.Add(0x83); c.Add(0xEC); c.Add(0x68);
            // mov r10, <state>
            c.Add(0x49); c.Add(0xBA);
            AppendU64(c, (ulong)state);
            // cmp dword ptr [r10], 0
            c.Add(0x41); c.Add(0x83); c.Add(0x3A); c.Add(0x00);
            JeInactive();
            // test r8, r8
            c.Add(0x4D); c.Add(0x85); c.Add(0xC0); JeInactive();
            // test r9, r9
            c.Add(0x4D); c.Add(0x85); c.Add(0xC9); JeInactive();

            // movss xmm0, [r10+08h]  (target.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x42); c.Add(0x08);
            // subss xmm0, [r8]       (origin.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x00);
            // movss [rsp+40h], xmm0
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x44); c.Add(0x24); c.Add(0x40);
            // movss xmm1, [r10+0Ch]  (target.y)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x4A); c.Add(0x0C);
            // subss xmm1, [r8+04h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x48); c.Add(0x04);
            // movss [rsp+44h], xmm1
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x4C); c.Add(0x24); c.Add(0x44);
            // movss xmm2, [r10+10h]  (target.z)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x52); c.Add(0x10);
            // subss xmm2, [r8+08h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x50); c.Add(0x08);
            // movss [rsp+48h], xmm2
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x54); c.Add(0x24); c.Add(0x48);

            // movaps xmm3, xmm0
            c.Add(0x0F); c.Add(0x28); c.Add(0xD8);
            // mulss xmm3, xmm3
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xDB);
            // movaps xmm4, xmm1
            c.Add(0x0F); c.Add(0x28); c.Add(0xE1);
            // mulss xmm4, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xE4);
            // addss xmm3, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xDC);
            // movaps xmm4, xmm2
            c.Add(0x0F); c.Add(0x28); c.Add(0xE2);
            // mulss xmm4, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xE4);
            // addss xmm3, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xDC);
            // sqrtss xmm3, xmm3
            c.Add(0xF3); c.Add(0x0F); c.Add(0x51); c.Add(0xDB);
            // xorps xmm5, xmm5
            c.Add(0x0F); c.Add(0x57); c.Add(0xED);
            // ucomiss xmm5, xmm3
            c.Add(0x0F); c.Add(0x2E); c.Add(0xDD);
            JbeInactive();

            // movss xmm4, [r9]       (direction.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x21);
            // mulss xmm4, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xE4);
            // movss xmm5, [r9+04h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x69); c.Add(0x04);
            // mulss xmm5, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xED);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE5);
            // movss xmm5, [r9+08h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x69); c.Add(0x08);
            // mulss xmm5, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xED);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE5);
            // sqrtss xmm4, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x51); c.Add(0xE4);
            // xorps xmm5, xmm5
            c.Add(0x0F); c.Add(0x57); c.Add(0xED);
            // ucomiss xmm5, xmm4
            c.Add(0x0F); c.Add(0x2E); c.Add(0xE5);
            JbeInactive();

            // mov eax, [r10+04h]  (reserved / wallbang flag)
            c.Add(0x41); c.Add(0x8B); c.Add(0x42); c.Add(0x04);
            // test al, 1
            c.Add(0xA8); c.Add(0x01);
            // jne wallbang_off
            c.Add(0x0F); c.Add(0x85);
            int wallbangJmp = c.Count;
            c.Add(0); c.Add(0); c.Add(0); c.Add(0);

            // ── Normal mode (dir-only): redirect direction toward target ──
            int dirOnlyOff = c.Count;
            // movaps xmm5, xmm4   (len of direction)
            c.Add(0x0F); c.Add(0x28); c.Add(0xEC);
            // divss xmm5, xmm3    (direction_len / distance)
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5E); c.Add(0xEB);
            // mulss xmm0, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xC5);
            // mulss xmm1, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xCD);
            // mulss xmm2, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xD5);
            // movss [r9], xmm0
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x11); c.Add(0x01);
            // movss [r9+04h], xmm1
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x11); c.Add(0x49); c.Add(0x04);
            // movss [r9+08h], xmm2
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x11); c.Add(0x51); c.Add(0x08);
            // inc qword ptr [r10+18h]
            c.Add(0x49); c.Add(0xFF); c.Add(0x42); c.Add(0x18);
            // jmp to_call
            c.Add(0xE9);
            int toCall = c.Count;
            c.Add(0); c.Add(0); c.Add(0); c.Add(0);

            // ── Wallbang mode: redirect both origin and direction ──
            int wallbangOff = c.Count;
            PatchRel32(c, wallbangJmp, wallbangOff);

            // origin ~ cam check: if origin is near camera, skip wallbang (use dir-only)
            // movss xmm4, [r8]        (origin.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x20);
            // subss xmm4, [r10+0x20]  (cam.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x62); c.Add(0x20);
            // mulss xmm4, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xE4);
            // movss xmm5, [r8+04h]    (origin.y)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x68); c.Add(0x04);
            // subss xmm5, [r10+0x24]  (cam.y)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x6A); c.Add(0x24);
            // mulss xmm5, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xED);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE5);
            // movss xmm5, [r8+08h]    (origin.z)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x68); c.Add(0x08);
            // subss xmm5, [r10+0x28]  (cam.z)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x5C); c.Add(0x6A); c.Add(0x28);
            // mulss xmm5, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x59); c.Add(0xED);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE5);
            // mov eax, 9.0f (0x41100000)
            c.Add(0xB8); c.Add(0x00); c.Add(0x00); c.Add(0x10); c.Add(0x41);
            // movd xmm5, eax
            c.Add(0x66); c.Add(0x0F); c.Add(0x6E); c.Add(0xE8);
            // comiss xmm5, xmm4  (if 9.0 > dist², jump to dir_only)
            c.Add(0x0F); c.Add(0x2F); c.Add(0xE5);
            // jb dir_only_off
            c.Add(0x0F); c.Add(0x82);
            int camJb = c.Count;
            c.Add(0); c.Add(0); c.Add(0); c.Add(0);
            PatchRel32(c, camJb, dirOnlyOff);

            // divss xmm0, xmm3
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5E); c.Add(0xC3);
            // divss xmm1, xmm3
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5E); c.Add(0xCB);
            // divss xmm2, xmm3
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5E); c.Add(0xD3);

            // movaps xmm4, xmm0
            c.Add(0x0F); c.Add(0x28); c.Add(0xE0);
            // mulss xmm4, [r10+14h]  (scale)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x59); c.Add(0x62); c.Add(0x14);
            // movss xmm5, [r10+08h]  (target.x)
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x6A); c.Add(0x08);
            // subss xmm5, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5C); c.Add(0xEC);
            // movss [rsp+50h], xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x6C); c.Add(0x24); c.Add(0x50);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE4);
            // movss [rsp+40h], xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x64); c.Add(0x24); c.Add(0x40);

            // movaps xmm4, xmm1
            c.Add(0x0F); c.Add(0x28); c.Add(0xE1);
            // mulss xmm4, [r10+14h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x59); c.Add(0x62); c.Add(0x14);
            // movss xmm5, [r10+0Ch]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x6A); c.Add(0x0C);
            // subss xmm5, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5C); c.Add(0xEC);
            // movss [rsp+54h], xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x6C); c.Add(0x24); c.Add(0x54);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE4);
            // movss [rsp+44h], xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x64); c.Add(0x24); c.Add(0x44);

            // movaps xmm4, xmm2
            c.Add(0x0F); c.Add(0x28); c.Add(0xE2);
            // mulss xmm4, [r10+14h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x59); c.Add(0x62); c.Add(0x14);
            // movss xmm5, [r10+10h]
            c.Add(0xF3); c.Add(0x41); c.Add(0x0F); c.Add(0x10); c.Add(0x6A); c.Add(0x10);
            // subss xmm5, xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x5C); c.Add(0xEC);
            // movss [rsp+58h], xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x6C); c.Add(0x24); c.Add(0x58);
            // addss xmm4, xmm5
            c.Add(0xF3); c.Add(0x0F); c.Add(0x58); c.Add(0xE4);
            // movss [rsp+48h], xmm4
            c.Add(0xF3); c.Add(0x0F); c.Add(0x11); c.Add(0x64); c.Add(0x24); c.Add(0x48);

            // lea r8, [rsp+50h]   (new origin)
            c.Add(0x4C); c.Add(0x8D); c.Add(0x44); c.Add(0x24); c.Add(0x50);
            // lea r9, [rsp+40h]   (new direction)
            c.Add(0x4C); c.Add(0x8D); c.Add(0x4C); c.Add(0x24); c.Add(0x40);
            // inc qword ptr [r10+18h]
            c.Add(0x49); c.Add(0xFF); c.Add(0x42); c.Add(0x18);

            // ── Call original ──
            int callOff = c.Count;
            PatchRel32(c, toCall, callOff);
            int inactiveOff = c.Count;
            foreach (var o in inactive) PatchRel32(c, o, inactiveOff);

            // mov rax, [rsp+90h]  (restore return address area)
            c.Add(0x48); c.Add(0x8B); c.Add(0x84); c.Add(0x24); c.Add(0x90); c.Add(0x00); c.Add(0x00); c.Add(0x00);
            // mov [rsp+20h], rax
            c.Add(0x48); c.Add(0x89); c.Add(0x44); c.Add(0x24); c.Add(0x20);
            // mov rax, <orig>
            c.Add(0x48); c.Add(0xB8);
            AppendU64(c, (ulong)orig);
            // call rax
            c.Add(0xFF); c.Add(0xD0);
            // add rsp, 68h
            c.Add(0x48); c.Add(0x83); c.Add(0xC4); c.Add(0x68);
            // ret
            c.Add(0xC3);

            return c.ToArray();
        }

        /// <summary>
        /// Simple jmp thunk (passthrough mode — no hooking, just redirect).
        /// </summary>
        private static byte[] MakeJmpThunk(long orig)
        {
            var c = new List<byte>();
            // jmp [rip+0]
            c.Add(0xFF); c.Add(0x25); c.Add(0x00); c.Add(0x00); c.Add(0x00); c.Add(0x00);
            AppendU64(c, (ulong)orig);
            return c.ToArray();
        }

        private static bool RegionIsPadding(long a, int n)
        {
            byte[] buf = new byte[n];
            if (!ReadVal(a, buf)) return false;
            foreach (var b in buf)
                if (b != 0xCC && b != 0x00 && b != 0x90) return false;
            return true;
        }

        private const uint IMAGE_DOS_SIGNATURE = 0x5A4D; // MZ
        private const uint IMAGE_NT_SIGNATURE = 0x00004550; // PE\0\0
        private const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000;

        private static bool ReadModuleVal(long addr, byte[] buf)
        {
            return ReadProcessMemory(SDKInstance.Mem.Handle, (IntPtr)addr, buf, (UIntPtr)buf.Length, out _);
        }

        /// <summary>
        /// Finds a padding cave inside a specific module's executable sections.
        /// Mirrors the C++ reference's find_cave_in_module. The cave address is
        /// 16-byte aligned and must satisfy run_len >= need + alignment loss.
        /// </summary>
        private static long FindCaveInModule(long moduleBase, int need, long minOffset, long ignore)
        {
            if (!OkAddr(moduleBase) || need <= 0) return 0;

            byte[] dosBuf = new byte[Marshal.SizeOf<IMAGE_DOS_HEADER>()];
            if (!ReadModuleVal(moduleBase, dosBuf)) return 0;
            IMAGE_DOS_HEADER dos = BytesToStruct<IMAGE_DOS_HEADER>(dosBuf);
            if (dos.e_magic != IMAGE_DOS_SIGNATURE) return 0;

            long ntAddr = moduleBase + dos.e_lfanew;

            byte[] ntSigBuf = new byte[4];
            if (!ReadModuleVal(ntAddr, ntSigBuf)) return 0;
            uint sig = BitConverter.ToUInt32(ntSigBuf, 0);
            if (sig != IMAGE_NT_SIGNATURE) return 0;

            // IMAGE_FILE_HEADER is at ntAddr + 4
            byte[] fhBuf = new byte[Marshal.SizeOf<IMAGE_FILE_HEADER>()];
            if (!ReadModuleVal(ntAddr + 4, fhBuf)) return 0;
            IMAGE_FILE_HEADER fh = BytesToStruct<IMAGE_FILE_HEADER>(fhBuf);
            ushort numSections = fh.NumberOfSections;
            ushort sizeOptHeader = fh.SizeOfOptionalHeader;

            // IMAGE_SECTION_HEADER array starts after optional header
            long sectionBase = ntAddr + 4 + Marshal.SizeOf<IMAGE_FILE_HEADER>() + sizeOptHeader;
            int sectionSize = Marshal.SizeOf<IMAGE_SECTION_HEADER>();

            for (int i = 0; i < numSections; i++)
            {
                byte[] shBuf = new byte[sectionSize];
                if (!ReadModuleVal(sectionBase + i * sectionSize, shBuf)) break;
                IMAGE_SECTION_HEADER sh = BytesToStruct<IMAGE_SECTION_HEADER>(shBuf);

                if ((sh.Characteristics & IMAGE_SCN_MEM_EXECUTE) == 0) continue;

                uint sectionOff = sh.VirtualAddress;
                uint sectionSizeV = sh.VirtualSize != 0 ? sh.VirtualSize : sh.SizeOfRawData;
                if (sectionOff == 0 || sectionSizeV < need) continue;

                long scanOff = sectionOff;
                if (scanOff < minOffset) scanOff = minOffset;
                if (scanOff >= sectionOff + sectionSizeV) continue;

                long scanStart = moduleBase + scanOff;
                int scanSize = (int)(sectionOff + sectionSizeV - scanOff);

                byte[] buf = new byte[scanSize];
                if (!ReadModuleVal(scanStart, buf)) continue;

                int runStart = 0;
                int runLen = 0;
                for (int j = 0; j < buf.Length; j++)
                {
                    byte b = buf[j];
                    if (b != 0x00 && b != 0xCC && b != 0x90)
                    {
                        runLen = 0;
                        runStart = j + 1;
                        continue;
                    }
                    runLen++;
                    if (runLen < need) continue;

                    long cand = scanStart + runStart;
                    long aligned = (cand + 0x0F) & ~(long)0x0F;
                    long loss = aligned - cand;
                    if (runLen < need + loss) continue;
                    if (ignore != 0 && aligned == ignore) continue;
                    if (g_hook.thunk != 0 && aligned == g_hook.thunk) continue;
                    return aligned;
                }
            }
            return 0;
        }

        private static T BytesToStruct<T>(byte[] bytes) where T : struct
        {
            GCHandle h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(h.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                h.Free();
            }
        }

        private static long GetRemoteModuleBase(string name)
        {
            IntPtr snap = CreateToolhelp32Snapshot(0x00000008 /*TH32CS_SNAPMODULE*/, (uint)Storage.ProcessId);
            if (snap == IntPtr.Zero || snap == (IntPtr)(-1)) return 0;
            try
            {
                MODULEENTRY32 me = new MODULEENTRY32();
                me.dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>();
                if (Module32First(snap, ref me))
                {
                    do
                    {
                    if (string.Equals(me.szModule, name, StringComparison.OrdinalIgnoreCase))
                            return me.modBaseAddr.ToInt64();
                    } while (Module32Next(snap, ref me));
                }
            }
            finally
            {
                CloseHandle(snap);
            }
            return 0;
        }

        /// <summary>
        /// Finds an executable padding cave to host the hook stub.
        /// Mirrors the C++ reference's find_exec_cave: it FIRST tries a set of
        /// system DLLs (winsta.dll, win32u.dll, ...) whose executable code pages
        /// are NOT CFG-guarded, because a stub placed in such a DLL can be called
        /// through a CFG-checked indirect call without needing a bitmap update.
        /// Only if all system modules fail does it fall back to a brute-force
        /// VirtualQueryEx scan (which may land inside a CFG-protected module).
        /// </summary>
        private static long FindExecCave(int need, long ignore = 0)
        {
            string[] pref = {
                "winsta.dll", "win32u.dll", "uxtheme.dll", "dwmapi.dll",
                "msctf.dll", "TextInputFramework.dll", "CoreMessaging.dll", "user32.dll"
            };

            for (int mi = 0; mi < pref.Length; mi++)
            {
                long mod = GetRemoteModuleBase(pref[mi]);
                if (mod == 0) continue;
                long minOff = (mi == 0) ? 0x2000L : 0x1000L;
                long cave = FindCaveInModule(mod, need, minOff, ignore);
                if (cave != 0) return cave;
            }

            IntPtr handle = SDKInstance.Mem.Handle;
            MEMORY_BASIC_INFORMATION mbi;
            long addr = 0;
            long fallbackRwx = 0;

            while (VirtualQueryEx(handle, (IntPtr)addr, out mbi, (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != 0)
            {
                long baseAddr = mbi.BaseAddress.ToInt64();
                long size = (long)mbi.RegionSize.ToUInt64();
                addr = baseAddr + size;
                if (addr < baseAddr) break;

                if (mbi.State != MEM_COMMIT) continue;
                if ((mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0) continue;
                if (!IsExecutableProtect(mbi.Protect)) continue;
                if (size < need) continue;

                if (g_hook.thunk != 0 && baseAddr <= g_hook.thunk && g_hook.thunk < addr)
                    continue;

                for (long off = 0; off + need <= size; off += 0x10)
                {
                    long cand = baseAddr + off;
                    if (ignore != 0 && cand == ignore) continue;
                    if (RegionIsPadding(cand, need))
                        return cand;
                }

                if (fallbackRwx == 0 && mbi.Type == MEM_PRIVATE &&
                    (mbi.Protect & 0xFF) == PAGE_EXECUTE_READWRITE &&
                    size >= need + 0x40)
                {
                    long cand = baseAddr + size - need;
                    if (ignore == 0 || cand != ignore)
                        fallbackRwx = cand;
                }
            }

            return fallbackRwx;
        }

        private static long AllocExecPage()
        {
            long p = SDKInstance.Mem.Allocate(PageSize());
            if (p == 0) return 0;

            uint prot = QueryProtect(p);
            if (!IsExecutableProtect(prot))
            {
                VirtualFreeEx(SDKInstance.Mem.Handle, (IntPtr)p, UIntPtr.Zero, MEM_RELEASE);
                return 0;
            }
            return p;
        }

        private static void FreeMem(long addr)
        {
            VirtualFreeEx(SDKInstance.Mem.Handle, (IntPtr)addr, UIntPtr.Zero, MEM_RELEASE);
        }

        /// <summary>
        /// Marks the stub as a valid CFG (Control Flow Guard) call target.
        /// Roblox is built with CFG enabled; without this, calling the patched
        /// function pointer in the BoundFuncDesc triggers an instant crash.
        /// </summary>
        private static bool MarkCfg(long t)
        {
            IntPtr proc = IntPtr.Zero;
            string[] mods = { "kernelbase.dll", "kernel32.dll", "api-ms-win-core-memory-l1-1-3.dll" };
            foreach (var m in mods)
            {
                IntPtr h = GetModuleHandle(m);
                if (h == IntPtr.Zero) h = LoadLibrary(m);
                if (h == IntPtr.Zero) continue;
                proc = GetProcAddress(h, "SetProcessValidCallTargets");
                if (proc != IntPtr.Zero) break;
            }
            if (proc == IntPtr.Zero) return false;

            int pg = PageSize();
            CFG_INFO info = new CFG_INFO
            {
                Offset = (UIntPtr)((ulong)t & (ulong)(pg - 1)),
                Flags = CFG_CALL_TARGET_VALID
            };

            var fn = (SetProcessValidCallTargetsFn)Marshal.GetDelegateForFunctionPointer(proc, typeof(SetProcessValidCallTargetsFn));
            return fn(SDKInstance.Mem.Handle, (IntPtr)(t & ~(long)(pg - 1)), (UIntPtr)pg, 1, ref info);
        }

        public static bool Install()
        {
            if (g_hook.installed) return true;
            if (DescRva == 0) return false; // offset not found yet

            long baseAddr = Storage.BaseAddress;
            if (baseAddr == 0) return false;

            var now = DateTime.Now;
            if (g_lastFail != DateTime.MinValue && (now - g_lastFail).TotalMilliseconds < 1500)
                return false;

            long slot = baseAddr + DescRva + BoundFnOffset;
            long fn = SDKInstance.Mem.ReadPtr(slot);

            if (!OkAddr(fn))
            {
                g_lastFail = now;
                return false;
            }

            if (g_hook.state == 0) g_hook.state = SDKInstance.Mem.Allocate(PageSize());
            if (g_hook.state == 0)
            {
                g_lastFail = now;
                return false;
            }

            byte[] thunk = MakeHookThunk(g_hook.state, fn);

            if (thunk.Length > StubBytes)
            {
                g_lastFail = now;
                return false;
            }

            bool owned = false;
            long stub = 0;
            long ignoreCave = 0;

            for (int attempt = 0; attempt < 8 && stub == 0; attempt++)
            {
                long cand = FindExecCave(StubBytes, ignoreCave);
                if (cand == 0) break;

                if (!WriteProtected(cand, thunk))
                {
                    ignoreCave = cand;
                    continue;
                }

                stub = cand;
                owned = false;
            }

            if (stub == 0)
            {
                stub = AllocExecPage();
                owned = stub != 0;
                if (stub != 0)
                {
                    if (!WriteProtected(stub, thunk))
                    {
                        FreeMem(stub);
                        stub = 0;
                        owned = false;
                    }
                }
            }

            if (stub == 0)
            {
                g_lastFail = now;
                return false;
            }

            // Write empty state
            RaycastState empty = new RaycastState();
            byte[] stateBytes = StructureToBytes(empty);
            if (!WMem(g_hook.state, stateBytes))
            {
                if (owned) FreeMem(stub);
                g_lastFail = now;
                return false;
            }

            FlushInstructionCache(SDKInstance.Mem.Handle, (IntPtr)stub, (UIntPtr)thunk.Length);

            // Mark the stub as a valid CFG call target (required for Roblox's
            // Control Flow Guard; without this the patched desc fn ptr crashes).
            MarkCfg(stub);

            uint prot = QueryProtect(stub);
            if (!IsExecutableProtect(prot))
            {
                if (owned) FreeMem(stub);
                g_lastFail = now;
                return false;
            }

            // Patch the slot to point to our thunk
            ProtectRemote(slot, 8, PAGE_READWRITE, out _);
            byte[] stubBytes = BitConverter.GetBytes(stub);
            if (!WriteProtected(slot, stubBytes) || SDKInstance.Mem.ReadPtr(slot) != stub)
            {
                if (owned) FreeMem(stub);
                g_lastFail = now;
                return false;
            }

            g_hook.moduleBase = baseAddr;
            g_hook.originalFunction = fn;
            g_hook.thunk = stub;
            g_hook.thunkOwned = owned;
            g_hook.installed = true;
            g_hook.active = false;
            return true;
        }

        public static void Remove()
        {
            if (g_hook.installed && OkAddr(g_hook.originalFunction) && g_hook.moduleBase != 0)
            {
                long slot = g_hook.moduleBase + DescRva + BoundFnOffset;
                byte[] origBytes = BitConverter.GetBytes(g_hook.originalFunction);
                WriteProtected(slot, origBytes);
            }

            if (g_hook.thunk != 0 && !g_hook.thunkOwned)
            {
                byte[] pad = new byte[StubBytes];
                for (int i = 0; i < pad.Length; i++) pad[i] = 0xCC;
                WriteProtected(g_hook.thunk, pad);
            }

            if (g_hook.thunk != 0 && g_hook.thunkOwned)
                FreeMem(g_hook.thunk);
            if (g_hook.state != 0)
                FreeMem(g_hook.state);

            g_hook.thunk = 0;
            g_hook.state = 0;
            g_hook.originalFunction = 0;
            g_hook.moduleBase = 0;
            g_hook.thunkOwned = false;
            g_hook.installed = false;
            g_hook.active = false;
            g_wallbang = false;
        }

        private static long s_lastBase = 0;

        public static void Ensure(bool want)
        {
            long baseAddr = Storage.BaseAddress;

            if (g_hook.installed && baseAddr != 0 && s_lastBase != 0 && baseAddr != s_lastBase)
            {
                Remove();
            }
            if (baseAddr != 0) s_lastBase = baseAddr;

            if (want)
            {
                if (baseAddr == 0) return;
                if (!g_hook.installed) Install();
            }
            else if (g_hook.installed)
            {
                Remove();
            }
        }

        public static void SetActive(bool on, Vector3 worldTarget, bool wallbang)
        {
            if (!on)
            {
                if (g_hook.active && g_hook.state != 0)
                {
                    uint v = 0;
                    WMem(g_hook.state, BitConverter.GetBytes(v));
                    g_hook.active = false;
                }
                g_wallbang = false;
                return;
            }

            if (!g_hook.installed) return;

            float[] pos = { worldTarget.x, worldTarget.y, worldTarget.z };
            uint flags = wallbang ? 1u : 0u;
            float scale = 1.15f;
            uint one = 1;

            // Camera position — used by the stub to skip wallbang when origin ~ camera
            float[] cam = { 0f, 0f, 0f };
            try
            {
                var camInst = Storage.CameraInstance;
                if (camInst.IsValid)
                {
                    var cp = SDKInstance.Mem.Read<Vector3>(camInst.Address + Offsets.Camera.Position);
                    cam[0] = cp.x; cam[1] = cp.y; cam[2] = cp.z;
                }
            }
            catch { }

            // Write reserved (wallbang flag)
            WMem(g_hook.state + 4, BitConverter.GetBytes(flags));
            // Write target position
            byte[] posBytes = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(pos[0]), 0, posBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos[1]), 0, posBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos[2]), 0, posBytes, 8, 4);
            WMem(g_hook.state + 8, posBytes);
            // Write scale
            WMem(g_hook.state + 20, BitConverter.GetBytes(scale));
            // Write camera position (state + 0x20)
            byte[] camBytes = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(cam[0]), 0, camBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(cam[1]), 0, camBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(cam[2]), 0, camBytes, 8, 4);
            WMem(g_hook.state + 32, camBytes);
            // Write active flag
            WMem(g_hook.state, BitConverter.GetBytes(one));

            g_hook.active = true;
            g_wallbang = wallbang;
        }

        // ── Helpers ────────────────────────────────────────────────
        private static byte[] StructureToBytes<T>(T s) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(s, ptr, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            Marshal.FreeHGlobal(ptr);
            return bytes;
        }
    }
}