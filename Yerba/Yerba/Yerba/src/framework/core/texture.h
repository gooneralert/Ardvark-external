#pragma once

#include <d3d11.h>
#include <cstddef>

struct AppTexture
{
    ID3D11ShaderResourceView* srv = nullptr;
    int width = 0;
    int height = 0;

    explicit operator bool() const { return srv != nullptr; }
};

bool texture_load_file(ID3D11Device* device, const char* path, AppTexture& out);
bool texture_load_memory(ID3D11Device* device, const unsigned char* data, size_t size, AppTexture& out);
void texture_release(AppTexture& tex);
