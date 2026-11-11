#include "kawarimi.h"
#include "game.h"
#include "hook.h"
#include <intrin.h>
#include <mutex>
#include <tuple>

namespace kw {

GetSlotFn g_origGetSlot = nullptr;
std::atomic<bool> g_ovActive{false};
std::atomic<bool> g_ovLookup{true};

namespace {

std::string g_ovChar;
int g_ovId = -1;
char g_ovCode[8] = {0};
std::vector<uint32_t> g_ovAllow;

const char* RealCodeOfChar(int chr, char out[9]) {
    if (!g_base) return nullptr;
    auto mgr = *(uintptr_t*)(g_base + game::kCharMgr);
    if (!mgr) return nullptr;
    auto tbl = *(uintptr_t*)(mgr + 0xf8);
    if (!tbl) return nullptr;
    int count = *(int*)(tbl + 0x18);
    auto arr = *(const char**)(tbl + 0x20);
    if (!arr || chr < 0 || chr >= count) return nullptr;
    memcpy(out, arr + (size_t)chr * 8, 8);
    out[8] = 0;
    return out;
}

int FreeIdxs(int chr, int* out, int max) {
    const char* def = g_origGetSlot(chr, 0);
    int n = 0;
    if (!def) return 0;
    for (int i = game::kFirstCostumeIdx; i <= game::kLastCostumeIdx && n < max; i++)
        if (g_origGetSlot(chr, i) == def) out[n++] = i;
    return n;
}

void TraceLookup(int chr, int idx, uint32_t ret) {
    static std::mutex mu;
    static std::map<std::tuple<int, int, uint32_t, uint64_t>, int> seen;
    static int total = 0;
    uint64_t bucket = GetTickCount64() / 2000;
    std::lock_guard<std::mutex> lk(mu);
    int& n = seen[{chr, idx, ret, bucket}];
    if (n++ < 2 && total++ < 8000) Log("[t=%llu] GetSlot chr=%d idx=%d ret=+0x%x", (unsigned long long)(bucket % 100000), chr, idx, ret);
}

bool OverrideApplies(int chr, uint32_t ret) {
    bool match = g_ovId == chr;
    if (!match && !g_ovChar.empty()) {
        char code[9];
        match = CodeOfChar(chr, code) && g_ovChar == code;
    }
    if (match && !g_ovAllow.empty()) {
        match = false;
        for (uint32_t a : g_ovAllow)
            if (a == ret) {
                match = true;
                break;
            }
    }
    return match;
}

const std::vector<Skin>* SkinsOf(const Snapshot* s, int chr) {
    auto byId = s->byId.find(chr);
    if (byId != s->byId.end()) return &byId->second;
    char code[9];
    if (!CodeOfChar(chr, code)) return nullptr;
    auto it = s->byCode.find(code);
    return it != s->byCode.end() ? &it->second : nullptr;
}

}

CodeFn CodeOfChar = RealCodeOfChar;

const char* Hook_GetSlot(int chr, int idx) {
    uint32_t ret = (uint32_t)((uintptr_t)_ReturnAddress() - g_base);
    if (g_trace) TraceLookup(chr, idx, ret);
    if (g_ovActive && g_ovLookup && NewCodesAllowed() && idx >= 0 && OverrideApplies(chr, ret)) return g_ovCode;
    const Snapshot* s = (NewCodesAllowed() && g_strategy == 0) ? g_snap.load() : nullptr;
    if (s && idx >= game::kFirstCostumeIdx && idx <= game::kLastCostumeIdx) {
        if (auto list = SkinsOf(s, chr)) {
            int fr[20], nf = FreeIdxs(chr, fr, 20);
            for (int k = 0; k < nf && k < (int)list->size(); k++)
                if (fr[k] == idx) return (*list)[k].code;
        }
    }
    return g_origGetSlot(chr, idx);
}

void SetOverrideCallers(const std::string& hexList) {
    const char* c = hexList.c_str();
    while (*c) {
        char* e;
        unsigned long v = strtoul(c, &e, 16);
        if (e == c) {
            c++;
            continue;
        }
        g_ovAllow.push_back((uint32_t)v);
        c = e;
    }
}

void InstallCostumeHooks() {
    void* target = (void*)(g_base + game::kGetSlot);
    if (InstallHook(target, game::kGetSlotPrologue, sizeof game::kGetSlotPrologue, (void*)Hook_GetSlot, (void**)&g_origGetSlot))
        Log("costume lookup hook installed at %p", target);
    else
        Log("costume lookup hook NOT installed (prologue mismatch - different game build?)");
}

namespace {

std::mutex g_activeMu;
std::map<std::string, std::string> g_activeByCostume;
std::string g_lastCostume;

std::string Lower(std::string s) {
    for (auto& c : s) c = (char)tolower((unsigned char)c);
    return s;
}

std::string LastSegment(std::string& d) {
    while (!d.empty() && (d.back() == '\\' || d.back() == '/')) d.pop_back();
    size_t cut = d.find_last_of("\\/");
    std::string seg = cut == std::string::npos ? d : d.substr(cut + 1);
    d = cut == std::string::npos ? "" : d.substr(0, cut);
    return seg;
}

std::string ModName(const Skin& k) {
    std::string d = k.dir;
    std::string seg = LastSegment(d);
    if (Lower(seg) == "spc") {
        std::string up = d;
        if (Lower(LastSegment(up)) == "data") {
            d = up;
            seg = LastSegment(d);
        }
    }
    if (Lower(seg) == "files") seg = LastSegment(d);
    return seg;
}

std::vector<std::pair<std::string, Skin>> SkinsForCostume(const std::string& costume) {
    std::vector<std::pair<std::string, Skin>> out;
    const Snapshot* snap = g_snap.load();
    if (!snap) return out;
    CostumeInfo info;
    std::string character = FindCostume(costume, info) ? Lower(info.character) : "";
    for (auto& e : snap->byCode)
        for (auto& k : e.second) {
            bool replaces = !k.replaces.empty() && Lower(k.replaces) == Lower(costume);
            bool adds = k.replaces.empty() && !character.empty() && Lower(e.first) == character;
            if (replaces || adds) out.push_back({e.first, k});
        }
    return out;
}

std::string Position(const CostumeInfo& info) {
    int total = 0, pos = SelectorPosition(info.chrId, info.slot, &total);
    return pos > 0 ? "LT/RT #" + std::to_string(pos) + " of " + std::to_string(total) : "";
}

std::string Label(const std::string& costume) {
    CostumeInfo info;
    if (!FindCostume(costume, info)) return costume;
    std::string pos = Position(info);
    return DisplayName(info.character) + " " + costume + (pos.empty() ? "" : " (" + pos + ")");
}

std::vector<std::string> SiblingCostumes(const std::string& costume, bool (*keep)(const std::string&)) {
    CostumeInfo info;
    std::vector<std::string> out;
    if (!FindCostume(costume, info)) return out;
    for (auto& other : CostumesOf(info.chrId))
        if (other != costume && keep(other)) out.push_back(other);
    return out;
}

bool HasMods(const std::string& costume) {
    return !SkinsForCostume(costume).empty();
}

bool HasActiveMod(const std::string& costume) {
    return !ActiveSkinFor(costume).empty();
}

std::string ModdedCostumes(const std::string& costume) {
    std::string out;
    for (auto& other : SiblingCostumes(costume, HasMods)) {
        CostumeInfo o;
        FindCostume(other, o);
        std::string pos = Position(o);
        out += (out.empty() ? "" : ", ") + other + (pos.empty() ? "" : " (" + pos + ")");
    }
    return out;
}

std::string ActiveModName(const std::string& costume) {
    std::string code = ActiveSkinFor(costume);
    for (auto& [_, k] : SkinsForCostume(costume))
        if (code == k.code) return ModName(k);
    return "";
}

void Feedback(bool ok) {
    MessageBeep(ok ? MB_OK : MB_ICONHAND);
}

void Activate(const std::string& costume, const std::string& character, const Skin& k) {
    {
        std::lock_guard<std::mutex> lk(g_activeMu);
        g_activeByCostume[Lower(costume)] = std::string(k.code, strnlen(k.code, 8));
    }
    if (k.replaces.empty()) {
        g_ovActive = false;
        g_ovChar = character;
        g_ovId = -1;
        memset(g_ovCode, 0, 8);
        memcpy(g_ovCode, k.code, strnlen(k.code, 7));
        g_ovLookup = true;
        g_ovActive = true;
    }
}

const char* kReload = "To see it now: press LT then RT (costume away and back), or just start the fight.";

void NextSkin() {
    std::string costume = LastCostume();
    if (costume.empty()) {
        Feedback(false);
        Log("HOTKEY F7: move the cursor onto a character in character select first, then press F7.");
        return;
    }
    auto skins = SkinsForCostume(costume);
    if (skins.empty()) {
        auto modded = SiblingCostumes(costume, HasMods);
        if (modded.size() == 1) {
            costume = modded[0];
            skins = SkinsForCostume(costume);
        }
    }
    if (skins.empty()) {
        Feedback(false);
        std::string elsewhere = ModdedCostumes(costume);
        if (elsewhere.empty())
            Log("HOTKEY F7: no mod for %s. Make one with the Kawarimi tool (menu 1).", Label(costume).c_str());
        else
            Log("HOTKEY F7: no mod for %s. Mods exist for %s - switch costume with LT/RT, then press F7.", Label(costume).c_str(), elsewhere.c_str());
        return;
    }
    std::string current = ActiveSkinFor(costume);
    size_t next = 0;
    for (size_t i = 0; i < skins.size(); i++)
        if (current == skins[i].second.code) next = (i + 1) % skins.size();
    const Skin& k = skins[next].second;
    Activate(costume, skins[next].first, k);
    Feedback(true);
    Log("HOTKEY F7: %s -> %s (mod %zu of %zu). %s", Label(costume).c_str(), ModName(k).c_str(), next + 1, skins.size(), kReload);
}

void SkinOff() {
    std::string costume = LastCostume();
    if (!HasActiveMod(costume)) {
        auto active = SiblingCostumes(costume, HasActiveMod);
        if (active.size() == 1) costume = active[0];
    }
    bool had;
    {
        std::lock_guard<std::mutex> lk(g_activeMu);
        had = g_activeByCostume.erase(Lower(costume)) > 0;
    }
    g_ovActive = false;
    Feedback(had);
    if (had)
        Log("HOTKEY F8: %s -> original. %s", Label(costume).c_str(), kReload);
    else
        Log("HOTKEY F8: no mod is on for %s.", costume.empty() ? "this costume" : Label(costume).c_str());
}

std::mutex g_namesMu;
std::map<std::string, std::string> g_familyNames;

}

void SetFamilyName(const std::string& family, const std::string& name) {
    std::lock_guard<std::mutex> lk(g_namesMu);
    g_familyNames[Lower(family)] = name;
}

std::string DisplayName(const std::string& characterCode) {
    std::lock_guard<std::mutex> lk(g_namesMu);
    auto it = characterCode.size() == 4 ? g_familyNames.find(Lower(characterCode.substr(1))) : g_familyNames.end();
    return it == g_familyNames.end() ? characterCode : it->second;
}

void NoteCostume(const std::string& costume) {
    {
        std::lock_guard<std::mutex> lk(g_activeMu);
        if (g_lastCostume == costume) return;
        g_lastCostume = costume;
    }
    CostumeInfo info;
    if (!FindCostume(costume, info)) return;
    size_t mods = SkinsForCostume(costume).size();
    std::string active = ActiveModName(costume), elsewhere = mods ? "" : ModdedCostumes(costume);
    std::string state = mods == 0        ? (elsewhere.empty() ? "no mods" : "no mods here, mods on " + elsewhere)
                        : active.empty() ? std::to_string(mods) + " mod(s) - press F7"
                                         : "mod on: " + active + " (F7 next, F8 original)";
    Log("COSTUME %s  |  %s", Label(costume).c_str(), state.c_str());
}

std::string LastCostume() {
    std::lock_guard<std::mutex> lk(g_activeMu);
    return g_lastCostume;
}

std::string ActiveSkinFor(const std::string& costume) {
    std::lock_guard<std::mutex> lk(g_activeMu);
    auto it = g_activeByCostume.find(Lower(costume));
    return it == g_activeByCostume.end() ? "" : it->second;
}

void PollHotkeys() {
    static bool f7 = false, f8 = false;
    HWND fg = GetForegroundWindow();
    DWORD pid = 0;
    if (fg) GetWindowThreadProcessId(fg, &pid);
    if (pid != GetCurrentProcessId()) return;
    bool next = (GetAsyncKeyState(VK_F7) & 0x8000) != 0, off = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
    if (next && !f7) NextSkin();
    if (off && !f8) SkinOff();
    f7 = next;
    f8 = off;
}

}
