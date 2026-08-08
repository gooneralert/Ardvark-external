#pragma once

#include "imgui.h"

struct ID3D11Device;

namespace menu::logo
{
    void init(ID3D11Device* device);
    void shutdown();
    bool draw(ImDrawList* dl, ImVec2 pos, float max_size);
}
