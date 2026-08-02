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
        // Workspace command pipeline offsets
        public const long WorkspaceCurrentCommand = 0x860;
        public const long WorkspaceStickyCommand = 0x870;

        // MouseCommand workspace pointer offset
        public const long MouseCommandWorkspace = 0x50;

        // Size of the tool object allocation
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
        // Found by statically locating the WorldRoot method-name table at
        // 0x611D030 (Raycast at index 21) and the matching 29-entry
        // BoundFuncDesc pointer table at 0x65EB940 (index 21 → 0x8091390),
        // cross-confirmed via the registration code in sub_528320.
        public const long RaycastBoundDesc = 0x8091390;
        public const long RaycastBoundFn   = 0x80;
    }

    // ── ADD MORE OFFSETS BELOW ────────────────────────────────
    // Example:
    // public static class MyFeature
    // {
    //     public const long SomeOffset = 0x1234;
    // }
}
