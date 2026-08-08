#include "ui/widgets/settings_widgets.h"

#include <cmath>

#define IMGUI_DEFINE_MATH_OPERATORS
#include "imgui_internal.h"
#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "data/icon_font_data.h"

namespace menu::ui
{
    namespace
    {
        void draw_icon_glyph(ImDrawList* dl, ImFont* font, const char* glyph, ImVec2 center, ImU32 col, float size = 11.f)
        {
            if (!font || !glyph || !glyph[0])
                return;

            const ImVec2 text_size = font->CalcTextSizeA(size, FLT_MAX, 0.f, glyph);
            const ImVec2 pos = { center.x - text_size.x * 0.5f, center.y - text_size.y * 0.5f };
            dl->AddText(font, size, pos, col, glyph);
        }

        void draw_rounded_vertical_gradient(ImDrawList* dl, const ImRect& bounds, float round,
            ImU32 top, ImU32 bottom, float gradient_span = 1.f)
        {
            const float height = bounds.GetHeight();
            if (height <= 0.f)
                return;

            const int vtx_start = dl->VtxBuffer.Size;
            dl->AddRectFilled(bounds.Min, bounds.Max, IM_COL32_WHITE, round);
            const int vtx_end = dl->VtxBuffer.Size;

            const float span = ImMax(gradient_span, 0.05f);
            const ImVec2 gradient_p0 = bounds.Min;
            const ImVec2 gradient_p1 = { bounds.Min.x, bounds.Min.y + height * span };
            ImGui::ShadeVertsLinearColorGradientKeepAlpha(dl, vtx_start, vtx_end, gradient_p0, gradient_p1, top, bottom);
        }

        void draw_rounded_vertical_gradient_3(ImDrawList* dl, const ImRect& bounds, float round,
            ImU32 top, ImU32 mid, ImU32 bottom, float gradient_span = 1.f)
        {
            using namespace colors;

            const float height = bounds.GetHeight();
            if (height <= 0.f)
                return;

            const int vtx_start = dl->VtxBuffer.Size;
            dl->AddRectFilled(bounds.Min, bounds.Max, IM_COL32_WHITE, round);
            const int vtx_end = dl->VtxBuffer.Size;

            const float span = ImMax(gradient_span, 0.05f);
            const float grad_h = height * span;
            const float y0 = bounds.Min.y;

            for (int i = vtx_start; i < vtx_end; ++i)
            {
                ImDrawVert& v = dl->VtxBuffer.Data[i];
                float t = (v.pos.y - y0) / grad_h;
                t = ImClamp(t, 0.f, 1.f);

                ImU32 col = mid;
                if (t < 0.5f)
                    col = lerp_color(top, mid, t * 2.f);
                else
                    col = lerp_color(mid, bottom, (t - 0.5f) * 2.f);

                v.col = (col & ~IM_COL32_A_MASK) | (v.col & IM_COL32_A_MASK);
            }
        }

        char key_from_imgui(ImGuiKey key)
        {
            if (key >= ImGuiKey_A && key <= ImGuiKey_Z)
                return (char)('a' + (key - ImGuiKey_A));
            if (key >= ImGuiKey_0 && key <= ImGuiKey_9)
                return (char)('0' + (key - ImGuiKey_0));
            if (key >= ImGuiKey_F1 && key <= ImGuiKey_F12)
                return (char)('1' + (key - ImGuiKey_F1) % 10);

            switch (key)
            {
            case ImGuiKey_Space:       return ' ';
            case ImGuiKey_LeftShift:
            case ImGuiKey_RightShift:  return '~';
            case ImGuiKey_LeftCtrl:
            case ImGuiKey_RightCtrl:   return '^';
            case ImGuiKey_LeftAlt:
            case ImGuiKey_RightAlt:    return '@';
            case ImGuiKey_Tab:         return '\t';
            default:                   return 0;
            }
        }

        void update_keybind_capture(SettingsState& state)
        {
            if (!state.keybind_listening)
                return;

            if (ImGui::IsKeyPressed(ImGuiKey_Escape))
            {
                state.keybind_listening = false;
                return;
            }

            for (int key = (int)ImGuiKey_NamedKey_BEGIN; key < (int)ImGuiKey_NamedKey_END; ++key)
            {
                const ImGuiKey imgui_key = (ImGuiKey)key;
                if (!ImGui::IsKeyPressed(imgui_key))
                    continue;

                const char mapped = key_from_imgui(imgui_key);
                if (mapped == 0)
                    continue;

                state.menu_key = mapped;
                state.keybind_listening = false;
                return;
            }
        }

        const char* listening_label()
        {
            const double t = ImGui::GetTime();
            const int phase = (int)(t * 2.5) % 3;
            switch (phase)
            {
            case 0:  return ".";
            case 1:  return "..";
            default: return "...";
            }
        }

        ImRect keybind_anim_bounds(const ImRect& bounds, SettingsState& state)
        {
            using namespace layout;

            if (state.keybind_press_time < 0.0)
                return bounds;

            const float elapsed = (float)(ImGui::GetTime() - state.keybind_press_time);
            if (elapsed >= keybind_press_dur)
            {
                state.keybind_press_time = -1.0;
                return bounds;
            }

            const float t = elapsed / keybind_press_dur;
            const float scale = 1.f + keybind_press_scale * sinf(t * IM_PI);
            const ImVec2 center = bounds.GetCenter();
            const ImVec2 half = bounds.GetSize() * 0.5f * scale;
            return ImRect(center - half, center + half);
        }

        void draw_action_button_highlight(ImDrawList* dl, const ImRect& bounds, float round)
        {
            using namespace colors;

            const float inset = ImMin(round, bounds.GetWidth() * 0.5f);
            dl->AddLine(
                { bounds.Min.x + inset, bounds.Min.y + 0.5f },
                { bounds.Max.x - inset, bounds.Min.y + 0.5f },
                action_btn_highlight,
                1.f);
        }

        void ease_anim(float& value, float target, float dt, float speed)
        {
            if (value < target)
                value = ImMin(value + speed * dt, target);
            else if (value > target)
                value = ImMax(value - speed * dt, target);
        }

        ImRect scale_rect_centered(const ImRect& bounds, float scale)
        {
            const ImVec2 center = bounds.GetCenter();
            const ImVec2 half = bounds.GetSize() * 0.5f * scale;
            return ImRect(center - half, center + half);
        }

        float button_hover_anim(const char* label, const ImRect& bounds)
        {
            using namespace layout;

            ImGuiID id = ImGui::GetID(label);
            ImGuiStorage* storage = ImGui::GetStateStorage();
            float hover_t = storage->GetFloat(id, 0.f);

            const bool hovered = ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max);
            ease_anim(hover_t, hovered ? 1.f : 0.f, ImGui::GetIO().DeltaTime, action_btn_anim_speed);
            storage->SetFloat(id, hover_t);
            return hover_t;
        }
    }

    bool draw_action_button(ImDrawList* dl, const ImRect& bounds, float round, const char* label, float font_size)
    {
        using namespace layout;
        using namespace colors;

        const float hover_t = label ? button_hover_anim(label, bounds) : 0.f;
        const bool held = hover_t > 0.01f &&
            ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max) &&
            ImGui::IsMouseDown(ImGuiMouseButton_Left);

        float scale = 1.f + action_btn_hover_scale * hover_t;
        if (held)
            scale -= action_btn_press_scale;

        const ImRect draw_bounds = scale_rect_centered(bounds, scale);

        const ImU32 top = colors::lerp_color(action_btn_top, action_btn_hover_top, hover_t);
        const ImU32 bottom = colors::lerp_color(action_btn_bottom, action_btn_hover_bottom, hover_t);

        draw_rounded_vertical_gradient(dl, draw_bounds, round, top, bottom, action_btn_gradient_span);
        draw_action_button_highlight(dl, draw_bounds, round);

        if (hover_t > 0.01f)
        {
            const ImU32 outline = colors::with_alpha(action_btn_hover_outline, hover_t * 0.85f);
            dl->AddRect(draw_bounds.Min, draw_bounds.Max, outline, round, 0, 1.f);
        }

        ImFont* font = fonts::setting_label();
        if (font && label)
        {
            const ImVec2 text_size = font->CalcTextSizeA(font_size, FLT_MAX, 0.f, label);
            const ImVec2 text_pos = {
                draw_bounds.GetCenter().x - text_size.x * 0.5f,
                draw_bounds.GetCenter().y - text_size.y * 0.5f
            };
            dl->AddText(font, font_size, text_pos, text_active, label);
        }

        const bool clicked =
            ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max) &&
            ImGui::IsMouseClicked(ImGuiMouseButton_Left);
        return clicked;
    }

    void draw_gradient_checkbox(ImDrawList* dl, const ImRect& bounds, bool checked, bool& value)
    {
        using namespace layout;
        using namespace colors;

        // Animated checkbox
        ImGuiID id = ImGui::GetID(&value);
        ImGuiStorage* storage = ImGui::GetStateStorage();
        float anim_t = storage->GetFloat(id, checked ? 1.f : 0.f);
        
        // Smooth animation
        const float target = checked ? 1.f : 0.f;
        const float speed = 12.f;
        ease_anim(anim_t, target, ImGui::GetIO().DeltaTime, speed);
        storage->SetFloat(id, anim_t);

        // Scale animation
        const float scale = 1.f + 0.08f * sinf(anim_t * IM_PI);
        const ImRect draw_bounds = scale_rect_centered(bounds, scale);

        // Color interpolation
        if (anim_t > 0.01f)
        {
            const ImU32 top = lerp_color(panel_inner_bg, toggle_on_top, anim_t);
            const ImU32 bottom = lerp_color(panel_inner_bg, toggle_on_bottom, anim_t);
            draw_rounded_vertical_gradient(dl, draw_bounds, checkbox_round, top, bottom);
        }
        else
        {
            dl->AddRectFilled(draw_bounds.Min, draw_bounds.Max, panel_inner_bg, checkbox_round);
        }

        // Outline fades out when active
        if (anim_t < 1.f)
        {
            const ImU32 outline_col = with_alpha(toggle_outline, 1.f - anim_t);
            dl->AddRect(draw_bounds.Min, draw_bounds.Max, outline_col, checkbox_round, 0, toggle_outline_w);
        }

        // Checkmark animation
        if (anim_t > 0.2f)
        {
            const float check_t = ImClamp((anim_t - 0.2f) / 0.8f, 0.f, 1.f);
            const ImVec2 center = draw_bounds.GetCenter();
            const float size = checkbox_size * 0.4f * check_t;
            
            ImFont* icon_font = fonts::icon();
            if (icon_font)
            {
                const ImU32 check_col = with_alpha(text_active, check_t);
                draw_icon_glyph(dl, icon_font, "\xEE\x80\x83", center, check_col, 8.f); // checkmark icon
            }
        }

        if (ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max) && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
            value = !value;
    }

    void draw_keybind_field(ImDrawList* dl, const ImRect& bounds, SettingsState& state)
    {
        using namespace layout;
        using namespace colors;

        update_keybind_capture(state);

        const ImRect draw_bounds = keybind_anim_bounds(bounds, state);
        const bool listening = state.keybind_listening;
        const ImU32 bg = listening ? keybind_bg_active : keybind_bg;
        const ImU32 outline = listening ? keybind_outline_active : keybind_outline;

        dl->AddRectFilled(draw_bounds.Min, draw_bounds.Max, bg, keybind_round);
        draw_field_outline(dl, draw_bounds, outline, keybind_round, keybind_outline_w);

        const float icon_w = keybind_icon_w;
        const ImVec2 div_min = { draw_bounds.Min.x + icon_w, draw_bounds.Min.y + 4.f };
        const ImVec2 div_max = { draw_bounds.Min.x + icon_w + keybind_div_w, draw_bounds.Max.y - 4.f };
        dl->AddRectFilled(div_min, div_max, keybind_divider);

        ImFont* icon_font = fonts::icon();
        if (icon_font)
        {
            const ImU32 icon_col = listening
                ? colors::with_alpha(keybind_icon, 0.45f + 0.55f * (0.5f + 0.5f * sinf((float)ImGui::GetTime() * 6.f)))
                : keybind_icon;
            draw_icon_glyph(dl, icon_font, icon_keyboard_utf8, { draw_bounds.Min.x + icon_w * 0.5f, draw_bounds.GetCenter().y }, icon_col);
        }

        ImFont* font = fonts::setting_label();
        if (font)
        {
            const char* label = listening ? listening_label() : nullptr;
            char key_text_buf[2] = {};
            if (!listening)
            {
                key_text_buf[0] = state.menu_key;
                key_text_buf[1] = '\0';
            }

            const char* text = listening ? label : key_text_buf;
            const ImU32 text_col = listening ? keybind_waiting_text : text_active;
            const ImVec2 text_size = font->CalcTextSizeA(keybind_key_font, FLT_MAX, 0.f, text);
            const ImVec2 text_pos = {
                div_max.x + (draw_bounds.Max.x - div_max.x - text_size.x) * 0.5f,
                draw_bounds.GetCenter().y - text_size.y * 0.5f
            };
            dl->AddText(font, keybind_key_font, text_pos, text_col, text);
        }

        if (ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max) && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
        {
            state.keybind_press_time = ImGui::GetTime();
            state.keybind_listening = true;
        }
    }

    void draw_unload_button(ImDrawList* dl, const ImRect& bounds)
    {
        using namespace layout;

        if (draw_action_button(dl, bounds, unload_round, "unload", setting_row_font))
        {
            // Placeholder for unload action.
        }
    }

    void draw_rounded_slider(ImDrawList* dl, const ImRect& bounds, float& value, float min_val, float max_val, const char* label)
    {
        using namespace layout;
        using namespace colors;

        // Clamp value
        value = ImClamp(value, min_val, max_val);
        const float t = (value - min_val) / (max_val - min_val);

        // Animated hover
        ImGuiID id = label ? ImGui::GetID(label) : ImGui::GetID(&value);
        ImGuiStorage* storage = ImGui::GetStateStorage();
        float hover_t = storage->GetFloat(id, 0.f);
        
        const bool hovered = ImGui::IsMouseHoveringRect(bounds.Min, bounds.Max);
        ease_anim(hover_t, hovered ? 1.f : 0.f, ImGui::GetIO().DeltaTime, 10.f);
        storage->SetFloat(id, hover_t);

        // Background track
        const float slider_round = bounds.GetHeight() * 0.5f;
        const ImU32 track_bg = lerp_color(panel_inner_bg, with_alpha(panel_inner_bg, 0.7f), hover_t);
        dl->AddRectFilled(bounds.Min, bounds.Max, track_bg, slider_round);
        
        const ImU32 track_outline = lerp_color(toggle_outline, with_alpha(ice_blue, 0.5f), hover_t);
        dl->AddRect(bounds.Min, bounds.Max, track_outline, slider_round, 0, toggle_outline_w);

        // Filled portion with gradient
        if (t > 0.01f)
        {
            const ImVec2 fill_max = { bounds.Min.x + t * bounds.GetWidth(), bounds.Max.y };
            const ImRect fill_bounds(bounds.Min, fill_max);
            draw_rounded_vertical_gradient(dl, fill_bounds, slider_round, toggle_on_top, toggle_on_bottom);
        }

        // Handle/thumb
        const float thumb_size = bounds.GetHeight() + 8.f;
        const ImVec2 thumb_center = {
            bounds.Min.x + t * bounds.GetWidth(),
            bounds.GetCenter().y
        };
        
        const float thumb_scale = 1.f + 0.15f * hover_t;
        const float thumb_r = (thumb_size * 0.5f) * thumb_scale;
        
        // Thumb shadow
        dl->AddCircleFilled({ thumb_center.x + 1.f, thumb_center.y + 1.f }, thumb_r, 
            IM_COL32(0, 0, 0, 40), 16);
        
        // Thumb
        const ImU32 thumb_col = lerp_color(text_active, ice_blue, hover_t);
        dl->AddCircleFilled(thumb_center, thumb_r, thumb_col, 16);
        dl->AddCircle(thumb_center, thumb_r, with_alpha(ice_blue, 0.5f + 0.3f * hover_t), 16, 1.5f);

        // Interaction
        static bool dragging = false;
        static ImGuiID dragging_id = 0;
        
        if (hovered && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
        {
            dragging = true;
            dragging_id = id;
        }
        
        if (dragging && dragging_id == id)
        {
            if (ImGui::IsMouseDown(ImGuiMouseButton_Left))
            {
                const ImVec2 mouse = ImGui::GetMousePos();
                const float new_t = ImClamp((mouse.x - bounds.Min.x) / bounds.GetWidth(), 0.f, 1.f);
                value = min_val + new_t * (max_val - min_val);
            }
            else
            {
                dragging = false;
                dragging_id = 0;
            }
        }

        // Label
        if (label)
        {
            ImFont* font = fonts::body();
            if (font)
            {
                char value_text[32];
                snprintf(value_text, sizeof(value_text), "%.1f", value);
                const ImVec2 text_size = font->CalcTextSizeA(10.f, FLT_MAX, 0.f, value_text);
                const ImVec2 text_pos = {
                    bounds.GetCenter().x - text_size.x * 0.5f,
                    bounds.GetCenter().y - text_size.y * 0.5f
                };
                dl->AddText(font, 10.f, text_pos, with_alpha(text_active, 0.8f), value_text);
            }
        }
    }
}
