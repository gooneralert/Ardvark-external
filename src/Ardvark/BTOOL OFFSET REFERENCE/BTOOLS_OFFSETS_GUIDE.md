# Btools Offsets — Complete Beginner's Guide (for AI / humans)

This document is the **definitive** reference for finding the btools manual
offsets in Roblox's `RobloxPlayerBeta.exe` using the **IDA MCP** server. It is
written so that an AI (or human) with **zero** prior experience reverse
engineering Roblox can follow it start-to-finish and produce correct offsets.

> **Target output:** the six constants in `src/Ardvark/offsets/manual_offsets.cs`
> under `ManualOffsets.Btools`:
>
> ```csharp
> WorkspaceCurrentCommand  = 0x868
> WorkspaceCurrentRefCount = 0x870
> WorkspaceStickyCommand   = 0x878
> WorkspaceStickyRefCount  = 0x880
> MouseCommandWorkspace    = 0x50
> ToolAllocationSize       = 0xD0
> ```

---

## Table of Contents

1. [What you need](#1-what-you-need)
2. [Concepts you must understand](#2-concepts-you-must-understand)
   - [What is a vtable?](#what-is-a-vtable)
   - [What is an offset?](#what-is-an-offset)
   - [What is a shared_ptr pair?](#what-is-a-shared_ptr-pair)
   - [What is RTTI?](#what-is-rtti)
3. [The big picture](#3-the-big-picture)
4. [Step 0 — Verify the server](#step-0--verify-the-server)
5. [Step 1 — Find the tool vtable](#step-1--find-the-tool-vtable)
6. [Step 2 — Find constructor + allocation size](#step-2--find-constructor--allocation-size)
7. [Step 3 — Find Workspace command offsets](#step-3--find-workspace-command-offsets)
8. [Step 4 — Verify MouseCommandWorkspace](#step-4--verify-mousecommandworkspace)
9. [Step 5 — Write the offsets](#step-5--write-the-offsets)
10. [How to verify a found offset](#how-to-verify-a-found-offset)
11. [Anti-patterns / traps](#11--anti-patterns--traps)
12. [Glossary of MCP tool calls](#12--glossary-of-mcp-tool-calls)
13. [Reference: this build's values](#13--reference-this-builds-values)

---

## 1. What you need

- **IDA Pro** with the **IDA MCP** server running and connected.
- A fresh `RobloxPlayerBeta.exe` IDB loaded (decrypted if needed).
- Ability to call the MCP tools: `find_regex`, `xrefs_to`, `decompile`,
  `disasm`, `int_convert`, `server_health`.

**The MCP tools you will use:**

| Tool | What it does | Example |
|---|---|---|
| `server_health` | Checks the server + imagebase is ready | `{}` |
| `find_regex` | Searches strings by regex | `pattern: "\\.\\?AVHammerTool@RBX@@"` |
| `xrefs_to` | Finds code that references an address | `addrs: ["0x..."]` |
| `decompile` | Hex-Rays pseudocode of a function | `addr: "0x..."` |
| `disasm` | Assembly of a function | `addr: "0x..."` |
| `int_convert` | Convert decimal↔hex | `inputs: [{text:"152"}]` |

---

## 2. Concepts you must understand

### What is a vtable?

A **vtable** (virtual method table) is a table of function pointers. In C++,
every object with virtual functions starts with a hidden pointer to its class's
vtable. The vtable pointer is at **object offset 0**. Roblox uses vtables
heavily — every `Instance` has one. In IDA the vtable symbol looks like
`??_7HammerTool@RBX@@6B@`.

### What is an offset?

An **offset** is "how many bytes into an object a field lives". E.g. if the
`Workspace` object's `currentCommand` field is `0x868` bytes from the start of
the `Workspace` object, then `currentCommand` lives at
`workspace_object_address + 0x868`.

Offsets change between Roblox updates, which is why they must be re-found.

### What is a shared_ptr pair?

`std::shared_ptr<T>` is not a single pointer — it's **two** qwords:

```
[ptr + 0x00]  → the actual object pointer
[ptr + 0x08]  → a "control block" (refcount object)
```

The control block holds reference counts (uses at +8, weaks at +12) so the
object can be safely shared and freed when the last owner releases it.

**This is the #1 trap for btools:** the `Workspace` command fields are
`shared_ptr<MouseCommand>`. So each command slot is **two** qwords (pointer +
control block). You MUST write both, and you MUST NOT confuse the control
block offset with the next field.

### What is RTTI?

RTTI (Run-Time Type Information) lets C++ identify an object's type at
runtime. For each class, MSVC emits:

1. A **type descriptor** containing the mangled class name string
   (e.g. `.?AVHammerTool@RBX@@`).
2. A **CompleteObjectLocator** that points to the type descriptor.
3. The **vtable** that points to the CompleteObjectLocator.

The cheat in `btools.cs` discovers the vtables at runtime by scanning PE
sections for the mangled name → locator → vtable. You only need the vtable to
find the constructor.

---

## 3. The big picture

Btools works by:

1. Discovering the `HammerTool` / `GrabTool` / `CloneTool` **vtable**.
2. Allocating a fake tool object sized `ToolAllocationSize`.
3. Setting its vtable, refcount, workspace back-pointer, and NameContainer.
4. Installing it as both `currentCommand` and `stickyCommand` (each a
   `shared_ptr<MouseCommand>` pair) in the `Workspace`.

The game then treats our fake object as a real building tool. When the player
clicks, `Workspace::Process` dispatches to it.

To make that work we need:

| Offset | Why |
|---|---|
| `WorkspaceCurrentCommand` | Where to put the fake tool pointer (current cmd) |
| `WorkspaceCurrentRefCount` | The shared_ptr control block for current cmd |
| `WorkspaceStickyCommand` | Where to put the fake tool pointer (sticky cmd) |
| `WorkspaceStickyRefCount` | The shared_ptr control block for sticky cmd |
| `MouseCommandWorkspace` | Back-pointer inside the tool → Workspace |
| `ToolAllocationSize` | Size to allocate for the fake tool |

---

## Step 0 — Verify the server

Call:

```text
server_health
```

Expected: `"status":"ok"`, an `imagebase` (usually `0x140000000`), and
`"hexrays_ready":true`.

If it times out, retry once — IDA may be busy.

---

## Step 1 — Find the tool vtable

We need the vtable to locate the constructor. Any tool works; HammerTool is
easiest.

**Call:**

```text
find_regex: pattern = "\\.\\?AVHammerTool@RBX@@"
```

This searches string literals. Expect a hit like:

```json
{"addr":"0x147c76cf0","string":".?AVHammerTool@RBX@@"}
```

This is the **RTTI type-descriptor name string**. The type descriptor object
starts 16 bytes *before* this string. The vtable is found by locating the
CompleteObjectLocator that references this type descriptor.

**To get the vtable address**, use `xrefs_to` on the type descriptor, or
simply look for the vtable symbol `??_7HammerTool@RBX@@6B@` nearby. On this
build the vtable is at **`0x146974E98`**.

> You do NOT hardcode this vtable — `btools.cs` discovers it at runtime. You
> only need it to find the constructor in Step 2.

**Grab / Clone vtables** (for reference):
- GrabTool: `0x146974C18`
- CloneTool: find via `find_regex: "\\.\\?AVCloneTool@RBX@@"`

---

## Step 2 — Find constructor + allocation size

**Call:**

```text
xrefs_to: addrs = ["0x146974E98"]
```

You'll get data xrefs from two functions: the **constructor** and the
**destructor**. On this build:

| Function | Role |
|---|---|
| `sub_1421C15C0` | HammerTool constructor |
| `sub_1421C16A0` | HammerTool destructor |

**Decompile the constructor:**

```text
decompile: addr = "0x1421C15C0"
```

In the pseudocode you MUST see these lines (they define the object layout):

```c
*(_QWORD *)(a1 + 8)  = -1;            // (1) shared_ptr refcount = -1
*(_QWORD *)(a1 + 80) = a2;            // (2) workspace back-ptr at 0x50
*(_QWORD *)a1 = &RBX::HammerTool::`vftable';   // (3) vtable at offset 0
```

- `(1)` → the fake tool must set `tool[0x08] = -1` (refcount).
- `(2)` → `MouseCommandWorkspace = 0x50` (80 decimal).
- `(3)` → vtable goes at offset 0.

**Now find the factory** (the function that allocates then calls the
constructor):

```text
xrefs_to: addrs = ["0x1421C15C0"]
```

On this build the factory is `sub_1421C1500`. Decompile it:

```c
v4 = sub_142C7DD30(152);   // <-- allocation size = 152 bytes
v5 = sub_1421C15C0(v4, ...);
```

**Convert 152 to hex** (use `int_convert`, never do it in your head):

```text
int_convert: inputs = [{text:"152", size:4}]
→ 152 = 0x98
```

So `HammerTool` size = **`0x98`**.

**Repeat for Grab and Clone** to get the largest size:

| Tool | Constructor | Factory | Alloc size |
|---|---|---|---|
| HammerTool | `sub_1421C15C0` | `sub_1421C1500` | `152` = **0x98** |
| GrabTool | `sub_1421C00C0` | `sub_1421C0000` | `208` = **0xD0** |
| CloneTool | `sub_1421C0E30` | `sub_1421C0D70` | `152` = **0x98** |

**`ToolAllocationSize = 0xD0`** (the largest), so one allocation is big
enough for any tool.

> To find Grab/Clone factories: `find_regex` for `\.\?AVGrabTool@RBX@@` /
> `\.\?AVCloneTool@RBX@@` → find vtable → `xrefs_to` vtable → constructor →
> `xrefs_to` constructor → factory → read the `sub_142C7DD30(N)` size arg.

---

## Step 3 — Find Workspace command offsets

This is the most important and error-prone part.

### 3a. Locate Workspace::Process

**Call:**

```text
find_regex: pattern = "currentCommand"
```

On this build the meaningful hit is:

```
[FLog::ChangeMouseCommand] Workspace process got nullptr currentCommand
```

at `0x146C04170`.

**Get its xref:**

```text
xrefs_to: addrs = ["0x146C04170"]
```

Single hit → `sub_1412377C0` = **`Workspace::Process`**.

### 3b. Read currentCommand offsets

**Disassemble** the start of the function:

```text
disasm: addr = "0x141237860"
```

Look for the current-command check:

```
.text:141237865  cmp [r15+868h], r13         ; currentCommand ptr @ 0x868
```

Then disassemble a bit later:

```text
disasm: addr = "0x141237BEC"
```

```
.text:141237BEC  mov rax, [r15+870h]         ; currentCommand refcount @ 0x870
.text:141237BF8  lock inc dword ptr [rax+8]  ; shared_ptr refcount increment
.text:141237BFC  mov r12, [r15+868h]         ; currentCommand @ 0x868
.text:141237C0B  mov rax, [r15+870h]         ; refcount again
```

**This proves:**
- `WorkspaceCurrentCommand = 0x868`
- `WorkspaceCurrentRefCount = 0x870`

The `lock inc dword ptr [rax+8]` is the smoking gun that `0x870` is a
**refcount control block**, not a plain pointer.

### 3c. Find sticky command offsets (the easy-to-miss part)

**Critical:** do NOT assume sticky = current + 0x10. You must confirm from
`ChangeMouseCommand`, not `Workspace::Process`.

`Workspace::Process` (`sub_1412377C0`) calls `sub_14122E2C0`
(`ChangeMouseCommand`). This is the function that installs/promotes commands.

**Decompile `ChangeMouseCommand`:**

```text
decompile: addr = "0x14122E2C0"
```

Near the end of the function you'll see the command slot writes. In
decompiler terms `a1` is `_QWORD *` (the Workspace), so:

```c
a1[269] = currentCommandPtr;    // 0x868
a1[270] = currentCommandCtrl;   // 0x870
a1[271] = stickyCommandPtr;     // 0x878
a1[272] = stickyCommandCtrl;    // 0x880
```

**Confirm with disassembly:**

```text
disasm: addr = "0x14122EC3F"
```

```
.text:14122EC3F  mov [r14+878h], rcx        ; stickyCommand ptr @ 0x878
.text:14122EC46  mov rdx, [r14+880h]        ; old refcount
.text:14122EC4D  mov [r14+880h], rax        ; stickyCommand refcount @ 0x880
```

**Final result:**

| Field | Offset |
|---|---|
| `WorkspaceCurrentCommand` | `0x868` |
| `WorkspaceCurrentRefCount` | `0x870` |
| `WorkspaceStickyCommand` | `0x878` |
| `WorkspaceStickyRefCount` | `0x880` |

> **THE TRAP:** `0x870` looks like a plausible "sticky" offset (it's right
> after `0x868`), and several docs/cheats use it. But `0x870` is the
> **currentCommand refcount**. The real sticky pointer is `0x878`. If you
> write the tool to `0x870` and leave `0x878` null, the tool deactivates
> after the first interaction because the game promotes the null sticky
> command.

---

## Step 4 — Verify MouseCommandWorkspace

Already confirmed in Step 2 from the HammerTool constructor:

```c
*(_QWORD *)(a1 + 80) = a2;   // 80 decimal = 0x50 → workspace
```

```csharp
MouseCommandWorkspace = 0x50;
```

---

## Step 5 — Write the offsets

Update `src/Ardvark/offsets/manual_offsets.cs`:

```csharp
public static class Btools
{
    public const long WorkspaceCurrentCommand  = 0x868;
    public const long WorkspaceCurrentRefCount = 0x870;
    public const long WorkspaceStickyCommand   = 0x878;
    public const long WorkspaceStickyRefCount  = 0x880;
    public const long MouseCommandWorkspace    = 0x50;
    public const int  ToolAllocationSize       = 0xD0;
}
```

And `btools.cs` must install the **pair** into both slots:

```csharp
// currentCommand pair
mem.Write(workspace + WorkspaceCurrentCommand, tool);
mem.Write(workspace + WorkspaceCurrentRefCount, control);
// stickyCommand pair
mem.Write(workspace + WorkspaceStickyCommand, tool);
mem.Write(workspace + WorkspaceStickyRefCount, control);
```

Where `tool[0x08] = -1` and `control[0x08] = 0x7FFFFFF0` (large use-count),
`control[0x0C] = 1`.

---

## How to verify a found offset

1. **Cross-check the refcount:** if a field is followed by `lock inc/dec
   dword ptr [x+8]` or `_InterlockedExchangeAdd(x+12, ...)`, it's a
   shared_ptr pair — the pointer is at `x`, the ctrl at `x+8`.
2. **Cross-check with ChangeMouseCommand:** the function that *installs*
   commands is authoritative for the layout.
3. **Build the project** after editing to confirm no compile errors.
4. **Test in game:** tool should stay active across multiple interactions.
   If it deactivates after one, the sticky command offset is wrong (almost
   always means you used `0x870` instead of `0x878`).

---

## 11. Anti-patterns / traps

| Mistake | Why it fails |
|---|---|
| Use `0x870` as sticky | It's currentCommand's refcount, not sticky |
| Read only `Workspace::Process` for sticky | It uses `[r15+870h]` as refcount — misleading |
| Write only a single qword per command slot | Workspace fields are shared_ptr pairs (2 qwords) |
| Skip the control-block refcount | `lock inc [ctrl+8]` on null/0 crashes |
| Skip `tool[8] = -1` | shared_ptr destructor path crashes |
| Set `tool[8] = 0` | shared_ptr refcount logic corrupts |
| Hardcode the vtable address | Changes every update; btools.cs discovers it at runtime |
| Allocate only 0x98 | GrabTool needs 0xD0 → buffer overflow / crash |
| Convert hex/decimal by hand | Use `int_convert` — arithmetic errors are fatal |

---

## 12. Glossary of MCP tool calls

```text
server_health
# → {"status":"ok","imagebase":"0x140000000",...}

find_regex: {"pattern":"\\.\\?AVHammerTool@RBX@@"}
# → RTTI name string address

xrefs_to: {"addrs":["0x146974E98"]}
# → functions referencing the vtable (constructor/destructor)

decompile: {"addr":"0x1421C15C0"}
# → C pseudocode of the constructor

xrefs_to: {"addrs":["0x1421C15C0"]}
# → find the factory (allocator)

decompile: {"addr":"0x1421C1500"}
# → factory; look for sub_142C7DD30(N) for the size

int_convert: {"inputs":[{"text":"152","size":4}]}
# → {"hexadecimal":"0x98"}

disasm: {"addr":"0x141237860"}
# → assembly around the currentCommand check

disasm: {"addr":"0x14122EC3F"}
# → assembly proving sticky ptr @ 0x878, ctrl @ 0x880
```

---

## 13. Reference: this build's values

**Binary:** `RobloxPlayerBeta_decrypted.exe` (version-d584fb6c717a43d9)
**Imagebase:** `0x140000000`

| Item | Address / Value |
|---|---|
| HammerTool RTTI name | `0x147c76cf0` |
| HammerTool vtable | `0x146974E98` |
| HammerTool constructor | `0x1421C15C0` |
| HammerTool factory | `0x1421C1500` (alloc 0x98) |
| GrabTool vtable | `0x146974C18` |
| GrabTool factory | `0x1421C0000` (alloc 0xD0) |
| CloneTool factory | `0x1421C0D70` (alloc 0x98) |
| Workspace::Process | `0x1412377C0` |
| ChangeMouseCommand | `0x14122E2C0` |
| `WorkspaceCurrentCommand` | `0x868` |
| `WorkspaceCurrentRefCount` | `0x870` |
| `WorkspaceStickyCommand` | `0x878` |
| `WorkspaceStickyRefCount` | `0x880` |
| `MouseCommandWorkspace` | `0x50` |
| `ToolAllocationSize` | `0xD0` |

---

## Final checklist before shipping

- [ ] `WorkspaceCurrentCommand = 0x868` (not 0x860)
- [ ] `WorkspaceStickyCommand = 0x878` (not 0x870)
- [ ] All four command slots (ptr+ctrl) written, not just two
- [ ] `tool[0x08] = -1`
- [ ] `ctrl[0x08] = 0x7FFFFFF0`, `ctrl[0x0C] = 1`
- [ ] `MouseCommandWorkspace = 0x50`
- [ ] `ToolAllocationSize = 0xD0`
- [ ] `dotnet build` succeeds with 0 errors
- [ ] In game: tool stays active across multiple interactions