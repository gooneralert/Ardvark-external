#pragma once

#include "imgui.h"
#include "imgui_internal.h"

namespace menu::ui
{
    void draw_dot_grid(ImDrawList* dl, const ImRect& bounds);
}
