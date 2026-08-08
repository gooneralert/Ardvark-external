#include "ui/background/dot_pattern.h"

#include "config/layout.h"
#include "config/theme.h"
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>//https://discord.gg/dopamina
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
    void draw_dot_grid(ImDrawList* dl, const ImRect& bounds)
    {
        using namespace layout;
        using namespace colors;

        dl->PushClipRect(bounds.Min, bounds.Max, true);
        for (float y = bounds.Min.y + dot_spacing * 0.5f; y < bounds.Max.y; y += dot_spacing)
        {
            for (float x = bounds.Min.x + dot_spacing * 0.5f; x < bounds.Max.x; x += dot_spacing)
                dl->AddCircleFilled({ x, y }, dot_radius, body_dot);
        }
        dl->PopClipRect();
    }
}
