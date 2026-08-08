#pragma once

#include "imgui.h"
#include "imgui_internal.h"

#include "ui/content/settings_state.h"

namespace menu::ui
{
    void draw_gradient_checkbox(ImDrawList* dl, const ImRect& bounds, bool checked, bool& value);
    void draw_keybind_field(ImDrawList* dl, const ImRect& bounds, SettingsState& state);
    bool draw_action_button(ImDrawList* dl, const ImRect& bounds, float round, const char* label, float font_size);
    void draw_unload_button(ImDrawList* dl, const ImRect& bounds);
    void draw_rounded_slider(ImDrawList* dl, const ImRect& bounds, float& value, float min_val, float max_val, const char* label = nullptr);
}
