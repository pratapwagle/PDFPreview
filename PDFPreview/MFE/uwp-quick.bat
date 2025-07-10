@echo off
REM UWP PDF Preview Quick Start Script
REM This batch file provides quick access to common UWP development tasks

if "%1"=="" goto help
if "%1"=="help" goto help
if "%1"=="-h" goto help
if "%1"=="--help" goto help

echo PDF Preview UWP Integration
echo ===========================
echo.

if "%1"=="start" goto start
if "%1"=="build" goto build
if "%1"=="test" goto test
if "%1"=="clean" goto clean
if "%1"=="install" goto install
if "%1"=="status" goto status

echo Unknown command: %1
echo Use "uwp-quick.bat help" for available commands
goto end

:help
echo Usage: uwp-quick.bat [command]
echo.
echo Commands:
echo   start    - Start development server for UWP
echo   build    - Build TypeScript application
echo   test     - Open UWP test page
echo   clean    - Clean build artifacts
echo   install  - Install dependencies
echo   status   - Check application status
echo   help     - Show this help
echo.
echo Examples:
echo   uwp-quick.bat start
echo   uwp-quick.bat test
goto end

:start
echo Starting PDF Preview development server...
npm run dev:uwp
goto end

:build
echo Building PDF Preview application...
npm run build:uwp
if %errorlevel%==0 (
    echo Build completed successfully!
) else (
    echo Build failed!
)
goto end

:test
echo Opening UWP test page...
start http://localhost:3000/uwp-test
echo Starting development server...
npm run dev
goto end

:clean
echo Cleaning build artifacts...
if exist dist rmdir /s /q dist
echo Clean completed!
goto end

:install
echo Installing dependencies...
npm install
if %errorlevel%==0 (
    echo Dependencies installed successfully!
) else (
    echo Failed to install dependencies!
)
goto end

:status
echo PDF Preview App Status
echo ======================
echo.
if exist node_modules (
    echo [OK] Dependencies installed
) else (
    echo [ERROR] Dependencies not installed - run: uwp-quick.bat install
)

if exist dist (
    echo [OK] Application built
) else (
    echo [WARNING] Application not built - run: uwp-quick.bat build
)

echo.
echo Configuration:
echo   Web App URL: http://localhost:3000
echo   Test Page: http://localhost:3000/uwp-test
echo   UWP Config: uwp-config.json
goto end

:end
echo.
pause
