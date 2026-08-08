#include "fonts.h"
#include "../data/fonts_data.h"
#include "../data/icon_font_data.h"
#include "imgui_impl_dx11.h"

namespace
{
    std::unique_ptr<AppFonts> g_menu_fonts = std::make_unique<AppFonts>();
    std::unique_ptr<AppFonts> g_loader_fonts = std::make_unique<AppFonts>();
    AppFonts* g_active_fonts = g_menu_fonts.get();

    AppFonts& active()
    {
        return *g_active_fonts;
    }

    void build_pool(AppFonts& pool)
    {
        pool.get(inter, 14.f);
        pool.get(inter, 14.f, true);
        pool.get(inter, 15.f);
        pool.get(inter, 15.f, true);
        pool.get(inter, 28.f, true);
        pool.get_icon(13.f);
        pool.update();
    }
}

void AppFonts::update()
{
    if (!dirty)
        return;

    ImFontConfig cfg;
    cfg.FontDataOwnedByAtlas = false;

    ImGuiIO& io = ImGui::GetIO();
    io.Fonts->Clear();
    icon_font = nullptr;

    for (auto& entry : entries)
    {
        const ImWchar* ranges = entry.display_font
            ? io.Fonts->GetGlyphRangesDefault()
            : io.Fonts->GetGlyphRangesCyrillic();

        entry.font = io.Fonts->AddFontFromMemoryTTF(entry.data.data(), (int)entry.data.size(),
            entry.size, &cfg, ranges);
    }

    if (icon_size > 0.f)
    {
        ImFontConfig icon_cfg;
        icon_cfg.FontDataOwnedByAtlas = false;
        icon_font = io.Fonts->AddFontFromMemoryTTF(
            (void*)icon_font_ttf, (int)icon_font_ttf_size, icon_size, &icon_cfg, icon_font_range);
    }

    io.Fonts->Build();
    ImGui_ImplDX11_CreateDeviceObjects();
    dirty = false;
}

ImFont* AppFonts::get(const std::vector<unsigned char>& font_data, float size, bool bold, bool display_font)
{
    for (auto& entry : entries)
    {
        if (entry.data == font_data && entry.size == size && entry.bold == bold
            && entry.display_font == display_font)
        {
            return entry.font;
        }
    }

    add(font_data, size, bold, display_font);
    update();
    return get(font_data, size, bold, display_font);
}

ImFont* AppFonts::get_icon(float size)
{
    if (icon_size != size || !icon_font)
    {
        icon_size = size;
        icon_font = nullptr;
        dirty = true;
        update();
    }
    return icon_font;
}

void AppFonts::add(const std::vector<unsigned char>& font_data, float size, bool bold, bool display_font)
{
    entries.push_back({ font_data, size, bold, display_font, nullptr });
    dirty = true;
}

namespace fonts
{
    void use_menu()
    {
        g_active_fonts = g_menu_fonts.get();
    }

    void use_loader()
    {
        g_active_fonts = g_loader_fonts.get();
    }

    void init()
    {
        use_menu();
        build_pool(active());
    }

    void init_loader()
    {
        use_loader();
        build_pool(active());
    }

    void shutdown()
    {
        g_menu_fonts = std::make_unique<AppFonts>();
        g_loader_fonts = std::make_unique<AppFonts>();
        g_active_fonts = g_menu_fonts.get();
    }

    ImFont* regular()
    {
        return active().get(inter, 15.f);
    }

    ImFont* bold()
    {
        return active().get(inter, 15.f, true);
    }

    ImFont* body()
    {
        return active().get(inter, 14.f);
    }

    ImFont* nav()
    {
        return active().get(inter, 14.f, true);
    }

    ImFont* search()
    {
        return active().get(inter, 14.f, true);
    }

    ImFont* panel_title()
    {
        return active().get(inter, 15.f, true);
    }

    ImFont* setting_label()
    {
        return active().get(inter, 15.f, true);
    }

    ImFont* icon()
    {
        return active().get_icon(13.f);
    }

    ImFont* logo()
    {
        return active().get(inter, 28.f, true);
    }
}
