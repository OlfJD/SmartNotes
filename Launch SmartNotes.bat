@echo off
setlocal
set "REL=%~dp0release"

rem 1) A running SmartNotes locks its files. Ask it to quit normally (it saves every note first) - never force-kill.
tasklist /FI "IMAGENAME eq SmartNotes.exe" 2>nul | find /I "SmartNotes.exe" >nul
if errorlevel 1 goto build
echo Closing the running SmartNotes (your notes are saved first)...
if exist "%REL%\SmartNotes.exe" "%REL%\SmartNotes.exe" --exit
set /a tries=0
:wait
tasklist /FI "IMAGENAME eq SmartNotes.exe" 2>nul | find /I "SmartNotes.exe" >nul
if errorlevel 1 goto build
set /a tries+=1
if %tries% geq 10 (
    echo.
    echo SmartNotes is still running. Quit it from the tray icon ^(right-click, Exit^),
    echo then press any key here to continue.
    pause >nul
    set /a tries=0
)
timeout /t 1 /nobreak >nul
goto wait

:build
rem 2) Build into the release folder
echo Compiling and publishing the latest version...
dotnet publish "%~dp0SmartNotes.csproj" -c Release -o "%REL%"
if errorlevel 1 (
    echo.
    echo Build failed - nothing was started. See the messages above.
    pause
    exit /b 1
)

rem 3) Start it
start "" /D "%REL%" "%REL%\SmartNotes.exe"
exit /b 0
