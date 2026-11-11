#pragma once
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <winsock2.h>
#include <windows.h>
#include <atomic>
#include <cstdint>
#include <map>
#include <string>
#include <vector>

namespace kw {

struct Skin {
    char code[8];
    std::string dir;
    std::string replaces;
};

struct Snapshot {
    std::map<std::string, std::vector<Skin>> byCode;
    std::map<int, std::vector<Skin>> byId;
};

extern std::atomic<const Snapshot*> g_snap;
extern std::atomic<bool> g_active;
extern std::atomic<int> g_onlinePolicy;
extern std::atomic<bool> g_online;
bool NewCodesAllowed();
bool VisualOnly();
extern int g_strategy;
extern bool g_trace;
extern uintptr_t g_base;
extern std::wstring g_gameDir;

void Log(const char* fmt, ...);
void OpenConsole();
void ConsoleLine(const char* line);
struct CostumeInfo {
    std::string character;
    int chrId = -1;
    int slot = 0;
};

void WriteCharacterList(const std::wstring& path);
bool FindCostume(const std::string& costume, CostumeInfo& out);
std::vector<std::string> CostumesOf(int chrId);
int SelectorPosition(int chrId, int slot, int* total);
void SetFamilyName(const std::string& family, const std::string& name);
std::string DisplayName(const std::string& characterCode);
void OpenLog(const std::wstring& path);
void LoadSkipFlags(const std::wstring& path);
bool Skip(const char* word);
bool Rd(const void* p, void* out, size_t n);
std::string ReadSmallFile(const std::wstring& path);

using CodeFn = const char* (*)(int, char[9]);
using GetSlotFn = const char* (*)(int, int);
extern CodeFn CodeOfChar;
extern GetSlotFn g_origGetSlot;
const char* Hook_GetSlot(int chr, int idx);
void InstallCostumeHooks();

extern std::atomic<bool> g_ovActive;
extern std::atomic<bool> g_ovLookup;
void SetOverrideCallers(const std::string& hexList);
void NoteCostume(const std::string& costume);
std::string LastCostume();
std::string ActiveSkinFor(const std::string& costume);
void PollHotkeys();

void InstallResearchHooks();
void ArmWatch(int chr);
extern int g_watchChr;

bool Redirect(const std::wstring& path, std::wstring& out);
void NoteModelRequest(const char* path);
void InstallFileHooks(HMODULE exe, bool logPaths);

void InstallOnlineWatch(HMODULE exe);
int WSAAPI Hook_Connect(SOCKET s, const sockaddr* name, int len);
void InstallDiagnostics();
void InstallCapture(HMODULE exe, const std::wstring& dir);
void StartMaintainer();

bool BootManaged(const std::wstring& dir);
void ApplyOption(const std::string& key, int value);

}
