#include "ui/separator/gradient_separator.h"

#include <cmath>

#include "config/layout.h"
#include "config/theme.h"
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
        ImU32 separator_color_at(float t)
        {
            using namespace colors;

            static constexpr ImU32 stops[] = {
                sep_grad_0,
                sep_grad_1,
                sep_grad_2,
                sep_grad_3,
                sep_grad_4,
                sep_grad_5,
            };

            if (t <= 0.f)
                return stops[0];
            if (t >= 1.f)
                return stops[5];

            const float scaled = t * 5.f;
            const int idx = (int)scaled;
            const float local = scaled - (float)idx;
            return lerp_color(stops[idx], stops[idx + 1], local);
        }
    }

    void draw_gradient_separator(ImDrawList* dl, const ImRect& bounds, bool horizontal_fade, float fade_power)
    {
        using namespace layout;
        using namespace colors;

        if (fade_power < 0.f)
            fade_power = separator_fade_power;

        const float width = bounds.GetWidth();
        const float height = bounds.GetHeight();
        if (width <= 0.f || height <= 0.f)
            return;

        constexpr int v_strips = 24;

        for (int vx = 0; vx < separator_segments; ++vx)
        {
            const float tx0 = (float)vx / (float)separator_segments;
            const float tx1 = (float)(vx + 1) / (float)separator_segments;
            const float x_mid = (tx0 + tx1) * 0.5f;

            float h_alpha = 1.f;
            if (horizontal_fade)
            {
                const float dist = fabsf(x_mid - 0.5f) * 2.f;
                h_alpha = powf(1.f - dist, fade_power);
                if (h_alpha <= 0.04f)
                    continue;
            }

            const ImVec2 col_min = { bounds.Min.x + width * tx0, bounds.Min.y };
            const ImVec2 col_max = { bounds.Min.x + width * tx1, bounds.Max.y };

            for (int vy = 0; vy < v_strips; ++vy)
            {
                const float ty0 = (float)vy / (float)v_strips;
                const float ty1 = (float)(vy + 1) / (float)v_strips;
                const float ty_mid = (ty0 + ty1) * 0.5f;

                const ImU32 col = with_alpha(separator_color_at(ty_mid), h_alpha);
                const ImVec2 seg_min = { col_min.x, bounds.Min.y + height * ty0 };
                const ImVec2 seg_max = { col_max.x, bounds.Min.y + height * ty1 };
                dl->AddRectFilled(seg_min, seg_max, col);
            }
        }
    }
}
