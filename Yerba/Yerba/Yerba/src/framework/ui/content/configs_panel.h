#pragma once

#include "imgui.h"
#include "imgui_internal.h"

#include "ui/content/configs_state.h"

namespace menu::ui
{
    void draw_configs_panel(ImDrawList* dl, const ImRect& inner, ConfigsState& state);
}
