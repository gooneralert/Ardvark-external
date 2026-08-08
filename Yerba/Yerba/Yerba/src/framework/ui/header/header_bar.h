#pragma once

#include "imgui.h"
#include "imgui_internal.h"

namespace menu::ui
{
    enum class NavTab
    {
        Aimbot,
        Visuals,
        Misc,
        Settings,
        Scripts,
        Count
    };

    struct HeaderState
    {
        NavTab active = NavTab::Settings;
        NavTab prev_active = NavTab::Settings;
        float tab_transition_t = 0.f;
        bool blocks_window_drag = false;
        char search[64] = {};
    };

    void draw_header_bar(ImDrawList* dl, const ImRect& bounds, HeaderState& state);
}
