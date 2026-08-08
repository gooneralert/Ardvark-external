#pragma once
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
    struct SettingsState
    {
        bool vsync = false;
        bool show_watermark = true;
        bool streamproof = false;
        bool dex_explorer = false;
        bool keybind_list = false;
        bool accent_color = true;
        char menu_key = 'z';
        bool keybind_listening = false;
        double keybind_press_time = -1.0;
        
        // Color picker
        float global_accent[4] = { 0.19f, 0.44f, 0.61f, 1.0f }; // Default blue accent
        bool color_picker_open = false;
        bool rainbow_mode = false;
        
        // Test slider
        float test_slider_value = 50.f;
    };
}
