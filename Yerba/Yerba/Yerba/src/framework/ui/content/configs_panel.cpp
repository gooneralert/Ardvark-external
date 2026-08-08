#include "ui/content/configs_panel.h"

#include <cstring>
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "ui/widgets/settings_widgets.h"
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
        bool str_ieq(const char* a, const char* b)
        {
            if (!a || !b)
                return false;

            while (*a && *b)
            {
                char ca = *a;
                char cb = *b;
                if (ca >= 'A' && ca <= 'Z') ca = (char)(ca - 'A' + 'a');
                if (cb >= 'A' && cb <= 'Z') cb = (char)(cb - 'A' + 'a');
                if (ca != cb)
                    return false;
                ++a;
                ++b;
            }
            return *a == '\0' && *b == '\0';
        }

        bool config_exists(const ConfigsState& state, const char* name)
        {
            for (int i = 0; i < state.count; ++i)
            {
                if (str_ieq(state.names[i], name))
                    return true;
            }
            return false;
        }

        void draw_list_label(ImDrawList* dl, const ImRect& inner, float& y)
        {
            using namespace layout;
            using namespace colors;

            ImFont* font = fonts::setting_label();
            if (!font)
                return;

            const char* label = "list";
            const ImVec2 text_size = font->CalcTextSizeA(config_label_font, FLT_MAX, 0.f, label);
            const ImVec2 text_pos = {
                inner.Min.x + config_pad,
                y
            };
            dl->AddText(font, config_label_font, text_pos, text_active, label);
            y += text_size.y + config_list_gap;
        }

        void draw_list_box(ImDrawList* dl, const ImRect& list_rect, ConfigsState& state)
        {
            using namespace layout;
            using namespace colors;

            dl->AddRectFilled(list_rect.Min, list_rect.Max, config_list_bg, config_list_round);
            dl->AddRect(list_rect.Min, list_rect.Max, config_list_border, config_list_round, 0, config_list_outline);

            ImFont* font = fonts::setting_label();
            if (!font)
                return;

            float item_y = list_rect.Min.y + 6.f;
            for (int i = 0; i < state.count; ++i)
            {
                if (item_y + config_list_item_h > list_rect.Max.y - 4.f)
                    break;

                const ImRect item_rect(
                    { list_rect.Min.x + 4.f, item_y },
                    { list_rect.Max.x - 4.f, item_y + config_list_item_h });

                if (i == state.selected)
                    dl->AddRectFilled(item_rect.Min, item_rect.Max, config_list_sel_bg, 4.f);

                const ImVec2 text_size = font->CalcTextSizeA(config_label_font, FLT_MAX, 0.f, state.names[i]);
                const ImVec2 text_pos = {
                    item_rect.Min.x + config_list_item_pad,
                    item_rect.GetCenter().y - text_size.y * 0.5f
                };
                dl->AddText(font, config_label_font, text_pos, text_active, state.names[i]);

                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left) &&
                    ImGui::IsMouseHoveringRect(item_rect.Min, item_rect.Max))
                {
                    state.selected = i;
                }

                item_y += config_list_item_h;
            }
        }

        void draw_config_input(const ImRect& bounds, ConfigsState& state)
        {
            using namespace layout;
            using namespace colors;

            ImDrawList* dl = ImGui::GetWindowDrawList();
            dl->AddRectFilled(bounds.Min, bounds.Max, config_input_bg, config_input_round);
            draw_field_outline(dl, bounds, config_input_border, config_input_round, config_input_outline);

            ImFont* font = fonts::setting_label();
            if (font)
                ImGui::PushFont(font);

            const float pad_y = ImMax(0.f, (bounds.GetHeight() - config_btn_font) * 0.5f);
            ImGui::SetCursorScreenPos({ bounds.Min.x + config_input_pad, bounds.Min.y + pad_y - 1.f });
            ImGui::PushItemWidth(bounds.GetWidth() - config_input_pad * 2.f);
            ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0.f, 0.f));
            ImGui::PushStyleVar(ImGuiStyleVar_FrameBorderSize, 0.f);
            ImGui::PushStyleColor(ImGuiCol_FrameBg, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_FrameBgHovered, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_FrameBgActive, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_Text, ImGui::ColorConvertU32ToFloat4(text_active));

            ImGui::InputText("##config_name", state.new_name, IM_ARRAYSIZE(state.new_name));

            ImGui::PopStyleColor(5);
            ImGui::PopStyleVar(2);
            ImGui::PopItemWidth();

            if (font)
                ImGui::PopFont();
        }

        void create_config(ConfigsState& state)
        {
            if (state.new_name[0] == '\0' || config_exists(state, state.new_name))
                return;
            if (state.count >= ConfigsState::max_configs)
                return;

            std::strncpy(state.names[state.count], state.new_name, sizeof(state.names[0]) - 1);
            state.names[state.count][sizeof(state.names[0]) - 1] = '\0';
            state.selected = state.count;
            ++state.count;
        }

        void delete_config(ConfigsState& state)
        {
            if (state.selected < 0 || state.selected >= state.count)
                return;

            for (int i = state.selected; i < state.count - 1; ++i)
                std::strcpy(state.names[i], state.names[i + 1]);

            --state.count;
            if (state.count == 0)
                state.selected = -1;
            else if (state.selected >= state.count)
                state.selected = state.count - 1;
        }
    }

    void draw_configs_panel(ImDrawList* dl, const ImRect& inner, ConfigsState& state)
    {
        using namespace layout;

        float y = inner.Min.y + config_pad;
        draw_list_label(dl, inner, y);

        const float bottom_block_h = config_input_h + config_row_gap + config_action_h;
        const ImRect list_rect(
            { inner.Min.x + config_pad, y },
            { inner.Max.x - config_pad, inner.Max.y - config_pad - bottom_block_h - config_row_gap });

        draw_list_box(dl, list_rect, state);

        y = list_rect.Max.y + config_row_gap;

        const ImRect input_rect(
            { inner.Min.x + config_pad, y },
            { inner.Max.x - config_pad - config_create_w - config_create_gap, y + config_input_h });
        const ImRect create_rect(
            { input_rect.Max.x + config_create_gap, y },
            { inner.Max.x - config_pad, y + config_input_h });

        draw_config_input(input_rect, state);
        if (draw_action_button(dl, create_rect, config_btn_round, "create", config_btn_font))
            create_config(state);

        y += config_input_h + config_row_gap;

        const float action_w = (inner.GetWidth() - config_pad * 2.f - config_action_gap) * 0.5f;
        const ImRect delete_rect(
            { inner.Min.x + config_pad, y },
            { inner.Min.x + config_pad + action_w, y + config_action_h });
        const ImRect load_rect(
            { delete_rect.Max.x + config_action_gap, y },
            { delete_rect.Max.x + config_action_gap + action_w, y + config_action_h });

        if (draw_action_button(dl, delete_rect, config_btn_round, "delete", config_btn_font))
            delete_config(state);

        draw_action_button(dl, load_rect, config_btn_round, "load", config_btn_font);
    }
}
