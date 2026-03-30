// ============================================================================
// LuaVM Memory - Memory read/write functions
// ============================================================================

#include "luavm_common.h"

namespace util {
    int L_getbase(lua_State* L) {
        lua_pushnumber(L, (double)base_address);
        return 1;
    }

    int L_memory_read(lua_State* L) {
        const char* type = luaL_checkstring(L, 1);
        uintptr_t address = (uintptr_t)luaL_checknumber(L, 2);
        
        if (!is_valid_address(address)) {
            lua_pushnil(L);
            return 1;
        }
        
        if (strcmp(type, "int") == 0 || strcmp(type, "i32") == 0) {
            lua_pushinteger(L, read<int>(address));
        } else if (strcmp(type, "float") == 0 || strcmp(type, "f32") == 0) {
            lua_pushnumber(L, read<float>(address));
        } else if (strcmp(type, "double") == 0 || strcmp(type, "f64") == 0) {
            lua_pushnumber(L, read<double>(address));
        } else if (strcmp(type, "byte") == 0 || strcmp(type, "i8") == 0) {
            lua_pushinteger(L, read<uint8_t>(address));
        } else if (strcmp(type, "short") == 0 || strcmp(type, "i16") == 0) {
            lua_pushinteger(L, read<int16_t>(address));
        } else if (strcmp(type, "long") == 0 || strcmp(type, "i64") == 0) {
            lua_pushnumber(L, (double)read<int64_t>(address));
        } else if (strcmp(type, "pointer") == 0 || strcmp(type, "ptr") == 0) {
            lua_pushnumber(L, (double)read<uintptr_t>(address));
        } else if (strcmp(type, "bool") == 0) {
            lua_pushboolean(L, read<bool>(address));
        } else if (strcmp(type, "string") == 0) {
            // Read null-terminated string (limited)
            char buffer[256] = {};
            for (int i = 0; i < 255; ++i) {
                char c = read<char>(address + i);
                if (c == 0) break;
                buffer[i] = c;
            }
            lua_pushstring(L, buffer);
        } else if (strcmp(type, "Vector3") == 0) {
            float x = read<float>(address);
            float y = read<float>(address + 4);
            float z = read<float>(address + 8);
            push_vector3_userdata(L, x, y, z);
        } else if (strcmp(type, "Vector2") == 0) {
            float x = read<float>(address);
            float y = read<float>(address + 4);
            push_vector2_userdata(L, x, y);
        } else {
            lua_pushnil(L);
        }
        return 1;
    }

    int L_memory_write(lua_State* L) {
        const char* type = luaL_checkstring(L, 1);
        uintptr_t address = (uintptr_t)luaL_checknumber(L, 2);
        
        if (!is_valid_address(address)) {
            return 0;
        }
        
        if (strcmp(type, "int") == 0 || strcmp(type, "i32") == 0) {
            write<int>(address, (int)luaL_checkinteger(L, 3));
        } else if (strcmp(type, "float") == 0 || strcmp(type, "f32") == 0) {
            write<float>(address, (float)luaL_checknumber(L, 3));
        } else if (strcmp(type, "double") == 0 || strcmp(type, "f64") == 0) {
            write<double>(address, luaL_checknumber(L, 3));
        } else if (strcmp(type, "byte") == 0 || strcmp(type, "i8") == 0) {
            write<uint8_t>(address, (uint8_t)luaL_checkinteger(L, 3));
        } else if (strcmp(type, "short") == 0 || strcmp(type, "i16") == 0) {
            write<int16_t>(address, (int16_t)luaL_checkinteger(L, 3));
        } else if (strcmp(type, "long") == 0 || strcmp(type, "i64") == 0) {
            write<int64_t>(address, (int64_t)luaL_checknumber(L, 3));
        } else if (strcmp(type, "pointer") == 0 || strcmp(type, "ptr") == 0) {
            write<uintptr_t>(address, (uintptr_t)luaL_checknumber(L, 3));
        } else if (strcmp(type, "bool") == 0) {
            write<bool>(address, lua_toboolean(L, 3));
        } else if (strcmp(type, "Vector3") == 0) {
            Vector3* v = try_get_vector3_userdata(L, 3);
            if (v) {
                write<float>(address, v->x);
                write<float>(address + 4, v->y);
                write<float>(address + 8, v->z);
            }
        } else if (strcmp(type, "Vector2") == 0) {
            Vector2* v = try_get_vector2_userdata(L, 3);
            if (v) {
                write<float>(address, v->x);
                write<float>(address + 4, v->y);
            }
        }
        return 0;
    }
}
