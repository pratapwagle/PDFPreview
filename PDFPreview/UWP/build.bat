@echo off
echo Building PDF Preview UWP Application...
echo.

REM Add MSBuild to PATH if not already available
set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin"
if not exist "%MSBUILD_PATH%\MSBuild.exe" (
    set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin"
)
if not exist "%MSBUILD_PATH%\MSBuild.exe" (
    set "MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin"
)
set "PATH=%MSBUILD_PATH%;%PATH%"

REM Restore NuGet packages
echo Restoring NuGet packages...
nuget restore PDFPreviewUWP.sln

REM Build for x64 Debug
echo Building for x64 Debug...
MSBuild.exe PDFPreviewUWP.csproj /p:Configuration=Debug /p:Platform=x64

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully!
echo.
echo To run the application:
echo 1. Make sure WebView2 Runtime is installed
echo 2. Deploy and run the application from Visual Studio
echo 3. Or run directly from: bin\x64\Debug\PDFPreviewUWP.exe
echo.
pause
