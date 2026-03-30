// ============================================================================
// LuaVM Environment - Environment, filesystem, and HTTP functions
// ============================================================================

#include "luavm_common.h"
#include <fstream>
#include <sstream>

namespace util {
    // ========================================================================
    // ENVIRONMENT FUNCTIONS
    // ========================================================================
    int L_getgenv(lua_State* L) {
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        return 1;
    }

    int L_getrenv(lua_State* L) {
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        return 1;
    }

    int L_getfenv_fn(lua_State* L) {
        if (lua_isfunction(L, 1)) {
            lua_getfenv(L, 1);
            return 1;
        }
        lua_pushvalue(L, LUA_GLOBALSINDEX);
        return 1;
    }

    int L_setfenv_fn(lua_State* L) {
        if (lua_isfunction(L, 1) && lua_istable(L, 2)) {
            lua_pushvalue(L, 2);
            lua_setfenv(L, 1);
        }
        return 0;
    }

    int L_getrawmetatable(lua_State* L) {
        if (!lua_getmetatable(L, 1)) {
            lua_pushnil(L);
        }
        return 1;
    }

    int L_setrawmetatable(lua_State* L) {
        lua_setmetatable(L, 1);
        return 0;
    }

    int L_setreadonly(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        bool ro = lua_toboolean(L, 2);
        lua_setreadonly(L, 1, ro);
        return 0;
    }

    int L_isreadonly(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_pushboolean(L, lua_getreadonly(L, 1));
        return 1;
    }

    int L_makereadonly(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_setreadonly(L, 1, true);
        return 0;
    }

    int L_makewriteable(lua_State* L) {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_setreadonly(L, 1, false);
        return 0;
    }

    int L_newcclosure(lua_State* L) {
        luaL_checktype(L, 1, LUA_TFUNCTION);
        lua_pushvalue(L, 1);
        return 1;
    }

    int L_clonefunction(lua_State* L) {
        luaL_checktype(L, 1, LUA_TFUNCTION);
        lua_pushvalue(L, 1);
        return 1;
    }

    int L_iscclosure(lua_State* L) {
        lua_pushboolean(L, lua_iscfunction(L, 1));
        return 1;
    }

    int L_islclosure(lua_State* L) {
        lua_pushboolean(L, lua_isfunction(L, 1) && !lua_iscfunction(L, 1));
        return 1;
    }

    int L_checkcaller(lua_State* L) {
        lua_pushboolean(L, true);
        return 1;
    }

    int L_identifyexecutor(lua_State* L) {
        lua_pushstring(L, "Nift");
        lua_pushstring(L, "1.0");
        return 2;
    }

    int L_getexecutorname(lua_State* L) {
        lua_pushstring(L, "Nift");
        return 1;
    }

    int L_getnamecallmethod(lua_State* L) {
        const char* method = lua_namecallatom(L, nullptr);
        if (method) lua_pushstring(L, method);
        else lua_pushnil(L);
        return 1;
    }

    int L_setnamecallmethod(lua_State* L) {
        // Cannot actually change the namecall method
        return 0;
    }

    int L_cloneref(lua_State* L) {
        lua_pushvalue(L, 1);
        return 1;
    }

    int L_gethui(lua_State* L) {
        // Return a stub PlayerGui
        lua_newtable(L);
        lua_pushstring(L, "ClassName");
        lua_pushstring(L, "PlayerGui");
        lua_settable(L, -3);
        return 1;
    }

    // ========================================================================
    // FILESYSTEM FUNCTIONS
    // ========================================================================
    static fs::path resolve_safe_path(const std::string& path) {
        fs::path ws = get_workspace();
        fs::path target = ws / path;
        
        // Security: ensure we stay within workspace
        fs::path normalized = fs::weakly_canonical(target);
        fs::path ws_normalized = fs::weakly_canonical(ws);
        
        std::string targetStr = normalized.string();
        std::string wsStr = ws_normalized.string();
        
        if (targetStr.find(wsStr) != 0) {
            return fs::path();
        }
        
        return normalized;
    }

    int L_listfiles(lua_State* L) {
        const char* path = luaL_optstring(L, 1, "");
        fs::path target = resolve_safe_path(path);
        
        if (target.empty() || !fs::exists(target) || !fs::is_directory(target)) {
            lua_newtable(L);
            return 1;
        }
        
        lua_newtable(L);
        int i = 1;
        for (const auto& entry : fs::directory_iterator(target)) {
            lua_pushstring(L, entry.path().filename().string().c_str());
            lua_rawseti(L, -2, i++);
        }
        return 1;
    }

    int L_isfile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        lua_pushboolean(L, !target.empty() && fs::exists(target) && fs::is_regular_file(target));
        return 1;
    }

    int L_isfolder(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        lua_pushboolean(L, !target.empty() && fs::exists(target) && fs::is_directory(target));
        return 1;
    }

    int L_writefile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        size_t len = 0;
        const char* data = luaL_checklstring(L, 2, &len);
        
        fs::path target = resolve_safe_path(path);
        if (target.empty()) {
            luaL_error(L, "Invalid path");
            return 0;
        }
        
        fs::create_directories(target.parent_path());
        std::ofstream file(target, std::ios::binary);
        if (file) {
            file.write(data, len);
        }
        return 0;
    }

    int L_readfile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        
        if (target.empty() || !fs::exists(target)) {
            luaL_error(L, "File not found: %s", path);
            return 0;
        }
        
        std::ifstream file(target, std::ios::binary);
        if (!file) {
            luaL_error(L, "Cannot open file: %s", path);
            return 0;
        }
        
        std::stringstream buffer;
        buffer << file.rdbuf();
        lua_pushstring(L, buffer.str().c_str());
        return 1;
    }

    int L_appendfile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        size_t len = 0;
        const char* data = luaL_checklstring(L, 2, &len);
        
        fs::path target = resolve_safe_path(path);
        if (target.empty()) {
            luaL_error(L, "Invalid path");
            return 0;
        }
        
        std::ofstream file(target, std::ios::binary | std::ios::app);
        if (file) {
            file.write(data, len);
        }
        return 0;
    }

    int L_loadfile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        
        if (target.empty() || !fs::exists(target)) {
            lua_pushnil(L);
            lua_pushstring(L, "File not found");
            return 2;
        }
        
        std::ifstream file(target);
        std::stringstream buffer;
        buffer << file.rdbuf();
        
        lua_pushvalue(L, 1);
        lua_pushstring(L, buffer.str().c_str());
        lua_replace(L, 1);
        return L_loadstring(L);
    }

    int L_delfile(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        
        if (!target.empty() && fs::exists(target) && fs::is_regular_file(target)) {
            std::error_code ec;
            fs::remove(target, ec);
        }
        return 0;
    }

    int L_makefolder(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        
        if (!target.empty()) {
            std::error_code ec;
            fs::create_directories(target, ec);
        }
        return 0;
    }

    int L_delfolder(lua_State* L) {
        const char* path = luaL_checkstring(L, 1);
        fs::path target = resolve_safe_path(path);
        
        if (!target.empty() && fs::exists(target) && fs::is_directory(target)) {
            std::error_code ec;
            fs::remove_all(target, ec);
        }
        return 0;
    }

    // ========================================================================
    // HTTP FUNCTIONS
    // ========================================================================
    int L_http_request(lua_State* L) {
        const char* url = nullptr;
        const char* method = "GET";
        
        if (lua_istable(L, 1)) {
            lua_getfield(L, 1, "Url");
            url = lua_tostring(L, -1);
            lua_pop(L, 1);
            
            lua_getfield(L, 1, "Method");
            if (lua_isstring(L, -1)) method = lua_tostring(L, -1);
            lua_pop(L, 1);
        } else {
            url = luaL_checkstring(L, 1);
        }
        
        if (!url) {
            lua_pushnil(L);
            return 1;
        }
        
        // Simple HTTP GET using WinINet
        HINTERNET hInternet = InternetOpenA("Nift/1.0", INTERNET_OPEN_TYPE_DIRECT, nullptr, nullptr, 0);
        if (!hInternet) {
            lua_pushnil(L);
            return 1;
        }
        
        HINTERNET hUrl = InternetOpenUrlA(hInternet, url, nullptr, 0, INTERNET_FLAG_RELOAD, 0);
        if (!hUrl) {
            InternetCloseHandle(hInternet);
            lua_pushnil(L);
            return 1;
        }
        
        std::string response;
        char buffer[4096];
        DWORD bytesRead;
        while (InternetReadFile(hUrl, buffer, sizeof(buffer) - 1, &bytesRead) && bytesRead > 0) {
            buffer[bytesRead] = '\0';
            response += buffer;
        }
        
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hInternet);
        
        lua_newtable(L);
        lua_pushstring(L, response.c_str());
        lua_setfield(L, -2, "Body");
        lua_pushboolean(L, true);
        lua_setfield(L, -2, "Success");
        lua_pushinteger(L, 200);
        lua_setfield(L, -2, "StatusCode");
        
        return 1;
    }

    int L_HttpService_GetAsync(lua_State* L) {
        return L_http_request(L);
    }

    int L_HttpService_JSONEncode(lua_State* L) {
        // Simple table to JSON - basic implementation
        if (!lua_istable(L, 1)) {
            lua_pushstring(L, "null");
            return 1;
        }
        
        std::string result = "{";
        bool first = true;
        
        lua_pushnil(L);
        while (lua_next(L, 1)) {
            if (!first) result += ",";
            first = false;
            
            result += "\"";
            if (lua_isstring(L, -2)) result += lua_tostring(L, -2);
            result += "\":";
            
            if (lua_isstring(L, -1)) {
                result += "\"" + std::string(lua_tostring(L, -1)) + "\"";
            } else if (lua_isnumber(L, -1)) {
                char num[64];
                snprintf(num, sizeof(num), "%g", lua_tonumber(L, -1));
                result += num;
            } else if (lua_isboolean(L, -1)) {
                result += lua_toboolean(L, -1) ? "true" : "false";
            } else {
                result += "null";
            }
            
            lua_pop(L, 1);
        }
        
        result += "}";
        lua_pushstring(L, result.c_str());
        return 1;
    }

    int L_HttpService_JSONDecode(lua_State* L) {
        const char* json = luaL_checkstring(L, 1);
        
        // Very basic JSON parsing - just create empty table for stub
        lua_newtable(L);
        return 1;
    }

    // ========================================================================
    // MISC FUNCTIONS
    // ========================================================================
    int L_setfflag(lua_State* L) {
        const char* flag = luaL_checkstring(L, 1);
        const char* value = luaL_checkstring(L, 2);
        vm_globals::fflags[flag] = value;
        return 0;
    }

    int L_getfflag(lua_State* L) {
        const char* flag = luaL_checkstring(L, 1);
        auto it = vm_globals::fflags.find(flag);
        if (it != vm_globals::fflags.end()) {
            lua_pushstring(L, it->second.c_str());
        } else {
            lua_pushnil(L);
        }
        return 1;
    }
}
