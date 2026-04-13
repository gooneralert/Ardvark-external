#pragma once
#include "luavm.h"
#include "../luau-master/VM/include/lua.h"
#include "../luau-master/VM/include/lualib.h"
#include "../luau-master/Compiler/include/Luau/Compiler.h"
#include "../luau-master/Common/include/Luau/Bytecode.h"
#include "../luau-master/VM/src/lapi.h"
#include "../luau-master/VM/src/lstate.h"
#include "../globals.h"
#include "../driver/driver.h"
#include "../classes/classes.h"
#include "../offsets.h"
#include <Windows.h>
#include <cstdlib>
#include <iostream>
#include <sstream>
#include <fstream>
#include <ctime>
#include <vector>
#include <map>
#include <unordered_set>
#include <algorithm>
#include <cmath>
#include <thread>
#include <chrono>
#include <atomic>
#include <cstdint>
#include <mutex>
#include <wininet.h>
#include <filesystem>

namespace fs = std::filesystem;

namespace util {
    // ============================================================================
    // LUA INSTANCE WRAPPER
    // ============================================================================
    struct LuaInstance {
        roblox::instance inst;
        LuaVM::VirtualInstance* v_inst = nullptr;
    };

    // ============================================================================
    // HELPER TEMPLATES
    // ============================================================================
    template <typename T>
    T safe_read(std::uintptr_t address, const T& fallback = T{}) {
        if (!is_valid_address(address)) return fallback;
        return read<T>(address);
    }

    template <typename T>
    void safe_write(std::uintptr_t address, const T& value) {
        if (!is_valid_address(address)) return;
        ::write<T>(address, value);
    }

    // ============================================================================
    // WORKSPACE UTILITIES
    // ============================================================================
    inline fs::path get_workspace() {
        const char* appdata = getenv("LOCALAPPDATA");
        fs::path p;
        if (appdata) p = fs::path(appdata) / "Nift" / "workspace";
        else p = fs::current_path() / "workspace";
        if (!fs::exists(p)) { std::error_code ec; fs::create_directories(p, ec); }
        return p;
    }

    // ============================================================================
    // TIMING FUNCTIONS
    // ============================================================================
    std::string L_fetchstring(std::uint64_t address);
    double now_seconds();
    long long now_microseconds();

    // ============================================================================
    // GLOBAL STATE MANAGEMENT
    // ============================================================================
    namespace vm_globals {
        struct AttributeValue {
            int type; // 0=bool, 1=number, 2=string
            bool b;
            double n;
            std::string s;
        };
        extern std::map<uintptr_t, std::map<std::string, AttributeValue>> attributes;
        extern std::map<std::string, std::string> fflags;
        extern std::mutex attribute_mutex;

        extern LuaVM::VirtualInstance* stub_datamodel;
        extern LuaVM::VirtualInstance* stub_workspace;
        extern LuaVM::VirtualInstance* stub_players;
        extern LuaVM::VirtualInstance* stub_lighting;
        extern LuaVM::VirtualInstance* stub_localplayer;
        extern LuaVM::VirtualInstance* stub_playergui;
        extern LuaVM::VirtualCamera* stub_camera;
        extern LuaVM::VirtualBasePart* stub_basepart;
        extern LuaVM::VirtualModel* stub_character;
        extern LuaVM::VirtualBasePart* stub_hrp;
        extern LuaVM::VirtualHumanoid* stub_humanoid;

        void ensure_stub_environment();
    }

    // ============================================================================
    // GLOBAL FUNCTIONS (luavm_globals.cpp)
    // ============================================================================
    int L_print(lua_State* L);
    int L_warn(lua_State* L);
    int L_error(lua_State* L);
    int L_loadstring(lua_State* L);
    int L_decompile(lua_State* L);
    int L_WorldToScreen(lua_State* L);
    int L_pcall(lua_State* L);
    int L_pairs(lua_State* L);
    int L_ipairs(lua_State* L);
    int L_require(lua_State* L);
    int L_run_secure(lua_State* L);
    int L_task_wait(lua_State* L);
    int L_task_delay(lua_State* L);
    int L_spawn(lua_State* L);
    int L_Global_index(lua_State* L);
    int L_tick(lua_State* L);

    // ============================================================================
    // BRIDGE FUNCTIONS (luavm_bridge.cpp)
    // ============================================================================
    int L_Vector3_new(lua_State* L);
    int L_Vector2_new(lua_State* L);
    int L_Color3_new(lua_State* L);
    int L_Color3_fromRGB(lua_State* L);
    int L_Color3_fromHSV(lua_State* L);
    int L_Color3_fromHex(lua_State* L);
    int L_Color3_index(lua_State* L);
    int L_UDim_new(lua_State* L);
    int L_UDim_index(lua_State* L);
    int L_UDim2_new(lua_State* L);
    int L_UDim2_index(lua_State* L);
    int L_UDim2_fromScale(lua_State* L);
    int L_UDim2_fromOffset(lua_State* L);
    int L_CFrame_new(lua_State* L);
    int L_CFrame_index(lua_State* L);
    int L_CFrame_lookAt(lua_State* L);
    int L_typeof(lua_State* L);
    int L_Enum_index(lua_State* L);

    void push_vector3_metatable(lua_State* L);
    void push_vector2_metatable(lua_State* L);
    void push_vector3_userdata(lua_State* L, float x, float y, float z);
    void push_vector2_userdata(lua_State* L, float x, float y);
    void push_color3_userdata(lua_State* L, float r, float g, float b);
    void push_udim2_userdata(lua_State* L, const LuaUDim2& v);
    void push_udim_userdata(lua_State* L, const LuaUDim& v);
    void push_cframe_userdata(lua_State* L, const LuaCFrame& cf);

    Vector3* try_get_vector3_userdata(lua_State* L, int idx);
    Vector2* try_get_vector2_userdata(lua_State* L, int idx);
    LuaCFrame* try_get_cframe_userdata(lua_State* L, int idx);
    LuaColor3* try_get_color3_userdata(lua_State* L, int idx);
    LuaUDim2* try_get_udim2_userdata(lua_State* L, int idx);

    // ============================================================================
    // INSTANCE FUNCTIONS (luavm_instances.cpp)
    // ============================================================================
    int L_Instance_new(lua_State* L);
    void push_roblox_instance_userdata(lua_State* L, const roblox::instance& inst);
    int L_Instance_index(lua_State* L);
    int L_Instance_newindex(lua_State* L);
    int L_Instance_namecall(lua_State* L);
    int L_Instance_eq(lua_State* L);
    int L_Instance_FindFirstChild(lua_State* L);
    int L_Instance_WaitForChild(lua_State* L);
    int L_Mouse_index(lua_State* L);

    // ============================================================================
    // INPUT FUNCTIONS (luavm_input.cpp)
    // ============================================================================
    int L_mouse1click(lua_State* L);
    int L_mouse1press(lua_State* L);
    int L_mouse1release(lua_State* L);
    int L_mouse2click(lua_State* L);
    int L_mouse2press(lua_State* L);
    int L_mouse2release(lua_State* L);
    int L_mousemoveabs(lua_State* L);
    int L_mousemoverel(lua_State* L);
    int L_mousescroll(lua_State* L);
    int L_keypress(lua_State* L);
    int L_keyrelease(lua_State* L);
    int L_iskeypressed(lua_State* L);
    int L_ismouse1pressed(lua_State* L);
    int L_ismouse2pressed(lua_State* L);
    int L_isrbxactive(lua_State* L);
    int L_setrobloxinput(lua_State* L);
    int L_setclipboard(lua_State* L);

    // ============================================================================
    // DRAWING FUNCTIONS (luavm_drawing.cpp)
    // ============================================================================
    int L_Drawing_new(lua_State* L);
    int L_Drawing_index(lua_State* L);
    int L_Drawing_newindex(lua_State* L);
    int L_Drawing_gc(lua_State* L);
    int L_Drawing_namecall(lua_State* L);

    // ============================================================================
    // ENVIRONMENT FUNCTIONS (luavm_env.cpp)
    // ============================================================================
    int L_getgenv(lua_State* L);
    int L_getrenv(lua_State* L);
    int L_getfenv_fn(lua_State* L);
    int L_setfenv_fn(lua_State* L);
    int L_getrawmetatable(lua_State* L);
    int L_setrawmetatable(lua_State* L);
    int L_setreadonly(lua_State* L);
    int L_isreadonly(lua_State* L);
    int L_makereadonly(lua_State* L);
    int L_makewriteable(lua_State* L);
    int L_newcclosure(lua_State* L);
    int L_clonefunction(lua_State* L);
    int L_iscclosure(lua_State* L);
    int L_islclosure(lua_State* L);
    int L_checkcaller(lua_State* L);
    int L_identifyexecutor(lua_State* L);
    int L_getexecutorname(lua_State* L);
    int L_getnamecallmethod(lua_State* L);
    int L_setnamecallmethod(lua_State* L);
    int L_cloneref(lua_State* L);
    int L_gethui(lua_State* L);

    // ============================================================================
    // MEMORY FUNCTIONS (luavm_memory.cpp)
    // ============================================================================
    int L_getbase(lua_State* L);
    int L_memory_read(lua_State* L);
    int L_memory_write(lua_State* L);

    // ============================================================================
    // FILESYSTEM FUNCTIONS (luavm_env.cpp)
    // ============================================================================
    int L_listfiles(lua_State* L);
    int L_isfile(lua_State* L);
    int L_isfolder(lua_State* L);
    int L_writefile(lua_State* L);
    int L_readfile(lua_State* L);
    int L_appendfile(lua_State* L);
    int L_loadfile(lua_State* L);
    int L_delfile(lua_State* L);
    int L_makefolder(lua_State* L);
    int L_delfolder(lua_State* L);

    // ============================================================================
    // HTTP FUNCTIONS (luavm_env.cpp)
    // ============================================================================
    int L_http_request(lua_State* L);
    int L_HttpService_GetAsync(lua_State* L);
    int L_HttpService_JSONEncode(lua_State* L);
    int L_HttpService_JSONDecode(lua_State* L);

    // ============================================================================
    // MISC FUNCTIONS (luavm.cpp)
    // ============================================================================
    int L_setfflag(lua_State* L);
    int L_getfflag(lua_State* L);
    int L_get_performance_stats(lua_State* L);
    void vm_interrupt(lua_State* L, int gc);
    std::string disassemble(const std::string& bytecode);
}
