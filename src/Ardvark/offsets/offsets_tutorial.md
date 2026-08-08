# Offsets Tutorial

This document explains how to find and update the **manual offsets** used by this project.

Manual offsets are offsets that are not available in auto-dumped offset files and must be found by hand using a disassembler (IDA Pro, Ghidra, x64dbg) or a memory inspection tool (ReClass.NET).

---

## Table of Contents

1. [Manual Offsets File](#manual-offsets-file)
2. [Tools You Need](#tools-you-need)
3. [How to Find Offsets in IDA Pro](#how-to-find-offsets-in-ida-pro)
4. [Btools Offsets — Step by Step](#btools-offsets--step-by-step)
   - [WorkspaceCurrentCommand](#workspacecurrentcommand)
   - [WorkspaceStickyCommand](#workspacestickycommand)
   - [MouseCommandWorkspace](#mousecommandworkspace)
   - [ToolAllocationSize](#toolallocationsize)
5. [MCP Guide: Finding RaycastBoundDesc](#mcp-guide-finding-raycastbounddesc)
6. [Btools Offsets Reference](#btools-offsets-reference)
7. [Updating Offsets After a Roblox Update](#updating-offsets-after-a-roblox-update)
8. [Adding New Offsets](#adding-new-offsets)

---

## Manual Offsets File

All manual offsets live in:

```
src/Ardvark/offsets/manual_offsets.cs
```

Structure:

```csharp
namespace ManualOffsets
{
    public static class Btools
    {
        public const long WorkspaceCurrentCommand  = 0x868;
        public const long WorkspaceCurrentRefCount = 0x870;
        public const long WorkspaceStickyCommand   = 0x878;
        public const long WorkspaceStickyRefCount  = 0x880;
        public const long MouseCommandWorkspace    = 0x50;
        public const int  ToolAllocationSize       = 0xD0;
    }
}
```

Usage in feature code:

```csharp
private const long WorkspaceCurrentCommand = ManualOffsets.Btools.WorkspaceCurrentCommand;
```

---

## Tools You Need

| Tool | Purpose |
|------|---------|
| **IDA Pro** (or Ghidra) | Static analysis — disassemble RobloxPlayerBeta.exe, find offsets by reading assembly. |
| **ReClass.NET** | Live memory inspection — attach to the running process and map out class layouts visually. |
| **x64dbg** | Dynamic debugging — set breakpoints, inspect registers, trace execution. |
| **Cheat Engine** | Memory scanning — find values and pointers in memory. |

> IDA Pro is recommended for finding offsets because you can search for strings, xrefs, and read the assembly without needing the game running.

---

## How to Find Offsets in IDA Pro

### General Workflow

1. **Open `RobloxPlayerBeta.exe` in IDA Pro.**
   - Wait for auto-analysis to finish.

2. **Find the relevant string or RTTI name.**
   - Press `Shift+F12` to open the Strings window.
   - Search for class names, property names, or RTTI strings (e.g., `.?AVHammerTool@RBX@@`).

3. **Follow cross-references (xrefs).**
   - Double-click the string to go to its location.
   - Press `X` to see all cross-references to that string.
   - Follow the xref to the function that uses it.

4. **Read the assembly to find the offset.**
   - Look for instructions like:
     ```
     mov rax, [rcx + 868h]   ; reads Workspace.CurrentCommand
     mov [r14 + 878h], rcx   ; writes Workspace.StickyCommand
     ```
   - The offset is the hex value after `rcx +` (or whatever register holds the object pointer).
   - **Important:** these are `shared_ptr<MouseCommand>` pairs — each slot has a pointer
     AND a separate refcount object 8 bytes later. `CurrentCommand` is at `0x868` with
     its refcount at `0x870`; `StickyCommand` is at `0x878` with its refcount at `0x880`.

5. **Note the offset and add it to `manual_offsets.cs`.**

### Finding RTTI / Vtable Names

Roblox uses C++ RTTI (Run-Time Type Information). Class names are mangled in the format:

```
.?AVClassName@RBX@@    (with RBX namespace)
.?AVClassName@@        (without namespace)
```

To find these in IDA:
1. `Shift+F12` → Strings window.
2. Search for `.?AV` to list all RTTI type descriptors.
3. Find the class you want (e.g., `HammerTool`, `GrabTool`, `CloneTool`).

---

## Btools Offsets — Step by Step

### WorkspaceCurrentCommand

This is the offset in the `Workspace` class that holds a pointer to the current `MouseCommand` object.

**In IDA Pro:**

1. Open Strings (`Shift+F12`) and search for `Workspace`.
2. Look for strings like `"currentCommand"`, `"stickyCommand"`.
3. Follow xrefs to the function that reads/writes this field.
4. In the assembly, look for:
   ```
   mov rax, [rcx + 868h]    ; reading CurrentCommand
   ```
   or
   ```
   mov [rcx + 868h], rax    ; writing CurrentCommand
   ```
5. The offset `0x868` is what you need.

**In ReClass.NET:**

1. Attach to the running Roblox process.
2. Navigate to the `Workspace` instance (found via `DataModel.Workspace`).
3. Scroll through the class fields. The `CurrentCommand` field is a pointer that changes when a building tool is active.
4. Note the offset.

---

### WorkspaceStickyCommand

This is the offset in the `Workspace` class that holds a pointer to the sticky `MouseCommand` object. It's typically right after `CurrentCommand`.

**In IDA Pro:**

1. Find the same function that references `CurrentCommand`.
2. Look nearby in the assembly for a second field access:
   ```
   mov [r14 + 878h], rcx    ; writing StickyCommand
   ```
3. The offset `0x878`.

**In ReClass.NET:**

1. In the `Workspace` instance, the `StickyCommand` is typically at `CurrentCommand + 0x10`.
2. It's a pointer that also changes when a building tool is active.

---

### MouseCommandWorkspace

This is the offset within a `MouseCommand` (or tool) object that points back to the `Workspace`.

**In IDA Pro:**

1. Search for `MouseCommand` or `MouseCommand::getWorkspace` in Strings/Functions.
2. Follow xrefs to the function that reads the workspace pointer from a MouseCommand.
3. Look for:
   ```
   mov rax, [rcx + 50h]    ; reading workspace from MouseCommand
   ```
4. The offset `0x50`.

**In ReClass.NET:**

1. Find a `MouseCommand` object (it's the `CurrentCommand` pointer from Workspace when a tool is active).
2. The field at offset `0x50` should point to the `Workspace` instance.

---

### ToolAllocationSize

This is the size of the tool object (`HammerTool`, `GrabTool`, `CloneTool`) that needs to be allocated.

**In IDA Pro:**

1. Find the `HammerTool` constructor (search for `.?AVHammerTool@RBX@@` → follow xrefs).
2. Follow xrefs to its constructor, then check the **factory** function (the caller that allocates
   then calls the constructor). Look for the allocation call (typically `operator new` or a custom
   allocator):
   ```
   sub_142C7DD30(152)          ; size = 152 → 0x98  (Hammer)
   sub_142C7DD30(208)          ; size = 208 → 0xD0  (Grab)
   sub_142C7DD30(152)          ; size = 152 → 0x98  (Clone)
   ```
3. The largest size across Hammer/Grab/Clone is `0xD0` — use that as `ToolAllocationSize`
   so one shared allocation is big enough for any tool.

**In ReClass.NET:**

1. Find a `HammerTool` object in memory (the `CurrentCommand` pointer when the hammer is active).
2. Check the object's size — it should be `0xD0` bytes.

---

### Btools RTTI vtable discovery (what btools.cs actually does)

`btools.cs` does **not** hardcode the Hammer/Grab/Clone vtables — it discovers them at runtime by
walking the C++ RTTI chain. This is important to understand so you can verify the primitive
offsets (especially `ToolAllocationSize`) against the real object layout.

The discovery chain (`FindVTableByRtti` in `btools.cs`):

1. **Find the type descriptor** — scan readable PE sections for the exact mangled RTTI name
   (`.?AVHammerTool@RBX@@`, `.?AVGrabTool@RBX@@`, `.?AVCloneTool@RBX@@`). The type descriptor
   (`RTTITypeDescriptor`) begins 16 bytes *before* the name string:
   ```
   [vftable ptr] [spare] [name ptr (RVA)]  -> "HammerTool@RBX@@"
   ```
   In the IDB the name pointer is a 32-bit RVA; the code computes it as
   `typeInfo = name_string_addr - 16`, `typeDescriptorRva = typeInfo - base`.

2. **Find the CompleteObjectLocator** — scan for the `(signature=1, typeDescriptorRva)` pair,
   which is the standard `RTTICompleteObjectLocator` (signature is the first dword, the type
   descriptor RVA is 12 bytes in).

3. **Find the vtable** — scan for a qword that equals the CompleteObjectLocator address. The
   vtable pointer (the value written at object offset `0`) is `locator_address + 8` (MSVC
   vtables store the COL pointer 8 bytes before the first virtual function).

**Validation guard (added to prevent crashes):** before writing a discovered vtable into the
fake tool object, `ActivateWithVTable` checks it falls **inside the Roblox module's address
range** (`Storage.BaseAddress` … `BaseAddress + ModuleSize`). If RTTI discovery returns garbage
(e.g. the scan races a game update, or the module base moved), the activation bails out instead
of installing a vtable that would make the game call invalid memory and crash.

---

## MCP Guide: Finding RaycastBoundDesc

Use this when an AI has **IDA Pro MCP** connected to a fresh `RobloxPlayerBeta.exe` IDB.
Goal: fill `ManualOffsets.WorldRoot.RaycastBoundDesc` and `RaycastBoundFn` in `offsets/manual_offsets.cs`.

Do **not** invent addresses. Every RVA must come from the open IDB (or a live process read).

### Hard facts (do not violate)

1. `BoundFuncDesc` for WorldRoot methods sits in **high `.data` / BSS**.
2. On disk / in a cold IDB the desc body is often **all `0x00` or `0xFF`**.
   - The name (`desc + 8`) and fn pointer (`desc + 0x80`) fields are filled **at runtime**.
3. Therefore these heuristics are **wrong** for this layout:
   - "xref to string `Raycast` → `place - 8` = BoundFuncDesc"
   - "scan for name pointer at `desc+8` in static file"
4. Correct static method (use the **IDA MCP tools**, not scripts):
   - Find the xref to the exact `"Raycast"` string → it lands in the **registration function**.
   - The registration function writes the desc's name field (`desc + 8`) and returns/uses the desc base address.
   - Cross-confirm by finding the **BoundFuncDesc pointer table** in `.rdata` (consecutive qwords pointing into high `.data`).
5. `RaycastBoundFn` is almost always **`0x80`**. Confirm live if a debugger/process is attached; otherwise keep `0x80`.

Current expected shape (example only — values change every update):

```csharp
public const long RaycastBoundDesc = 0x........; // RVA
public const long RaycastBoundFn   = 0x80;
```

Usage: `slot = module_base + RaycastBoundDesc + RaycastBoundFn`.

### Worked example (this repo's current build)

Recovered from the fresh Roblox IDB (imagebase `0x140000000`). Use it as a template for re-finding after an update.

1. **Find the exact strings** (use `find` with type `string`):
   - `WorldRoot` → `0x146cb8b30`
   - `FindPartOnRay` → `0x146cb8db8`
   - `Blockcast` → `0x146cb8e58`
   - `Raycast` → `0x146cb8d78`

2. **Locate the WorldRoot method-name table** — 16-byte pairs `(ptr "WorldRoot", ptr MethodName)` in `.rdata`. The `Raycast` entry is at `0x146854520`, so the table start is `0x1468543d0`:
   ```
   [00] ArePartsTouchingOthers
   [01] Blockcast
   [05] FindPartOnRay
   [21] Raycast          ← index 21
   ...
   ```

3. **Cross-confirm via the registration function** — xref to the `"Raycast"` string (`0x146cb8d78`) from `0x142590e79` lands in `sub_142590E20`. Decompiled:
   ```c
   qword_1481E7150 = (__int64)&off_1468A1268;       // desc + 0x00
   qword_1481E7158 = sub_140D65CD0(v12, "Raycast"); // desc + 0x08 = name
   xmmword_1481E7160 = *a9;                          // desc + 0x10 = (fn, 0)
   ```
   `qword_1481E7150` is the desc base → RVA = `0x1481E7150 - 0x140000000 = 0x81E7150`.

4. **Cross-confirm via the BoundFuncDesc pointer table** — the 8-byte pointer `0x1481E7150` appears in `.rdata` at `0x1461744c8`, inside a run of consecutive desc pointers (the pointer table). The `Raycast` entry at index 21 of the method-name table maps into this same table.

5. **Result**:
   ```csharp
   public const long RaycastBoundDesc = 0x81E7150;
   public const long RaycastBoundFn   = 0x80;
   ```

> **CRITICAL — CFG (Control Flow Guard):** Roblox is built with CFG enabled. When you overwrite the `BoundFuncDesc` function pointer to point at your stub, the indirect call through it is CFG-checked. If the stub is **not** marked as a valid call target, Roblox crashes instantly the first time a raycast fires. You **must** call `SetProcessValidCallTargets` on the stub's page after writing it (see `MarkCfg` in `raycastsilent.cs`). The C++ reference (`raycastsilent stuff/RaycastSilent.cpp`) does this via `mark_cfg(stub)`; the C# port must do the same or it will crash on the first shot.

### Step-by-step using IDA MCP tools

Server: `IDA MCP`.

#### Step 0 — health + imagebase

```text
server_health
py_eval: print(hex(ida_nalt.get_imagebase()))
```

Note `imagebase` (often `0x140000000`). All RVAs = `VA - imagebase`.

#### Step 1 — confirm exact strings exist

Use `find` with type `string` for:

- `WorldRoot`
- `Raycast`
- `FindPartOnRay`

If `"Raycast"` or `"WorldRoot"` is missing, wrong binary / stripped IDB — stop.

#### Step 2 — find the registration function via xrefs

Use `xrefs_to` on the exact `"Raycast"` string address. The single xref lands in the WorldRoot method registration function. Decompile it and identify the desc base address (the qword whose `+8` is set to the `"Raycast"` name, and whose `+0x10` gets the fn pointer pair).

Compute:
```text
desc_rva = desc_va - imagebase
```

#### Step 3 — cross-confirm via the BoundFuncDesc pointer table

Search for the 8-byte little-endian pointer to `desc_va` (use `find_bytes` with the reversed bytes). The hit is inside a run of consecutive desc pointers in `.rdata` — that is the BoundFuncDesc pointer table, and it should align with the WorldRoot method-name table order (Raycast index matches).

Optional live check (debugger attached or external RPM):

```text
name = read_string(*(desc_va + 0x8))   # should be "Raycast" when initialized
fn   = *(desc_va + 0x80)               # should be executable code
```

If live name/fn work, set `RaycastBoundFn` to the working offset (`0x80` preferred among `0x78/0x80/0x88/...`).

#### Step 4 — write offsets

Update only:

```csharp
namespace ManualOffsets
{
    public static class WorldRoot
    {
        public const long RaycastBoundDesc = /* desc_rva */;
        public const long RaycastBoundFn   = 0x80; // or live-validated
    }
}
```

Do not change unrelated offsets "just in case".

### If static registration is unavailable (fallback)

If after an update the registration xref or pointer-table scan fails:

1. Re-check exact strings and that the IDB matches the running build.
2. Widen the pointer-table scan / relax the high-RVA threshold slightly; re-require `Raycast` + `FindPartOnRay` anchors.
3. **Runtime scan** (most reliable fallback):
   - In the live `RobloxPlayerBeta.exe` module, walk candidate desc VAs (from any remaining pointer tables, or known desc region).
   - Accept VA where `read_string(*(va+8)) == "Raycast"` and `*(va+0x80)` points to RX code.
   - `RaycastBoundDesc = va - module_base`.

Do not ship an offset that was only "nearby" a wrong xref (e.g. reflection dumps that store `"Raycast"` next to `"WorldRoot"` without a BoundFuncDesc pointer).

### Anti-patterns (common AI mistakes)

| Mistake | Why it fails |
|---|---|
| `xref("Raycast") - 8` | Hits reflection / name lists, not BoundFuncDesc |
| Require static `name` at `desc+8` | Empty in file |
| Take first pointer to `"Raycast"` | Often class/method dump, not desc |
| Hardcode old RVA and "search near it" | Region moves every update |
| Confuse `FindPartOnRay` desc with `Raycast` | Different indices in the same tables |
| Change `RaycastBoundFn` without live proof | Keep `0x80` unless validated |
| Use the name string address as the desc | `"Raycast"` sits in `.rdata`, not the desc |

### Practical IDA MCP tips (learned the hard way)

These are real gotchas hit while re-finding the offsets on the current build. They save a lot of
time on the next update.

1. **`find` with `type: "string"` returns substring matches too.** Searching `"Raycast"` returns
   ~70 hits (e.g. `RaycastCachedTerrain`, `raycastParams`, `findPartOnRay`). Always confirm the
   exact string with `get_string` on the candidate address before trusting it. The exact
   `"Raycast"` method name is the one whose neighbors are `Spherecast` / `FindPartOnRay` /
   `Blockcast` in the same `.rdata` cluster.

2. **`xrefs_to` on a string can return 0 hits even though code references it.** On this build the
   `"Raycast"` string had exactly one xref (`0x142590e79`), but the btools strings
   (`"currentCommand"`, `"MouseCommand] Workspace::Process"`) returned **zero** xrefs — they are
   referenced via RIP-relative `lea` in code that IDA hadn't auto-created xrefs for. Don't
   conclude "string unused" from an empty xref list; fall back to `find_bytes` on the 8-byte
   little-endian pointer (or the 4-byte RVA) to locate references.

3. **`py_eval` loops over the whole image time out.** Iterating every byte of `.text`
   (0x140001000–0x145ec0000) with `idc.next_head` / `ida_ua.decode_insn` exceeds the MCP
   request timeout. Scope scans to a small window (e.g. `0x146c00000–0x146c10000`) or use the
   native `find_bytes` tool instead of a Python byte-walk.

4. **`find_bytes` is the fastest way to confirm a pointer table.** To verify a desc address
   `0x1481E7150` is in the BoundFuncDesc pointer table, search for its little-endian bytes
   `50 71 1E 48 01 00 00 00`. One hit in `.rdata` inside a run of consecutive desc pointers =
   confirmed. This is much faster and more reliable than a Python scan.

5. **The desc body is all zeros in the static file — that's expected.** `get_bytes` on
   `0x1481E7150` returns 160 zero bytes. The name (`+8`) and fn pointer (`+0x80`) are filled at
   runtime. Do not treat this as a failure; the registration function's writes are the proof.

6. **Cross-check against the auto-generated reference headers.** The repo's parent directory
   contains `raycast_offsets.hpp` / `offsets.hpp` auto-generated for the exact build
   (`version-d584fb6c717a43d9`). They independently confirm `RaycastBoundDesc = 0x81E7150` and
   `RaycastBoundFn = 0x80`. If your IDA result matches the reference header, you're done.

7. **`server_health` can time out while IDA is busy.** If the first `server_health` call times
   out, retry — a `py_eval` (e.g. `print(hex(ida_nalt.get_imagebase()))`) often succeeds and
   confirms the server is alive.

---

## Btools Offsets Reference

| Offset | Class | Description | Current Value |
|---|---|---|---|
| `WorkspaceCurrentCommand` | `Workspace` | currentCommand shared_ptr ptr | `0x868` |
| `WorkspaceCurrentRefCount` | `Workspace` | currentCommand shared_ptr ctrl | `0x870` |
| `WorkspaceStickyCommand` | `Workspace` | stickyCommand shared_ptr ptr | `0x878` |
| `WorkspaceStickyRefCount` | `Workspace` | stickyCommand shared_ptr ctrl | `0x880` |
| `MouseCommandWorkspace` | `MouseCommand` | Back-pointer to `Workspace` | `0x50` |
| `ToolAllocationSize` | — | Allocation size for Hammer/Grab/Clone tools | `0xD0` |

---

## Updating Offsets After a Roblox Update

When Roblox updates, these offsets may change. To re-find them:

1. **Load the new `RobloxPlayerBeta.exe` into IDA Pro.**
2. Find the btools offsets as described in the [Btools Offsets — Step by Step](#btools-offsets--step-by-step) section.
3. Find the RaycastBoundDesc as described in the [MCP Guide](#mcp-guide-finding-raycastbounddesc) section.
4. Update the values in `manual_offsets.cs`.
5. Rebuild and test the affected features.

---

## Adding New Offsets

To add a new manual offset:

1. Add a new `public static class` (or add to an existing one) in `manual_offsets.cs`.
2. Add `public const long` (or `public const int`) fields for each offset.
3. Reference them via `ManualOffsets.ClassName.OffsetName` in feature code.
4. Document how the offset was found in this tutorial so it can be re-found after updates.