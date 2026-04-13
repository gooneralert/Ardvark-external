// ============================================================================
// LuaVM - Matcha-compatible Lua Virtual Machine
// ============================================================================
// This module provides a Luau-based VM with Roblox executor-style APIs.
// Compatible with Matcha/Synapse/Script-Ware style scripts.
// ============================================================================

#include "luavm.h"
#include "luavm_common.h"
#include "../globals.h"
#include "../classes/classes.h"
#include "../driver/driver.h"
#include "../offsets.h"
#include <Luau/Compiler.h>
#include <Luau/Bytecode.h>
#include <wininet.h>
#pragma comment(lib, "wininet.lib")

namespace util {

    // ========================================================================
    // MODULE SYSTEM
    // ========================================================================
    static std::map<std::string, int> module_cache;
    static std::recursive_mutex module_mutex;

    // ========================================================================
    // PERFORMANCE METRICS
    // ========================================================================
    struct PerformanceMetrics {
        std::atomic<uint64_t> total_scripts_executed{0};
        std::atomic<uint64_t> total_execution_time_us{0};
        std::atomic<uint64_t> peak_memory_usage{0};
        std::atomic<uint64_t> function_calls{0};
        std::chrono::steady_clock::time_point start_time;

        PerformanceMetrics() : start_time(std::chrono::steady_clock::now()) {}

        void record_script_execution(uint64_t execution_time_us) {
            total_scripts_executed++;
            total_execution_time_us += execution_time_us;
        }

        void record_function_call() {
            function_calls++;
        }

        void update_memory_usage(uint64_t memory_used) {
            uint64_t current_peak = peak_memory_usage.load();
            while (memory_used > current_peak &&
                   !peak_memory_usage.compare_exchange_weak(current_peak, memory_used)) {}
        }

        double get_average_execution_time_ms() const {
            if (total_scripts_executed == 0) return 0.0;
            return (total_execution_time_us.load() / 1000.0) / total_scripts_executed.load();
        }

        double get_uptime_seconds() const {
            auto now = std::chrono::steady_clock::now();
            return std::chrono::duration<double>(now - start_time).count();
        }
    };

    static PerformanceMetrics g_performance_metrics;

    // ========================================================================
    // HELPER STRUCTURES
    // ========================================================================
    struct NamedCFunc {
        const char* name;
        lua_CFunction fn;
    };

    static void register_global_functions(lua_State* L, const NamedCFunc* fns, size_t count) {
        for (size_t i = 0; i < count; ++i) {
            const NamedCFunc& f = fns[i];
            if (!f.name || !f.fn) continue;
            lua_pushcfunction(L, f.fn, f.name);
            lua_setglobal(L, f.name);
        }
    }

    // ========================================================================
    // TIMING & SCHEDULER
    // ========================================================================
    double now_seconds() {
        using clock = std::chrono::steady_clock;
        static const auto start = clock::now();
        const auto elapsed = clock::now() - start;
        return std::chrono::duration<double>(elapsed).count();
    }

    long long now_microseconds() { return (long long)(now_seconds() * 1000000.0); }
    
    static std::atomic<long long> g_interrupt_start_us{ 0 };
    static long long g_interrupt_budget_us = 10000;

    void vm_interrupt(lua_State* L, int /*gc*/) {
        const long long start = g_interrupt_start_us.load(std::memory_order_relaxed);
        if (start == 0) return;
        if (now_microseconds() - start > g_interrupt_budget_us) {
            if (lua_isyieldable(L)) lua_break(L);
        }
    }

    int L_task_wait(lua_State* L) {
        double seconds = 0.0;
        if (!lua_isnoneornil(L, 1)) {
            seconds = lua_tonumber(L, 1);
        }
        
        if (seconds <= 0.0) seconds = 1.0 / 240.0;
        
        if (!lua_isyieldable(L)) {
            std::this_thread::sleep_for(std::chrono::duration<double>(seconds));
            lua_pushnumber(L, seconds);
            return 1;
        }
        
        int is_main = lua_pushthread(L);
        lua_pop(L, 1);
        
        if (is_main) {
            std::this_thread::sleep_for(std::chrono::duration<double>(seconds));
            lua_pushnumber(L, seconds);
            return 1;
        }
        
        lua_pushnumber(L, seconds);
        return lua_yield(L, 1);
    }

    int L_spawn(lua_State* L) {
        luaL_checktype(L, 1, LUA_TFUNCTION);
        lua_State* co = nullptr; 
        int ref = LuaVM::get().create_coroutine(&co);
        lua_xmove(L, co, 1); 
        // Schedule at front to ensure it runs before resumed main thread (fix race condition)
        LuaVM::get().schedule_thread(co, ref, now_seconds(), 0, true);
        return 0;
    }

    int L_task_delay(lua_State* L) {
        double seconds = lua_isnumber(L, 1) ? lua_tonumber(L, 1) : 0.0;
        luaL_checktype(L, 2, LUA_TFUNCTION);
        lua_State* co = nullptr; 
        int ref = LuaVM::get().create_coroutine(&co);
        lua_pushvalue(L, 2); 
        lua_xmove(L, co, 1);
        LuaVM::get().schedule_thread(co, ref, now_seconds() + seconds);
        return 0;
    }

    // ========================================================================
    // VM INITIALIZATION
    // ========================================================================
    void LuaVM::init() {
        std::lock_guard<std::recursive_mutex> vlock(vm_mutex);
        
        if (L) lua_close(L);
        
        L = luaL_newstate();
        luaL_openlibs(L);
        
        if (lua_Callbacks* cb = lua_callbacks(L)) cb->interrupt = vm_interrupt;

        // Core global functions
        static const NamedCFunc kGlobalFuncs[] = {
            { "print",         L_print },
            { "printl",        L_print },
            { "warn",          L_warn },
            { "error",         L_error },
            { "loadstring",    L_loadstring },
            { "load",          L_loadstring },
            { "decompile",     L_decompile },
            { "WorldToScreen", L_WorldToScreen },
            { "require",       L_require },
            { "wait",          L_task_wait },
            { "spawn",         L_spawn },
            // { "pcall",         L_pcall }, // Use native yieldable pcall
            { "pairs",         L_pairs },
            { "ipairs",        L_ipairs },
            { "typeof",        L_typeof },
            { "run_secure",    L_run_secure },
            { "tick",          L_tick },
        };
        register_global_functions(L, kGlobalFuncs, sizeof(kGlobalFuncs) / sizeof(kGlobalFuncs[0]));

        // Input functions
        static const NamedCFunc kInputFuncs[] = {
            { "mouse1click",    L_mouse1click },
            { "mouse1press",    L_mouse1press },
            { "mouse1release",  L_mouse1release },
            { "mouse2click",    L_mouse2click },
            { "mouse2press",    L_mouse2press },
            { "mouse2release",  L_mouse2release },
            { "mousemoveabs",   L_mousemoveabs },
            { "mousemoverel",   L_mousemoverel },
            { "mousescroll",    L_mousescroll },
            { "keypress",       L_keypress },
            { "keyrelease",     L_keyrelease },
            { "iskeypressed",   L_iskeypressed },
            { "ismouse1pressed",L_ismouse1pressed },
            { "ismouse2pressed",L_ismouse2pressed },
            { "isrbxactive",    L_isrbxactive },
            { "setrobloxinput", L_setrobloxinput },
            { "setclipboard",   L_setclipboard },
        };
        register_global_functions(L, kInputFuncs, sizeof(kInputFuncs) / sizeof(kInputFuncs[0]));

        // Vector3
        push_vector3_metatable(L); lua_pop(L, 1);
        lua_newtable(L); 
        lua_pushcfunction(L, L_Vector3_new, "new"); 
        lua_setfield(L, -2, "new"); 
        lua_setglobal(L, "Vector3");

        // Vector2
        push_vector2_metatable(L); lua_pop(L, 1);
        lua_newtable(L); 
        lua_pushcfunction(L, L_Vector2_new, "new"); 
        lua_setfield(L, -2, "new"); 
        lua_setglobal(L, "Vector2");

        // CFrame
        luaL_newmetatable(L, "CFrame");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_CFrame_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "CFrame"); lua_settable(L, -3);
        lua_pop(L, 1);
        lua_newtable(L); 
        lua_pushcfunction(L, L_CFrame_new, "new"); lua_setfield(L, -2, "new"); 
        lua_pushcfunction(L, L_CFrame_lookAt, "lookAt"); lua_setfield(L, -2, "lookAt"); 
        lua_setglobal(L, "CFrame");

        // Color3
        luaL_newmetatable(L, "Color3");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_Color3_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "Color3"); lua_settable(L, -3);
        lua_pop(L, 1);
        lua_newtable(L);
        lua_pushcfunction(L, L_Color3_new, "new"); lua_setfield(L, -2, "new");
        lua_pushcfunction(L, L_Color3_fromRGB, "fromRGB"); lua_setfield(L, -2, "fromRGB");
        lua_pushcfunction(L, L_Color3_fromHSV, "fromHSV"); lua_setfield(L, -2, "fromHSV");
        lua_pushcfunction(L, L_Color3_fromHex, "fromHex"); lua_setfield(L, -2, "fromHex");
        lua_setglobal(L, "Color3");

        // UDim
        luaL_newmetatable(L, "UDim");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_UDim_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "UDim"); lua_settable(L, -3);
        lua_pop(L, 1);
        lua_newtable(L); 
        lua_pushcfunction(L, L_UDim_new, "new"); 
        lua_setfield(L, -2, "new"); 
        lua_setglobal(L, "UDim");

        // UDim2
        luaL_newmetatable(L, "UDim2");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_UDim2_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "UDim2"); lua_settable(L, -3);
        lua_pop(L, 1);
        lua_newtable(L);
        lua_pushcfunction(L, L_UDim2_new, "new"); lua_setfield(L, -2, "new");
        lua_pushcfunction(L, L_UDim2_fromScale, "fromScale"); lua_setfield(L, -2, "fromScale");
        lua_pushcfunction(L, L_UDim2_fromOffset, "fromOffset"); lua_setfield(L, -2, "fromOffset");
        lua_setglobal(L, "UDim2");

        // Instance metatable
        luaL_newmetatable(L, "RobloxInstance");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_Instance_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__newindex"); lua_pushcfunction(L, L_Instance_newindex, "__newindex"); lua_settable(L, -3);
        lua_pushstring(L, "__namecall"); lua_pushcfunction(L, L_Instance_namecall, "__namecall"); lua_settable(L, -3);
        lua_pushstring(L, "__eq"); lua_pushcfunction(L, L_Instance_eq, "__eq"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "Instance"); lua_settable(L, -3);
        lua_pop(L, 1);

        lua_newtable(L); 
        lua_pushstring(L, "new"); 
        lua_pushcfunction(L, L_Instance_new, "new"); 
        lua_settable(L, -3); 
        lua_setglobal(L, "Instance");

        // Mouse metatable
        luaL_newmetatable(L, "Mouse");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_Mouse_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "Mouse"); lua_settable(L, -3);
        lua_pop(L, 1);

        // Environment functions
        static const NamedCFunc kEnvFuncs[] = {
            { "getgenv",           L_getgenv },
            { "getrenv",           L_getrenv },
            { "getfenv",           L_getfenv_fn },
            { "setfenv",           L_setfenv_fn },
            { "getrawmetatable",   L_getrawmetatable },
            { "setrawmetatable",   L_setrawmetatable },
            { "setreadonly",       L_setreadonly },
            { "isreadonly",        L_isreadonly },
            { "makereadonly",      L_makereadonly },
            { "makewriteable",     L_makewriteable },
            { "gethui",            L_gethui },
            { "cloneref",          L_cloneref },
            { "checkcaller",       L_checkcaller },
            { "identifyexecutor",  L_identifyexecutor },
            { "getexecutorname",   L_getexecutorname },
        };
        register_global_functions(L, kEnvFuncs, sizeof(kEnvFuncs) / sizeof(kEnvFuncs[0]));

        // Drawing library
        luaL_newmetatable(L, "DrawingInstance");
        lua_pushstring(L, "__index"); lua_pushcfunction(L, L_Drawing_index, "__index"); lua_settable(L, -3);
        lua_pushstring(L, "__newindex"); lua_pushcfunction(L, L_Drawing_newindex, "__newindex"); lua_settable(L, -3);
        lua_pushstring(L, "__namecall"); lua_pushcfunction(L, L_Drawing_namecall, "__namecall"); lua_settable(L, -3);
        lua_pushstring(L, "__gc"); lua_pushcfunction(L, L_Drawing_gc, "__gc"); lua_settable(L, -3);
        lua_pushstring(L, "__type"); lua_pushstring(L, "Drawing"); lua_settable(L, -3);
        lua_pop(L, 1);
        lua_newtable(L); 
        lua_pushstring(L, "new"); 
        lua_pushcfunction(L, L_Drawing_new, "new"); 
        lua_settable(L, -3); 
        lua_setglobal(L, "Drawing");

        // Filesystem functions
        lua_pushcfunction(L, L_listfiles, "listfiles"); lua_setglobal(L, "listfiles");
        lua_pushcfunction(L, L_isfile, "isfile"); lua_setglobal(L, "isfile");
        lua_pushcfunction(L, L_isfolder, "isfolder"); lua_setglobal(L, "isfolder");
        lua_pushcfunction(L, L_writefile, "writefile"); lua_setglobal(L, "writefile");
        lua_pushcfunction(L, L_readfile, "readfile"); lua_setglobal(L, "readfile");
        lua_pushcfunction(L, L_appendfile, "appendfile"); lua_setglobal(L, "appendfile");
        lua_pushcfunction(L, L_loadfile, "loadfile"); lua_setglobal(L, "loadfile");
        lua_pushcfunction(L, L_delfile, "delfile"); lua_setglobal(L, "delfile");
        lua_pushcfunction(L, L_makefolder, "makefolder"); lua_setglobal(L, "makefolder");
        lua_pushcfunction(L, L_delfolder, "delfolder"); lua_setglobal(L, "delfolder");

        // Memory functions
        lua_pushcfunction(L, L_getbase, "getbase"); lua_setglobal(L, "getbase");
        lua_pushcfunction(L, L_memory_read, "memory_read"); lua_setglobal(L, "memory_read");
        lua_pushcfunction(L, L_memory_write, "memory_write"); lua_setglobal(L, "memory_write");

        // FFlag functions
        lua_pushcfunction(L, L_setfflag, "setfflag"); lua_setglobal(L, "setfflag");
        lua_pushcfunction(L, L_getfflag, "getfflag"); lua_setglobal(L, "getfflag");

        // HTTP
        lua_pushcfunction(L, L_http_request, "request"); lua_setglobal(L, "request");

        // Task library
        lua_newtable(L);
        lua_pushcfunction(L, L_task_wait, "wait"); lua_setfield(L, -2, "wait");
        lua_pushcfunction(L, L_spawn, "spawn"); lua_setfield(L, -2, "spawn");
        lua_pushcfunction(L, L_spawn, "defer"); lua_setfield(L, -2, "defer");
        lua_pushcfunction(L, L_task_delay, "delay"); lua_setfield(L, -2, "delay");
        lua_setglobal(L, "task");

        // Global task functions
        lua_pushcfunction(L, L_spawn, "spawn"); lua_setglobal(L, "spawn");
        lua_pushcfunction(L, L_task_wait, "wait"); lua_setglobal(L, "wait");
        lua_pushcfunction(L, L_task_delay, "delay"); lua_setglobal(L, "delay");

        // Global metatable for game/workspace access
        lua_newtable(L); 
        lua_pushstring(L, "__index"); 
        lua_pushcfunction(L, L_Global_index, "__index"); 
        lua_settable(L, -3);
        lua_setmetatable(L, LUA_GLOBALSINDEX);

        // Explicitly set globals to ensure they are correct (overriding any potential table definitions)
        vm_globals::ensure_stub_environment();
        
        // game
        {
            LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
            new (ud) LuaInstance();
            ud->v_inst = vm_globals::stub_datamodel;
            luaL_getmetatable(L, "RobloxInstance");
            lua_setmetatable(L, -2);
            lua_setglobal(L, "game");
        }
        
        // workspace
        {
            LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
            new (ud) LuaInstance();
            ud->v_inst = vm_globals::stub_workspace;
            luaL_getmetatable(L, "RobloxInstance");
            lua_setmetatable(L, -2);
            lua_setglobal(L, "workspace");
        }
        
        // Players
        {
            LuaInstance* ud = (LuaInstance*)lua_newuserdata(L, sizeof(LuaInstance));
            new (ud) LuaInstance();
            ud->v_inst = vm_globals::stub_players;
            luaL_getmetatable(L, "RobloxInstance");
            lua_setmetatable(L, -2);
            lua_setglobal(L, "Players");
        }

        log_print("[System] LuaVM Initialized");
    }

    void LuaVM::execute_script(const std::string& script) {
        std::lock_guard<std::recursive_mutex> lock(vm_mutex);
        if (!L) init();

        auto start_time = std::chrono::steady_clock::now();

        log_print("[System] Compiling script...");

        try {
            Luau::CompileOptions opts;
            std::string bc = Luau::compile(script, opts);
            
            if (bc.empty()) {
                log_print("[Error] Compilation produced empty bytecode");
                return;
            }

            if (bc[0] == '\0') {
                log_print(std::string("[Compile Error] ") + (bc.c_str() + 1));
                return;
            }

            if (luau_load(L, "Script", bc.data(), bc.size(), 0) == 0) {
                lua_State* co = lua_newthread(L); 
                lua_insert(L, -2); 
                lua_xmove(L, co, 1);
                int ref = lua_ref(L, -1); 
                lua_pop(L, 1);
                schedule_thread(co, ref, now_seconds());
                log_print("[System] Script scheduled");
            } else {
                log_print(std::string("[Load Error] ") + lua_tostring(L, -1));
                lua_pop(L, 1);
            }
        } catch (const std::exception& e) {
            log_print(std::string("[Exception] ") + e.what());
        } catch (...) {
            log_print("[Exception] Unknown error during execution");
        }

        auto end_time = std::chrono::steady_clock::now();
        uint64_t execution_time_us = std::chrono::duration_cast<std::chrono::microseconds>(end_time - start_time).count();
        g_performance_metrics.record_script_execution(execution_time_us);

        if (L) {
            lua_gc(L, LUA_GCCOLLECT, 0);
            g_performance_metrics.update_memory_usage(static_cast<uint64_t>(lua_gc(L, LUA_GCCOUNT, 0)) * 1024);
        }
    }

    void LuaVM::schedule_thread(lua_State* co, int registry_ref, double resume_time, int nargs, bool at_front) {
        std::lock_guard<std::recursive_mutex> lock(script_mutex);
        if (at_front)
            script_threads.insert(script_threads.begin(), { co, registry_ref, resume_time, nargs });
        else
            script_threads.push_back({ co, registry_ref, resume_time, nargs });
    }

#if defined(_MSC_VER)
    static thread_local DWORD t_last_seh_code = 0;
    static thread_local void* t_last_seh_address = nullptr;

    static int seh_capture_filter(EXCEPTION_POINTERS* ep) {
        if (ep && ep->ExceptionRecord) {
            t_last_seh_code = ep->ExceptionRecord->ExceptionCode;
            t_last_seh_address = ep->ExceptionRecord->ExceptionAddress;
        } else {
            t_last_seh_code = 0;
            t_last_seh_address = nullptr;
        }
        return EXCEPTION_EXECUTE_HANDLER;
    }

    static int resume_thread_seh(lua_State* thread, int nargs) {
        t_last_seh_code = 0;
        t_last_seh_address = nullptr;
        int status = LUA_ERRRUN;
        __try {
            status = lua_resume(thread, nullptr, nargs);
        }
        __except (seh_capture_filter(GetExceptionInformation())) {
            status = LUA_ERRRUN;
        }
        return status;
    }
#endif

    void LuaVM::step() {
         // Update Roblox cache (ViewMatrix, etc.) for WorldToScreen
         roblox::cache::update();

        std::lock_guard<std::recursive_mutex> vlock(vm_mutex);
        if (!L) return;

        const int MAX_RESUMES_PER_FRAME = 100;
        int resume_count = 0;
        
        while (resume_count < MAX_RESUMES_PER_FRAME) {
            ++resume_count;
            lua_State* thread = nullptr;
            int pending_args = 0;
            int registry_ref = -1;

            {
                const double now = now_seconds();
                std::lock_guard<std::recursive_mutex> lock(script_mutex);

                for (auto& t : script_threads) {
                    if (t.thread && t.wake_time_sec <= now) {
                        thread = t.thread;
                        pending_args = t.pending_args;
                        registry_ref = t.registry_ref;
                        t.pending_args = 0;
                        break;
                    }
                }
            }

            if (!thread) break;
            
            int thread_status = lua_costatus(L, thread);
            if (thread_status == LUA_COFIN || thread_status == LUA_COERR) {
                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                    if (it->thread == thread) {
                        if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                        script_threads.erase(it);
                        break;
                    }
                }
                continue;
            }

            int status = LUA_ERRRUN;
            try {
                g_interrupt_start_us.store(now_microseconds(), std::memory_order_relaxed);
#if defined(_MSC_VER)
                status = resume_thread_seh(thread, pending_args);
#else
                status = lua_resume(thread, nullptr, pending_args);
#endif
                g_interrupt_start_us.store(0, std::memory_order_relaxed);
            }
            catch (const std::exception& e) {
                g_interrupt_start_us.store(0, std::memory_order_relaxed);
                log_print(std::string("[Exception] lua_resume: ") + e.what());
                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                    if (it->thread == thread) {
                        if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                        script_threads.erase(it);
                        break;
                    }
                }
                continue;
            }
            catch (...) {
                g_interrupt_start_us.store(0, std::memory_order_relaxed);
                log_print("[Exception] lua_resume: unknown");
                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                    if (it->thread == thread) {
                        if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                        script_threads.erase(it);
                        break;
                    }
                }
                continue;
            }

            const double now = now_seconds();

#if defined(_MSC_VER)
            if (status == LUA_ERRRUN && t_last_seh_code != 0) {
                lua_resetthread(thread);
                std::ostringstream oss;
                oss << "[SEH] lua_resume structured exception 0x" << std::hex << t_last_seh_code
                    << " at " << t_last_seh_address;
                log_print(oss.str());

                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                    if (it->thread == thread) {
                        if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                        script_threads.erase(it);
                        break;
                    }
                }
                continue;
            }
#endif

            if (status == LUA_YIELD || status == LUA_BREAK) {
                double wait_duration = 0.0;
                if (lua_gettop(thread) > 0 && lua_isnumber(thread, -1)) {
                    wait_duration = lua_tonumber(thread, -1);
                } 
                if (wait_duration <= 0.0) wait_duration = 1.0 / 240.0;
                
                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto& t : script_threads) {
                    if (t.thread == thread) {
                        t.wake_time_sec = now + wait_duration;
                        break;
                    }
                }
                continue;
            }

            if (status == LUA_OK) {
                int nres = lua_gettop(thread);
                if (nres > 0) {
                    lua_xmove(thread, L, 1);
                    lua_setglobal(L, "__last_return");
                }
                log_print("[System] Script execution finished");

                std::lock_guard<std::recursive_mutex> lock(script_mutex);
                for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                    if (it->thread == thread) {
                        if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                        script_threads.erase(it);
                        break;
                    }
                }
                continue;
            }

            size_t err_len = 0;
            const char* err = luaL_tolstring(thread, -1, &err_len);
            log_print(std::string("[Runtime Error] ") + (err ? std::string(err, err_len) : "Unknown"));
            const char* trace = lua_debugtrace(thread);
            if (trace && *trace)
                log_print(std::string("[Stacktrace] ") + trace);
            lua_pop(thread, 2);

            std::lock_guard<std::recursive_mutex> lock(script_mutex);
            for (auto it = script_threads.begin(); it != script_threads.end(); ++it) {
                if (it->thread == thread) {
                    if (it->registry_ref != -1) lua_unref(L, it->registry_ref);
                    script_threads.erase(it);
                    break;
                }
            }
        }
    }

    void LuaVM::push_game_service(lua_State* L) {
        lua_getglobal(L, "game");
        if (lua_isnil(L, -1)) {
            lua_pop(L, 1);
            lua_newtable(L);
            lua_pushstring(L, "ClassName");
            lua_pushstring(L, "DataModel");
            lua_settable(L, -3);
        }
    }

    void LuaVM::push_workspace_service(lua_State* L) {
        lua_getglobal(L, "workspace");
        if (lua_isnil(L, -1)) {
            lua_pop(L, 1);
            lua_newtable(L);
            lua_pushstring(L, "ClassName");
            lua_pushstring(L, "Workspace");
            lua_settable(L, -3);
        }
    }

    void LuaVM::render_drawings(ImDrawList* draw_list) {
        if (!draw_list) return;
        
        std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
        
        std::vector<DrawingBase*> sorted_drawings = drawings;
        std::sort(sorted_drawings.begin(), sorted_drawings.end(), [](DrawingBase* a, DrawingBase* b) {
            return a->zindex < b->zindex;
        });
        
        for (size_t i = 0; i < sorted_drawings.size(); ++i) {
            auto* drawing = sorted_drawings[i];
            if (!drawing || !drawing->visible) continue;
            
            try {
                drawing->render(draw_list);
            }
            catch (...) {
                auto it = std::find(drawings.begin(), drawings.end(), drawing);
                if (it != drawings.end()) drawings.erase(it);
            }
        }
        
        if (!ImGui::GetCurrentContext()) return;
        ImVec2 ds = ImGui::GetIO().DisplaySize;
        
        for (size_t i = 0; i < virtual_roots.size(); ++i) {
            auto* root = virtual_roots[i];
            if (!root) continue;
            
            try {
                root->render(draw_list, ImVec2(0,0), ds);
            }
            catch (...) {
                virtual_roots.erase(virtual_roots.begin() + i);
                --i;
            }
        }
    }

    void LuaVM::add_drawing(DrawingBase* d) {
        std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
        drawings.push_back(d);
    }

    void LuaVM::remove_drawing(DrawingBase* d) {
        std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
        auto it = std::find(drawings.begin(), drawings.end(), d);
        if (it != drawings.end()) {
            drawings.erase(it);
        }
        delete d;
    }

    void LuaVM::add_virtual_root(VirtualScreenGui* gui) {
        std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
        virtual_roots.push_back(gui);
    }

    void LuaVM::remove_virtual_root(VirtualScreenGui* gui) {
        std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
        virtual_roots.erase(std::remove(virtual_roots.begin(), virtual_roots.end(), gui), virtual_roots.end());
    }

    void LuaVM::set_simulated_velocity(uintptr_t instance_addr, uintptr_t primitive_addr, const math::Vector3& velocity) {
        std::lock_guard<std::recursive_mutex> lock(m_physics_mutex);
        m_simulated_physics[instance_addr] = { primitive_addr, instance_addr, velocity };
    }
    
    void LuaVM::restart() { 
        {
            std::lock_guard<std::recursive_mutex> lock(script_mutex);
            for (auto& t : script_threads) {
                if (L && t.registry_ref != -1) {
                    lua_unref(L, t.registry_ref);
                }
            }
            script_threads.clear();
        }
        
        {
            std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
            for (auto* d : drawings) {
                delete d;
            }
            drawings.clear();
            
            for (auto* r : virtual_roots) {
                delete r;
            }
            virtual_roots.clear();
        }
        
        {
            std::lock_guard<std::recursive_mutex> lock(m_physics_mutex);
            m_simulated_physics.clear();
        }
        
        clear_logs();
        init(); 
        log_print("[System] LuaVM Restarted");
    }
    
    void LuaVM::clear_logs() {
        std::lock_guard<std::recursive_mutex> lock(log_mutex);
        logs.clear();
    }
    
    size_t LuaVM::get_log_count() {
        std::lock_guard<std::recursive_mutex> lock(log_mutex);
        return logs.size();
    }
    
    std::vector<std::string> LuaVM::get_logs() {
        std::lock_guard<std::recursive_mutex> lock(log_mutex);
        return logs;
    }
    
    void LuaVM::log_print(const std::string& str) {
        std::lock_guard<std::recursive_mutex> lock(log_mutex);
        logs.push_back(str);
        if (logs.size() > 500) logs.erase(logs.begin());
    }

    int LuaVM::ensure_thread_registry_ref(lua_State* thread) {
        lua_pushthread(thread);
        int ref = lua_ref(thread, -1);
        lua_pop(thread, 1);
        return ref;
    }

    int LuaVM::create_coroutine(lua_State** out_thread) {
        *out_thread = lua_newthread(L);
        int ref = lua_ref(L, -1);
        lua_pop(L, 1);
        return ref;
    }

    lua_State* LuaVM::state() { return L; }

    LuaVM& LuaVM::get() { static LuaVM vm; return vm; }
    LuaVM::LuaVM() : L(nullptr) {}
    LuaVM::~LuaVM() {
        if (L) {
            lua_close(L);
            L = nullptr;
        }

        {
            std::lock_guard<std::recursive_mutex> lock(m_physics_mutex);
            m_simulated_physics.clear();
        }

        {
            std::lock_guard<std::recursive_mutex> lock(drawing_mutex);
            for (auto* d : drawings) {
                delete d;
            }
            drawings.clear();

            for (auto* r : virtual_roots) {
                delete r;
            }
            virtual_roots.clear();
        }
    }

    // Performance stats function
    int L_get_performance_stats(lua_State* L) {
        lua_newtable(L);
        lua_pushnumber(L, (double)g_performance_metrics.total_scripts_executed.load());
        lua_setfield(L, -2, "total_scripts");
        lua_pushnumber(L, g_performance_metrics.get_average_execution_time_ms());
        lua_setfield(L, -2, "avg_execution_ms");
        lua_pushnumber(L, (double)g_performance_metrics.peak_memory_usage.load());
        lua_setfield(L, -2, "peak_memory_bytes");
        lua_pushnumber(L, g_performance_metrics.get_uptime_seconds());
        lua_setfield(L, -2, "uptime_seconds");
        return 1;
    }
}
