@echo off
setlocal enabledelayedexpansion

:: Tim duong dan MSBuild
set "MSBUILD_PATH="

for /f "usebackq tokens=*" %%i in (`dir /b /s "C:\Program Files\Microsoft Visual Studio\*.exe" 20^>nul ^| findstr /i "msbuild.exe"`) do (
    set "MSBUILD_PATH=%%i"
    goto :found
)

for /f "usebackq tokens=*" %%i in (`dir /b /s "C:\Program Files (x86)\Microsoft Visual Studio\*.exe" 20^>nul ^| findstr /i "msbuild.exe"`) do (
    set "MSBUILD_PATH=%%i"
    goto :found
)

:found
if not defined MSBUILD_PATH (
    :: Thu tim trong Windows .NET Framework folder mac dinh nhu fallback
    if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
        set "MSBUILD_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    ) else if exist "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" (
        set "MSBUILD_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
    ) else (
        echo [ERROR] Khong tim thay MSBuild.exe tren he thong. Vui long cai dat Visual Studio hoac .NET Build Tools.
        pause
        exit /b 1
    )
)

echo [INFO] Tim thay MSBuild tai: !MSBUILD_PATH!
echo [INFO] Dang bat dau build project o che do Release...

:: Kill running app to prevent locked files
taskkill /f /im "Rate Calculation.exe" 2>nul

:: Build project
"!MSBUILD_PATH!" "Rate Calculation\Rate Calculation.csproj" /p:Configuration=Release /p:Platform="AnyCPU"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Build thanh cong!
    echo [INFO] File executable nam tai: Rate Calculation\bin\Release\Rate Calculation.exe
) else (
    echo.
    echo [ERROR] Build that bai voi ma loi %ERRORLEVEL%.
)

pause
