#pragma once

#include "imgui.h"
#include "imgui_internal.h"

namespace menu::ui
{
    void draw_header_background(ImDrawList* dl, const ImRect& bounds, float corner_r);
}
