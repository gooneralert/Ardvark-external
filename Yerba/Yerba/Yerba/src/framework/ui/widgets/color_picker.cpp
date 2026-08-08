#include "ui/widgets/color_picker.h"

#include <cmath>

#define IMGUI_DEFINE_MATH_OPERATORS
#include "imgui_internal.h"
#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
namespace menu::ui
{
    namespace
    {
        void hsv_to_rgb(float h, float s, float v, float& r, float& g, float& b)
        {
            if (s == 0.0f)
            {
                r = g = b = v;
                return;
            }

            h = fmodf(h, 1.0f) / (60.0f / 360.0f);
            int i = (int)h;
            float f = h - (float)i;
            float p = v * (1.0f - s);
            float q = v * (1.0f - s * f);
            float t = v * (1.0f - s * (1.0f - f));

            switch (i)
            {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: default: r = v; g = p; b = q; break;
            }
        }

        void rgb_to_hsv(float r, float g, float b, float& h, float& s, float& v)
        {
            float K = 0.f;
            if (g < b)
            {
                const float tmp = g; g = b; b = tmp;
                K = -1.f;
            }
            if (r < g)
            {
                const float tmp = r; r = g; g = tmp;
                K = -2.f / 6.f - K;
            }

            const float chroma = r - (g < b ? g : b);
            h = fabsf(K + (g - b) / (6.f * chroma + 1e-20f));
            s = chroma / (r + 1e-20f);
            v = r;
        }

        void draw_sv_square(ImDrawList* dl, const ImRect& bounds, float hue, float& sat, float& val, bool allow_interaction)
        {
            using namespace colors;

            const int steps_x = 32;
            const int steps_y = 32;
            const float step_x = bounds.GetWidth() / steps_x;
            const float step_y = bounds.GetHeight() / steps_y;

            // Draw gradient
            for (int y = 0; y < steps_y; ++y)
            {
                const float v0 = 1.0f - (float)y / steps_y;
                const float v1 = 1.0f - (float)(y + 1) / steps_y;

                for (int x = 0; x < steps_x; ++x)
                {
                    const float s0 = (float)x / steps_x;
                    const float s1 = (float)(x + 1) / steps_x;

                    float r0, g0, b0, r1, g1, b1, r2, g2, b2, r3, g3, b3;
                    hsv_to_rgb(hue, s0, v0, r0, g0, b0);
                    hsv_to_rgb(hue, s1, v0, r1, g1, b1);
                    hsv_to_rgb(hue, s1, v1, r2, g2, b2);
                    hsv_to_rgb(hue, s0, v1, r3, g3, b3);

                    const ImVec2 p0 = { bounds.Min.x + x * step_x, bounds.Min.y + y * step_y };
                    const ImVec2 p2 = { p0.x + step_x, p0.y + step_y };

                    dl->AddRectFilledMultiColor(p0, p2,
                        IM_COL32((int)(r0 * 255), (int)(g0 * 255), (int)(b0 * 255), 255),
                        IM_COL32((int)(r1 * 255), (int)(g1 * 255), (int)(b1 * 255), 255),
                        IM_COL32((int)(r2 * 255), (int)(g2 * 255), (int)(b2 * 255), 255),
                        IM_COL32((int)(r3 * 255), (int)(g3 * 255), (int)(b3 * 255), 255));
                }
            }

            dl->AddRect(bounds.Min, bounds.Max, with_alpha(ice_blue, 0.3f), 0.f, 0, 1.f);

            // Handle interaction
            static bool sv_active = false;
            if (allow_interaction && ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max))
            {
                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                    sv_active = true;
            }
            if (allow_interaction && sv_active && ImGui::IsMouseDown(ImGuiMouseButton_Left))
            {
                const ImVec2 mouse = ImGui::GetMousePos();
                sat = ImClamp((mouse.x - bounds.Min.x) / bounds.GetWidth(), 0.0f, 1.0f);
                val = 1.0f - ImClamp((mouse.y - bounds.Min.y) / bounds.GetHeight(), 0.0f, 1.0f);
            }
            if (ImGui::IsMouseReleased(ImGuiMouseButton_Left))
                sv_active = false;

            // Draw cursor
            const ImVec2 cursor_pos = {
                bounds.Min.x + sat * bounds.GetWidth(),
                bounds.Min.y + (1.0f - val) * bounds.GetHeight()
            };
            dl->AddCircleFilled(cursor_pos, 6.f, IM_COL32(255, 255, 255, 255), 12);
            dl->AddCircle(cursor_pos, 6.f, IM_COL32(0, 0, 0, 200), 12, 2.f);
        }

        void draw_hue_bar(ImDrawList* dl, const ImRect& bounds, float& hue, bool allow_interaction)
        {
            using namespace colors;

            const int steps = 64;
            const float step_h = bounds.GetHeight() / steps;

            for (int i = 0; i < steps; ++i)
            {
                const float h0 = (float)i / steps;
                const float h1 = (float)(i + 1) / steps;

                float r0, g0, b0, r1, g1, b1;
                hsv_to_rgb(h0, 1.0f, 1.0f, r0, g0, b0);
                hsv_to_rgb(h1, 1.0f, 1.0f, r1, g1, b1);

                const ImVec2 p0 = { bounds.Min.x, bounds.Min.y + i * step_h };
                const ImVec2 p1 = { bounds.Max.x, bounds.Min.y + (i + 1) * step_h };

                dl->AddRectFilledMultiColor(p0, p1,
                    IM_COL32((int)(r0 * 255), (int)(g0 * 255), (int)(b0 * 255), 255),
                    IM_COL32((int)(r0 * 255), (int)(g0 * 255), (int)(b0 * 255), 255),
                    IM_COL32((int)(r1 * 255), (int)(g1 * 255), (int)(b1 * 255), 255),
                    IM_COL32((int)(r1 * 255), (int)(g1 * 255), (int)(b1 * 255), 255));
            }

            dl->AddRect(bounds.Min, bounds.Max, with_alpha(ice_blue, 0.3f), 0.f, 0, 1.f);

            // Handle interaction
            static bool hue_active = false;
            if (allow_interaction && ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max))
            {
                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                    hue_active = true;
            }
            if (allow_interaction && hue_active && ImGui::IsMouseDown(ImGuiMouseButton_Left))
            {
                const ImVec2 mouse = ImGui::GetMousePos();
                hue = ImClamp((mouse.y - bounds.Min.y) / bounds.GetHeight(), 0.0f, 1.0f);
            }
            if (ImGui::IsMouseReleased(ImGuiMouseButton_Left))
                hue_active = false;

            // Draw cursor
            const ImVec2 cursor_pos = {
                bounds.GetCenter().x,
                bounds.Min.y + hue * bounds.GetHeight()
            };
            const ImVec2 cursor_min = { bounds.Min.x - 2.f, cursor_pos.y - 3.f };
            const ImVec2 cursor_max = { bounds.Max.x + 2.f, cursor_pos.y + 3.f };
            dl->AddRectFilled(cursor_min, cursor_max, IM_COL32(255, 255, 255, 255));
            dl->AddRect(cursor_min, cursor_max, IM_COL32(0, 0, 0, 200), 0.f, 0, 2.f);
        }

        void draw_alpha_bar(ImDrawList* dl, const ImRect& bounds, float& alpha, const float color[3], bool allow_interaction)
        {
            using namespace colors;

            // Checkerboard
            const float checker_size = 6.f;
            const ImU32 checker_col1 = IM_COL32(40, 40, 40, 255);
            const ImU32 checker_col2 = IM_COL32(60, 60, 60, 255);

            for (float y = bounds.Min.y; y < bounds.Max.y; y += checker_size)
            {
                for (float x = bounds.Min.x; x < bounds.Max.x; x += checker_size)
                {
                    const int ix = (int)((x - bounds.Min.x) / checker_size);
                    const int iy = (int)((y - bounds.Min.y) / checker_size);
                    const ImU32 col = ((ix + iy) % 2 == 0) ? checker_col1 : checker_col2;
                    const ImVec2 p_min = { x, y };
                    const ImVec2 p_max = { ImMin(x + checker_size, bounds.Max.x), ImMin(y + checker_size, bounds.Max.y) };
                    dl->AddRectFilled(p_min, p_max, col);
                }
            }

            // Alpha gradient
            const ImU32 col_full = IM_COL32((int)(color[0] * 255), (int)(color[1] * 255), (int)(color[2] * 255), 255);
            const ImU32 col_zero = IM_COL32((int)(color[0] * 255), (int)(color[1] * 255), (int)(color[2] * 255), 0);
            dl->AddRectFilledMultiColor(bounds.Min, bounds.Max, col_full, col_full, col_zero, col_zero);
            dl->AddRect(bounds.Min, bounds.Max, with_alpha(ice_blue, 0.3f), 0.f, 0, 1.f);

            // Handle interaction
            static bool alpha_active = false;
            if (allow_interaction && ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max))
            {
                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                    alpha_active = true;
            }
            if (allow_interaction && alpha_active && ImGui::IsMouseDown(ImGuiMouseButton_Left))
            {
                const ImVec2 mouse = ImGui::GetMousePos();
                alpha = 1.0f - ImClamp((mouse.x - bounds.Min.x) / bounds.GetWidth(), 0.0f, 1.0f);
            }
            if (ImGui::IsMouseReleased(ImGuiMouseButton_Left))
                alpha_active = false;

            // Draw cursor
            const ImVec2 cursor_pos = {
                bounds.Min.x + (1.0f - alpha) * bounds.GetWidth(),
                bounds.GetCenter().y
            };
            const ImVec2 cursor_min = { cursor_pos.x - 3.f, bounds.Min.y - 2.f };
            const ImVec2 cursor_max = { cursor_pos.x + 3.f, bounds.Max.y + 2.f };
            dl->AddRectFilled(cursor_min, cursor_max, IM_COL32(255, 255, 255, 255));
            dl->AddRect(cursor_min, cursor_max, IM_COL32(0, 0, 0, 200), 0.f, 0, 2.f);
        }
    }

    bool draw_color_picker(ImDrawList* dl, const ImRect& bounds, float color[4], bool& picker_open, bool& rainbow_mode, int picker_id, const char* label)
    {
        using namespace layout;
        using namespace colors;

        bool changed = false;

        // Rainbow animation
        if (rainbow_mode)
        {
            const float time = (float)ImGui::GetTime();
            const float hue = fmodf(time * 0.3f, 1.0f);
            float r, g, b;
            hsv_to_rgb(hue, 0.8f, 0.95f, r, g, b);
            color[0] = r;
            color[1] = g;
            color[2] = b;
        }

        // Draw color preview button
        const ImU32 preview_col = IM_COL32(
            (int)(color[0] * 255), (int)(color[1] * 255), (int)(color[2] * 255), (int)(color[3] * 255));
        
        // Checkerboard
        const float checker_size = 4.f;
        const ImU32 checker_col1 = IM_COL32(40, 40, 40, 255);
        const ImU32 checker_col2 = IM_COL32(60, 60, 60, 255);
        for (float y = bounds.Min.y; y < bounds.Max.y; y += checker_size)
        {
            for (float x = bounds.Min.x; x < bounds.Max.x; x += checker_size)
            {
                const int ix = (int)((x - bounds.Min.x) / checker_size);
                const int iy = (int)((y - bounds.Min.y) / checker_size);
                const ImU32 col = ((ix + iy) % 2 == 0) ? checker_col1 : checker_col2;
                const ImVec2 p_min = { x, y };
                const ImVec2 p_max = { ImMin(x + checker_size, bounds.Max.x), ImMin(y + checker_size, bounds.Max.y) };
                dl->AddRectFilled(p_min, p_max, col);
            }
        }

        dl->AddRectFilled(bounds.Min, bounds.Max, preview_col, 4.f);
        draw_field_outline(dl, bounds, keybind_outline, 4.f, 1.f);

        // Click to open/close
        if (ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max))
        {
            if (ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                picker_open = !picker_open;
        }

        // Draw popup as separate top-level window
        if (picker_open)
        {
            const float popup_w = 220.f;
            const float popup_h = 290.f;
            
            ImVec2 popup_pos = { bounds.Max.x + 30.f, bounds.Min.y - 40.f };
            const ImVec2 display_size = ImGui::GetIO().DisplaySize;
            
            if (popup_pos.x + popup_w > display_size.x - 20.f)
                popup_pos.x = bounds.Min.x - popup_w - 30.f;
            if (popup_pos.y + popup_h > display_size.y - 10.f)
                popup_pos.y = display_size.y - popup_h - 10.f;
            if (popup_pos.y < 10.f)
                popup_pos.y = 10.f;

            ImGui::SetNextWindowPos(popup_pos, ImGuiCond_FirstUseEver);
            ImGui::SetNextWindowSize({ popup_w, popup_h }, ImGuiCond_Always);

            ImGuiWindowFlags window_flags = 
                ImGuiWindowFlags_NoResize |
                ImGuiWindowFlags_NoScrollbar |
                ImGuiWindowFlags_NoScrollWithMouse |
                ImGuiWindowFlags_NoCollapse |
                ImGuiWindowFlags_NoSavedSettings |
                ImGuiWindowFlags_NoTitleBar |
                ImGuiWindowFlags_NoMove;

            ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0.f, 0.f));
            ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, panel_round);
            ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 1.f);
            ImGui::PushStyleColor(ImGuiCol_WindowBg, ImGui::ColorConvertU32ToFloat4(panel_inner_bg));
            ImGui::PushStyleColor(ImGuiCol_Border, ImGui::ColorConvertU32ToFloat4(with_alpha(ice_blue, 0.5f)));

            if (ImGui::Begin("##ColorPickerPopup", &picker_open, window_flags))
            {
                ImDrawList* popup_dl = ImGui::GetWindowDrawList();
                const ImVec2 win_pos = ImGui::GetWindowPos();
                const ImVec2 win_size = ImGui::GetWindowSize();

                // Draggable titlebar - only this area can move the window
                const float titlebar_h = 24.f;
                const ImRect titlebar_rect(win_pos, { win_pos.x + win_size.x, win_pos.y + titlebar_h });
                
                // Handle manual dragging
                static bool is_dragging = false;
                static ImVec2 drag_offset = { 0, 0 };
                
                const bool titlebar_hovered = ImGui::IsMouseHoveringRect(titlebar_rect.Min, titlebar_rect.Max);
                if (titlebar_hovered && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
                {
                    is_dragging = true;
                    const ImVec2 mouse = ImGui::GetMousePos();
                    drag_offset = { mouse.x - win_pos.x, mouse.y - win_pos.y };
                }
                
                if (is_dragging)
                {
                    if (ImGui::IsMouseDown(ImGuiMouseButton_Left))
                    {
                        const ImVec2 mouse = ImGui::GetMousePos();
                        ImVec2 new_pos = { mouse.x - drag_offset.x, mouse.y - drag_offset.y };
                        ImGui::SetWindowPos("##ColorPickerPopup", new_pos);
                    }
                    else
                    {
                        is_dragging = false;
                    }
                }

                // Title in titlebar
                ImFont* title_font = fonts::panel_title();
                if (title_font)
                {
                    const ImVec2 text_pos = { win_pos.x + 10.f, win_pos.y + 5.f };
                    popup_dl->AddText(title_font, 12.f, text_pos, text_active, label);
                }
                
                // Draw separator line under titlebar
                const ImVec2 sep_start = { win_pos.x, win_pos.y + titlebar_h };
                const ImVec2 sep_end = { win_pos.x + win_size.x, win_pos.y + titlebar_h };
                popup_dl->AddLine(sep_start, sep_end, with_alpha(ice_blue, 0.2f), 1.f);

                // Content
                const float content_top = win_pos.y + titlebar_h + 8.f;
                const float pad = 10.f;

                // Rainbow checkbox
                const float checkbox_size = 13.f;
                const ImRect rainbow_rect(
                    { win_pos.x + pad, content_top },
                    { win_pos.x + pad + checkbox_size, content_top + checkbox_size });
                
                ImFont* body_font = fonts::body();
                if (body_font)
                {
                    popup_dl->AddText(body_font, 11.f, { rainbow_rect.Max.x + 5.f, content_top - 1.f }, text_active, "Rainbow");
                }

                const bool rainbow_hovered = ImGui::IsMouseHoveringRect(rainbow_rect.Min, rainbow_rect.Max);
                if (rainbow_hovered && ImGui::IsMouseClicked(ImGuiMouseButton_Left) && !is_dragging)
                {
                    rainbow_mode = !rainbow_mode;
                    changed = true;
                }

                if (rainbow_mode)
                {
                    const float time = (float)ImGui::GetTime();
                    const float h1 = fmodf(time * 0.3f, 1.0f);
                    const float h2 = fmodf(h1 + 0.33f, 1.0f);
                    float r1, g1, b1, r2, g2, b2;
                    hsv_to_rgb(h1, 0.8f, 0.95f, r1, g1, b1);
                    hsv_to_rgb(h2, 0.8f, 0.95f, r2, g2, b2);
                    popup_dl->AddRectFilledMultiColor(
                        rainbow_rect.Min, rainbow_rect.Max,
                        IM_COL32((int)(r1 * 255), (int)(g1 * 255), (int)(b1 * 255), 255),
                        IM_COL32((int)(r2 * 255), (int)(g2 * 255), (int)(b2 * 255), 255),
                        IM_COL32((int)(r2 * 255), (int)(g2 * 255), (int)(b2 * 255), 255),
                        IM_COL32((int)(r1 * 255), (int)(g1 * 255), (int)(b1 * 255), 255));
                }
                else
                {
                    popup_dl->AddRectFilled(rainbow_rect.Min, rainbow_rect.Max, keybind_bg, 3.f);
                }
                draw_field_outline(popup_dl, rainbow_rect, 
                    rainbow_hovered ? keybind_outline_active : keybind_outline, 3.f, 1.f);

                // Color pickers
                const float picker_top = content_top + 20.f;
                const float square_size = popup_w - pad * 2.f - 20.f;
                const ImRect sv_rect({ win_pos.x + pad, picker_top }, 
                    { win_pos.x + pad + square_size, picker_top + square_size });
                const ImRect hue_rect({ sv_rect.Max.x + 5.f, sv_rect.Min.y }, 
                    { sv_rect.Max.x + 5.f + 15.f, sv_rect.Max.y });

                if (!rainbow_mode)
                {
                    float h, s, v;
                    rgb_to_hsv(color[0], color[1], color[2], h, s, v);
                    draw_sv_square(popup_dl, sv_rect, h, s, v, !is_dragging);
                    draw_hue_bar(popup_dl, hue_rect, h, !is_dragging);
                    hsv_to_rgb(h, s, v, color[0], color[1], color[2]);
                }
                else
                {
                    const ImU32 disabled_overlay = with_alpha(panel_inner_bg, 0.7f);
                    popup_dl->AddRectFilled(sv_rect.Min, sv_rect.Max, disabled_overlay, 4.f);
                    popup_dl->AddRectFilled(hue_rect.Min, hue_rect.Max, disabled_overlay, 4.f);
                    draw_field_outline(popup_dl, sv_rect, with_alpha(keybind_outline, 0.3f), 4.f, 1.f);
                    draw_field_outline(popup_dl, hue_rect, with_alpha(keybind_outline, 0.3f), 4.f, 1.f);
                }

                // Alpha bar
                const float alpha_top = sv_rect.Max.y + 8.f;
                const ImRect alpha_rect({ win_pos.x + pad, alpha_top }, 
                    { win_pos.x + popup_w - pad, alpha_top + 14.f });
                
                if (!rainbow_mode)
                {
                    draw_alpha_bar(popup_dl, alpha_rect, color[3], color, !is_dragging);
                }
                else
                {
                    const ImU32 disabled_overlay = with_alpha(panel_inner_bg, 0.7f);
                    popup_dl->AddRectFilled(alpha_rect.Min, alpha_rect.Max, disabled_overlay, 4.f);
                    draw_field_outline(popup_dl, alpha_rect, with_alpha(keybind_outline, 0.3f), 4.f, 1.f);
                }

                // Opacity label
                if (body_font)
                {
                    char opacity_text[32];
                    snprintf(opacity_text, sizeof(opacity_text), "Opacity: %d%%", (int)(color[3] * 100.f));
                    const ImU32 label_col = rainbow_mode ? with_alpha(text_active, 0.5f) : text_active;
                    popup_dl->AddText(body_font, 10.f, { win_pos.x + pad, alpha_rect.Max.y + 4.f }, label_col, opacity_text);
                }

                // Close button - much smaller
                const float close_top = win_pos.y + popup_h - 28.f;
                const ImRect close_rect({ win_pos.x + pad, close_top }, 
                    { win_pos.x + popup_w - pad, close_top + 20.f });

                const bool close_hovered = ImGui::IsMouseHoveringRect(close_rect.Min, close_rect.Max);
                const ImU32 close_bg = close_hovered ? lerp_color(panel_inner_bg, text_active, 0.1f) : panel_inner_bg;
                
                popup_dl->AddRectFilled(close_rect.Min, close_rect.Max, close_bg, 3.f);
                draw_field_outline(popup_dl, close_rect, with_alpha(ice_blue, 0.3f), 3.f, 1.f);

                if (body_font)
                {
                    const char* close_text = "Close";
                    const ImVec2 text_size = body_font->CalcTextSizeA(10.f, FLT_MAX, 0.f, close_text);
                    popup_dl->AddText(body_font, 10.f, 
                        { close_rect.GetCenter().x - text_size.x * 0.5f, close_rect.GetCenter().y - text_size.y * 0.5f },
                        text_active, close_text);
                }

                if (close_hovered && ImGui::IsMouseClicked(ImGuiMouseButton_Left) && !is_dragging)
                {
                    picker_open = false;
                    changed = true;
                }

                // Close if clicked outside
                if (ImGui::IsMouseClicked(ImGuiMouseButton_Left) && !is_dragging)
                {
                    const ImVec2 mouse = ImGui::GetMousePos();
                    const ImRect window_rect(win_pos, { win_pos.x + win_size.x, win_pos.y + win_size.y });
                    if (!window_rect.Contains(mouse) && !bounds.Contains(mouse))
                    {
                        picker_open = false;
                        changed = true;
                    }
                }
            }
            ImGui::End();

            ImGui::PopStyleColor(2);
            ImGui::PopStyleVar(3);
            
            if (!picker_open)
                changed = true;
        }

        return changed;
    }
}
