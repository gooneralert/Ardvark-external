#pragma once

#include <windows.h>
#include "imgui_internal.h"

namespace menu::ui
{
    struct HeaderState;

    void handle_window_drag(HWND hwnd, const ImRect& drag_rect, bool block_drag = false);
    void handle_window_drag(HWND hwnd, const ImRect& drag_rect, const HeaderState& header);
}
