#include "ui/header/header_bar.h"

#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "core/logo.h"

namespace menu::ui
{
    namespace
    {
        const char* tab_label(NavTab tab)
        {
            switch (tab)
            {
            case NavTab::Aimbot:   return "aimbot";
            case NavTab::Visuals:  return "visuals";
            case NavTab::Misc:     return "misc";
            case NavTab::Settings: return "settings";
            case NavTab::Scripts:  return "Scripts";
            default:               return "";
            }
        }

        void draw_logo(ImDrawList* dl, ImVec2 pos, float size)
        {
            const ImVec2 center = { pos.x + size * 0.5f, pos.y + size * 0.52f };
            const float scale = size / 34.f;

            ImFont* font = fonts::logo();
            if (font)
            {
                const float font_size = 26.f * scale;
                const char* letter = "M";
                const ImVec2 text_size = font->CalcTextSizeA(font_size, FLT_MAX, 0.f, letter);
                const ImVec2 text_pos = {
                    center.x - text_size.x * 0.5f,
                    center.y - text_size.y * 0.55f
                };

                dl->AddText(font, font_size, { text_pos.x + 1.f, text_pos.y + 1.f },
                    colors::with_alpha(colors::logo_blue_lo, 0.35f), letter);
                dl->AddText(font, font_size, text_pos, colors::logo_blue_hi, letter);
            }
        }

        void draw_search_icon(ImDrawList* dl, ImVec2 center, float radius, ImU32 col)
        {
            dl->AddCircle(center, radius, col, 12, 1.4f);
            const ImVec2 handle_start = { center.x + radius * 0.65f, center.y + radius * 0.65f };
            const ImVec2 handle_end = { handle_start.x + radius * 0.85f, handle_start.y + radius * 0.85f };
            dl->AddLine(handle_start, handle_end, col, 1.4f);
        }

        void draw_search_pill_bg(ImDrawList* dl, const ImRect& bounds)
        {
            using namespace layout;
            using namespace colors;

            dl->AddRectFilled(bounds.Min, bounds.Max, search_bg, search_round);
            draw_field_outline(dl, bounds, search_border, search_round, search_field_outline_w);

            const float icon_r = 4.5f;
            const ImVec2 icon_center = {
                bounds.Min.x + search_icon_pad + icon_r,
                bounds.GetCenter().y
            };
            draw_search_icon(dl, icon_center, icon_r, search_icon);
        }

        void draw_search_input(const ImRect& bounds, HeaderState& state)
        {
            using namespace layout;
            using namespace colors;

            ImFont* font = fonts::search();
            if (font)
                ImGui::PushFont(font);

            const float pad_y = ImMax(0.f, (search_h - search_font) * 0.5f);
            ImGui::SetCursorScreenPos({
                bounds.Min.x + search_text_pad_left,
                bounds.Min.y + pad_y - 1.f});

            ImGui::PushItemWidth(bounds.Max.x - bounds.Min.x - search_text_pad_left - 8.f);
            ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0.f, 0.f));
            ImGui::PushStyleVar(ImGuiStyleVar_FrameBorderSize, 0.f);
            ImGui::PushStyleVar(ImGuiStyleVar_FrameRounding, search_round);
            ImGui::PushStyleColor(ImGuiCol_FrameBg, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_FrameBgHovered, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_FrameBgActive, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(0.f, 0.f, 0.f, 0.f));
            ImGui::PushStyleColor(ImGuiCol_Text, ImGui::ColorConvertU32ToFloat4(text_active));

            ImGuiInputTextFlags flags =
                ImGuiInputTextFlags_AutoSelectAll |
                ImGuiInputTextFlags_EscapeClearsAll;

            ImGui::InputText("##header_search", state.search, IM_ARRAYSIZE(state.search), flags);

            const bool search_active = ImGui::IsItemActive() || ImGui::IsItemHovered();
            if (search_active)
                state.blocks_window_drag = true;

            if (state.search[0] == '\0' && !ImGui::IsItemActive())
            {
                ImDrawList* dl = ImGui::GetWindowDrawList();
                const char* hint = "search";
                const ImVec2 hint_size = font
                    ? font->CalcTextSizeA(search_font, FLT_MAX, 0.f, hint)
                    : ImGui::CalcTextSize(hint);
                const ImVec2 hint_pos = {
                    bounds.Min.x + search_text_pad_left,
                    bounds.GetCenter().y - hint_size.y * 0.5f
                };
                if (font)
                    dl->AddText(font, search_font, hint_pos, search_hint, hint);
            }

            ImGui::PopStyleColor(5);
            ImGui::PopStyleVar(3);
            ImGui::PopItemWidth();

            if (font)
                ImGui::PopFont();
        }
    }

    void draw_header_bar(ImDrawList* dl, const ImRect& bounds, HeaderState& state)
    {
        using namespace layout;
        using namespace colors;

        state.blocks_window_drag = false;

        ImFont* nav_font_ref = fonts::nav();
        if (!nav_font_ref)
            return;

        float x = bounds.Min.x + pad_x;
        const float center_y = bounds.GetCenter().y;

        const ImVec2 logo_pos = { x, center_y - logo_size * 0.5f };
        if (!logo::draw(dl, logo_pos, logo_size))
            draw_logo(dl, logo_pos, logo_size);
        x += logo_size + logo_sep_gap;

        const ImVec2 sep_min = { x, center_y - logo_sep_h * 0.5f };
        const ImVec2 sep_max = { x + logo_sep_w, center_y + logo_sep_h * 0.5f };
        dl->AddRectFilled(sep_min, sep_max, divider);
        x += logo_sep_w + logo_sep_gap;

        for (int i = 0; i < (int)NavTab::Count; ++i)
        {
            const NavTab tab = (NavTab)i;
            const char* label = tab_label(tab);
            const bool active = (state.active == tab);
            ImFont* font = fonts::nav();
            const float font_size = nav_font;
            
            // Smooth tab transition
            ImGuiID id = ImGui::GetID(label);
            ImGuiStorage* storage = ImGui::GetStateStorage();
            float anim_t = storage->GetFloat(id, active ? 1.f : 0.f);
            
            const float target = active ? 1.f : 0.f;
            if (anim_t < target)
                anim_t = ImMin(anim_t + ImGui::GetIO().DeltaTime * 8.f, target);
            else if (anim_t > target)
                anim_t = ImMax(anim_t - ImGui::GetIO().DeltaTime * 8.f, target);
                
            storage->SetFloat(id, anim_t);
            
            // Color animation
            const ImU32 col = colors::lerp_color(text_idle, text_active, anim_t);
            
            // Scale animation
            const float scale = active ? 1.f + 0.05f * sinf(anim_t * IM_PI) : 1.f;

            const ImVec2 text_size = font->CalcTextSizeA(font_size, FLT_MAX, 0.f, label);
            const ImVec2 text_pos = { x, center_y - text_size.y * 0.5f };
            
            // Draw with scale
            const ImVec2 center = { x + text_size.x * 0.5f, center_y };
            const float scaled_font = font_size * scale;
            const ImVec2 scaled_size = font->CalcTextSizeA(scaled_font, FLT_MAX, 0.f, label);
            const ImVec2 scaled_pos = {
                center.x - scaled_size.x * 0.5f,
                center.y - scaled_size.y * 0.5f
            };
            
            dl->AddText(font, scaled_font, scaled_pos, col, label);
            
            // Underline animation
            if (active && anim_t > 0.1f)
            {
                const float line_t = ImClamp((anim_t - 0.1f) / 0.9f, 0.f, 1.f);
                const float line_w = text_size.x * line_t;
                const ImVec2 line_start = { text_pos.x + (text_size.x - line_w) * 0.5f, text_pos.y + text_size.y + 2.f };
                const ImVec2 line_end = { line_start.x + line_w, line_start.y };
                dl->AddLine(line_start, line_end, colors::with_alpha(ice_blue, 0.8f * line_t), 2.f);
            }

            const ImRect item_rect(text_pos, { text_pos.x + text_size.x, text_pos.y + text_size.y });
            if (ImGui::IsMouseHoveringRect(item_rect.Min, item_rect.Max))
                state.blocks_window_drag = true;
            if (ImGui::IsMouseHoveringRect(item_rect.Min, item_rect.Max) && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                state.active = tab;

            x += text_size.x + nav_gap;
        }

        const ImRect search_rect(
            { bounds.Max.x - pad_x - search_w, center_y - search_h * 0.5f },
            { bounds.Max.x - pad_x, center_y + search_h * 0.5f });

        draw_search_pill_bg(dl, search_rect);
        draw_search_input(search_rect, state);
    }
}
