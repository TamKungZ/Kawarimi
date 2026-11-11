Unicode true
RequestExecutionLevel user
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"
!include "LogicLib.nsh"
!insertmacro WordFind
!insertmacro GetParameters
!insertmacro GetOptions
!insertmacro un.WordFind
!insertmacro un.GetParent

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!define APP "Kawarimi"
!define GAME_EXE "NSUNS4.exe"
!define STEAM_FOLDER "NARUTO SHIPPUDEN Ultimate Ninja STORM 4"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP}"
!define PAYLOAD "..\stage"
!define DOTNET_URL "https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe"

Name "${APP} ${VERSION}"
OutFile "..\dist\KawarimiSetup-${VERSION}.exe"
InstallDir "C:\__kawarimi_unset__"
BrandingText "${APP} ${VERSION}"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2026 TamKungZ_ <dev@tamkungz.me> - GPL-3.0-or-later"
VIAddVersionKey "CompanyName" "TamKungZ_"
VIAddVersionKey "FileDescription" "${APP} skin loader installer"

!define MUI_ICON "..\assets\kawarimi.ico"
!define MUI_UNICON "..\assets\kawarimi.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "This installs ${APP} ${VERSION} into your NARUTO SHIPPUDEN: Ultimate Ninja STORM 4 folder: the skin loader, the Kawarimi tool (model and texture editing) and the documentation.$\r$\n$\r$\nNo game archive or original game file is modified. Use offline only.$\r$\n$\r$\nClose the game before continuing."
!define MUI_DIRECTORYPAGE_TEXT_TOP "Select the game folder - the one that contains ${GAME_EXE}. The folder below was detected if possible."
!define MUI_DIRECTORYPAGE_TEXT_DESTINATION "Game folder"
!define MUI_FINISHPAGE_TEXT "${APP} is installed.$\r$\n$\r$\nA log window opens next to the game. In character select hover a costume: the log shows its code. F7 = next mod for it, F8 = original.$\r$\n$\r$\nCreate and build mods with the Kawarimi tool (Start menu)."
!define MUI_FINISHPAGE_RUN "$INSTDIR\kawarimi\Kawarimi.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open the Kawarimi tool"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!define MUI_PAGE_CUSTOMFUNCTION_PRE DirPre
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var Existing
Var ExistingVer

!macro IsGameDir dir result
  ${If} ${FileExists} "${dir}\${GAME_EXE}"
    StrCpy ${result} 1
  ${Else}
    StrCpy ${result} 0
  ${EndIf}
!macroend

!macro TaskList
  nsExec::ExecToStack 'tasklist /FI "IMAGENAME eq ${GAME_EXE}" /NH'
  Pop $1
  Pop $2
!macroend

!macro GameRunning result
  !insertmacro TaskList
  ${WordFind} $2 "${GAME_EXE}" "E+1" $3
  ${If} ${Errors}
    StrCpy ${result} 0
  ${Else}
    StrCpy ${result} 1
  ${EndIf}
!macroend

!macro UnGameRunning result
  !insertmacro TaskList
  ${un.WordFind} $2 "${GAME_EXE}" "E+1" $3
  ${If} ${Errors}
    StrCpy ${result} 0
  ${Else}
    StrCpy ${result} 1
  ${EndIf}
!macroend

Function CheckSteamLib
  !insertmacro IsGameDir "$0\steamapps\common\${STEAM_FOLDER}" $1
  ${If} $1 == 1
    StrCpy $INSTDIR "$0\steamapps\common\${STEAM_FOLDER}"
  ${EndIf}
FunctionEnd

Function DetectGameDir
  ${If} $Existing != ""
    StrCpy $INSTDIR $Existing
    Return
  ${EndIf}
  ReadRegStr $0 HKCU "Software\Valve\Steam" "SteamPath"
  ${If} $0 == ""
    ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Valve\Steam" "InstallPath"
  ${EndIf}
  ${If} $0 == ""
    Return
  ${EndIf}
  ${WordReplace} $0 "/" "\" "+" $0
  Call CheckSteamLib
  ${If} $INSTDIR != ""
    Return
  ${EndIf}
  FileOpen $2 "$0\steamapps\libraryfolders.vdf" r
  ${If} ${Errors}
    Return
  ${EndIf}
  ${Do}
    FileRead $2 $3
    ${If} ${Errors}
      ${Break}
    ${EndIf}
    ${WordFind} $3 '"' "+2" $4
    ${If} $4 == "path"
      ${WordFind} $3 '"' "+4" $0
      ${WordReplace} $0 "\\" "\" "+" $0
      Call CheckSteamLib
      ${If} $INSTDIR != ""
        ${Break}
      ${EndIf}
    ${EndIf}
  ${Loop}
  FileClose $2
FunctionEnd

Function HasDotNet
  StrCpy $5 0
  ${ForEach} $6 8 20 + 1
    FindFirst $7 $8 "$PROGRAMFILES64\dotnet\shared\Microsoft.NETCore.App\$6.*"
    ${If} $8 != ""
      StrCpy $5 1
    ${EndIf}
    FindClose $7
  ${Next}
FunctionEnd

Function EnsureDotNet
  Call HasDotNet
  ${If} $5 == 1
    Return
  ${EndIf}
  MessageBox MB_YESNO|MB_ICONQUESTION "${APP} needs the .NET 8 runtime (free, from Microsoft), which is not installed.$\r$\n$\r$\nDownload and install it now?" /SD IDNO IDYES install
  DetailPrint ".NET runtime not installed - Kawarimi will not start until it is."
  Return
  install:
  DetailPrint "Downloading the .NET 8 runtime..."
  nsExec::ExecToLog 'powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing ${DOTNET_URL} -OutFile $\"$TEMP\dotnet-runtime-kawarimi.exe$\""'
  Pop $0
  ${If} ${FileExists} "$TEMP\dotnet-runtime-kawarimi.exe"
    DetailPrint "Installing the .NET 8 runtime..."
    ExecWait '"$TEMP\dotnet-runtime-kawarimi.exe" /install /passive /norestart' $0
    Delete "$TEMP\dotnet-runtime-kawarimi.exe"
  ${Else}
    MessageBox MB_OK|MB_ICONEXCLAMATION "The download failed. Install the .NET 8 x64 Runtime from https://dotnet.microsoft.com/download/dotnet/8.0" /SD IDOK
  ${EndIf}
FunctionEnd

Function DirPre
  ${If} $Existing != ""
    MessageBox MB_YESNO|MB_ICONQUESTION "${APP} $ExistingVer is already installed in:$\r$\n$Existing$\r$\n$\r$\nUpdate it to ${VERSION}? (Choose No to pick another folder.)" /SD IDYES IDNO +2
    Abort
  ${EndIf}
FunctionEnd

Function .onVerifyInstDir
  !insertmacro IsGameDir $INSTDIR $0
  ${If} $0 == 0
    Abort
  ${EndIf}
FunctionEnd

Function BackUpForeignProxy
  StrCpy $9 0
  ${If} ${FileExists} "$INSTDIR\winmm.dll"
    ReadRegStr $0 HKCU "${UNINST_KEY}" "GameDir"
    ${If} $0 != $INSTDIR
      ${GetParameters} $0
      ${GetOptions} $0 "/BACKUP" $1
      ${If} ${Errors}
        MessageBox MB_YESNO|MB_ICONQUESTION "$INSTDIR already contains a winmm.dll that is not from ${APP}.$\r$\nBack it up as winmm.dll.pre-kawarimi and continue? It is restored on uninstall." /SD IDNO IDYES +3
        SetErrorLevel 4
        Abort
      ${EndIf}
      Delete "$INSTDIR\winmm.dll.pre-kawarimi"
      Rename "$INSTDIR\winmm.dll" "$INSTDIR\winmm.dll.pre-kawarimi"
      StrCpy $9 1
    ${EndIf}
  ${EndIf}
  ${If} $9 == 0
    ReadRegDWORD $9 HKCU "${UNINST_KEY}" "BackedUpWinmm"
  ${EndIf}
FunctionEnd

Section "${APP}" SecMain
  SectionIn RO
  !insertmacro IsGameDir $INSTDIR $0
  ${If} $0 == 0
    MessageBox MB_OK|MB_ICONSTOP "${GAME_EXE} was not found in:$\r$\n$INSTDIR$\r$\nChoose the NSUNS4 game folder." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  !insertmacro GameRunning $0
  ${If} $0 == 1
    MessageBox MB_OK|MB_ICONSTOP "${GAME_EXE} is running. Close the game and run the installer again." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  Call EnsureDotNet

  ${If} $Existing != ""
  ${AndIf} $Existing != $INSTDIR
    ExecWait '"$Existing\kawarimi\uninstall.exe" /S _?=$Existing\kawarimi'
    Delete "$Existing\kawarimi\uninstall.exe"
    RMDir "$Existing\kawarimi"
  ${EndIf}
  Call BackUpForeignProxy

  SetOutPath "$INSTDIR"
  File "${PAYLOAD}\winmm.dll"
  !include "gen\payload.nsh"
  CreateDirectory "$INSTDIR\mods"

  CreateDirectory "$SMPROGRAMS\${APP}"
  CreateShortcut "$SMPROGRAMS\${APP}\Kawarimi.lnk" "$INSTDIR\kawarimi\Kawarimi.exe" "" "$INSTDIR\kawarimi\Kawarimi.exe"
  CreateShortcut "$SMPROGRAMS\${APP}\Mods folder.lnk" "$INSTDIR\mods"
  CreateShortcut "$SMPROGRAMS\${APP}\Kawarimi log.lnk" "$INSTDIR\kawarimi\kawarimi.log"
  CreateShortcut "$SMPROGRAMS\${APP}\Uninstall Kawarimi.lnk" "$INSTDIR\kawarimi\uninstall.exe"

  nsExec::Exec '"$INSTDIR\kawarimi\Kawarimi.exe" path add'
  Pop $0

  WriteUninstaller "$INSTDIR\kawarimi\uninstall.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP} (NSUNS4 skin loader)"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\kawarimi\Kawarimi.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "TamKungZ_"
  WriteRegStr HKCU "${UNINST_KEY}" "GameDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\kawarimi\uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\kawarimi\uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "BackedUpWinmm" $9
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

Function .onInit
  ReadRegStr $Existing HKCU "${UNINST_KEY}" "GameDir"
  ReadRegStr $ExistingVer HKCU "${UNINST_KEY}" "DisplayVersion"
  ${If} $Existing != ""
    !insertmacro IsGameDir $Existing $0
    ${If} $0 == 0
      StrCpy $Existing ""
    ${EndIf}
  ${EndIf}
  ${If} $INSTDIR == "C:\__kawarimi_unset__"
    StrCpy $INSTDIR ""
    Call DetectGameDir
  ${EndIf}
FunctionEnd

Function un.onInit
  ReadRegStr $INSTDIR HKCU "${UNINST_KEY}" "GameDir"
  ${If} $INSTDIR == ""
    ${un.GetParent} "$EXEDIR" $INSTDIR
  ${EndIf}
FunctionEnd

Section "Uninstall"
  !insertmacro UnGameRunning $0
  ${If} $0 == 1
    MessageBox MB_OK|MB_ICONSTOP "${GAME_EXE} is running. Close the game and try again." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  nsExec::Exec '"$INSTDIR\kawarimi\Kawarimi.exe" path remove'
  Pop $0
  ReadRegDWORD $9 HKCU "${UNINST_KEY}" "BackedUpWinmm"
  Delete "$INSTDIR\winmm.dll"
  ${If} $9 == 1
  ${AndIf} ${FileExists} "$INSTDIR\winmm.dll.pre-kawarimi"
    Rename "$INSTDIR\winmm.dll.pre-kawarimi" "$INSTDIR\winmm.dll"
  ${EndIf}
  !include "gen\payload-un.nsh"
  Delete "$INSTDIR\kawarimi\kawarimi.log"
  Delete "$INSTDIR\kawarimi\mods-report.txt"
  Delete "$INSTDIR\kawarimi\characters.json"
  Delete "$INSTDIR\kawarimi\config.json"
  Delete "$INSTDIR\kawarimi\uninstall.exe"
  RMDir "$INSTDIR\kawarimi"
  RMDir "$INSTDIR\mods"
  RMDir /r "$SMPROGRAMS\${APP}"
  DeleteRegKey HKCU "${UNINST_KEY}"
SectionEnd
