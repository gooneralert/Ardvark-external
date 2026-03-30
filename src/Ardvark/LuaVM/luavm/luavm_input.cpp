// ============================================================================
// LuaVM Input - Input simulation functions
// ============================================================================

#include "luavm_common.h"

namespace util {
    static void safe_mouse_event(DWORD flags, int dx = 0, int dy = 0, int data = 0) {
        INPUT input = { 0 }; 
        input.type = INPUT_MOUSE; 
        input.mi.dwFlags = flags;
        input.mi.dx = dx; 
        input.mi.dy = dy; 
        input.mi.mouseData = data;
        SendInput(1, &input, sizeof(INPUT));
    }

    static void safe_key_event(WORD key, bool up) {
        INPUT input = { 0 }; 
        input.type = INPUT_KEYBOARD; 
        input.ki.wVk = key;
        input.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
        SendInput(1, &input, sizeof(INPUT));
    }

    int L_mouse1click(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_LEFTDOWN); 
        safe_mouse_event(MOUSEEVENTF_LEFTUP); 
        return 0; 
    }

    int L_mouse1press(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_LEFTDOWN); 
        return 0; 
    }

    int L_mouse1release(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_LEFTUP); 
        return 0; 
    }

    int L_mouse2click(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_RIGHTDOWN); 
        safe_mouse_event(MOUSEEVENTF_RIGHTUP); 
        return 0; 
    }

    int L_mouse2press(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_RIGHTDOWN); 
        return 0; 
    }

    int L_mouse2release(lua_State* L) { 
        safe_mouse_event(MOUSEEVENTF_RIGHTUP); 
        return 0; 
    }
    
    int L_mousemoveabs(lua_State* L) {
        int x = luaL_checkinteger(L, 1);
        int y = luaL_checkinteger(L, 2);
        int w = GetSystemMetrics(SM_CXSCREEN);
        int h = GetSystemMetrics(SM_CYSCREEN);
        safe_mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, (x * 65535) / w, (y * 65535) / h);
        return 0;
    }

    int L_mousemoverel(lua_State* L) {
        int x = luaL_checkinteger(L, 1);
        int y = luaL_checkinteger(L, 2);
        safe_mouse_event(MOUSEEVENTF_MOVE, x, y);
        return 0;
    }

    int L_mousescroll(lua_State* L) {
        int amount = luaL_checkinteger(L, 1);
        safe_mouse_event(MOUSEEVENTF_WHEEL, 0, 0, amount);
        return 0;
    }

    int L_keypress(lua_State* L) { 
        safe_key_event((WORD)luaL_checkinteger(L, 1), false); 
        return 0; 
    }

    int L_keyrelease(lua_State* L) { 
        safe_key_event((WORD)luaL_checkinteger(L, 1), true); 
        return 0; 
    }

    int L_iskeypressed(lua_State* L) {
        lua_pushboolean(L, (GetAsyncKeyState(luaL_checkinteger(L, 1)) & 0x8000) != 0);
        return 1;
    }

    int L_ismouse1pressed(lua_State* L) {
        lua_pushboolean(L, (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0);
        return 1;
    }

    int L_ismouse2pressed(lua_State* L) {
        lua_pushboolean(L, (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0);
        return 1;
    }

    int L_isrbxactive(lua_State* L) {
        HWND roblox = FindWindowA(nullptr, "Roblox");
        HWND fg = GetForegroundWindow();
        lua_pushboolean(L, roblox && fg && (fg == roblox));
        return 1;
    }

    int L_setrobloxinput(lua_State* L) {
        // Stub: we don't globally toggle input routing
        luaL_checktype(L, 1, LUA_TBOOLEAN);
        return 0;
    }

    int L_setclipboard(lua_State* L) {
        const char* text = luaL_checkstring(L, 1);
        
        if (!OpenClipboard(nullptr)) {
            return 0;
        }
        
        EmptyClipboard();
        
        size_t len = strlen(text) + 1;
        HGLOBAL hMem = GlobalAlloc(GMEM_MOVEABLE, len);
        if (hMem) {
            char* pMem = (char*)GlobalLock(hMem);
            if (pMem) {
                memcpy(pMem, text, len);
                GlobalUnlock(hMem);
                SetClipboardData(CF_TEXT, hMem);
            }
        }
        
        CloseClipboard();
        return 0;
    }
}
