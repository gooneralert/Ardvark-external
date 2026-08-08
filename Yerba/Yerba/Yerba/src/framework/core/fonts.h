#pragma once

#include <vector>
#include <memory>
#include <cstddef>
#include "imgui.h"

class AppFonts
{
public:
    void update();
    ImFont* get(const std::vector<unsigned char>& font_data, float size, bool bold = false, bool display_font = false);
    ImFont* get_icon(float size);

private:
    struct font_entry
    {
        std::vector<unsigned char> data;
        float size = 0.f;
        bool bold = false;
        bool display_font = false;
        ImFont* font = nullptr;
    };

    void add(const std::vector<unsigned char>& font_data, float size, bool bold, bool display_font = false);
    std::vector<font_entry> entries;
    float icon_size = 0.f;
    ImFont* icon_font = nullptr;
    bool dirty = true;
};

namespace fonts
{
    void init();
    void init_loader();
    void shutdown();
    void use_menu();
    void use_loader();

    ImFont* regular();
    ImFont* bold();
    ImFont* body();
    ImFont* nav();
    ImFont* search();
    ImFont* panel_title();
    ImFont* setting_label();
    ImFont* icon();
    ImFont* logo();
}
