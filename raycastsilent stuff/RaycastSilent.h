#pragma once
#include "../../Roblox/Math/Math.h"
#include <cstdint>

namespace Cheat {
    namespace Features {
        namespace RaycastSilent {

            bool Install();
            void Remove();

            void Ensure(bool want = true);

            void SetActive(bool on, const Vector3& world_target = {}, bool wallbang = false);

            bool Ready();
            bool Aiming();
            bool WallbangMode();
            std::uintptr_t OriginalHandler();

        }
    }
}
