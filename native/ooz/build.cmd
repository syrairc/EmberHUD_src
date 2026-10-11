@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VS=%%i
call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1 || exit /b 1
cd /d "%~dp0"
if not exist obj mkdir obj
cl /nologo /O2 /MT /EHsc /DNDEBUG /FIsys/stat.h /wd4319 /wd4477 /LD /Foobj\ src\*.cpp ooz_export.cpp /Fe:ooz.dll /link /NOLOGO || exit /b 1
cl /nologo /O2 /MT /EHsc /DNDEBUG /FIsys/stat.h /wd4319 /wd4477 /Foobj\ src\*.cpp /Fe:obj\ooz.exe /link /NOLOGO || exit /b 1
del /q ooz.exp ooz.lib 2>nul
dumpbin /nologo /dependents ooz.dll
dumpbin /nologo /exports ooz.dll
