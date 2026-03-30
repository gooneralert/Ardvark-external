// ============================================================================
// LuaVM Bridge - Roblox datatype implementations
// ============================================================================

#include "luavm_common.h"
#include <new>

namespace util {
    // ========================================================================
    // PUSH HELPERS
    // ========================================================================
    void push_vector3_userdata(lua_State* L, float x, float y, float z) {
        Vector3* v = (Vector3*)lua_newuserdata(L, sizeof(Vector3));
        new (v) Vector3{ x, y, z };
        luaL_getmetatable(L, "Vector3");
        lua_setmetatable(L, -2);
    }

    void push_vector2_userdata(lua_State* L, float x, float y) {
        Vector2* v = (Vector2*)lua_newuserdata(L, sizeof(Vector2));
        new (v) Vector2{ x, y };
        luaL_getmetatable(L, "Vector2");
        lua_setmetatable(L, -2);
    }

    void push_color3_userdata(lua_State* L, float r, float g, float b) {
        LuaColor3* c = (LuaColor3*)lua_newuserdata(L, sizeof(LuaColor3));
        c->r = r; c->g = g; c->b = b;
        luaL_getmetatable(L, "Color3");
        lua_setmetatable(L, -2);
    }

    void push_udim2_userdata(lua_State* L, const LuaUDim2& v) {
        LuaUDim2* ud = (LuaUDim2*)lua_newuserdata(L, sizeof(LuaUDim2));
        *ud = v;
        luaL_getmetatable(L, "UDim2");
        lua_setmetatable(L, -2);
    }

    void push_udim_userdata(lua_State* L, const LuaUDim& v) {
        LuaUDim* ud = (LuaUDim*)lua_newuserdata(L, sizeof(LuaUDim));
        *ud = v;
        luaL_getmetatable(L, "UDim");
        lua_setmetatable(L, -2);
    }

    void push_cframe_userdata(lua_State* L, const LuaCFrame& cf) {
        LuaCFrame* c = (LuaCFrame*)lua_newuserdata(L, sizeof(LuaCFrame));
        *c = cf;
        luaL_getmetatable(L, "CFrame");
        lua_setmetatable(L, -2);
    }

    // ========================================================================
    // TRY GET HELPERS
    // ========================================================================
    Vector3* try_get_vector3_userdata(lua_State* L, int idx) {
        if (!lua_isuserdata(L, idx)) return nullptr;
        if (!lua_getmetatable(L, idx)) return nullptr;
        luaL_getmetatable(L, "Vector3");
        bool match = lua_rawequal(L, -1, -2);
        lua_pop(L, 2);
        return match ? (Vector3*)lua_touserdata(L, idx) : nullptr;
    }

    Vector2* try_get_vector2_userdata(lua_State* L, int idx) {
        if (!lua_isuserdata(L, idx)) return nullptr;
        if (!lua_getmetatable(L, idx)) return nullptr;
        luaL_getmetatable(L, "Vector2");
        bool match = lua_rawequal(L, -1, -2);
        lua_pop(L, 2);
        return match ? (Vector2*)lua_touserdata(L, idx) : nullptr;
    }

    LuaCFrame* try_get_cframe_userdata(lua_State* L, int idx) {
        if (!lua_isuserdata(L, idx)) return nullptr;
        if (!lua_getmetatable(L, idx)) return nullptr;
        luaL_getmetatable(L, "CFrame");
        bool match = lua_rawequal(L, -1, -2);
        lua_pop(L, 2);
        return match ? (LuaCFrame*)lua_touserdata(L, idx) : nullptr;
    }

    LuaUDim2* try_get_udim2_userdata(lua_State* L, int idx) {
        if (!lua_isuserdata(L, idx)) return nullptr;
        if (!lua_getmetatable(L, idx)) return nullptr;
        luaL_getmetatable(L, "UDim2");
        bool match = lua_rawequal(L, -1, -2);
        lua_pop(L, 2);
        return match ? (LuaUDim2*)lua_touserdata(L, idx) : nullptr;
    }

    LuaColor3* try_get_color3_userdata(lua_State* L, int idx) {
        if (!lua_isuserdata(L, idx)) return nullptr;
        if (!lua_getmetatable(L, idx)) return nullptr;
        luaL_getmetatable(L, "Color3");
        bool match = lua_rawequal(L, -1, -2);
        lua_pop(L, 2);
        return match ? (LuaColor3*)lua_touserdata(L, idx) : nullptr;
    }

    // ========================================================================
    // VECTOR3 IMPLEMENTATION
    // ========================================================================
    static int L_Vector3_add(lua_State* L) {
        Vector3* a = try_get_vector3_userdata(L, 1);
        Vector3* b = try_get_vector3_userdata(L, 2);
        if (a && b) push_vector3_userdata(L, a->x + b->x, a->y + b->y, a->z + b->z);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector3_sub(lua_State* L) {
        Vector3* a = try_get_vector3_userdata(L, 1);
        Vector3* b = try_get_vector3_userdata(L, 2);
        if (a && b) push_vector3_userdata(L, a->x - b->x, a->y - b->y, a->z - b->z);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector3_mul(lua_State* L) {
        Vector3* a = try_get_vector3_userdata(L, 1);
        if (a && lua_isnumber(L, 2)) {
            float s = (float)lua_tonumber(L, 2);
            push_vector3_userdata(L, a->x * s, a->y * s, a->z * s);
        } else if (lua_isnumber(L, 1) && try_get_vector3_userdata(L, 2)) {
            float s = (float)lua_tonumber(L, 1);
            Vector3* v = try_get_vector3_userdata(L, 2);
            push_vector3_userdata(L, v->x * s, v->y * s, v->z * s);
        } else if (a && try_get_vector3_userdata(L, 2)) {
            Vector3* b = try_get_vector3_userdata(L, 2);
            push_vector3_userdata(L, a->x * b->x, a->y * b->y, a->z * b->z);
        } else {
            lua_pushnil(L);
        }
        return 1;
    }

    static int L_Vector3_div(lua_State* L) {
        Vector3* a = try_get_vector3_userdata(L, 1);
        float s = (float)lua_tonumber(L, 2);
        if (a && s != 0) push_vector3_userdata(L, a->x / s, a->y / s, a->z / s);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector3_index_impl(lua_State* L) {
        Vector3* v = try_get_vector3_userdata(L, 1);
        if (!v) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "X") == 0 || strcmp(key, "x") == 0) lua_pushnumber(L, v->x);
        else if (strcmp(key, "Y") == 0 || strcmp(key, "y") == 0) lua_pushnumber(L, v->y);
        else if (strcmp(key, "Z") == 0 || strcmp(key, "z") == 0) lua_pushnumber(L, v->z);
        else if (strcmp(key, "Magnitude") == 0) lua_pushnumber(L, sqrt(v->x*v->x + v->y*v->y + v->z*v->z));
        else if (strcmp(key, "Unit") == 0) {
            float mag = sqrt(v->x*v->x + v->y*v->y + v->z*v->z);
            if (mag > 0) push_vector3_userdata(L, v->x/mag, v->y/mag, v->z/mag);
            else push_vector3_userdata(L, 0, 0, 0);
        }
        else lua_pushnil(L);
        return 1;
    }

    void push_vector3_metatable(lua_State* L) {
        if (luaL_newmetatable(L, "Vector3")) {
            lua_pushcfunction(L, L_Vector3_index_impl, "__index"); lua_setfield(L, -2, "__index");
            lua_pushcfunction(L, L_Vector3_add, "__add"); lua_setfield(L, -2, "__add");
            lua_pushcfunction(L, L_Vector3_sub, "__sub"); lua_setfield(L, -2, "__sub");
            lua_pushcfunction(L, L_Vector3_mul, "__mul"); lua_setfield(L, -2, "__mul");
            lua_pushcfunction(L, L_Vector3_div, "__div"); lua_setfield(L, -2, "__div");
            lua_pushstring(L, "Vector3"); lua_setfield(L, -2, "__type");
        }
    }

    int L_Vector3_new(lua_State* L) {
        float x = (float)luaL_optnumber(L, 1, 0);
        float y = (float)luaL_optnumber(L, 2, 0);
        float z = (float)luaL_optnumber(L, 3, 0);
        push_vector3_userdata(L, x, y, z);
        return 1;
    }

    // ========================================================================
    // VECTOR2 IMPLEMENTATION
    // ========================================================================
    static int L_Vector2_add(lua_State* L) {
        Vector2* a = try_get_vector2_userdata(L, 1);
        Vector2* b = try_get_vector2_userdata(L, 2);
        if (a && b) push_vector2_userdata(L, a->x + b->x, a->y + b->y);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector2_sub(lua_State* L) {
        Vector2* a = try_get_vector2_userdata(L, 1);
        Vector2* b = try_get_vector2_userdata(L, 2);
        if (a && b) push_vector2_userdata(L, a->x - b->x, a->y - b->y);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector2_mul(lua_State* L) {
        Vector2* a = try_get_vector2_userdata(L, 1);
        if (a && lua_isnumber(L, 2)) {
            float s = (float)lua_tonumber(L, 2);
            push_vector2_userdata(L, a->x * s, a->y * s);
        } else lua_pushnil(L);
        return 1;
    }

    static int L_Vector2_div(lua_State* L) {
        Vector2* a = try_get_vector2_userdata(L, 1);
        float s = (float)lua_tonumber(L, 2);
        if (a && s != 0) push_vector2_userdata(L, a->x / s, a->y / s);
        else lua_pushnil(L);
        return 1;
    }

    static int L_Vector2_index_impl(lua_State* L) {
        Vector2* v = try_get_vector2_userdata(L, 1);
        if (!v) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "X") == 0 || strcmp(key, "x") == 0) lua_pushnumber(L, v->x);
        else if (strcmp(key, "Y") == 0 || strcmp(key, "y") == 0) lua_pushnumber(L, v->y);
        else if (strcmp(key, "Magnitude") == 0) lua_pushnumber(L, sqrt(v->x*v->x + v->y*v->y));
        else lua_pushnil(L);
        return 1;
    }

    void push_vector2_metatable(lua_State* L) {
        if (luaL_newmetatable(L, "Vector2")) {
            lua_pushcfunction(L, L_Vector2_index_impl, "__index"); lua_setfield(L, -2, "__index");
            lua_pushcfunction(L, L_Vector2_add, "__add"); lua_setfield(L, -2, "__add");
            lua_pushcfunction(L, L_Vector2_sub, "__sub"); lua_setfield(L, -2, "__sub");
            lua_pushcfunction(L, L_Vector2_mul, "__mul"); lua_setfield(L, -2, "__mul");
            lua_pushcfunction(L, L_Vector2_div, "__div"); lua_setfield(L, -2, "__div");
            lua_pushstring(L, "Vector2"); lua_setfield(L, -2, "__type");
        }
    }

    int L_Vector2_new(lua_State* L) {
        float x = (float)luaL_optnumber(L, 1, 0);
        float y = (float)luaL_optnumber(L, 2, 0);
        push_vector2_userdata(L, x, y);
        return 1;
    }

    // ========================================================================
    // COLOR3 IMPLEMENTATION
    // ========================================================================
    int L_Color3_new(lua_State* L) {
        float r = (float)luaL_optnumber(L, 1, 0);
        float g = (float)luaL_optnumber(L, 2, 0);
        float b = (float)luaL_optnumber(L, 3, 0);
        push_color3_userdata(L, r, g, b);
        return 1;
    }

    int L_Color3_fromRGB(lua_State* L) {
        float r = (float)luaL_optnumber(L, 1, 0) / 255.0f;
        float g = (float)luaL_optnumber(L, 2, 0) / 255.0f;
        float b = (float)luaL_optnumber(L, 3, 0) / 255.0f;
        push_color3_userdata(L, r, g, b);
        return 1;
    }

    int L_Color3_fromHSV(lua_State* L) {
        float h = (float)luaL_optnumber(L, 1, 0);
        float s = (float)luaL_optnumber(L, 2, 0);
        float v = (float)luaL_optnumber(L, 3, 0);
        
        float c = v * s;
        float x = c * (1 - fabs(fmod(h * 6, 2) - 1));
        float m = v - c;
        float r, g, b;
        
        if (h < 1.0f/6) { r = c; g = x; b = 0; }
        else if (h < 2.0f/6) { r = x; g = c; b = 0; }
        else if (h < 3.0f/6) { r = 0; g = c; b = x; }
        else if (h < 4.0f/6) { r = 0; g = x; b = c; }
        else if (h < 5.0f/6) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        
        push_color3_userdata(L, r + m, g + m, b + m);
        return 1;
    }

    int L_Color3_fromHex(lua_State* L) {
        const char* hex = luaL_checkstring(L, 1);
        if (hex[0] == '#') hex++;
        
        unsigned int val = 0;
        sscanf(hex, "%x", &val);
        
        float r = ((val >> 16) & 0xFF) / 255.0f;
        float g = ((val >> 8) & 0xFF) / 255.0f;
        float b = (val & 0xFF) / 255.0f;
        
        push_color3_userdata(L, r, g, b);
        return 1;
    }

    int L_Color3_index(lua_State* L) {
        LuaColor3* c = try_get_color3_userdata(L, 1);
        if (!c) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "R") == 0 || strcmp(key, "r") == 0) lua_pushnumber(L, c->r);
        else if (strcmp(key, "G") == 0 || strcmp(key, "g") == 0) lua_pushnumber(L, c->g);
        else if (strcmp(key, "B") == 0 || strcmp(key, "b") == 0) lua_pushnumber(L, c->b);
        else lua_pushnil(L);
        return 1;
    }

    // ========================================================================
    // UDIM / UDIM2 IMPLEMENTATION
    // ========================================================================
    int L_UDim_new(lua_State* L) {
        LuaUDim ud;
        ud.scale = (float)luaL_optnumber(L, 1, 0);
        ud.offset = (int)luaL_optinteger(L, 2, 0);
        push_udim_userdata(L, ud);
        return 1;
    }

    int L_UDim_index(lua_State* L) {
        LuaUDim* ud = (LuaUDim*)lua_touserdata(L, 1);
        if (!ud) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "Scale") == 0) lua_pushnumber(L, ud->scale);
        else if (strcmp(key, "Offset") == 0) lua_pushinteger(L, ud->offset);
        else lua_pushnil(L);
        return 1;
    }

    int L_UDim2_new(lua_State* L) {
        LuaUDim2 ud;
        if (lua_gettop(L) >= 4) {
            ud.x.scale = (float)luaL_optnumber(L, 1, 0);
            ud.x.offset = (int)luaL_optinteger(L, 2, 0);
            ud.y.scale = (float)luaL_optnumber(L, 3, 0);
            ud.y.offset = (int)luaL_optinteger(L, 4, 0);
        } else {
            ud.x.scale = 0; ud.x.offset = 0;
            ud.y.scale = 0; ud.y.offset = 0;
        }
        push_udim2_userdata(L, ud);
        return 1;
    }

    int L_UDim2_fromScale(lua_State* L) {
        LuaUDim2 ud;
        ud.x.scale = (float)luaL_optnumber(L, 1, 0);
        ud.x.offset = 0;
        ud.y.scale = (float)luaL_optnumber(L, 2, 0);
        ud.y.offset = 0;
        push_udim2_userdata(L, ud);
        return 1;
    }

    int L_UDim2_fromOffset(lua_State* L) {
        LuaUDim2 ud;
        ud.x.scale = 0;
        ud.x.offset = (int)luaL_optinteger(L, 1, 0);
        ud.y.scale = 0;
        ud.y.offset = (int)luaL_optinteger(L, 2, 0);
        push_udim2_userdata(L, ud);
        return 1;
    }

    int L_UDim2_index(lua_State* L) {
        LuaUDim2* ud = try_get_udim2_userdata(L, 1);
        if (!ud) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "X") == 0) push_udim_userdata(L, ud->x);
        else if (strcmp(key, "Y") == 0) push_udim_userdata(L, ud->y);
        else lua_pushnil(L);
        return 1;
    }

    // ========================================================================
    // CFRAME IMPLEMENTATION
    // ========================================================================
    int L_CFrame_new(lua_State* L) {
        LuaCFrame cf = {};
        int nargs = lua_gettop(L);
        
        if (nargs >= 3) {
            cf.m[0] = (float)lua_tonumber(L, 1); // X
            cf.m[1] = (float)lua_tonumber(L, 2); // Y
            cf.m[2] = (float)lua_tonumber(L, 3); // Z
            
            // Default to identity rotation
            cf.m[3] = 1; cf.m[4] = 0; cf.m[5] = 0;  // Right
            cf.m[6] = 0; cf.m[7] = 1; cf.m[8] = 0;  // Up
            cf.m[9] = 0; cf.m[10] = 0; cf.m[11] = 1; // Look
        }
        
        push_cframe_userdata(L, cf);
        return 1;
    }

    int L_CFrame_lookAt(lua_State* L) {
        Vector3* at = try_get_vector3_userdata(L, 1);
        Vector3* lookAt = try_get_vector3_userdata(L, 2);
        
        if (!at || !lookAt) {
            LuaCFrame cf = {};
            push_cframe_userdata(L, cf);
            return 1;
        }
        
        LuaCFrame cf = {};
        cf.m[0] = at->x;
        cf.m[1] = at->y;
        cf.m[2] = at->z;
        
        // Calculate look direction
        float dx = lookAt->x - at->x;
        float dy = lookAt->y - at->y;
        float dz = lookAt->z - at->z;
        float mag = sqrt(dx*dx + dy*dy + dz*dz);
        
        if (mag > 0) {
            cf.m[9] = dx/mag;
            cf.m[10] = dy/mag;
            cf.m[11] = dz/mag;
        } else {
            cf.m[9] = 0; cf.m[10] = 0; cf.m[11] = -1;
        }
        
        // Default up and right
        cf.m[3] = 1; cf.m[4] = 0; cf.m[5] = 0;
        cf.m[6] = 0; cf.m[7] = 1; cf.m[8] = 0;
        
        push_cframe_userdata(L, cf);
        return 1;
    }

    int L_CFrame_index(lua_State* L) {
        LuaCFrame* cf = try_get_cframe_userdata(L, 1);
        if (!cf) return 0;
        const char* key = lua_tostring(L, 2);
        if (!key) return 0;
        
        if (strcmp(key, "Position") == 0 || strcmp(key, "p") == 0) {
            push_vector3_userdata(L, cf->m[0], cf->m[1], cf->m[2]);
        }
        else if (strcmp(key, "X") == 0) lua_pushnumber(L, cf->m[0]);
        else if (strcmp(key, "Y") == 0) lua_pushnumber(L, cf->m[1]);
        else if (strcmp(key, "Z") == 0) lua_pushnumber(L, cf->m[2]);
        else if (strcmp(key, "LookVector") == 0) {
            push_vector3_userdata(L, cf->m[9], cf->m[10], cf->m[11]);
        }
        else if (strcmp(key, "RightVector") == 0) {
            push_vector3_userdata(L, cf->m[3], cf->m[4], cf->m[5]);
        }
        else if (strcmp(key, "UpVector") == 0) {
            push_vector3_userdata(L, cf->m[6], cf->m[7], cf->m[8]);
        }
        else lua_pushnil(L);
        return 1;
    }

    // ========================================================================
    // ENUM IMPLEMENTATION
    // ========================================================================
    int L_Enum_index(lua_State* L) {
        // Stub enum implementation
        const char* enumName = lua_tostring(L, 2);
        if (!enumName) return 0;
        
        lua_newtable(L);
        lua_pushstring(L, enumName);
        lua_setfield(L, -2, "Name");
        return 1;
    }
}
