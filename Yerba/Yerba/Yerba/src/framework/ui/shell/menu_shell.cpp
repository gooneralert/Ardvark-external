#include "ui/shell/menu_shell.h"

#include "imgui_impl_dx11.h"
#include "imgui_internal.h"
#include "config/layout.h"
#include "config/theme.h"
#include "core/fonts.h"
#include "core/logo.h"
#include "ui/background/dot_pattern.h"
#include "ui/header/header_background.h"
#include "ui/header/header_bar.h"
#include "ui/content/content_panels.h"
#include "ui/separator/header_separator.h"
#include "ui/shell/window_drag.h"
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
namespace menu
{
    namespace
    {
        ui::HeaderState g_header;

        void draw_shell_outlines(ImDrawList* dl, const ImRect& panel)
        {
            using namespace layout;
            using namespace colors;

            dl->AddRect(panel.Min, panel.Max, with_alpha(ice_blue, outline_opacity), corner_r, 0, ice_border);

            const ImRect outer(
                { panel.Min.x - outer_border, panel.Min.y - outer_border },
                { panel.Max.x + outer_border, panel.Max.y + outer_border });
            dl->AddRect(outer.Min, outer.Max, with_alpha(border_black, shell_outer_opacity), corner_r + outer_border, 0, outer_border);
        }
    }

    void apply_style()
    {
        ImGuiStyle& style = ImGui::GetStyle();
        style.WindowRounding = layout::corner_r;
        style.WindowBorderSize = 0.f;
        style.WindowPadding = ImVec2(0.f, 0.f);
        style.FrameRounding = 6.f;
        style.ScrollbarSize = 8.f;
        style.AntiAliasedLines = true;
        style.AntiAliasedFill = true;

        ImVec4* c = style.Colors;
        c[ImGuiCol_WindowBg] = ImVec4(0.f, 0.f, 0.f, 0.f);
        c[ImGuiCol_Text] = ImVec4(1.f, 1.f, 1.f, 1.f);
        c[ImGuiCol_Border] = ImVec4(0.1f, 0.1f, 0.12f, 1.f);
    }

    void init(ID3D11Device* device)
    {
        fonts::init();
        logo::init(device);
        apply_style();
    }

    void shutdown()
    {
        logo::shutdown();
        fonts::shutdown();
    }

    void draw_menu(ImDrawList* dl, HWND hwnd, const ImVec2& origin)
    {
        using namespace layout;
        using namespace colors;

        // Menu open animation
        static float menu_open_t = 0.f;
        if (ImGui::IsWindowAppearing())
            menu_open_t = 0.f;
        if (menu_open_t < 1.f)
            menu_open_t = ImMin(menu_open_t + ImGui::GetIO().DeltaTime * 5.f, 1.f);
        
        const float fade_t = ImClamp(menu_open_t / 0.3f, 0.f, 1.f);
        const float scale_t = 0.95f + 0.05f * menu_open_t;

        const ImRect panel(
            { origin.x + outer_border, origin.y + outer_border },
            { origin.x + outer_border + window_w, origin.y + outer_border + window_h });

        // Apply scale animation
        const ImVec2 panel_center = panel.GetCenter();
        const ImVec2 panel_half = panel.GetSize() * 0.5f * scale_t;
        const ImRect anim_panel(panel_center - panel_half, panel_center + panel_half);

        const ImRect header_rect(
            anim_panel.Min,
            { anim_panel.Max.x, anim_panel.Min.y + header_h });

        const ImRect separator_rect(
            { anim_panel.Min.x, header_rect.Max.y },
            { anim_panel.Max.x, header_rect.Max.y + separator_h });

        const ImRect body_rect(
            { anim_panel.Min.x, separator_rect.Max.y },
            anim_panel.Max);

        // Apply fade
        const ImU32 body_bg_anim = with_alpha(body_bg, fade_t);
        const ImU32 header_top_anim = with_alpha(header_bg_top, fade_t);
        const ImU32 header_bottom_anim = with_alpha(header_bg_bottom, fade_t);
        const ImU32 body_dot_anim = with_alpha(body_dot, fade_t);

        dl->AddRectFilled(anim_panel.Min, anim_panel.Max, body_bg_anim, corner_r);
        ui::draw_header_background(dl, header_rect, corner_r);

        const ImRect header_sep_fill(
            { anim_panel.Min.x, header_rect.Max.y - 1.f },
            separator_rect.Max);
        dl->AddRectFilled(header_sep_fill.Min, header_sep_fill.Max, header_bottom_anim);

        dl->AddRectFilled(separator_rect.Min, separator_rect.Max, header_bottom_anim);
        dl->AddRectFilled(body_rect.Min, body_rect.Max, body_bg_anim, corner_r, ImDrawFlags_RoundCornersBottom);
        ui::draw_dot_grid(dl, body_rect);

        // Fade content
        if (fade_t > 0.1f)
        {
            ui::draw_header_bar(dl, header_rect, g_header);
            ui::draw_header_separator(dl, separator_rect);
            ui::draw_content_panels(dl, anim_panel, separator_rect.Max.y, g_header.search, g_header.active);
        }
        
        draw_shell_outlines(dl, anim_panel);
        ui::handle_window_drag(hwnd, header_rect, g_header);
    }

    void render(HWND hwnd)
    {
        using namespace layout;

        fonts::use_menu();

        ImGui::SetNextWindowPos({ 0.f, 0.f }, ImGuiCond_Always);
        ImGui::SetNextWindowSize({ client_w, client_h }, ImGuiCond_Always);

        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoTitleBar |
            ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoScrollbar |
            ImGuiWindowFlags_NoCollapse |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoBackground;

        ImGui::Begin("##menu_shell", nullptr, flags);

        ImDrawList* dl = ImGui::GetWindowDrawList();
        const ImVec2 win_pos = ImGui::GetWindowPos();
        draw_menu(dl, hwnd, win_pos);

        ImGui::End();
    }

    void init_loader_context(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        ImGui_ImplDX11_Init(device, context);
        apply_style();
        fonts::init_loader();
    }

    void shutdown_loader_context()
    {
        ImGui_ImplDX11_Shutdown();
    }
}
