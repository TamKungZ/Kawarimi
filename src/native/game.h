#pragma once
#include <cstdint>

namespace kw::game {

constexpr uintptr_t kImageBase = 0x140000000;
constexpr uintptr_t Rva(uintptr_t va) {
    return va - kImageBase;
}

constexpr uintptr_t kGetSlot = Rva(0x140529718);
constexpr uintptr_t kCharMgr = Rva(0x14161C890);
constexpr uintptr_t kEntryLookup = Rva(0x140527c74);
constexpr uintptr_t kCriFind = Rva(0x140001b20);
constexpr uintptr_t kCriLoad = Rva(0x140006e40);
constexpr uintptr_t kSlotStatus = Rva(0x140a91160);
constexpr uintptr_t kSlotDone = Rva(0x140a91190);
constexpr uintptr_t kSlotLoadReturn = Rva(0x140a5851c);

constexpr uint8_t kGetSlotPrologue[10] = {0x48, 0x89, 0x5c, 0x24, 0x08, 0x57, 0x48, 0x83, 0xec, 0x20};
constexpr uint8_t kEntryLookupPrologue[9] = {0x48, 0x89, 0x5c, 0x24, 0x08, 0x4c, 0x8b, 0x49, 0x20};
constexpr uint8_t kCriFindPrologue[5] = {0x48, 0x89, 0x5c, 0x24, 0x08};
constexpr uint8_t kCriLoadPrologue[5] = {0x48, 0x89, 0x5c, 0x24, 0x08};
constexpr uint8_t kSlotStatusPrologue[8] = {0x48, 0x83, 0xec, 0x28, 0x48, 0x8b, 0x49, 0x08};
constexpr uint8_t kSlotDonePrologue[5] = {0x48, 0x89, 0x5c, 0x24, 0x10};

constexpr int kRecordSize = 0x2d8;
constexpr int kMaxSlot = 90;
constexpr int kFirstCostumeIdx = 1;
constexpr int kLastCostumeIdx = 19;
constexpr size_t kSelectorEntrySize = 40;
constexpr uint32_t kVirtualBinderId = 0x4b415752;
constexpr uint8_t kEncryptedSignature[8] = {0x8f, 0xcf, 0x31, 0x40, 0x76, 0x41, 0x95, 0xd1};

}
