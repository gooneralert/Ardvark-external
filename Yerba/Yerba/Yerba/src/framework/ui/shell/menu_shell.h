#pragma once

#include <windows.h>

#include "imgui.h"

struct ID3D11Device;
struct ID3D11DeviceContext;

namespace menu
{
    void apply_style();
    void init(ID3D11Device* device);
    void shutdown();
    void render(HWND hwnd);
    void init_loader_context(ID3D11Device* device, ID3D11DeviceContext* context);
    void shutdown_loader_context();
}
/// <summary>
/// made by maybach_gh from dopamina 
/// </summary>