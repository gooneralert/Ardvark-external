# Offsets Tutorial

This document explains how to find and update the **manual offsets** used by this project.

Manual offsets are offsets that are not available in auto-dumped offset files and must be found by hand using a disassembler (IDA Pro, Ghidra, x64dbg) or a memory inspection tool (ReClass.NET).

---

## Table of Contents

1. [Manual Offsets File](#manual-offsets-file)
2. [Tools You Need](#tools-you-need)
3. [How to Find Offsets in IDA Pro](#how-to-find-offsets-in-ida-pro)
4. [Btools Offsets — Step by Step](#btools-offsets--step-by-step)
   - [WorkspaceCurrentCommand (0x860)](#workspacecurrentcommand-0x860)
   - [WorkspaceStickyCommand (0x870)](#workspacestickycommand-0x870)
   - [MouseCommandWorkspace (0x50)](#mousecommandworkspace-0x50)
   - [ToolAllocationSize (0xD0)](#toolallocationsize-0xd0)
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
        public const long WorkspaceCurrentCommand = 0x860;
        public const long WorkspaceStickyCommand = 0x870;
        public const long MouseCommandWorkspace = 0x50;
        public const int ToolAllocationSize = 0xD0;
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
     mov rax, [rcx + 860h]   ; reads Workspace.CurrentCommand
     mov [rcx + 870h], rax   ; writes Workspace.StickyCommand
     ```
   - The offset is the hex value after `rcx +` (or whatever register holds the object pointer).

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

### WorkspaceCurrentCommand (0x860)

This is the offset in the `Workspace` class that holds a pointer to the current `MouseCommand` object.

**In IDA Pro:**

1. Open Strings (`Shift+F12`) and search for `Workspace`.
2. Look for strings like `"CurrentCommand"`, `"currentCommand"`, or `"setCurrentCommand"`.
3. Follow xrefs to the function that reads/writes this field.
4. In the assembly, look for:
   ```
   mov rax, [rcx + 860h]    ; reading CurrentCommand
   ```
   or
   ```
   mov [rcx + 860h], rax    ; writing CurrentCommand
   ```
5. The offset `860h` = `0x860` is what you need.

**In ReClass.NET:**

1. Attach to the running Roblox process.
2. Navigate to the `Workspace` instance (found via `DataModel.Workspace`).
3. Scroll through the class fields. The `CurrentCommand` field is a pointer that changes when a building tool is active.
4. Note the offset.

---

### WorkspaceStickyCommand (0x870)

This is the offset in the `Workspace` class that holds a pointer to the sticky `MouseCommand` object. It's typically right after `CurrentCommand`.

**In IDA Pro:**

1. Find the same function that references `CurrentCommand` (0x860).
2. Look nearby in the assembly for a second field access:
   ```
   mov rax, [rcx + 870h]    ; reading StickyCommand
   ```
3. The offset `870h` = `0x870`.

**In ReClass.NET:**

1. In the `Workspace` instance, the `StickyCommand` is typically at `CurrentCommand + 0x10`.
2. It's a pointer that also changes when a building tool is active.

---

### MouseCommandWorkspace (0x50)

This is the offset within a `MouseCommand` (or tool) object that points back to the `Workspace`.

**In IDA Pro:**

1. Search for `MouseCommand` or `MouseCommand::getWorkspace` in Strings/Functions.
2. Follow xrefs to the function that reads the workspace pointer from a MouseCommand.
3. Look for:
   ```
   mov rax, [rcx + 50h]    ; reading workspace from MouseCommand
   ```
4. The offset `50h` = `0x50`.

**In ReClass.NET:**

1. Find a `MouseCommand` object (it's the `CurrentCommand` pointer from Workspace when a tool is active).
2. The field at offset `0x50` should point to the `Workspace` instance.

---

### ToolAllocationSize (0xD0)

This is the size of the tool object (`HammerTool`, `GrabTool`, `CloneTool`) that needs to be allocated.

**In IDA Pro:**

1. Find the `HammerTool` constructor (search for `.?AVHammerTool@RBX@@` → follow xrefs).
2. Look for the allocation call (typically `operator new` or a custom allocator):
   ```
   mov edx, 0D0h            ; size = 0xD0
   call ??_M@YGXPAXI@Z       ; allocator
   ```
   or
   ```
   push 0D0h
   call <allocator>
   ```
3. The size `0D0h` = `0xD0` is the `ToolAllocationSize`.

**In ReClass.NET:**

1. Find a `HammerTool` object in memory (the `CurrentCommand` pointer when the hammer is active).
2. Check the object's size — it should be `0xD0` bytes.

---

## MCP Guide: Finding RaycastBoundDesc

Use this when an AI has **IDA Pro MCP** connected to a fresh `RobloxPlayerBeta.exe` IDB.
Goal: fill `ManualOffsets.WorldRoot.RaycastBoundDesc` and `RaycastBoundFn` in `offsets/manual_offsets.cs`.

Do **not** invent addresses. Every RVA must come from the open IDB (or a live process read).

### Hard facts (do not violate)

1. `BoundFuncDesc` for WorldRoot methods sits in **high `.data` / BSS**.
2. On disk / in a cold IDB the desc body is often **all `0x00` or `0xFF`**.
   - `*(desc + 8)` (name) and `*(desc + 0x80)` (fn) are filled **at runtime**.
3. Therefore these heuristics are **wrong** for this layout:
   - "xref to string `Raycast` → `place - 8` = BoundFuncDesc"
   - "scan for name pointer at `desc+8` in static file"
4. Correct static method:
   - WorldRoot **method-name table** (string pointers)
   - WorldRoot **BoundFuncDesc pointer table** (pointers into `.data`)
   - Same **order** and usually same **count** → index of `"Raycast"` selects the desc pointer.
5. `RaycastBoundFn` is almost always **`0x80`**. Confirm live if a debugger/process is attached; otherwise keep `0x80`.

Current expected shape (example only — values change every update):

```csharp
public const long RaycastBoundDesc = 0x........; // RVA
public const long RaycastBoundFn   = 0x80;
```

Usage: `slot = module_base + RaycastBoundDesc + RaycastBoundFn`.

### Preferred path A — run the IDA script

If the repo is available:

1. Open the matching IDB for the target Roblox build.
2. Run `tools/ida_find_raycast_desc.py` (File → Script file…).
3. Copy printed `RaycastBoundDesc` / `RaycastBoundFn` into `manual_offsets.cs`.

If the script output looks sane (Raycast index maps to a high RVA, nearby methods listed), **stop**. Done.

### Worked example (this repo's current build)

The following was recovered from the `decrypted_version-145f189a6a974303.bin` IDB (imagebase `0x0`, so RVA == VA). Use it as a template for re-finding after an update.

1. **Exact strings** (via `find_bytes`):
   - `WorldRoot` → `0x6106be0`
   - `ArePartsTouchingOthers` → `0x6135628` (first method string)
   - `Blockcast` → `0x6135640`
   - `FindPartOnRay` → `0x6135690`
   - `Raycast` → `0x6135828`

2. **WorldRoot method-name table** at `0x611D030` — 16-byte pairs `(ptr "WorldRoot", ptr MethodName)`, 29 methods:
   ```
   [00] ArePartsTouchingOthers
   [01] Blockcast
   [05] FindPartOnRay
   [21] Raycast          ← index 21
   [24] Shapecast
   [25] Spherecast
   [28] findPartsInRegion3
   ```

3. **BoundFuncDesc pointer table** — scan `.rdata` for runs of exactly 29 consecutive qwords pointing into high `.data` (`.data` = `0x79AF000–0x8AD6000`). The matching run is at `0x65EB940` (first desc `0x8090EA0`). **Index 21 → `0x8091390`**.

4. **Cross-confirm via the registration function** — xref to the `"Raycast"` string (`0x6135828`) from `0x528421` lands in `sub_528320`. Decompiled:
   ```c
   sub_4C38790((unsigned int)&qword_8091390, v1, (unsigned int)"Raycast", 0, ...);
   qword_8091390 = (__int64)off_65EBCC0;   // desc + 0x00
   xmmword_8091410 = v9;                    // desc + 0x80 = (sub_3BE2F40, 0)
   ```
   `0x8091410 = 0x8091390 + 0x80`, so the bound fn pointer is at **offset `0x80`** → `RaycastBoundFn = 0x80`.

5. **Verify the bound function signature** — `sub_3BE2F40` decompiles to:
   ```c
   __int64 __fastcall sub_3BE2F40(__int64 a1, __int64 a2, __int64 *a3, __int64 *a4, __int64 a5)
   ```
   - `a1` = rcx (this/WorldRoot)
   - `a2` = rdx (output)
   - `a3` = r8 (**origin**)
   - `a4` = r9 (**direction**)
   - `a5` = stack (raycastParams)
   This confirms the hook thunk's ABI assumption that `r8 = origin`, `r9 = direction`.

6. **Result**:
   ```csharp
   public const long RaycastBoundDesc = 0x8091390;
   public const long RaycastBoundFn   = 0x80;
   ```

> **CRITICAL — CFG (Control Flow Guard):** Roblox is built with CFG enabled. When you overwrite the `BoundFuncDesc` function pointer to point at your stub, the indirect call through it is CFG-checked. If the stub is **not** marked as a valid call target, Roblox crashes instantly the first time a raycast fires. You **must** call `SetProcessValidCallTargets` on the stub's page after writing it (see `MarkCfg` in `raycastsilent.cs`). The C++ reference (`raycastsilent stuff/RaycastSilent.cpp`) does this via `mark_cfg(stub)`; the C# port must do the same or it will crash on the first shot.

### Path B — find via IDA MCP tools (for an AI)

Server: `user-ida-pro-mcp`.

Always discover tool schemas with `GetMcpTools` before calling unfamiliar tools.
Keep outputs small. Prefer `py_eval` for multi-step scans; use `find` / `xrefs_to` only for confirmation.

#### Step 0 — health + imagebase

```text
server_health
py_eval: print(hex(ida_nalt.get_imagebase()))
```

Note `imagebase` (often `0x140000000`). All RVAs = `VA - imagebase`.

#### Step 1 — confirm exact strings exist

Search exact C-strings (not substrings):

- `WorldRoot`
- `Raycast`
- `FindPartOnRay`
- optionally `Blockcast`, `Spherecast`

MCP options:

- `find` / `find_regex` / `search_text` for the string
- or `py_eval` over `idautils.Strings()` with `str(s) == "Raycast"`

If `"Raycast"` or `"WorldRoot"` is missing, wrong binary / stripped IDB — stop.

#### Step 2 — recover WorldRoot method-name table

Pattern in `.rdata` (or similar): repeating pairs

```text
qword: pointer to "WorldRoot"
qword: pointer to MethodName   // ArePartsTouchingOthers, Blockcast, ..., Raycast, ...
```

Algorithm via `py_eval`:

1. Find VA of exact string `"WorldRoot"`.
2. `bin_search` for that 8-byte little-endian pointer across non-code (or whole image).
   - IDA 9.x: `parse_binpat_str` returns `""` on success; check `len(pats)`.
   - `bin_search` may return `(ea, status)` — use `ea = r[0]`.
3. At each hit `H`, try walking:
   - mode `cm`: `cstr(*(H)) == "WorldRoot"` and `cstr(*(H+8))` is a method name; step `+0x10`
   - mode `mc`: swapped order
4. Read C-strings **byte-by-byte until `\0`**. Do **not** use `get_strlit_contents` — IDA merges adjacent rdata names and breaks matching.
5. Keep the **longest** run that contains **all** of:
   - `Raycast`
   - `FindPartOnRay`
   - at least two of `Blockcast` / `Spherecast` / `Shapecast`
6. Record:
   - `methods[]` list
   - `idx = methods.index("Raycast")`

Sanity: typically ~25–40 methods. Index of `Raycast` is usually mid/late in the list.

#### Step 3 — recover BoundFuncDesc pointer table

Near the **method name string cluster** (same area as `"FindPartOnRay"`, `"Raycast"`, `"Spherecast"`):

Just **before** those strings there is a table of consecutive qwords:

```text
qword: pointer into high .data  // BoundFuncDesc for methods[0]
qword: pointer into high .data  // methods[1]
...
```

Algorithm via `py_eval`:

1. Take VA of exact `"FindPartOnRay"` (fallback: `"Raycast"`).
2. Scan backward ~`0x400` bytes, 8-byte aligned.
3. Collect the longest run of qwords `V` where:
   - `V` is inside the image
   - target segment is **not executable**
   - RVA `(V - imagebase)` is **high** (this build: often `> 0x4000000`, commonly `0x8xxxxxx`)
4. Prefer a run whose **length == len(methods)**.
5. Then:

```text
desc_va  = table[idx]
desc_rva = desc_va - imagebase
```

#### Step 4 — verify

Must hold:

- `methods[idx] == "Raycast"`
- `len(table) == len(methods)` (or explain a tiny mismatch)
- Only **one** static qword in the image points at `desc_va` (optional `bin_search` of the 8-byte VA) — usually the table slot itself
- Peek `desc_va` bytes: often uninitialized on disk → **expected**, not a failure

Optional live check (debugger attached or external RPM):

```text
name = cstr(*(desc_va + 0x8))   # should be "Raycast" when initialized
fn   = *(desc_va + 0x80)        # should be executable code
```

If live name/fn work, set `RaycastBoundFn` to the working offset (`0x80` preferred among `0x78/0x80/0x88/...`).

#### Step 5 — write offsets

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

### Path C — if static tables moved (fallback)

If after an update Steps 2–3 fail:

1. Re-check exact strings and that the IDB matches the running build.
2. Widen the backward scan / relax the high-RVA threshold slightly; re-require `Raycast` + `FindPartOnRay` anchors.
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

### Minimal `py_eval` checklist (copy for the model)

```text
1. imagebase
2. exact strings: WorldRoot, Raycast, FindPartOnRay
3. longest (WorldRoot, MethodName)* table containing Raycast + FindPartOnRay
4. idx = index of Raycast
5. consecutive high-.data pointer run before FindPartOnRay strings, length == methods
6. desc_rva = table[idx] - imagebase
7. RaycastBoundFn = 0x80 (live-confirm if possible)
8. print manual_offsets.cs lines only when steps 3–6 succeeded
```
