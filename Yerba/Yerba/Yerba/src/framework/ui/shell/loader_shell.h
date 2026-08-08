#pragma once

#include "imgui.h"
#include <windows.h>

namespace menu::loader
{
    void draw(ImDrawList* dl, HWND hwnd, const ImVec2& origin);
    void render(HWND hwnd);
    void update(HWND hwnd, float dt);
    bool consume_close_request();
    ImVec2 animated_client_size();
}
