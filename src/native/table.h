#pragma once
#include "kawarimi.h"

namespace kw {

struct TableView {
    uintptr_t arr = 0, arr2 = 0, codes = 0;
    int count = 0;
};

struct CostumeVec {
    uintptr_t obj = 0, begin = 0, end = 0, cap = 0;
};

bool GetTable(TableView& t);
bool GetCostumeVec(CostumeVec& v);
void DumpTable(const TableView& t, const Snapshot* snap);
void InjectRecords(const TableView& t, const Snapshot* snap);

}
