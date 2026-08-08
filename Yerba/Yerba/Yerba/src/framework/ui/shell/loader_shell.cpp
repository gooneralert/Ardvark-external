#include "ui/shell/loader_shell.h"

#include <cmath>
#include <cstdio>
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
#include "config/loader_layout.h"
#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "ui/background/dot_pattern.h"
#include "ui/header/header_background.h"
#include "ui/separator/gradient_separator.h"
#include "ui/shell/window_drag.h"
#include "ui/widgets/settings_widgets.h"

namespace menu::loader
{
    namespace
    {
        using namespace menu::loader_layout;
        using namespace menu::colors;

        enum class Game : int
        {
            Cs2 = 0,
            Roblox,
            Count
        };

        enum class Phase : int
        {
            Idle = 0,
            Loading,
            Loaded,
        };

        struct Runtime
        {
            Phase phase = Phase::Idle;
            Game selected = Game::Cs2;
            float progress = 0.f;
            float phase_time = 0.f;
            float close_timer = 0.f;
            float anim_w = window_w + outer_border * 2.f;
            float anim_h = window_h + outer_border * 2.f;
            float target_w = window_w + outer_border * 2.f;
            float target_h = window_h + outer_border * 2.f;
            bool close_requested = false;
        };

        Runtime g_rt;

        const char* game_name(Game game)
        {
            switch (game)
            {
            case Game::Cs2:    return "cs2";
            case Game::Roblox: return "roblox";
            default:           return "";
            }
        }

        float ease_out(float t)
        {
            t = ImClamp(t, 0.f, 1.f);
            return 1.f - powf(1.f - t, 3.f);
        }

        float ease(float t)
        {
            return ease_out(t);
        }

        void set_target_size(float w, float h)
        {
            g_rt.target_w = w + outer_border * 2.f;
            g_rt.target_h = h + outer_border * 2.f;
        }

        ImRect shell_panel(const ImVec2& origin, float panel_w, float panel_h)
        {
            return ImRect(
                { origin.x + outer_border, origin.y + outer_border },
                { origin.x + outer_border + panel_w, origin.y + outer_border + panel_h });
        }

        void draw_shell_outlines(ImDrawList* dl, const ImRect& panel)
        {
            using namespace colors;

            dl->AddRect(panel.Min, panel.Max, with_alpha(ice_blue, outline_opacity), corner_r, 0, ice_border);

            const ImRect outer(
                { panel.Min.x - outer_border, panel.Min.y - outer_border },
                { panel.Max.x + outer_border, panel.Max.y + outer_border });
            dl->AddRect(outer.Min, outer.Max, with_alpha(border_black, shell_outer_opacity), corner_r + outer_border, 0, outer_border);
        }

        void draw_text(ImDrawList* dl, ImFont* font, float size, ImVec2 pos, ImU32 col, const char* text)
        {
            if (!font || !text)
                return;
            dl->AddText(font, size, pos, col, text);
        }

        void draw_title_bar(ImDrawList* dl, const ImRect& bounds, bool& blocks_drag, bool show_close_hover, bool& close_clicked)
        {
            blocks_drag = false;
            close_clicked = false;
            ui::draw_header_background(dl, bounds, corner_r);

            ImFont* font = fonts::nav();
            if (font)
            {
                const char* title = "loader";
                const ImVec2 text_size = font->CalcTextSizeA(title_font, FLT_MAX, 0.f, title);
                draw_text(dl, font, title_font,
                    { bounds.Min.x + title_pad_x, bounds.GetCenter().y - text_size.y * 0.5f },
                    text_idle, title);
            }

            const ImRect close_rect(
                { bounds.Max.x - close_pad - close_size, bounds.GetCenter().y - close_size * 0.5f },
                { bounds.Max.x - close_pad, bounds.GetCenter().y + close_size * 0.5f });

            const ImU32 close_col = (show_close_hover && ImGui::IsMouseHoveringRect(close_rect.Min, close_rect.Max))
                ? text_active : text_idle;
            const ImVec2 c = close_rect.GetCenter();
            const float half = close_size * 0.38f;
            dl->AddLine({ c.x - half, c.y - half }, { c.x + half, c.y + half }, close_col, 1.2f);
            dl->AddLine({ c.x + half, c.y - half }, { c.x - half, c.y + half }, close_col, 1.2f);

            if (ImGui::IsMouseHoveringRect(close_rect.Min, close_rect.Max))
            {
                blocks_drag = true;
                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                    close_clicked = true;
            }
        }

        void draw_panel_header_line(ImDrawList* dl, const ImRect& header_rect)
        {
            const ImRect line_rect(
                { header_rect.Min.x, header_rect.Max.y },
                { header_rect.Max.x, header_rect.Max.y + panel_header_sep });
            ui::draw_gradient_separator(dl, line_rect, true, panel_sep_fade_power);
        }

        void draw_section_header(ImDrawList* dl, const ImRect& row, const char* title)
        {
            using namespace colors;

            ImFont* font = fonts::body();
            ImFont* bold_font = fonts::nav();
            if (!bold_font)
                bold_font = font;
            if (!font)
                return;

            const ImVec2 text_size = bold_font->CalcTextSizeA(section_font, FLT_MAX, 0.f, title);
            const float center_y = row.GetCenter().y;
            draw_text(dl, bold_font, section_font,
                { row.Min.x, center_y - text_size.y * 0.5f }, text_active, title);

            const float line_start = row.Min.x + text_size.x + section_line_gap;
            if (line_start < row.Max.x - 1.f)
            {
                dl->AddLine(
                    { line_start, center_y },
                    { row.Max.x, center_y },
                    divider,
                    section_line_thickness);
            }
        }

        void draw_info_row(ImDrawList* dl, const ImRect& row, const char* label, const char* value)
        {
            using namespace colors;

            ImFont* font = fonts::body();
            ImFont* bold_font = fonts::nav();
            if (!font)
                return;

            const ImVec2 label_size = font->CalcTextSizeA(info_font, FLT_MAX, 0.f, label);
            const ImVec2 value_size = bold_font
                ? bold_font->CalcTextSizeA(info_font, FLT_MAX, 0.f, value)
                : font->CalcTextSizeA(info_font, FLT_MAX, 0.f, value);
            const float center_y = row.GetCenter().y;

            draw_text(dl, font, info_font,
                { row.Min.x, center_y - label_size.y * 0.5f }, text_active, label);
            draw_text(dl, bold_font ? bold_font : font, info_font,
                { row.Max.x - value_size.x, center_y - value_size.y * 0.5f }, text_active, value);
        }

        void draw_game_row(ImDrawList* dl, const ImRect& row, const char* label, bool selected, bool interactive)
        {
            using namespace colors;

            if (selected)
                dl->AddRectFilled(row.Min, row.Max, config_list_sel_bg, game_row_round);

            ImFont* font = fonts::body();
            if (!font)
                return;

            const ImVec2 text_size = font->CalcTextSizeA(game_font, FLT_MAX, 0.f, label);
            draw_text(dl, font, game_font,
                { row.Min.x + 10.f, row.GetCenter().y - text_size.y * 0.5f },
                selected ? text_active : text_idle, label);

            if (interactive && ImGui::IsMouseHoveringRect(row.Min, row.Max) && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
            {
                if (label[0] == 'c')
                    g_rt.selected = Game::Cs2;
                else
                    g_rt.selected = Game::Roblox;
            }
        }

        void draw_injector_panel(ImDrawList* dl, const ImRect& bounds)
        {
            using namespace colors;

            dl->AddRectFilled(bounds.Min, bounds.Max, panel_inner_bg, panel_round);

            const ImRect header_rect(
                bounds.Min,
                { bounds.Max.x, bounds.Min.y + panel_header_h });

            ui::draw_header_background(dl, header_rect, panel_round);
            draw_panel_header_line(dl, header_rect);

            ImFont* title_font_ref = fonts::nav();
            if (title_font_ref)
            {
                const char* title = "injector";
                const ImVec2 text_size = title_font_ref->CalcTextSizeA(panel_title_font, FLT_MAX, 0.f, title);
                draw_text(dl, title_font_ref, panel_title_font,
                    { header_rect.Min.x + panel_pad, header_rect.GetCenter().y - text_size.y * 0.5f },
                    panel_title, title);
            }

            const ImRect content_rect(
                { bounds.Min.x + panel_pad, header_rect.Max.y + panel_header_sep + info_top_pad },
                { bounds.Max.x - panel_pad, bounds.Max.y - panel_bottom_pad });

            float row_y = content_rect.Min.y;
            const ImRect info_header(
                { content_rect.Min.x, row_y },
                { content_rect.Max.x, row_y + section_header_h });
            draw_section_header(dl, info_header, "information");
            row_y += section_header_h + info_row_gap;

            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "version", "0.1.0");
            row_y += info_row_h + info_row_gap;
            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "build", "Apr 19 2026");
            row_y += info_row_h + info_row_gap;
            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "update", "Apr 19 2026");

            row_y += info_row_h + info_section_gap;
            const ImRect sub_header(
                { content_rect.Min.x, row_y },
                { content_rect.Max.x, row_y + section_header_h });
            draw_section_header(dl, sub_header, "subscription");
            row_y += section_header_h + info_row_gap;

            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "user", "averta047");
            row_y += info_row_h + info_row_gap;
            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "plan", "premium");
            row_y += info_row_h + info_row_gap;
            draw_info_row(dl, { content_rect.Min.x, row_y, content_rect.Max.x, row_y + info_row_h }, "expiry", "3 weeks left");
        }

        void draw_progress_bar(ImDrawList* dl, const ImRect& bounds, float t)
        {
            using namespace colors;

            dl->AddRectFilled(bounds.Min, bounds.Max, config_list_bg, progress_round);
            dl->AddRect(bounds.Min, bounds.Max, config_list_border, progress_round, 0, 0.5f);

            if (t <= 0.f)
                return;

            const ImRect fill_bounds(
                bounds.Min,
                { bounds.Min.x + bounds.GetWidth() * ImClamp(t, 0.f, 1.f), bounds.Max.y });
            if (fill_bounds.GetWidth() <= 0.5f)
                return;

            dl->AddRectFilled(fill_bounds.Min, fill_bounds.Max, toggle_on_top, progress_round);
        }

        void draw_loading_body(ImDrawList* dl, const ImRect& body_rect)
        {
            using namespace colors;

            ui::draw_dot_grid(dl, body_rect);

            ImFont* font = fonts::body();
            ImFont* bold_font = fonts::nav();
            if (!font)
                return;

            const char* label = "loading";
            char pct_buf[16] = {};
            std::snprintf(pct_buf, sizeof(pct_buf), "%d%%", (int)(g_rt.progress * 100.f + 0.5f));

            const float text_y = body_rect.Min.y + progress_pad;
            const ImVec2 label_size = font->CalcTextSizeA(info_font, FLT_MAX, 0.f, label);
            const ImVec2 pct_size = bold_font
                ? bold_font->CalcTextSizeA(info_font, FLT_MAX, 0.f, pct_buf)
                : font->CalcTextSizeA(info_font, FLT_MAX, 0.f, pct_buf);

            draw_text(dl, font, info_font,
                { body_rect.Min.x + progress_pad, text_y }, text_active, label);
            draw_text(dl, bold_font ? bold_font : font, info_font,
                { body_rect.Max.x - progress_pad - pct_size.x, text_y }, text_active, pct_buf);

            const ImRect bar_rect(
                { body_rect.Min.x + progress_pad, text_y + label_size.y + loading_text_gap },
                { body_rect.Max.x - progress_pad, text_y + label_size.y + loading_text_gap + progress_h });
            draw_progress_bar(dl, bar_rect, g_rt.progress);
        }

        void draw_loaded_body(ImDrawList* dl, const ImRect& body_rect)
        {
            using namespace colors;

            ui::draw_dot_grid(dl, body_rect);

            ImFont* bold_font = fonts::nav();
            ImFont* font = fonts::body();
            if (!font)
                return;

            const char* game = game_name(g_rt.selected);
            const char* loaded = "loaded!";
            char closing_buf[32] = {};
            const int secs = (int)ceilf(g_rt.close_timer);
            std::snprintf(closing_buf, sizeof(closing_buf), "closing in %d", ImMax(secs, 0));

            const float icon_size = 28.f;
            const ImVec2 center = body_rect.GetCenter();
            const float icon_reveal = ease_out(ImClamp(g_rt.phase_time / 0.35f, 0.f, 1.f));
            const ImRect icon_rect(
                { center.x - icon_size * 0.5f, body_rect.Min.y + 14.f + (1.f - icon_reveal) * 6.f },
                { center.x + icon_size * 0.5f, body_rect.Min.y + 14.f + icon_size + (1.f - icon_reveal) * 6.f });
            const ImU32 icon_bg = with_alpha(config_list_sel_bg, icon_reveal);
            dl->AddRectFilled(icon_rect.Min, icon_rect.Max, icon_bg, 8.f);
            if (game[0] && icon_reveal > 0.05f)
            {
                const ImVec2 glyph_size = bold_font
                    ? bold_font->CalcTextSizeA(15.f, FLT_MAX, 0.f, game)
                    : font->CalcTextSizeA(15.f, FLT_MAX, 0.f, game);
                draw_text(dl, bold_font ? bold_font : font, 15.f,
                    { icon_rect.GetCenter().x - glyph_size.x * 0.5f, icon_rect.GetCenter().y - glyph_size.y * 0.5f },
                    with_alpha(text_active, icon_reveal), game);
            }

            float text_y = icon_rect.Max.y + 10.f;
            const float line_stagger = 0.14f;
            const float line_dur = 0.32f;

            auto draw_centered_animated = [&](const char* text, float size, ImFont* f, int line_index, bool bold_line)
            {
                const float t = ease_out(ImClamp((g_rt.phase_time - line_index * line_stagger) / line_dur, 0.f, 1.f));
                if (t <= 0.f)
                    return;

                ImFont* use_font = (bold_line && bold_font) ? bold_font : f;
                const ImVec2 ts = use_font->CalcTextSizeA(size, FLT_MAX, 0.f, text);
                const float slide = (1.f - t) * 10.f;
                draw_text(dl, use_font, size,
                    { center.x - ts.x * 0.5f, text_y + slide },
                    with_alpha(text_active, t), text);
                text_y += ts.y * t + 3.f;
            };

            draw_centered_animated(game, 14.f, font, 0, true);
            draw_centered_animated(loaded, 14.f, font, 1, true);
            draw_centered_animated(closing_buf, 13.f, font, 2, true);
        }

        void draw_idle_body(ImDrawList* dl, HWND hwnd, const ImRect& body_rect, bool& blocks_drag)
        {
            const ImRect sidebar_rect(
                { body_rect.Min.x + sidebar_pad, body_rect.Min.y + sidebar_pad },
                { body_rect.Min.x + sidebar_w, body_rect.Max.y - inject_h - inject_bottom_pad - inject_top_gap });

            float game_y = sidebar_rect.Min.y;
            draw_game_row(dl,
                { sidebar_rect.Min.x, game_y, sidebar_rect.Max.x, game_y + game_row_h },
                "cs2", g_rt.selected == Game::Cs2, true);
            game_y += game_row_h + game_row_gap;
            draw_game_row(dl,
                { sidebar_rect.Min.x, game_y, sidebar_rect.Max.x, game_y + game_row_h },
                "roblox", g_rt.selected == Game::Roblox, true);

            const ImRect inject_rect(
                { body_rect.Min.x + body_pad_x, body_rect.Max.y - inject_bottom_pad - inject_h },
                { body_rect.Max.x - body_pad_x, body_rect.Max.y - inject_bottom_pad });

            const float injector_top = body_rect.Min.y + injector_margin;
            const ImRect injector_bounds(
                { sidebar_rect.Max.x + injector_margin, injector_top },
                { body_rect.Max.x - injector_margin, inject_rect.Min.y - inject_top_gap });
            draw_injector_panel(dl, injector_bounds);

            if (ui::draw_action_button(dl, inject_rect, inject_round, "inject", inject_font))
            {
                g_rt.phase = Phase::Loading;
                g_rt.phase_time = 0.f;
                g_rt.progress = 0.f;
                set_target_size(loading_window_w, loading_window_h);
            }
        }

        void apply_window_client_size(HWND hwnd, float client_w, float client_h)
        {
            RECT wr = {};
            ::GetWindowRect(hwnd, &wr);
            const float cx = (wr.left + wr.right) * 0.5f;
            const float cy = (wr.top + wr.bottom) * 0.5f;
            const int w = (int)(client_w + 0.5f);
            const int h = (int)(client_h + 0.5f);
            ::SetWindowPos(hwnd, nullptr,
                (int)(cx - w * 0.5f), (int)(cy - h * 0.5f),
                w, h, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    void draw(ImDrawList* dl, HWND hwnd, const ImVec2& origin)
    {
        fonts::use_loader();

        const float panel_w = g_rt.anim_w - outer_border * 2.f;
        const float panel_h = g_rt.anim_h - outer_border * 2.f;
        const ImRect panel = shell_panel(origin, panel_w, panel_h);

        const ImRect title_rect(
            panel.Min,
            { panel.Max.x, panel.Min.y + title_h });

        const ImRect body_rect(
            { panel.Min.x, title_rect.Max.y },
            panel.Max);

        dl->AddRectFilled(panel.Min, panel.Max, body_bg, corner_r);
        dl->AddRectFilled(body_rect.Min, body_rect.Max, body_bg, corner_r, ImDrawFlags_RoundCornersBottom);

        bool blocks_drag = false;
        bool close_clicked = false;
        draw_title_bar(dl, title_rect, blocks_drag, g_rt.phase == Phase::Idle, close_clicked);
        if (close_clicked)
            g_rt.close_requested = true;

        if (g_rt.phase == Phase::Idle)
        {
            ui::draw_dot_grid(dl, body_rect);
            draw_idle_body(dl, hwnd, body_rect, blocks_drag);
        }
        else if (g_rt.phase == Phase::Loading)
            draw_loading_body(dl, body_rect);
        else
            draw_loaded_body(dl, body_rect);

        draw_shell_outlines(dl, panel);
        ui::handle_window_drag(hwnd, title_rect, blocks_drag);
    }

    void update(HWND hwnd, float dt)
    {
        if (dt <= 0.f)
            dt = 1.f / 60.f;

        const float k = 1.f - powf(0.001f, dt * resize_anim_speed);
        g_rt.anim_w += (g_rt.target_w - g_rt.anim_w) * k;
        g_rt.anim_h += (g_rt.target_h - g_rt.anim_h) * k;

        if (fabsf(g_rt.anim_w - g_rt.target_w) < 0.5f) g_rt.anim_w = g_rt.target_w;
        if (fabsf(g_rt.anim_h - g_rt.target_h) < 0.5f) g_rt.anim_h = g_rt.target_h;

        static float last_w = 0.f;
        static float last_h = 0.f;
        if (fabsf(g_rt.anim_w - last_w) > 0.5f || fabsf(g_rt.anim_h - last_h) > 0.5f)
        {
            apply_window_client_size(hwnd, g_rt.anim_w, g_rt.anim_h);
            last_w = g_rt.anim_w;
            last_h = g_rt.anim_h;
        }

        g_rt.phase_time += dt;

        if (g_rt.phase == Phase::Loading)
        {
            g_rt.progress = ease(g_rt.phase_time / load_duration);
            if (g_rt.progress >= 1.f && g_rt.phase_time >= load_duration + loaded_hold)
            {
                g_rt.phase = Phase::Loaded;
                g_rt.phase_time = 0.f;
                g_rt.close_timer = close_countdown;
                set_target_size(loaded_window_w, loaded_window_h);
            }
        }
        else if (g_rt.phase == Phase::Loaded)
        {
            g_rt.close_timer -= dt;
            if (g_rt.close_timer <= 0.f)
            {
                g_rt.phase = Phase::Idle;
                g_rt.phase_time = 0.f;
                g_rt.progress = 0.f;
                set_target_size(window_w, window_h);
            }
        }
    }

    bool consume_close_request()
    {
        if (!g_rt.close_requested)
            return false;
        g_rt.close_requested = false;
        return true;
    }

    ImVec2 animated_client_size()
    {
        return { g_rt.anim_w, g_rt.anim_h };
    }

    void render(HWND hwnd)
    {
        fonts::use_loader();

        const ImVec2 size = animated_client_size();
        ImGui::SetNextWindowPos({ 0.f, 0.f }, ImGuiCond_Always);
        ImGui::SetNextWindowSize(size, ImGuiCond_Always);

        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoTitleBar |
            ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoScrollbar |
            ImGuiWindowFlags_NoCollapse |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoBackground;

        ImGui::Begin("##loader_shell", nullptr, flags);

        ImDrawList* dl = ImGui::GetWindowDrawList();
        draw(dl, hwnd, ImGui::GetWindowPos());

        ImGui::End();
    }
}
