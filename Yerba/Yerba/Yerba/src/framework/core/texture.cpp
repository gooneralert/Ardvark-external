#include "texture.h"

#include "stb_image.h"

static bool texture_upload_rgba(ID3D11Device* device, unsigned char* rgba, int w, int h, AppTexture& out)
{
    if (!rgba || w <= 0 || h <= 0)
    {
        if (rgba)
            stbi_image_free(rgba);
        return false;
    }

    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = (UINT)w;
    desc.Height = (UINT)h;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

    D3D11_SUBRESOURCE_DATA sub = {};
    sub.pSysMem = rgba;
    sub.SysMemPitch = (UINT)(w * 4);

    ID3D11Texture2D* tex = nullptr;
    HRESULT hr = device->CreateTexture2D(&desc, &sub, &tex);
    stbi_image_free(rgba);
    if (FAILED(hr) || !tex)
        return false;

    D3D11_SHADER_RESOURCE_VIEW_DESC srv_desc = {};
    srv_desc.Format = desc.Format;
    srv_desc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
    srv_desc.Texture2D.MipLevels = 1;

    hr = device->CreateShaderResourceView(tex, &srv_desc, &out.srv);
    tex->Release();
    if (FAILED(hr))
        return false;

    out.width = w;
    out.height = h;
    return true;
}

bool texture_load_file(ID3D11Device* device, const char* path, AppTexture& out)
{
    texture_release(out);
    if (!device || !path || !path[0])
        return false;

    int w = 0, h = 0, n = 0;
    unsigned char* rgba = stbi_load(path, &w, &h, &n, 4);
    if (!rgba)
        return false;

    return texture_upload_rgba(device, rgba, w, h, out);
}

bool texture_load_memory(ID3D11Device* device, const unsigned char* data, size_t size, AppTexture& out)
{
    texture_release(out);
    if (!device || !data || size == 0)
        return false;

    int w = 0, h = 0, n = 0;
    unsigned char* rgba = stbi_load_from_memory(data, (int)size, &w, &h, &n, 4);
    if (!rgba)
        return false;

    return texture_upload_rgba(device, rgba, w, h, out);
}

void texture_release(AppTexture& tex)
{
    if (tex.srv)
    {
        tex.srv->Release();
        tex.srv = nullptr;
    }
    tex.width = 0;
    tex.height = 0;
}
