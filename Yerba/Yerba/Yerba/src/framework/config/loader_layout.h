#pragma once

namespace menu::loader_layout
{
    inline constexpr float window_w = 397.f;
    inline constexpr float window_h = 315.f;

    inline constexpr float outer_border = 0.5f;
    inline constexpr float ice_border = 0.5f;
    inline constexpr float corner_r = 9.f;

    inline constexpr float title_h = 32.f;
    inline constexpr float title_pad_x = 12.f;
    inline constexpr float title_font = 14.f;

    inline constexpr float close_size = 10.f;
    inline constexpr float close_pad = 10.f;

    inline constexpr float sidebar_w = 108.f;
    inline constexpr float sidebar_pad = 10.f;
    inline constexpr float game_row_h = 42.f;
    inline constexpr float game_row_gap = 8.f;
    inline constexpr float game_row_round = 6.f;
    inline constexpr float game_font = 14.f;

    inline constexpr float injector_margin = 10.f;
    inline constexpr float body_pad_x = 10.f;
    inline constexpr float inject_top_gap = 10.f;
    inline constexpr float inject_bottom_pad = 10.f;
    inline constexpr float panel_bottom_pad = 10.f;

    inline constexpr float panel_header_h = 33.f;
    inline constexpr float panel_header_sep = 2.f;
    inline constexpr float panel_sep_fade_power = 0.62f;
    inline constexpr float panel_pad = 12.f;
    inline constexpr float panel_title_font = 14.f;
    inline constexpr float panel_round = 7.f;

    inline constexpr float section_header_h = 20.f;
    inline constexpr float section_font = 14.f;
    inline constexpr float section_line_gap = 8.f;
    inline constexpr float section_line_thickness = 1.f;

    inline constexpr float info_row_h = 19.f;
    inline constexpr float info_row_gap = 2.f;
    inline constexpr float info_font = 14.f;
    inline constexpr float info_top_pad = 6.f;
    inline constexpr float info_section_gap = 10.f;

    inline constexpr float inject_h = 38.f;
    inline constexpr float inject_round = 5.f;
    inline constexpr float inject_font = 15.f;

    inline constexpr float injector_content_h =
        panel_header_h + panel_header_sep + info_top_pad
        + section_header_h + info_row_gap
        + (info_row_h + info_row_gap) * 3.f
        + info_section_gap
        + section_header_h + info_row_gap
        + (info_row_h + info_row_gap) * 3.f
        + panel_bottom_pad;

    inline constexpr float dot_spacing = 10.f;
    inline constexpr float loading_window_w = window_w - 30.f;
    inline constexpr float loading_window_h = 108.f;
    inline constexpr float loaded_window_w = 220.f;
    inline constexpr float loaded_window_h = 148.f;

    inline constexpr float resize_anim_speed = 11.f;
    inline constexpr float load_duration = 2.4f;
    inline constexpr float loaded_hold = 0.35f;
    inline constexpr float close_countdown = 3.f;

    inline constexpr float progress_h = 12.f;
    inline constexpr float progress_round = 4.f;
    inline constexpr float progress_pad = 14.f;
    inline constexpr float loading_text_gap = 10.f;
    inline constexpr float dot_radius = 0.9f;
    inline constexpr float outline_opacity = 0.5f;
    inline constexpr float shell_outer_opacity = 0.85f;

    inline constexpr float client_w = window_w + outer_border * 2.f;
    inline constexpr float client_h = window_h + outer_border * 2.f;
}
