#include "ui/content/settings_panel.h"

#include <cstring>

#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "ui/widgets/settings_widgets.h"
#include "ui/widgets/color_picker.h"
//https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopaminahttps://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopaminahttps://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////
////https://discord.gg/dopaminahttps://discord.gg/dopamina
////
////https://discord.gg/dopamina
////https://discord.gg/dopaminahttps://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////
////https://discord.gg/dopamina
////https://discord.gg/dopamina
////https://discord.gg/dopaminahttps://discord.gg/dopamina
namespace menu::ui
{
    namespace
    {
        char to_lower_char(char c)
        {
            if (c >= 'A' && c <= 'Z')
                return (char)(c - 'A' + 'a');
            return c;
        }

        bool matches_search(const char* label, const char* query)
        {
            if (!query || query[0] == '\0')
                return true;
            if (!label)
                return false;

            const size_t query_len = strlen(query);
            for (const char* p = label; *p; ++p)
            {
                size_t i = 0;
                while (i < query_len && p[i] && to_lower_char(p[i]) == to_lower_char(query[i]))
                    ++i;
                if (i == query_len)
                    return true;
            }
            return false;
        }

        ImRect color_picker_rect(const ImRect& row)
        {
            using namespace layout;
            const float size = checkbox_size + 2.f;
            return ImRect(
                { row.Max.x - setting_control_pad - size, row.GetCenter().y - size * 0.5f },
                { row.Max.x - setting_control_pad, row.GetCenter().y + size * 0.5f });
        }

        void draw_setting_row(ImDrawList* dl, const ImRect& row, const char* label,
            const ImRect& control, SettingsState& state, int row_index)
        {
            using namespace layout;
            using namespace colors;

            ImFont* font = fonts::setting_label();
            if (!font)
                return;

            const ImVec2 text_size = font->CalcTextSizeA(setting_row_font, FLT_MAX, 0.f, label);
            const ImVec2 text_pos = {
                row.Min.x + setting_label_pad,
                row.GetCenter().y - text_size.y * 0.5f
            };
            dl->AddText(font, setting_row_font, text_pos, text_active, label);

            switch (row_index)
            {
            case 0:
                draw_keybind_field(dl, control, state);
                break;
            case 1:
                draw_gradient_checkbox(dl, control, state.vsync, state.vsync);
                break;
            case 2:
                draw_gradient_checkbox(dl, control, state.show_watermark, state.show_watermark);
                break;
            case 3:
                draw_gradient_checkbox(dl, control, state.streamproof, state.streamproof);
                break;
            case 4:
                draw_gradient_checkbox(dl, control, state.dex_explorer, state.dex_explorer);
                break;
            case 5:
                draw_gradient_checkbox(dl, control, state.keybind_list, state.keybind_list);
                break;
            case 6:
                draw_gradient_checkbox(dl, control, state.accent_color, state.accent_color);
                break;
            case 7:
                draw_color_picker(dl, control, state.global_accent, state.color_picker_open, state.rainbow_mode, 0, "Global Accent");
                break;
            case 8:
                draw_rounded_slider(dl, control, state.test_slider_value, 0.f, 100.f, "speed");
                break;
            default:
                break;
            }
        }

        ImRect checkbox_rect(const ImRect& row)
        {
            using namespace layout;
            return ImRect(
                { row.Max.x - setting_control_pad - checkbox_size, row.GetCenter().y - checkbox_size * 0.5f },
                { row.Max.x - setting_control_pad, row.GetCenter().y + checkbox_size * 0.5f });
        }

        ImRect keybind_rect(const ImRect& row)
        {
            using namespace layout;
            return ImRect(
                { row.Max.x - setting_control_pad - keybind_w, row.GetCenter().y - keybind_h * 0.5f },
                { row.Max.x - setting_control_pad, row.GetCenter().y + keybind_h * 0.5f });
        }
    }

    void draw_settings_panel(ImDrawList* dl, const ImRect& inner, SettingsState& state, const char* search_query)
    {
        using namespace layout;

        static constexpr const char* labels[] = {
            "menu key",
            "vsync",
            "show watermark",
            "streamproof",
            "dex explorer",
            "keybind list",
            "accent color",
            "global accent color",
            "animation speed",
        };

        float y = inner.Min.y + setting_top_pad;
        for (int i = 0; i < 9; ++i)
        {
            if (!matches_search(labels[i], search_query))
                continue;

            const ImRect row(
                { inner.Min.x, y },
                { inner.Max.x, y + setting_row_h });
            
            ImRect control;
            if (i == 0)
                control = keybind_rect(row);
            else if (i == 7)
                control = color_picker_rect(row);
            else if (i == 8)
            {
                // Slider control
                control = ImRect(
                    { row.Max.x - setting_control_pad - 100.f, row.GetCenter().y - 5.f },
                    { row.Max.x - setting_control_pad, row.GetCenter().y + 5.f });
            }
            else
                control = checkbox_rect(row);
                
            draw_setting_row(dl, row, labels[i], control, state, i);
            y += setting_row_h + setting_row_gap;
        }

        if (!matches_search("unload", search_query))
            return;

        y += unload_top_gap;
        const ImRect unload_rect(
            { inner.Min.x + setting_label_pad, y },
            { inner.Max.x - setting_label_pad, y + unload_h });
        draw_unload_button(dl, unload_rect);
    }
}
