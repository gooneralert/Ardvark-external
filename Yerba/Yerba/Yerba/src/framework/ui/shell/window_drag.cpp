#include "ui/shell/window_drag.h"

#include "ui/header/header_bar.h"

#include "imgui.h"

#include <unordered_map>
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
namespace menu::ui
{
    namespace
    {
        struct drag_state
        {
            bool active = false;
            POINT mouse_start = {};
            POINT win_start = {};
        };

        drag_state& drag_for(HWND hwnd)
        {
            static std::unordered_map<HWND, drag_state> states;
            return states[hwnd];
        }
    }

    void handle_window_drag(HWND hwnd, const ImRect& drag_rect, bool block_drag)
    {
        drag_state& s_drag = drag_for(hwnd);

        if (block_drag)
        {
            if (!ImGui::IsMouseDown(ImGuiMouseButton_Left))
                s_drag.active = false;
            return;
        }

        const ImVec2 mouse = ImGui::GetIO().MousePos;
        const bool in_drag_rect = drag_rect.Contains(mouse);

        if (ImGui::IsMouseClicked(ImGuiMouseButton_Left) && in_drag_rect)
        {
            s_drag.active = true;
            ::GetCursorPos(&s_drag.mouse_start);
            RECT rc = {};
            ::GetWindowRect(hwnd, &rc);
            s_drag.win_start.x = rc.left;
            s_drag.win_start.y = rc.top;
        }

        if (!ImGui::IsMouseDown(ImGuiMouseButton_Left))
            s_drag.active = false;

        if (!s_drag.active)
            return;

        POINT cur = {};
        ::GetCursorPos(&cur);
        ::SetWindowPos(hwnd, nullptr,
            s_drag.win_start.x + (cur.x - s_drag.mouse_start.x),
            s_drag.win_start.y + (cur.y - s_drag.mouse_start.y),
            0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    void handle_window_drag(HWND hwnd, const ImRect& drag_rect, const HeaderState& header)
    {
        handle_window_drag(hwnd, drag_rect, header.blocks_window_drag);
    }
}
