#pragma once

#include "imgui.h"
#include "imgui_internal.h"

namespace menu::colors
{
    inline constexpr ImU32 shell_bg      = IM_COL32(11, 11, 13, 255);
    inline constexpr ImU32 header_bg_top     = IM_COL32(32, 32, 36, 255);
    inline constexpr ImU32 header_bg_bottom  = IM_COL32(17, 17, 20, 255);
    inline constexpr ImU32 body_bg       = IM_COL32(0x0A, 0x0A, 0x0A, 255);
    inline constexpr ImU32 body_dot      = IM_COL32(48, 48, 54, 255);
    inline constexpr ImU32 border_black  = IM_COL32(0, 0, 0, 255);
    inline constexpr ImU32 divider       = IM_COL32(42, 42, 50, 255);

    inline constexpr ImU32 text_active   = IM_COL32(255, 255, 255, 255);
    inline constexpr ImU32 text_idle     = IM_COL32(118, 118, 128, 255);
    inline constexpr ImU32 search_bg     = IM_COL32(17, 17, 17, 255);
    inline constexpr ImU32 search_icon     = IM_COL32(96, 96, 108, 255);
    inline constexpr ImU32 search_hint    = IM_COL32(118, 118, 128, 255);
    inline constexpr ImU32 search_border = IM_COL32(0x52, 0x52, 0x58, 140);

    inline constexpr ImU32 panel_inner_bg  = IM_COL32(17, 17, 17, 255);
    inline constexpr ImU32 panel_title       = IM_COL32(255, 255, 255, 255);

    inline constexpr ImU32 toggle_on_top     = IM_COL32(0x31, 0x71, 0x9B, 255);
    inline constexpr ImU32 toggle_on_bottom  = IM_COL32(0x1E, 0x52, 0x72, 255);
    inline constexpr ImU32 toggle_outline    = IM_COL32(0x52, 0x52, 0x58, 140);

    inline constexpr ImU32 keybind_bg        = IM_COL32(0x1E, 0x1E, 0x22, 255);
    inline constexpr ImU32 keybind_bg_active = IM_COL32(0x24, 0x28, 0x30, 255);
    inline constexpr ImU32 keybind_outline   = IM_COL32(0x52, 0x52, 0x58, 110);
    inline constexpr ImU32 keybind_outline_active = IM_COL32(0x58, 0x78, 0x92, 150);
    inline constexpr ImU32 keybind_divider   = IM_COL32(0x42, 0x42, 0x48, 255);
    inline constexpr ImU32 keybind_icon      = IM_COL32(0xA8, 0xA8, 0xB0, 255);
    inline constexpr ImU32 keybind_waiting_text = IM_COL32(0xC8, 0xD8, 0xE8, 255);

    inline constexpr ImU32 panel_header_line  = IM_COL32(0x31, 0x71, 0x9B, 255);

    inline constexpr ImU32 action_btn_top       = IM_COL32(0x1C, 0x1C, 0x1C, 255);
    inline constexpr ImU32 action_btn_bottom    = IM_COL32(0x14, 0x14, 0x14, 255);
    inline constexpr ImU32 action_btn_highlight = IM_COL32(0x24, 0x24, 0x24, 255);
    inline constexpr ImU32 action_btn_hover_top = IM_COL32(0x3A, 0x3A, 0x3A, 255);
    inline constexpr ImU32 action_btn_hover_bottom = IM_COL32(0x2A, 0x2A, 0x2A, 255);
    inline constexpr ImU32 action_btn_hover_outline = IM_COL32(0x4A, 0x4A, 0x50, 255);

    inline constexpr ImU32 config_list_bg       = IM_COL32(0x00, 0x00, 0x00, 255);
    inline constexpr ImU32 config_list_border   = IM_COL32(0x3A, 0x3A, 0x3E, 255);
    inline constexpr ImU32 config_input_bg      = IM_COL32(0x11, 0x11, 0x11, 255);
    inline constexpr ImU32 config_input_border  = IM_COL32(0x52, 0x52, 0x58, 110);
    inline constexpr ImU32 config_list_sel_bg   = IM_COL32(0x1E, 0x1E, 0x22, 255);

    inline constexpr ImU32 unload_top        = action_btn_top;
    inline constexpr ImU32 unload_bottom     = action_btn_bottom;

    inline constexpr ImU32 ice_blue      = IM_COL32(118, 208, 242, 255);

    inline constexpr ImU32 sep_grad_0    = IM_COL32(0x13, 0x1A, 0x1E, 255);
    inline constexpr ImU32 sep_grad_1    = IM_COL32(0x23, 0x31, 0x39, 255);
    inline constexpr ImU32 sep_grad_2    = IM_COL32(0x33, 0x46, 0x52, 255);
    inline constexpr ImU32 sep_grad_3    = IM_COL32(0x1C, 0x34, 0x43, 255);
    inline constexpr ImU32 sep_grad_4    = IM_COL32(0x0D, 0x24, 0x32, 255);
    inline constexpr ImU32 sep_grad_5    = IM_COL32(0x09, 0x13, 0x18, 255);
    inline constexpr ImU32 logo_blue_hi  = IM_COL32(170, 215, 255, 255);
    inline constexpr ImU32 logo_blue_lo  = IM_COL32(70, 130, 210, 255);

    inline ImU32 lerp_color(ImU32 a, ImU32 b, float t)
    {
        if (t < 0.f) t = 0.f;
        if (t > 1.f) t = 1.f;
        const int ar = (a >> IM_COL32_R_SHIFT) & 0xFF;
        const int ag = (a >> IM_COL32_G_SHIFT) & 0xFF;
        const int ab = (a >> IM_COL32_B_SHIFT) & 0xFF;
        const int aa = (a >> IM_COL32_A_SHIFT) & 0xFF;
        const int br = (b >> IM_COL32_R_SHIFT) & 0xFF;
        const int bg = (b >> IM_COL32_G_SHIFT) & 0xFF;
        const int bb = (b >> IM_COL32_B_SHIFT) & 0xFF;
        const int ba = (b >> IM_COL32_A_SHIFT) & 0xFF;
        return IM_COL32(
            ar + (int)((br - ar) * t),
            ag + (int)((bg - ag) * t),
            ab + (int)((bb - ab) * t),
            aa + (int)((ba - aa) * t));
    }

    inline ImU32 with_alpha(ImU32 col, float alpha)
    {
        if (alpha < 0.f) alpha = 0.f;
        if (alpha > 1.f) alpha = 1.f;
        return IM_COL32(
            (col >> IM_COL32_R_SHIFT) & 0xFF,
            (col >> IM_COL32_G_SHIFT) & 0xFF,
            (col >> IM_COL32_B_SHIFT) & 0xFF,
            (int)(255.f * alpha));
    }

    inline void draw_field_outline(ImDrawList* dl, const ImRect& bounds, ImU32 col, float round, float thickness = 0.5f)
    {
        const float inset = thickness * 0.5f;
        dl->AddRect(
            { bounds.Min.x + inset, bounds.Min.y + inset },
            { bounds.Max.x - inset, bounds.Max.y - inset },
            col, ImMax(round - inset, 0.f), 0, thickness);
    }
}
