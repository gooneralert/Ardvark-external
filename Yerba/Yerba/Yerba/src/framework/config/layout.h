#pragma once

#include "loader_layout.h"

namespace menu::layout
{
    inline constexpr float window_w = 579.f;
    inline constexpr float window_h = 455.f;

    inline constexpr float outer_border = 0.5f;
    inline constexpr float ice_border = 0.5f;
    inline constexpr float corner_r = 9.f;

    inline constexpr float header_h = 45.f;
    inline constexpr float pad_x = 12.f;

    inline constexpr float logo_size = 42.f;
    inline constexpr float logo_sep_w = 2.f;
    inline constexpr float logo_sep_h = 20.f;
    inline constexpr float logo_sep_gap = 10.f;

    inline constexpr float nav_gap = 22.f;
    inline constexpr float nav_font = 14.f;

    inline constexpr float search_w = 163.f;
    inline constexpr float search_h = 28.f;
    inline constexpr float search_round = 4.f;
    inline constexpr float search_icon_pad = 9.f;
    inline constexpr float search_outline = 0.5f;
    inline constexpr float search_field_outline_w = 1.f;
    inline constexpr float field_outline_w = 0.5f;

    inline constexpr float outline_opacity = 0.5f;
    inline constexpr float shell_outer_opacity = 0.85f;

    inline constexpr float separator_h = 2.f;
    inline constexpr float separator_fade_power = 0.62f;
    inline constexpr int   separator_segments = 96;

    inline constexpr float dot_spacing = 10.f;
    inline constexpr float dot_radius = 0.9f;

    inline constexpr float content_top_offset = 14.f;
    inline constexpr float content_pad_x = 14.f;
    inline constexpr float content_pad_bottom = 14.f;
    inline constexpr float column_gap = 12.f;

    inline constexpr float panel_round = 7.f;
    inline constexpr float panel_header_h = 29.f;
    inline constexpr float panel_header_sep = 1.f;
    inline constexpr float panel_pad = 10.f;
    inline constexpr float panel_title_font = 14.f;

    inline constexpr float setting_top_pad = 14.f;
    inline constexpr float setting_label_pad = 14.f;
    inline constexpr float setting_control_pad = 14.f;
    inline constexpr float setting_row_h = 32.f;
    inline constexpr float setting_row_gap = 2.f;
    inline constexpr float setting_row_font = 15.f;

    inline constexpr float checkbox_size = 17.f;
    inline constexpr float checkbox_round = 3.5f;

    inline constexpr float keybind_w = 74.f;
    inline constexpr float keybind_h = 22.f;
    inline constexpr float keybind_round = 6.f;
    inline constexpr float keybind_icon_w = 34.f;
    inline constexpr float keybind_div_w = 1.f;
    inline constexpr float keybind_key_font = 15.f;
    inline constexpr float keybind_outline_w = field_outline_w;
    inline constexpr float keybind_press_scale = 0.07f;
    inline constexpr float keybind_press_dur = 0.28f;

    inline constexpr float search_text_pad_left = 28.f;
    inline constexpr float search_font = 14.f;

    inline constexpr float toggle_outline_w = 0.5f;

    inline constexpr float unload_h = 36.f;
    inline constexpr float unload_round = 5.f;
    inline constexpr float unload_top_gap = 10.f;
    inline constexpr float action_btn_gradient_span = 1.f;
    inline constexpr float action_btn_hover_scale = 0.025f;
    inline constexpr float action_btn_press_scale = 0.018f;
    inline constexpr float action_btn_anim_speed = 14.f;

    inline constexpr float config_pad = 12.f;
    inline constexpr float config_label_font = 15.f;
    inline constexpr float config_list_gap = 8.f;
    inline constexpr float config_list_round = 6.f;
    inline constexpr float config_list_outline = 0.5f;
    inline constexpr float config_input_outline = field_outline_w;
    inline constexpr float config_row_gap = 10.f;
    inline constexpr float config_input_h = 32.f;
    inline constexpr float config_input_round = 6.f;
    inline constexpr float config_input_pad = 12.f;
    inline constexpr float config_create_w = 74.f;
    inline constexpr float config_create_gap = 8.f;
    inline constexpr float config_action_h = 38.f;
    inline constexpr float config_action_gap = 8.f;
    inline constexpr float config_btn_round = 5.f;
    inline constexpr float config_btn_font = 15.f;
    inline constexpr float config_list_item_h = 28.f;
    inline constexpr float config_list_item_pad = 10.f;

    inline constexpr float icon_font_size = 13.f;

    inline constexpr float client_w = window_w + outer_border * 2.f;
    inline constexpr float client_h = window_h + outer_border * 2.f;

    inline constexpr float desktop_gap = 14.f;
    inline constexpr float desktop_client_w =
        ::menu::loader_layout::client_w + desktop_gap + client_w;
    inline constexpr float desktop_client_h = client_h;
}
