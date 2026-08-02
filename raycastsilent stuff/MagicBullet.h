#pragma once
#include "../../Roblox/Math/Math.h"
#include <cstdint>

namespace Cheat {
    namespace Features {

        namespace MagicBullet {

            bool Install();
            void Remove();
            void Ensure(bool want);

            void SetActive(bool on, const Vector3& world_target = {});

            bool Ready();
            bool Aiming();

        }
    }
}
