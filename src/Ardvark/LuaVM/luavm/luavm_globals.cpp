// ============================================================================
// LuaVM Globals - Global Lua functions implementation
// ============================================================================

#include "luavm_common.h"
#include <new>
#include <Luau/Compiler.h>
#include <fstream>
#include <sstream>

namespace util {
    namespace vm_globals {
        std::map<uintptr_t, std::map<std::string, AttributeValue>> attributes;
        std::map<std::string, std::string> fflags;
        std::mutex attribute_mutex;

        LuaVM::VirtualInstance* stub_datamodel = nullptr;
        LuaVM::VirtualInstance* stub_workspace = nullptr;
        LuaVM::VirtualInstance* stub_players = nullptr;
        LuaVM::VirtualInstance* stub_lighting = nullptr;
        LuaVM::VirtualInstance* stub_localplayer = nullptr;
        LuaVM::VirtualInstance* stub_playergui = nullptr;
        LuaVM::VirtualCamera* stub_camera = nullptr;
        LuaVM::VirtualBasePart* stub_basepart = nullptr;
        LuaVM::VirtualModel* stub_character = nullptr;
        LuaVM::VirtualBasePart* stub_hrp = nullptr;
        LuaVM::VirtualHumanoid* stub_humanoid = nullptr;

        void ensure_stub_environment() {
            if (!stub_datamodel) {
                stub_datamodel = new LuaVM::VirtualInstance();
                stub_datamodel->ClassName = "DataModel";
                stub_datamodel->Name = "DataModel";
                stub_datamodel->RealAddress = globals::instances::datamodel.address;
            }
            if (!stub_workspace) {
                stub_workspace = new LuaVM::VirtualInstance();
                stub_workspace->ClassName = "Workspace";
                stub_workspace->Name = "Workspace";
                stub_workspace->Parent = stub_datamodel;
                stub_workspace->RealAddress = globals::instances::workspace.address;
            }
            if (!stub_players) {
                stub_players = new LuaVM::VirtualInstance();
                stub_players->ClassName = "Players";
                stub_players->Name = "Players";
                stub_players->Parent = stub_datamodel;
                stub_players->RealAddress = globals::instances::players.address;
            }
            if (auto* cam = dynamic_cast<LuaVM::VirtualCamera*>(stub_camera)) {
                cam->Parent = stub_workspace;
                cam->RealAddress = globals::instances::camera.address;
            }
            if (!stub_localplayer) {
                stub_localplayer = new LuaVM::VirtualInstance();
                stub_localplayer->ClassName = "Player";
                stub_localplayer->Name = "LocalPlayer";
                stub_localplayer->Parent = stub_players;
                stub_localplayer->RealAddress = globals::instances::localplayer.address;
            }
        }
    }

    int L_print(lua_State* L) {
        std::string output;
        int n = lua_gettop(L);
        for (int i = 1; i <= n; ++i) {
            size_t len = 0;
            const char* s = luaL_tolstring(L, i, &len);
            if (s) {
                if (i > 1) output += "\t";
                output.append(s, len);
            }
            lua_pop(L, 1);
        }
        LuaVM::get().log_print(output);
        return 0;
    }

    int L_warn(lua_State* L) {
        std::string output = "[Warn] ";
        int n = lua_gettop(L);
        for (int i = 1; i <= n; ++i) {
            size_t len = 0;
            const char* s = luaL_tolstring(L, i, &len);
            if (s) {
                if (i > 1) output += "\t";
                output.append(s, len);
            }
            lua_pop(L, 1);
        }
        LuaVM::get().log_print(output);
        return 0;
    }

    int L_error(lua_State* L) {
        const char* msg = luaL_optstring(L, 1, "");
        LuaVM::get().log_print(std::string("[Error] ") + msg);
        luaL_error(L, "%s", msg);
        return 0;
    }

    int L_loadstring(lua_State* L) {
        size_t len = 0;
        const char* chunk = luaL_checklstring(L, 1, &len);
        const char* chunkname = luaL_optstring(L, 2, "loadstring");

        try {
            Luau::CompileOptions opts;
            std::string bytecode = Luau::compile(std::string(chunk, len), opts);
            
            if (bytecode.empty()) {
                lua_pushnil(L);
                lua_pushstring(L, "Compilation failed: empty bytecode");
                return 2;
            }
            
            if (bytecode[0] == '\0') {
                lua_pushnil(L);
                lua_pushstring(L, bytecode.c_str() + 1);
                return 2;
            }
            
            if (luau_load(L, chunkname, bytecode.data(), bytecode.size(), 0) != 0) {
                const char* err = lua_tostring(L, -1);
                lua_pushnil(L);
                lua_pushstring(L, err ? err : "Load failed");
                return 2;
            }
            
            return 1;
        }
        catch (const std::exception& e) {
            lua_pushnil(L);
            lua_pushstring(L, e.what());
            return 2;
        }
    }

    int L_decompile(lua_State* L) {
        // Check if argument is an instance userdata
        if (!lua_isuserdata(L, 1)) {
            lua_pushstring(L, "-- Decompile requires a Script instance");
            return 1;
        }
        
        LuaInstance* inst = (LuaInstance*)lua_touserdata(L, 1);
        if (!inst || !inst->inst.is_valid()) {
            lua_pushstring(L, "-- Invalid script instance");
            return 1;
        }
        
        // Try to read bytecode from the script
        std::string bytecode = inst->inst.read_bytecode();
        if (bytecode.empty()) {
            lua_pushstring(L, "-- Failed to read script bytecode");
            return 1;
        }
        
        // Return disassembly or placeholder
        std::string result = "-- Decompiled Script\n-- Source extraction not available\n";
        lua_pushstring(L, result.c_str());
        return 1;
    }

    int L_pcall(lua_State* L) {
        luaL_checktype(L, 1, LUA_TFUNCTION);
        int nargs = lua_gettop(L) - 1;
        
        int status = lua_pcall(L, nargs, LUA_MULTRET, 0);
        
        lua_pushboolean(L, status == 0);
        lua_insert(L, 1);
        
        return lua_gettop(L);
    }

    int L_pairs(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_pushcfunction(L, [](lua_State* L) -> int {
            luaL_checktype(L, 1, LUA_TTABLE);
            lua_pushvalue(L, 2);
            if (lua_next(L, 1)) {
                return 2;
            }
            lua_pushnil(L);
            return 1;
        }, "next");
        lua_pushvalue(L, 1);
        lua_pushnil(L);
        return 3;
    }

    int L_ipairs(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_pushcfunction(L, [](lua_State* L) -> int {
            int i = (int)luaL_checkinteger(L, 2) + 1;
            lua_pushinteger(L, i);
            lua_rawgeti(L, 1, i);
            return lua_isnil(L, -1) ? 0 : 2;
        }, "ipairs_iter");
        lua_pushvalue(L, 1);
        lua_pushinteger(L, 0);
        return 3;
    }

    int L_require(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        
        fs::path ws = get_workspace();
        fs::path full_path = ws / path;
        
        std::string ext = full_path.extension().string();
        if (ext != ".lua" && ext != ".luau") {
            if (fs::exists(full_path.string() + ".lua")) {
                full_path = full_path.string() + ".lua";
            } else if (fs::exists(full_path.string() + ".luau")) {
                full_path = full_path.string() + ".luau";
            }
        }
        
        if (!fs::exists(full_path)) {
            luaL_error(L, "module '%s' not found", path);
            return 0;
        }
        
        std::ifstream file(full_path);
        if (!file.is_open()) {
            luaL_error(L, "cannot open module '%s'", path);
            return 0;
        }
        
        std::stringstream buffer;
        buffer << file.rdbuf();
        std::string content = buffer.str();
        
        Luau::CompileOptions opts;
        std::string bytecode = Luau::compile(content, opts);
        
        if (bytecode.empty() || bytecode[0] == '\0') {
            luaL_error(L, "failed to compile module '%s'", path);
            return 0;
        }
        
        if (luau_load(L, path, bytecode.data(), bytecode.size(), 0) != 0) {
            luaL_error(L, "failed to load module '%s': %s", path, lua_tostring(L, -1));
            return 0;
        }
        
        if (lua_pcall(L, 0, 1, 0) != 0) {
            luaL_error(L, "failed to execute module '%s': %s", path, lua_tostring(L, -1));
            return 0;
        }
        
        return 1;
    }

    int L_WorldToScreen(lua_State* L) {
        Vector3* v = try_get_vector3_userdata(L, 1);
        if (!v) {
            lua_pushnil(L);
            lua_pushboolean(L, false);
            return 2;
        }
        
        Vector2 screen = roblox::worldtoscreen(*v);

        // Sanitize return value for compatibility
        // If behind camera (-1, -1), report as off-screen and return 0,0 (common script convention for hidden)
        bool onScreen = true;
        
        if (screen.x == -1.0f && screen.y == -1.0f) {
            onScreen = false;
            screen.x = 0;
            screen.y = 0;
        } else {
             // Basic bounds check for onScreen boolean
             // Note: Some scripts use the vector even if offscreen, so we keep the coordinates if they are valid projections
             // But valid projections shouldn't be -1,-1 unless they are exactly there.
             // Usually onScreen check involves viewport dimensions, but simple >= 0 check was here.
             // We'll trust the caller/script to handle bounds if it's a valid projection.
             // But for the boolean, we can be more accurate if we had dimensions. 
             // For now, preserving existing 'onScreen' logic for valid projections if strictness not required, 
             // but strictly -1,-1 is the failure case from our math function.
             onScreen = true; 
        }

        push_vector2_userdata(L, screen.x, screen.y);
        lua_pushboolean(L, onScreen);
        return 2;
    }

    int L_run_secure(lua_State* L) {
        // Stub: protected code execution not available
        LuaVM::get().log_print("[Warn] run_secure is not available");
        return 0;
    }

    int L_Global_index(lua_State* L) {
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        std::string k(key);
        
        // Handle game/Game
        if (k == "game" || k == "Game") {
            if (globals::instances::datamodel.is_valid()) {
                push_roblox_instance_userdata(L, globals::instances::datamodel);
            } else {
                vm_globals::ensure_stub_environment();
                LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (ud) LuaInstance();
                ud->v_inst = vm_globals::stub_datamodel;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
            }
            return 1;
        }
        
        // Handle workspace/Workspace
        if (k == "workspace" || k == "Workspace") {
            if (globals::instances::workspace.is_valid()) {
                push_roblox_instance_userdata(L, globals::instances::workspace);
            } else {
                vm_globals::ensure_stub_environment();
                LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (ud) LuaInstance();
                ud->v_inst = vm_globals::stub_workspace;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
            }
            return 1;
        }
        
        // Handle Players
        if (k == "Players") {
            if (globals::instances::players.is_valid()) {
                push_roblox_instance_userdata(L, globals::instances::players);
            } else {
                vm_globals::ensure_stub_environment();
                LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                new (ud) LuaInstance();
                ud->v_inst = vm_globals::stub_players;
                luaL_getmetatable(L, "RobloxInstance");
                lua_setmetatable(L, -2);
            }
            return 1;
        }
        
        // Handle Camera
        if (k == "Camera") {
            if (globals::instances::camera.address != 0) {
                roblox::instance cam;
                cam.address = globals::instances::camera.address;
                push_roblox_instance_userdata(L, cam);
            } else {
                vm_globals::ensure_stub_environment();
                if (vm_globals::stub_camera) {
                    LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
                    new (ud) LuaInstance();
                    ud->v_inst = vm_globals::stub_camera;
                    luaL_getmetatable(L, "RobloxInstance");
                    lua_setmetatable(L, -2);
                } else {
                    lua_pushnil(L);
                }
            }
            return 1;
        }
        
        // Handle shared
        if (k == "shared") {
            lua_getglobal(L, "_G");
            lua_getfield(L, -1, "shared");
            if (lua_isnil(L, -1)) {
                lua_pop(L, 1);
                lua_newtable(L);
                lua_pushvalue(L, -1);
                lua_setfield(L, -3, "shared");
            }
            lua_remove(L, -2);
            return 1;
        }
        
        // Handle _G
        if (k == "_G") {
            lua_getglobal(L, "_G");
            return 1;
        }
        
        return 0;
    }

    // tick() - returns current Unix time as a high-precision double (Roblox global)
    int L_tick(lua_State* L) {
        auto now = std::chrono::system_clock::now();
        double t = std::chrono::duration<double>(now.time_since_epoch()).count();
        lua_pushnumber(L, t);
        return 1;
    }

    int L_typeof(lua_State* L) {
        if (lua_isuserdata(L, 1)) {
            if (lua_getmetatable(L, 1)) {
                lua_pushstring(L, "__type");
                lua_rawget(L, -2);
                if (lua_isstring(L, -1)) {
                    return 1;
                }
                lua_pop(L, 2);
            }
        }
        lua_pushstring(L, luaL_typename(L, 1));
        return 1;
    }
}
