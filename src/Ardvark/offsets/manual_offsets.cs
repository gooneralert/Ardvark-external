/* =============================================================
   MANUAL OFFSETS
   -------------------------------------------------------------
   This file contains offsets that are manually maintained.

   These offsets are specific to certain features (like btools)
   and must be found by hand using IDA Pro / ReClass.NET / x64dbg.
   They may need to be updated manually when Roblox updates.

   To add a new offset:
   1. Add a new static class for the feature (or add to an
      existing one).
   2. Add a public const long for each offset.
   3. Reference it in the feature code via ManualOffsets.ClassName.OffsetName

   See offsets_tutorial.md for instructions on how to find
   these offsets using IDA Pro and other tools.
   =============================================================
*/

namespace ManualOffsets
{
    // ── BTOOLS ────────────────────────────────────────────────
    // Building tools (Hammer / Grab / Clone) offsets.
    // These are used by features/games/universal/btools/btools.cs
    public static class Btools
    {
        // Workspace command pipeline offsets (shared_ptr<MouseCommand> pairs)
        // Found via IDA against the currently-targeted RobloxPlayerBeta build:
        //   Workspace::Process (sub_14120FA60):
        //     - `mov r12, [r15+870h]`        → currentCommand at 0x870
        //     - `mov rax, [r15+878h]`        → currentCommand refcount at 0x878
        //     - `lock inc dword ptr [rax+8]` → refcount object (shared_ptr control)
        //   ChangeMouseCommand (sub_141206490):
        //     - `lea r12, [r14+870h]`        → currentCommand shared_ptr at 0x870
        //     - `lea rdx, [r14+880h]`        → stickyCommand shared_ptr at 0x880
        // NOTE: these are +8 vs the older build documented in BTOOLS_OFFSETS_GUIDE.md
        // (0x868/0x870/0x878/0x880). Re-verify with ChangeMouseCommand after updates.
        public const long WorkspaceCurrentCommand = 0x870;
        public const long WorkspaceCurrentRefCount = 0x878;
        public const long WorkspaceStickyCommand = 0x880;
        public const long WorkspaceStickyRefCount = 0x888;

        // MouseCommand workspace pointer offset
        // Found via IDA: HammerTool constructor (sub_142183830)
        //   - `*((_QWORD *)a1 + 10) = a2` → workspace ptr at +80 (0x50)
        public const long MouseCommandWorkspace = 0x50;

        // Size of the tool object allocation
        // Found via IDA (factory -> sub_142CFF5C0(N) allocation size):
        //   - HammerTool: sub_142183770 (152)  → 0x98
        //   - GrabTool:   sub_142182270 (208)  → 0xD0
        //   - CloneTool:  sub_142182FE0 (152)  → 0x98
        // Using the largest (0xD0) is safe for all tools.
        public const int ToolAllocationSize = 0xD0;
    }

    // ── WORLDROOT (RAYCAST) ───────────────────────────────────
    // Used by raycast silent aim.
    // RaycastBoundDesc is an RVA into the module — add it to the
    // module base at runtime, then read the BoundFuncDesc struct.
    // RaycastBoundFn is the offset within the desc to the function
    // pointer (almost always 0x80).
    //
    // See offsets_tutorial.md → "MCP guide: find RaycastBoundDesc"
    // for how to find RaycastBoundDesc after a Roblox update.
    public static class WorldRoot
    {
        // RVA of the Raycast BoundFuncDesc — add to module base at runtime,
        // then read the BoundFuncDesc struct (+0x80 holds the bound fn ptr).
        // Found by locating the WorldRoot method-name table (Raycast at index 21)
        // and cross-confirming via the registration function sub_142590E20,
        // which writes the desc at qword_1481E7150 (RVA 0x81E7150).
        public const long RaycastBoundDesc = 0x82012a0;
        public const long RaycastBoundFn   = 0x80;
    }

    // ── ADD MORE OFFSETS BELOW ────────────────────────────────
    // Example:
    // public static class MyFeature
    // {
    //     public const long SomeOffset = 0x1234;
    // }
}
