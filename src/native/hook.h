#pragma once
#include <cstddef>
#include <cstdint>

namespace kw {

bool InstallHook(void* target, const uint8_t* expect, size_t stolen, void* detour, void** orig);
int PatchIat(void* module, void* original, void* replacement);

}
