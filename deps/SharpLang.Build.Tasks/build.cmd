@echo off
setlocal

:: Check prerequisites
set _msbuildexe="%ProgramFiles(x86)%\MSBuild\12.0\Bin\MSBuild.exe"
if not exist %_msbuildexe% set _msbuildexe="%ProgramFiles%\MSBuild\12.0\Bin\MSBuild.exe"
if not exist %_msbuildexe% echo Error: Could not find MSBuild.exe (12.0). && goto :eof

%_msbuildexe% /nologo src\SharpLang.Build.Tasks.sln /p:Configuration=Release;OutputPath=%cd%