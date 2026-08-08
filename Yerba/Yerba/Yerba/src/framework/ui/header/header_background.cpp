#include "ui/header/header_background.h"

#include "imgui.h"
#include "config/theme.h"
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>
namespace menu::ui
{
    void draw_header_background(ImDrawList* dl, const ImRect& bounds, float corner_r)
    {
        using namespace colors;

        const float height = bounds.GetHeight();
        if (height <= 0.f)
            return;

        constexpr int strips = 28;
        const float round_span = (corner_r > 0.f)
            ? ImMin(corner_r + 2.f, height)
            : 0.f;

        if (round_span > 0.f)
        {
            const float grad_t = (round_span * 0.5f) / height;
            const ImU32 col = lerp_color(header_bg_top, header_bg_bottom, grad_t);
            dl->AddRectFilled(
                bounds.Min,
                { bounds.Max.x, bounds.Min.y + round_span },
                col,
                corner_r,
                ImDrawFlags_RoundCornersTop);
        }

        const float rest_h = height - round_span;
        if (rest_h <= 0.f)
            return;

        for (int i = 0; i < strips; ++i)
        {
            const float t0 = (float)i / (float)strips;
            const float t1 = (float)(i + 1) / (float)strips;
            const float tm = (t0 + t1) * 0.5f;
            const float grad_t = (round_span + rest_h * tm) / height;
            const ImU32 col = lerp_color(header_bg_top, header_bg_bottom, grad_t);

            const ImVec2 strip_min = { bounds.Min.x, bounds.Min.y + round_span + rest_h * t0 };
            const ImVec2 strip_max = { bounds.Max.x, bounds.Min.y + round_span + rest_h * t1 };
            dl->AddRectFilled(strip_min, strip_max, col);
        }
    }
}
