#include "logo.h"

#include <string>
#include <windows.h>

#include "texture.h"
#include "../data/logoV2.h"

namespace menu::logo
{
    namespace
    {
        AppTexture g_logo = {};
        bool g_tried_load = false;

        void ensure_loaded(ID3D11Device* device)
        {
            if (g_tried_load || g_logo)
                return;

            g_tried_load = true;
            if (!device)
                return;

            // Load logo from embedded bytes
            texture_load_memory(device, LogoV2, sizeof(LogoV2), g_logo);
        }
    }

    void init(ID3D11Device* device)
    {
        ensure_loaded(device);
    }

    void shutdown()
    {
        texture_release(g_logo);
        g_tried_load = false;
    }

    bool draw(ImDrawList* dl, ImVec2 pos, float max_size)
    {
        if (!dl || max_size <= 0.f || !g_logo)
            return false;

        const float aspect = (float)g_logo.width / (float)g_logo.height;
        ImVec2 size = { max_size, max_size };
        if (aspect > 1.f)
            size.y = max_size / aspect;
        else
            size.x = max_size * aspect;

        const ImVec2 min = pos;
        const ImVec2 max = { pos.x + size.x, pos.y + size.y };
        dl->AddImage((ImTextureID)g_logo.srv, min, max);
        return true;
    }
}
