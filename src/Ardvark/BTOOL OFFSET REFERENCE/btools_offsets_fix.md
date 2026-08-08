# Btools Offsets Fix — Full Analysis Report

**Date:** 2026-08-08
**Binary:** `RobloxPlayerBeta_decrypted.exe` (version-d584fb6c717a43d9)
**Image Base:** `0x140000000`
**Feature:** `features/games/universal/btools/btools.cs`

---

## Part 1 — The Crash (offset was wrong)

### Original bug

The btools feature crashed the game on activation. `WorkspaceCurrentCommand`
was `0x860`, but the real offset is `0x868`. Because the feature also wrote a
"control block" at `currentCommand + 8`, the game:

1. Read `currentCommand` from `0x868` expecting a `MouseCommand` object.
2. Found our scratch "control block" garbage instead of a vtable.
3. Called a virtual function through the garbage → **crash**.

### Fix

`WorkspaceCurrentCommand = 0x868` (found via `Workspace::Process`).

---

## Part 2 — Tool deactivates after one interaction

### Second bug (sticky command offset)

After fixing the crash, the tool *activated* but deactivated after a single
interaction (e.g. destroying one brick, releasing a grabbed part). The root
cause: `WorkspaceStickyCommand` was `0x870`, but that is the **refcount
slot** of the `currentCommand` shared_ptr. The real sticky command slot is
at **`0x878`**.

The `Workspace` command fields are **`shared_ptr<MouseCommand>` pairs**, not
raw pointers:

| Offset | Field |
|---|---|
| `0x868` | currentCommand → object ptr |
| `0x870` | currentCommand → refcount ctrl block |
| `0x878` | stickyCommand → object ptr |
| `0x880` | stickyCommand → refcount ctrl block |

The old code wrote the tool at `0x870`, so:
- The game read the real stickyCommand from `0x878` → **NULL**.
- After one command finished, `ChangeMouseCommand` promoted the (null)
  sticky command → generated a default command → our tool was replaced →
  **deactivated**.

### Fix

`WorkspaceStickyCommand = 0x878` and add `WorkspaceStickyRefCount = 0x880`.

---

## Part 3 — Correct object layout (from IDA)

### HammerTool (size 0x98 from factory `sub_1421C1500`)

```c
// sub_1421C15C0 (constructor)
*(_QWORD *)(a1 + 8)  = -1;                          // shared_ptr ref-count
*(_QWORD *)(a1 + 80) = a2;                          // workspace back-ptr (0x50)
*(_QWORD *)a1 = &RBX::HammerTool::`vftable';       // vtable at 0
// +0x18 … +0x90 zeroed except NameContainer wiring
```

| Offset | Field |
|---|---|
| `0x00` | vftable |
| `0x08` | shared_ptr refcount = `-1` |
| `0x50` | workspace back-pointer |
| `0x70` | NameContainer ptr |
| `0x98` | total size |

### GrabTool (size 0xD0 from factory `sub_1421C0000`)

Same base layout as HammerTool, plus cursor-name `std::string` at `0x88`
(`"ArrowCursor"`) and extra fields through `0xC8`.

### CloneTool (size 0x98 from factory `sub_1421C0D70`)

Same base layout as HammerTool.

---

## Part 4 — What `btools.cs` now does

The feature allocates one fake `Tool` object (0xD0, large enough for any
tool) plus one shared_ptr control block, then installs the pair into both
command slots:

```csharp
// fake tool
tool[0x00] = vtable;         // discovered Hammer/Grab/Clone vtable
tool[0x08] = -1;             // matching real constructor refcount
tool[0x50] = workspace;      // workspace back-pointer
tool[0x70] = nameContainer;  // valid NameContainer for Instance::GetName()

// control block (shared_ptr ctrl)
ctrl[0x08] = 0x7FFFFFF0;     // large use-count → game never deletes
ctrl[0x0C] = 1;              // weak count

// Workspace command slots
workspace[0x868] = tool;     workspace[0x870] = ctrl;   // currentCommand pair
workspace[0x878] = tool;     workspace[0x880] = ctrl;   // stickyCommand pair
```

On deactivate, the original four values are restored.

---

# Part 5 — Detailed Guide: Finding Btools Offsets in IDA

This is a complete, copy-paste-able walkthrough using the IDA MCP server.
It worked on this build; adapt addresses if the binary changed.

## Step 0 — Health + imagebase

```text
server_health
(optional) py_eval: print(hex(ida_nalt.get_imagebase()))
```

Expect `imagebase = 0x140000000`. All offsets here are VAs (absolute) —
they appear directly in `manual_offsets.cs` **as absolute** because the
feature adds them to the runtime module base. Only the WorldRoot raycast
RVAs need `VA - imagebase`.

## Step 1 — Find the HammerTool vtable (any of the three tools)

```text
find_regex: "\.\?AVHammerTool@RBX@@"
```

This returns the RTTI type-descriptor string (e.g. `0x147c76cf0`).

- The type descriptor object starts 16 bytes before the name string.
- The `RTTICompleteObjectLocator` referencing it points to the vtable.

The vtable symbol in the IDB will be named `??_7HammerTool@RBX@@6B@`
(e.g. `0x146974E98`).

**Important:** the cheat discovers vtables at runtime by scanning PE
sections for the RTTI name, so you don't hardcode the vtable address —
you only need it to find the constructor.

## Step 2 — Find the constructor and factory (allocation size)

```text
xrefs_to: 0x146974E98
```

You'll see data refs from two functions: the **constructor** and the
**destructor**. On this build:

| Function | Role |
|---|---|
| `sub_1421C15C0` | HammerTool constructor |
| `sub_1421C16A0` | HammerTool destructor |

Decompile the constructor (`decompile` on `sub_1421C15C0`). Confirm you
see:

```c
*(_QWORD *)(a1 + 8)  = -1;            // refcount = -1 (shared_ptr)
*(_QWORD *)(a1 + 80) = a2;            // workspace back-ptr at 0x50
*(_QWORD *)a1 = &RBX::HammerTool::`vftable';
```

Then find the **factory** — the function that calls the constructor:

```text
xrefs_to: 0x1421C15C0
```

On this build the factory is `sub_1421C1500`. Decompile it:

```c
v4 = sub_142C7DD30(152);   // allocator size = 152 = 0x98
v5 = sub_1421C15C0(v4, *(a1+80));
```

**Allocation sizes (this build):**

| Tool | Factory | Size |
|---|---|---|
| HammerTool | `sub_1421C1500` | `152` = **0x98** |
| GrabTool | `sub_1421C0000` | `208` = **0xD0** |
| CloneTool | `sub_1421C0D70` | `152` = **0x98** |

Use the largest (`0xD0`) for `ToolAllocationSize` so one allocation is big
enough for any tool.

## Step 3 — Find Workspace command offsets

### 3a. Locate the right function

```text
find_regex: "currentCommand"
```

On this build the only relevant string is:

```
[FLog::ChangeMouseCommand] Workspace process got nullptr currentCommand
```

at `0x146C04170`. Get its xref:

```text
xrefs_to: 0x146C04170
```

Single hit → `sub_1412377C0` = **`Workspace::Process`** (the main loop
that dispatches input to the current command).

### 3b. Read the offsets from Workspace::Process

Disassemble around the current-command check:

```text
disasm: 0x141237860
```

Key lines:

```
.text:141237865  cmp [r15+868h], r13         ; currentCommand ptr @ 0x868
.text:141237BEC  mov rax, [r15+870h]         ; currentCommand refcount @ 0x870
.text:141237BF8  lock inc dword ptr [rax+8]  ; shared_ptr refcount inc
.text:141237BFC  mov r12, [r15+868h]         ; currentCommand again
```

This proves:

- `currentCommand` ptr = **`0x868`**
- its refcount ctrl = **`0x870`**

### 3c. Find the sticky command offset (the easy-to-miss one)

The **primary** source for sticky is `ChangeMouseCommand`
(`sub_14122E2C0`). That's the function that promotes/installs commands.

Find it via xref from `Workspace::Process` (`sub_1412377C0` references it
as `sub_14122E2C0`). Decompile it and look near the end:

```c
// sub_14122E2C0
a1[269] = currentCommandPtr;   // 0x868
a1[270] = currentCommandCtrl;  // 0x870
a1[271] = stickyCommandPtr;    // 0x878
a1[272] = stickyCommandCtrl;   // 0x880
```

Confirm with disassembly:

```text
disasm: 0x14122EC3F
```

Which shows:

```
.text:14122EC3F  mov [r14+878h], rcx        ; stickyCommand ptr @ 0x878
.text:14122EC46  mov rdx, [r14+880h]        ; old refcount
.text:14122EC4D  mov [r14+880h], rax        ; stickyCommand refcount @ 0x880
```

**CRITICAL gotcha:** many references in `Workspace::Process` use
`[r15+870h]` as a *refcount* — do not mistake `0x870` for the sticky
command pointer. The sticky pointer is `0x878` (the third qword of the
4-slot command pair region `0x868…0x887`).

## Step 4 — Verify MouseCommandWorkspace

Already confirmed from the HammerTool constructor:

```
*(_QWORD *)(a1 + 80) = a2;  // 80 = 0x50 → workspace
```

So `MouseCommandWorkspace = 0x50`.

## Step 5 — Write the offsets

`src/Ardvark/offsets/manual_offsets.cs`:

```csharp
public const long WorkspaceCurrentCommand  = 0x868;
public const long WorkspaceCurrentRefCount = 0x870;
public const long WorkspaceStickyCommand   = 0x878;
public const long WorkspaceStickyRefCount  = 0x880;
public const long MouseCommandWorkspace    = 0x50;
public const int  ToolAllocationSize       = 0xD0;
```

---

## Anti-patterns to avoid (learned the hard way)

| Mistake | Why it fails |
|---|---|
| Read only `Workspace::Process` for sticky | You'll see `[r15+870h]` and think it's sticky — it's the refcount! |
| Write tool at `0x870` | Corrupts currentCommand's shared_ptr refcount |
| Leave sticky at `0x870` | After one interaction sticky (0x878) is NULL → tool replaced |
| Skip the refcount in the ctrl block | `lock inc dword ptr [ctrl+8]` on null/0 crashes |
| Skip `tool[8] = -1` | shared_ptr destructor path on our fake object crashes |
| Use 0xD0 for Hammer only | Fine, but know Hammer/Clone are 0x98 — D0 covers all |

---

## Current conclusion

Both the crash and the single-interaction deactivation are fixed:
- `WorkspaceCurrentCommand = 0x868` (+ ctrl `0x870`)
- `WorkspaceStickyCommand = 0x878` (+ ctrl `0x880`)
- `MouseCommandWorkspace = 0x50`
- `ToolAllocationSize = 0xD0`

The project builds cleanly.