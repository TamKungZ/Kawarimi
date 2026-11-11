#include "kawarimi.h"
#include "table.h"

namespace kw {

namespace {

DWORD WINAPI Maintainer(LPVOID) {
    bool dumped = false;
    uintptr_t lastArr = 0;
    for (int tick = 0;; tick++) {
        for (int w = 0; w < (tick < 600 ? 20 : 100); w++) {
            Sleep(50);
            PollHotkeys();
        }
        TableView t;
        if (!GetTable(t)) continue;
        const Snapshot* snap = g_active ? g_snap.load() : nullptr;
        if (!dumped) {
            Sleep(3000);
            if (!GetTable(t)) continue;
            DumpTable(t, snap);
            WriteCharacterList(g_gameDir + L"\\kawarimi\\characters.json");
            dumped = true;
            lastArr = t.arr;
            if (g_watchChr >= 0) ArmWatch(g_watchChr);
        } else if (t.arr != lastArr) {
            Log("table reloaded (records moved)");
            lastArr = t.arr;
            DumpTable(t, snap);
            WriteCharacterList(g_gameDir + L"\\kawarimi\\characters.json");
        }
        if (g_strategy == 1 && snap && NewCodesAllowed()) InjectRecords(t, snap);
    }
}

}

void StartMaintainer() {
    CreateThread(nullptr, 0, Maintainer, nullptr, 0, nullptr);
}

}
