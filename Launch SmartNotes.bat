@echo off
if exist "%~dp0SmartNotes.exe" (
    start "" "%~dp0SmartNotes.exe"
) else (
    start "" "%~dp0release\SmartNotes.exe"
)
exit
