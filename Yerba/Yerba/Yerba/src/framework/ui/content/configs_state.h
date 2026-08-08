#pragma once

namespace menu::ui
{
    struct ConfigsState
    {
        static constexpr int max_configs = 64;

        char new_name[64] = "new config";
        char names[max_configs][64] = {};
        int count = 0;
        int selected = -1;
    };
}
