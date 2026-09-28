@echo off
echo Compiling and publishing the latest version...
dotnet publish "%~dp0SmartNotes.csproj" -c Release -o "%~dp0release"
start "" /D "%~dp0release" "%~dp0release\SmartNotes.exe"
exit
