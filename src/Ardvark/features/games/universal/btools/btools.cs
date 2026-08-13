using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using FoulzExternal.SDK;
using FoulzExternal.storage;
using Offsets;

namespace FoulzExternal.features.games.universal.btools
{
    internal static class btools
    {
        // Offsets sourced from ManualOffsets (offsets/manual_offsets.cs)
        // Workspace Command pipeline is a shared_ptr<MouseCommand> pair:
        //   [ptr + 0x00] = command pointer
        //   [ptr + 0x08] = refcount control block (shared_ptr ctrl)
        private const long WorkspaceCurrentCommand = ManualOffsets.Btools.WorkspaceCurrentCommand;
        private const long WorkspaceCurrentRefCount = ManualOffsets.Btools.WorkspaceCurrentRefCount;
        private const long WorkspaceStickyCommand = ManualOffsets.Btools.WorkspaceStickyCommand;
        private const long WorkspaceStickyRefCount = ManualOffsets.Btools.WorkspaceStickyRefCount;
        private const long MouseCommandWorkspace = ManualOffsets.Btools.MouseCommandWorkspace;
        private const int ToolAllocationSize = ManualOffsets.Btools.ToolAllocationSize;

        // Internal constants (don't change between Roblox versions)
        // The real HammerTool constructor (sub_1421C15C0) sets the
        // ref count at offset 8 to -1. We must do the same or the
        // game's shared_ptr ref-counting will crash.
        private const int ToolRefCountOffset = 0x8;
        private const long ToolRefCountValue = -1;

        // Control block structure (shared_ptr refcount object)
        // The game does `lock inc dword ptr [ctrl+8]` on Workspace::Process
        // and `_InterlockedExchangeAdd(ctrl+12, -1)` on release — so +8 (uses)
        // and +12 (weaks) must be 1/0. The old "ControlBlockValue1 = 0x7FFFFFF0"
        // is a large use-count so the game never actually deletes our block.
        private const int ControlBlockSize = 0x20;
        private const int ControlBlockField1 = 0x8;
        private const int ControlBlockField2 = 0xC;
        private const int ControlBlockValue1 = 0x7FFFFFF0;
        private const int ControlBlockValue2 = 1;

        private static readonly string[] HammerNames = { ".?AVHammerTool@RBX@@", ".?AVHammerTool@@", null! };
        private static readonly string[] GrabNames = { ".?AVGrabTool@RBX@@", ".?AVGrabTool@@", ".?AVDragTool@RBX@@", ".?AVDragTool@@", null! };
        private static readonly string[] CloneNames = { ".?AVCloneTool@RBX@@", ".?AVCloneTool@@", null! };

        // State
        private static long hammer_vtable = 0;
        private static long grab_vtable = 0;
        private static long clone_vtable = 0;
        private static long active_tool = 0;
        private static long active_ctrl = 0;
        private static long original_current_object = 0;
        private static long original_current_control = 0;
        private static long original_sticky_object = 0;
        private static long original_sticky_control = 0;
        private static bool saved_original = false;
        private static int last_activated_tool = -1; // tracks which tool was last activated

        // Public state
        public static bool Enabled = false;
        public static int SelectedTool = 0; // 0 = Hammer, 1 = Grab, 2 = Clone

        private static bool running;
        private static Thread? thread;
        private static readonly object locker = new();

        // PE section structure
        private struct Section
        {
            public long address;
            public uint size;
            public uint characteristics;
        }

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

        private static void Loop()
        {
            while (running)
            {
                try
                {
                    if (!Enabled)
                    {
                        if (active_tool != 0)
                            Deactivate();
                        last_activated_tool = -1;
                        Thread.Sleep(100);
                        continue;
                    }

                    if (!Storage.IsInitialized || SDK.Instance.Mem == null)
                    { Thread.Sleep(100); continue; }

                    var mem = SDK.Instance.Mem;

                    // Discover vtables if not found yet
                    if (hammer_vtable == 0 && grab_vtable == 0 && clone_vtable == 0)
                    {
                        Discover(mem);
                    }

                    // Get workspace from local player's mouse
                    var lp = Storage.LocalPlayerInstance;
                    if (!lp.IsValid) { Thread.Sleep(100); continue; }

                    long mouse = mem.ReadPtr(lp.Address + Player.Mouse);
                    if (mouse == 0) { Thread.Sleep(100); continue; }

                    long workspace = mem.ReadPtr(mouse + PlayerMouse.Workspace);
                    if (workspace == 0) { Thread.Sleep(100); continue; }

                    // Only activate if the tool selection changed or not yet activated
                    if (SelectedTool != last_activated_tool)
                    {
                        // Deactivate current tool first
                        if (active_tool != 0)
                        {
                            // Restore original state
                            if (saved_original)
                            {
                                mem.Write(workspace + WorkspaceCurrentCommand, original_current_object);
                                mem.Write(workspace + WorkspaceCurrentRefCount, original_current_control);
                                mem.Write(workspace + WorkspaceStickyCommand, original_sticky_object);
                                mem.Write(workspace + WorkspaceStickyRefCount, original_sticky_control);
                            }
                            active_tool = 0;
                            active_ctrl = 0;
                            saved_original = false;
                        }

                        // Activate the selected tool
                        bool activated = false;
                        switch (SelectedTool)
                        {
                            case 0:
                                activated = ActivateHammer(mem, workspace);
                                break;
                            case 1:
                                activated = ActivateGrab(mem, workspace);
                                break;
                            case 2:
                                activated = ActivateClone(mem, workspace);
                                break;
                        }
                        if (activated)
                            last_activated_tool = SelectedTool;
                    }
                }
                catch { }

                Thread.Sleep(100);
            }
        }

        private static List<Section> GetPESections(Memory mem)
        {
            var sections = new List<Section>();
            long baseAddr = Storage.BaseAddress;
            if (baseAddr == 0) return sections;

            try
            {
                uint peOffset = mem.Read<uint>(baseAddr + 0x3C);
                ushort sectionCount = mem.Read<ushort>(baseAddr + peOffset + 6);
                ushort optionalSize = mem.Read<ushort>(baseAddr + peOffset + 0x14);
                long sectionTable = baseAddr + peOffset + 0x18 + optionalSize;

                for (ushort index = 0; index < sectionCount && index < 64; ++index)
                {
                    uint virtualSize = mem.Read<uint>(sectionTable + index * 40 + 8);
                    uint virtualAddress = mem.Read<uint>(sectionTable + index * 40 + 12);
                    uint flags = mem.Read<uint>(sectionTable + index * 40 + 36);

                    if (virtualSize > 0 && virtualSize < 0x10000000)
                    {
                        sections.Add(new Section
                        {
                            address = baseAddr + virtualAddress,
                            size = virtualSize,
                            characteristics = flags
                        });
                    }
                }
            }
            catch { }

            return sections;
        }

        private static List<Section> GetReadableSections(List<Section> sections)
        {
            var readable = new List<Section>();
            foreach (var section in sections)
            {
                if ((section.characteristics & 0x40000000) != 0)
                {
                    readable.Add(section);
                }
            }
            return readable;
        }

        private static long FindVTableByRtti(Memory mem, string name, List<Section> readableSections)
        {
            long baseAddr = Storage.BaseAddress;
            if (baseAddr == 0 || string.IsNullOrEmpty(name)) return 0;

            int nameLength = name.Length;
            long typeInfo = 0;
            uint typeDescriptorRva = 0;

            // Find the type descriptor by scanning for the name string
            foreach (var section in readableSections)
            {
                if (typeInfo != 0) break;
                uint chunkSize = 0x10000;
                for (uint offset = 0; offset < section.size && typeInfo == 0; offset += chunkSize)
                {
                    uint readSize = Math.Min(chunkSize + 256, section.size - offset);
                    byte[] chunk = new byte[readSize];
                    if (!mem.ReadRaw(section.address + offset, chunk, (int)readSize)) continue;

                    for (int index = 0; index + nameLength + 1 <= readSize; ++index)
                    {
                        bool match = true;
                        for (int i = 0; i < nameLength; i++)
                        {
                            if (chunk[index + i] != (byte)name[i])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match && chunk[index + nameLength] == 0)
                        {
                            typeInfo = section.address + offset + index - 16;
                            typeDescriptorRva = (uint)(typeInfo - baseAddr);
                            break;
                        }
                    }
                }
            }

            if (typeInfo == 0) return 0;

            // Find the complete object locator that references this type descriptor
            long completeObjectLocator = 0;
            foreach (var section in readableSections)
            {
                if (completeObjectLocator != 0) break;
                uint chunkSize = 0x10000;
                for (uint offset = 0; offset < section.size && completeObjectLocator == 0; offset += chunkSize)
                {
                    uint readSize = Math.Min(chunkSize, section.size - offset);
                    byte[] chunk = new byte[readSize];
                    if (!mem.ReadRaw(section.address + offset, chunk, (int)readSize)) continue;

                    for (int index = 0; index + 24 <= readSize; index += 4)
                    {
                        uint signature = BitConverter.ToUInt32(chunk, index);
                        uint descriptor = BitConverter.ToUInt32(chunk, index + 12);
                        if (signature == 1 && descriptor == typeDescriptorRva)
                        {
                            completeObjectLocator = section.address + offset + index;
                            break;
                        }
                    }
                }
            }

            if (completeObjectLocator == 0) return 0;

            // Find the vtable that points to this complete object locator
            foreach (var section in readableSections)
            {
                uint chunkSize = 0x10000;
                for (uint offset = 0; offset < section.size; offset += chunkSize)
                {
                    uint readSize = Math.Min(chunkSize, section.size - offset);
                    byte[] chunk = new byte[readSize];
                    if (!mem.ReadRaw(section.address + offset, chunk, (int)readSize)) continue;

                    for (int index = 0; index + 16 <= readSize; index += 8)
                    {
                        long locator = BitConverter.ToInt64(chunk, index);
                        if (locator == completeObjectLocator)
                        {
                            return section.address + offset + index + 8;
                        }
                    }
                }
            }

            return 0;
        }

        private static long FindFirstVTable(Memory mem, string[] names, List<Section> readableSections)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                long vtable = FindVTableByRtti(mem, name, readableSections);
                if (vtable != 0) return vtable;
            }
            return 0;
        }

        private static bool Discover(Memory mem)
        {
            var sections = GetPESections(mem);
            var readableSections = GetReadableSections(sections);

            hammer_vtable = FindFirstVTable(mem, HammerNames, readableSections);
            grab_vtable = FindFirstVTable(mem, GrabNames, readableSections);
            clone_vtable = FindFirstVTable(mem, CloneNames, readableSections);

            return hammer_vtable != 0 || grab_vtable != 0 || clone_vtable != 0;
        }

        /// <summary>
        /// Creates a fake shared_ptr control block for the Workspace command slots.
        /// The game reads ctrl+8 (use count) and ctrl+12 (weak count) via
        /// `_InterlockedIncrement` / `_InterlockedExchangeAdd`. We set a huge
        /// use-count so the game never deletes our block but increments/decrements
        /// it safely.
        /// </summary>
        private static long MakeControlBlock(Memory mem)
        {
            long control = mem.Allocate(ControlBlockSize);
            if (control == 0) return 0;

            byte[] zeroes = new byte[ControlBlockSize];
            mem.WriteRaw(control, zeroes, zeroes.Length);
            mem.Write(control + ControlBlockField1, ControlBlockValue1);
            mem.Write(control + ControlBlockField2, ControlBlockValue2);
            return control;
        }

        /// <summary>
        /// Allocates a valid NameContainer for a fake Instance so that
        /// Instance::GetName() — which Roblox changed to resolve
        /// NameContainer → *NameContainer + Name — does not dereference
        /// null and crash the game.
        /// </summary>
        private static long MakeNameContainer(Memory mem, string name)
        {
            const int containerSize = 0x40;
            long container = mem.Allocate(containerSize);
            if (container == 0) return 0;

            byte[] zeroes = new byte[containerSize];
            mem.WriteRaw(container, zeroes, zeroes.Length);

            // The std::string lives at container + Offsets.Instance.Name (0x8).
            long str = container + Offsets.Instance.Name;
            byte[] strData = Encoding.UTF8.GetBytes(name);

            if (strData.Length >= 16)
            {
                // Long string: heap pointer + size + capacity.
                long heap = mem.Allocate(16);
                if (heap == 0) return 0;
                byte[] buf = new byte[16];
                Array.Copy(strData, buf, Math.Min(16, strData.Length));
                mem.WriteRaw(heap, buf, buf.Length);
                mem.Write(str, heap);
            }
            else
            {
                // SSO: bytes inline (buffer already zeroed).
                byte[] sso = new byte[16];
                Array.Copy(strData, sso, strData.Length);
                mem.WriteRaw(str, sso, sso.Length);
            }

            // _Mysize (offset 0x18) then _Myres (offset 0x20), write as long
            // so the upper 4 bytes are 0 (little-endian: int read still works).
            mem.Write(str + 0x18, (long)strData.Length);
            mem.Write(str + 0x20, 15L); // SSO capacity

            return container;
        }

        private static bool ActivateWithVTable(Memory mem, long workspace, long vtable)
        {
            if (workspace == 0 || vtable == 0) return false;

            // Sanity-check the vtable is a valid pointer within the Roblox module.
            // Writing a discovered-but-garbage vtable into the fake tool object would
            // make the game call into invalid memory → instant crash.
            long modBase = Storage.BaseAddress;
            long modSize = Storage.ModuleSize;
            if (modBase == 0) return false;
            long modEnd = modSize > 0 ? modBase + modSize : modBase + 0x10000000;
            if (vtable < modBase || vtable >= modEnd) return false;

            if (!saved_original)
            {
                original_current_object = mem.ReadPtr(workspace + WorkspaceCurrentCommand);
                original_current_control = mem.ReadPtr(workspace + WorkspaceCurrentRefCount);
                original_sticky_object = mem.ReadPtr(workspace + WorkspaceStickyCommand);
                original_sticky_control = mem.ReadPtr(workspace + WorkspaceStickyRefCount);
                saved_original = true;
            }

            long tool = mem.Allocate(ToolAllocationSize);
            if (tool == 0) return false;

            byte[] zeroes = new byte[ToolAllocationSize];
            mem.WriteRaw(tool, zeroes, zeroes.Length);

            // Match the real HammerTool constructor layout:
            //   offset 0: vtable
            //   offset 8: ref count = -1 (shared_ptr)
            //   offset 0x50: workspace pointer
            mem.Write(tool, vtable);
            mem.Write(tool + ToolRefCountOffset, ToolRefCountValue);
            mem.Write(tool + MouseCommandWorkspace, workspace);

            // Give the fake tool a valid NameContainer. Roblox changed
            // Instance::GetName() to resolve NameContainer → name; with a
            // zeroed NameContainer (0x70 == 0) the game dereferences null
            // while processing the tool's name → crash.
            long nameContainer = MakeNameContainer(mem, "Tool");
            if (nameContainer == 0) return false;
            mem.Write(tool + Offsets.Instance.NameContainer, nameContainer);

            // Allocate one shared control block for both command slots.
            long control = MakeControlBlock(mem);
            if (control == 0) return false;

            // Install the fake tool as both currentCommand (0x870/0x878) and
            // stickyCommand (0x880/0x888) — each a shared_ptr pair.
            // Verified (current build):
            //   Workspace::Process (sub_14120FA60) reads [0x870] + incs [0x878]
            //   ChangeMouseCommand (sub_141206490) installs current [0x870]
            //     and sticky [0x880] shared_ptr slots.
            mem.Write(workspace + WorkspaceCurrentCommand, tool);
            mem.Write(workspace + WorkspaceCurrentRefCount, control);
            mem.Write(workspace + WorkspaceStickyCommand, tool);
            mem.Write(workspace + WorkspaceStickyRefCount, control);

            active_tool = tool;
            active_ctrl = control;
            return true;
        }

        private static bool ActivateHammer(Memory mem, long workspace)
        {
            return ActivateWithVTable(mem, workspace, hammer_vtable);
        }

        private static bool ActivateGrab(Memory mem, long workspace)
        {
            return ActivateWithVTable(mem, workspace, grab_vtable);
        }

        private static bool ActivateClone(Memory mem, long workspace)
        {
            return ActivateWithVTable(mem, workspace, clone_vtable);
        }

        private static bool Deactivate()
        {
            if (!Storage.IsInitialized || SDK.Instance.Mem == null) return false;
            var mem = SDK.Instance.Mem;

            // Find workspace to restore
            var lp = Storage.LocalPlayerInstance;
            if (lp.IsValid)
            {
                long mouse = mem.ReadPtr(lp.Address + Player.Mouse);
                if (mouse != 0)
                {
                    long workspace = mem.ReadPtr(mouse + PlayerMouse.Workspace);
                    if (workspace != 0 && saved_original)
                    {
                        mem.Write(workspace + WorkspaceCurrentCommand, original_current_object);
                        mem.Write(workspace + WorkspaceCurrentRefCount, original_current_control);
                        mem.Write(workspace + WorkspaceStickyCommand, original_sticky_object);
                        mem.Write(workspace + WorkspaceStickyRefCount, original_sticky_control);
                    }
                }
            }

            active_tool = 0;
            active_ctrl = 0;
            original_current_object = 0;
            original_current_control = 0;
            original_sticky_object = 0;
            original_sticky_control = 0;
            saved_original = false;
            return true;
        }
    }
}