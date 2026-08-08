#pragma once

#include "imgui.h"
#include "imgui_internal.h"

namespace menu::ui
{
    struct ColorState
    {
        float color[4] = { 1.0f, 1.0f, 1.0f, 1.0f }; // RGBA
        bool picker_open = false;
        int active_picker = -1; // Which color picker is open
    };

    // Draw a color picker button that opens a popup
    bool draw_color_picker(ImDrawList* dl, const ImRect& bounds, float color[4], bool& picker_open, bool& rainbow_mode, int picker_id, const char* label = "Color");
}
