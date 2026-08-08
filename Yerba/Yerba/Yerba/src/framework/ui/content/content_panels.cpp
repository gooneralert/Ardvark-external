#include "ui/content/content_panels.h"

#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "ui/content/configs_panel.h"
#include "ui/content/configs_state.h"
#include "ui/content/settings_panel.h"
#include "ui/content/settings_state.h"
#include "ui/header/header_bar.h"
#include "ui/header/header_background.h"

namespace menu::ui
{
    namespace
    {
        SettingsState g_settings;
        ConfigsState g_configs;

        ImRect content_bounds(const ImRect& panel, float separator_bottom_y)
        {
            using namespace layout;
            return ImRect(
                { panel.Min.x + content_pad_x, separator_bottom_y + content_top_offset },
                { panel.Max.x - content_pad_x, panel.Max.y - content_pad_bottom });
        }

        void draw_panel_header_line(ImDrawList* dl, const ImRect& header_rect)
        {
            using namespace layout;
            using namespace colors;

            const ImRect line_rect(
                { header_rect.Min.x, header_rect.Max.y },
                { header_rect.Max.x, header_rect.Max.y + panel_header_sep });
            dl->AddRectFilled(line_rect.Min, line_rect.Max, panel_header_line);
        }

        ImRect draw_panel_shell(ImDrawList* dl, const ImRect& bounds, const char* title)
        {
            using namespace layout;
            using namespace colors;

            const ImRect header_rect(
                bounds.Min,
                { bounds.Max.x, bounds.Min.y + panel_header_h });
            const ImRect inner_rect(
                { bounds.Min.x, header_rect.Max.y + panel_header_sep },
                bounds.Max);

            dl->AddRectFilled(inner_rect.Min, inner_rect.Max, panel_inner_bg, panel_round, ImDrawFlags_RoundCornersBottom);
            draw_header_background(dl, header_rect, panel_round);
            draw_panel_header_line(dl, header_rect);

            ImFont* font = fonts::panel_title();
            if (font && title)
            {
                const ImVec2 text_size = font->CalcTextSizeA(panel_title_font, FLT_MAX, 0.f, title);
                const ImVec2 text_pos = {
                    header_rect.Min.x + panel_pad,
                    header_rect.GetCenter().y - text_size.y * 0.5f
                };
                dl->AddText(font, panel_title_font, text_pos, panel_title, title);
            }

            return inner_rect;
        }
    }

    void draw_content_panels(ImDrawList* dl, const ImRect& panel, float separator_bottom_y,
        const char* search_query, NavTab active_tab)
    {
        using namespace layout;

        const ImRect content = content_bounds(panel, separator_bottom_y);
        if (content.GetWidth() <= column_gap || content.GetHeight() <= 0.f)
            return;

        const float column_w = (content.GetWidth() - column_gap) * 0.5f;

        const ImRect left_panel(
            content.Min,
            { content.Min.x + column_w, content.Max.y });
        const ImRect right_panel(
            { left_panel.Max.x + column_gap, content.Min.y },
            content.Max);

        const ImRect left_inner = draw_panel_shell(dl, left_panel, "settings");
        if (active_tab == NavTab::Settings)
            draw_settings_panel(dl, left_inner, g_settings, search_query);

        const ImRect right_inner = draw_panel_shell(dl, right_panel, "configs");
        draw_configs_panel(dl, right_inner, g_configs);
    }
}
