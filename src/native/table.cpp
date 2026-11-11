#include "table.h"
#include "game.h"
#include <algorithm>
#include <cstdio>
#include <mutex>

namespace kw {

namespace {

std::mutex g_ownersMu;
std::map<std::string, CostumeInfo> g_owners;

bool SlotText(const uint8_t* s, std::string* out) {
    int n = 0;
    while (n < 8 && s[n]) {
        if (s[n] < 0x21 || s[n] > 0x7e) return false;
        n++;
    }
    if (n < 3 || n > 7) return false;
    for (int i = n; i < 8; i++)
        if (s[i]) return false;
    if (out) out->assign((const char*)s, n);
    return true;
}

std::string Hex(const uint8_t* p, size_t n) {
    static const char* d = "0123456789abcdef";
    std::string r;
    for (size_t i = 0; i < n; i++) {
        r += d[p[i] >> 4];
        r += d[p[i] & 15];
    }
    return r;
}

bool Wr(void* at, const void* data, size_t n) {
    SIZE_T w = 0;
    return WriteProcessMemory(GetCurrentProcess(), at, data, n, &w) && w == n;
}

int EnsureCostumeEntry(int chr, int idx) {
    CostumeVec v;
    if (!GetCostumeVec(v)) return -1;
    size_t count = (v.end - v.begin) / game::kSelectorEntrySize;
    std::vector<uint8_t> all(count * game::kSelectorEntrySize);
    if (!Rd((void*)v.begin, all.data(), all.size())) return -1;
    int tmpl = -1;
    for (size_t i = 0; i < count; i++) {
        int cid = *(int*)&all[i * game::kSelectorEntrySize], cidx = *(int*)&all[i * game::kSelectorEntrySize + 4];
        if (cid != chr) continue;
        if (cidx == idx) return 0;
        if (tmpl < 0) tmpl = (int)i;
    }
    if (tmpl < 0 || v.end + game::kSelectorEntrySize > v.cap) return -1;
    uint8_t e[game::kSelectorEntrySize];
    memcpy(e, &all[tmpl * game::kSelectorEntrySize], game::kSelectorEntrySize);
    *(int*)(e + 4) = idx;
    if (!Wr((void*)v.end, e, game::kSelectorEntrySize)) return -1;
    uintptr_t newEnd = v.end + game::kSelectorEntrySize;
    return Wr((void*)(v.obj + 0x20), &newEnd, 8) ? 1 : -1;
}

std::map<int, const std::vector<Skin>*> InjectTargets(const TableView& t, const Snapshot* snap) {
    std::map<int, const std::vector<Skin>*> targets;
    for (int c = 0; c < t.count; c++) {
        auto it = snap->byId.find(c);
        if (it != snap->byId.end()) targets[c] = &it->second;
        uint8_t code[8];
        std::string s;
        if (t.codes && Rd((void*)(t.codes + (uintptr_t)c * 8), code, 8) && SlotText(code, &s)) {
            auto bc = snap->byCode.find(s);
            if (bc != snap->byCode.end() && !targets.count(c)) targets[c] = &bc->second;
        }
    }
    return targets;
}

int SlotHolding(const uint8_t* rec, const std::string& code) {
    for (int sl = game::kFirstCostumeIdx + 1; sl <= game::kLastCostumeIdx + 1; sl++) {
        std::string have;
        if (SlotText(rec + sl * 8, &have) && have == code) return sl;
    }
    return -1;
}

}

bool GetTable(TableView& t) {
    uintptr_t mgr = 0, tbl = 0, arr = 0, codes = 0, arr2 = 0;
    int count = 0;
    if (!Rd((void*)(g_base + game::kCharMgr), &mgr, 8) || !mgr) return false;
    if (!Rd((void*)(mgr + 0xf8), &tbl, 8) || !tbl) return false;
    if (!Rd((void*)(tbl + 0x18), &count, 4) || !Rd((void*)(tbl + 0x28), &arr, 8) || !Rd((void*)(tbl + 0x20), &codes, 8)) return false;
    Rd((void*)(tbl + 0x58), &arr2, 8);
    if (count <= 0 || count > 4096 || !arr) return false;
    t = {arr, arr2, codes, count};
    return true;
}

bool GetCostumeVec(CostumeVec& v) {
    uintptr_t mgr = 0;
    if (!Rd((void*)(g_base + game::kCharMgr), &mgr, 8) || !mgr) return false;
    if (!Rd((void*)(mgr + 0x20), &v.obj, 8) || !v.obj) return false;
    if (!Rd((void*)(v.obj + 0x18), &v.begin, 8) || !Rd((void*)(v.obj + 0x20), &v.end, 8) || !Rd((void*)(v.obj + 0x28), &v.cap, 8)) return false;
    return v.begin && v.end >= v.begin && v.cap >= v.end && (v.end - v.begin) % game::kSelectorEntrySize == 0 &&
           (v.end - v.begin) / game::kSelectorEntrySize < 20000;
}

void DumpTable(const TableView& t, const Snapshot* snap) {
    Log("TABLE count=%d records=%p codes=%p", t.count, (void*)t.arr, (void*)t.codes);
    std::string line;
    for (int c = 0; c < t.count; c++) {
        uint8_t code[8];
        std::string s;
        if (t.codes && Rd((void*)(t.codes + (uintptr_t)c * 8), code, 8) && SlotText(code, &s)) line += std::to_string(c) + "=" + s + " ";
        if (line.size() > 900) {
            Log("TABLE chars: %s", line.c_str());
            line.clear();
        }
    }
    if (!line.empty()) Log("TABLE chars: %s", line.c_str());
    for (int c = 0; c < t.count; c++) {
        bool wanted = c < 3;
        char code[9];
        if (snap && g_origGetSlot && CodeOfChar(c, code)) wanted |= snap->byCode.count(code) > 0 || snap->byId.count(c) > 0;
        if (!wanted) continue;
        uint8_t rec[game::kRecordSize];
        if (!Rd((void*)(t.arr + (uintptr_t)c * game::kRecordSize), rec, game::kRecordSize)) continue;
        std::string slots;
        for (int s = 0; s < game::kRecordSize / 8; s++) {
            std::string txt;
            if (SlotText(rec + s * 8, &txt)) slots += "[" + std::to_string(s) + "]" + txt + " ";
        }
        Log("RECORD chr=%d slots: %s", c, slots.c_str());
        Log("RECORD chr=%d hex: %s", c, Hex(rec, game::kRecordSize).c_str());
    }
}

void WriteCharacterList(const std::wstring& path) {
    TableView t;
    if (!GetTable(t) || !t.codes) return;
    std::map<std::string, CostumeInfo> owners;
    std::string json = "[\n";
    for (int c = 0; c < t.count; c++) {
        uint8_t code[8], rec[game::kRecordSize];
        std::string chr;
        if (!Rd((void*)(t.codes + (uintptr_t)c * 8), code, 8) || !SlotText(code, &chr)) continue;
        if (!Rd((void*)(t.arr + (uintptr_t)c * game::kRecordSize), rec, game::kRecordSize)) continue;
        std::vector<std::pair<std::string, int>> costumes;
        for (int sl = game::kFirstCostumeIdx; sl <= game::kLastCostumeIdx + 1; sl++) {
            std::string s;
            if (SlotText(rec + sl * 8, &s) && !std::any_of(costumes.begin(), costumes.end(), [&](const auto& e) { return e.first == s; }))
                costumes.push_back({s, sl});
        }
        if (costumes.empty()) continue;
        json += "  { \"id\": " + std::to_string(c) + ", \"code\": \"" + chr + "\", \"costumes\": [";
        for (size_t i = 0; i < costumes.size(); i++) {
            json += (i ? ", \"" : "\"") + costumes[i].first + "\"";
            owners.emplace(costumes[i].first, CostumeInfo{chr, c, costumes[i].second});
        }
        json += "] },\n";
    }
    if (json.size() > 2) json.erase(json.size() - 2, 1);
    json += "]\n";
    FILE* f = nullptr;
    if (!_wfopen_s(&f, path.c_str(), L"wb") && f) {
        fwrite(json.data(), 1, json.size(), f);
        fclose(f);
    }
    std::lock_guard<std::mutex> lk(g_ownersMu);
    g_owners.swap(owners);
    Log("character list written: %zu costumes -> kawarimi\\characters.json", g_owners.size());
}

bool FindCostume(const std::string& costume, CostumeInfo& out) {
    std::lock_guard<std::mutex> lk(g_ownersMu);
    auto it = g_owners.find(costume);
    if (it == g_owners.end()) return false;
    out = it->second;
    return true;
}

std::vector<std::string> CostumesOf(int chrId) {
    std::lock_guard<std::mutex> lk(g_ownersMu);
    std::vector<std::string> out;
    for (auto& [code, info] : g_owners)
        if (info.chrId == chrId) out.push_back(code);
    return out;
}

int SelectorPosition(int chrId, int slot, int* total) {
    *total = 0;
    CostumeVec v;
    if (!GetCostumeVec(v)) return 0;
    size_t count = (v.end - v.begin) / game::kSelectorEntrySize;
    std::vector<uint8_t> all(count * game::kSelectorEntrySize);
    if (!Rd((void*)v.begin, all.data(), all.size())) return 0;
    int position = 0;
    for (size_t i = 0; i < count; i++) {
        int cid = *(int*)&all[i * game::kSelectorEntrySize], idx = *(int*)&all[i * game::kSelectorEntrySize + 4];
        if (cid != chrId) continue;
        ++*total;
        if (idx == slot - 1 && position == 0) position = *total;
    }
    return position;
}

void InjectRecords(const TableView& t, const Snapshot* snap) {
    for (auto& [chr, list] : InjectTargets(t, snap)) {
        uint8_t rec[game::kRecordSize];
        uintptr_t addr = t.arr + (uintptr_t)chr * game::kRecordSize;
        if (!Rd((void*)addr, rec, game::kRecordSize)) continue;
        std::vector<int> freeSlots;
        for (int sl = game::kFirstCostumeIdx + 1; sl <= game::kLastCostumeIdx + 1; sl++) {
            bool zero = true;
            for (int b = 0; b < 8; b++)
                if (rec[sl * 8 + b]) {
                    zero = false;
                    break;
                }
            if (zero) freeSlots.push_back(sl);
        }
        size_t next = 0;
        for (auto& skin : *list) {
            std::string want(skin.code, strnlen(skin.code, 8));
            int present = SlotHolding(rec, want);
            if (present >= 0) {
                if (EnsureCostumeEntry(chr, present - 1) == 1) Log("INJECT chr=%d: selector entry re-added for index %d", chr, present - 1);
                continue;
            }
            static std::map<int, int> warned;
            if (next >= freeSlots.size()) {
                if (!warned[chr]++) Log("INJECT chr=%d: no free costume slot left for skin %s", chr, want.c_str());
                continue;
            }
            int sl = freeSlots[next++];
            uint8_t slot[8] = {0};
            memcpy(slot, skin.code, strnlen(skin.code, 7));
            if (!Wr((void*)(addr + (uintptr_t)sl * 8), slot, 8)) {
                Log("INJECT chr=%d: write failed", chr);
                break;
            }
            if (t.arr2) Wr((void*)(t.arr2 + (uintptr_t)chr * game::kRecordSize + (uintptr_t)sl * 8), slot, 8);
            Log("INJECT chr=%d: skin %s -> costume slot %d (costume index %d)", chr, want.c_str(), sl, sl - 1);
            int er = EnsureCostumeEntry(chr, sl - 1);
            Log("INJECT chr=%d: selector entry for costume index %d: %s", chr, sl - 1, er == 1 ? "added" : er == 0 ? "present" : "FAILED");
        }
    }
}

}
