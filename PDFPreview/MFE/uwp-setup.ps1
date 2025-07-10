# UWP PDF Preview Setup Script
# This script helps set up and test the PDF Preview app for UWP integration

param(
    [string]$Action = "start",
    [switch]$Help
)

function Show-Help {
    Write-Host "UWP PDF Preview Setup Script" -ForegroundColor Green
    Write-Host "=============================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\uwp-setup.ps1 [-Action <action>] [-Help]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Actions:" -ForegroundColor Cyan
    Write-Host "  start        - Start the development server for UWP testing"
    Write-Host "  build        - Build the TypeScript application"
    Write-Host "  test         - Open UWP test page in browser"
    Write-Host "  clean        - Clean build artifacts"
    Write-Host "  install      - Install dependencies"
    Write-Host "  status       - Check application status"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\uwp-setup.ps1 -Action start"
    Write-Host "  .\uwp-setup.ps1 -Action build"
    Write-Host "  .\uwp-setup.ps1 -Action test"
}

function Start-DevServer {
    Write-Host "Starting PDF Preview app for UWP development..." -ForegroundColor Green
    Write-Host "This will start the server on http://localhost:3000" -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
    Write-Host ""
    
    # Check if node_modules exists
    if (-not (Test-Path "node_modules")) {
        Write-Host "Dependencies not found. Installing..." -ForegroundColor Yellow
        npm install
    }
    
    # Start the development server
    npm run dev:uwp
}

function Build-Application {
    Write-Host "Building PDF Preview app for UWP..." -ForegroundColor Green
    
    # Clean previous build
    if (Test-Path "dist") {
        Remove-Item -Recurse -Force "dist"
        Write-Host "Cleaned previous build" -ForegroundColor Yellow
    }
    
    # Build TypeScript
    npm run build:uwp
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build completed successfully!" -ForegroundColor Green
        Write-Host "Built files are in the 'dist' directory" -ForegroundColor Yellow
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

function Test-UWPIntegration {
    Write-Host "Opening UWP test page..." -ForegroundColor Green
    Write-Host "This will start the server and open the test page in your browser" -ForegroundColor Yellow
    
    # Start server in background and open test page
    Start-Process powershell -ArgumentList "-Command", "npm run dev" -WindowStyle Minimized
    Start-Sleep 3
    Start-Process "http://localhost:3000/uwp-test"
    
    Write-Host "Test page opened. Use this to simulate UWP messages." -ForegroundColor Green
    Write-Host "To stop the server, close the PowerShell window or press Ctrl+C" -ForegroundColor Yellow
}

function Clean-Build {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Green
    
    if (Test-Path "dist") {
        Remove-Item -Recurse -Force "dist"
        Write-Host "Removed dist directory" -ForegroundColor Yellow
    }
    
    if (Test-Path "node_modules") {
        Write-Host "Found node_modules directory (use 'npm clean' to remove)" -ForegroundColor Yellow
    }
    
    Write-Host "Clean completed!" -ForegroundColor Green
}

function Install-Dependencies {
    Write-Host "Installing dependencies..." -ForegroundColor Green
    npm install
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Dependencies installed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Failed to install dependencies!" -ForegroundColor Red
        exit 1
    }
}

function Show-Status {
    Write-Host "PDF Preview App Status" -ForegroundColor Green
    Write-Host "======================" -ForegroundColor Green
    Write-Host ""
    
    # Check if dependencies are installed
    if (Test-Path "node_modules") {
        Write-Host "✓ Dependencies installed" -ForegroundColor Green
    } else {
        Write-Host "✗ Dependencies not installed (run: .\uwp-setup.ps1 -Action install)" -ForegroundColor Red
    }
    
    # Check if build exists
    if (Test-Path "dist") {
        Write-Host "✓ Application built" -ForegroundColor Green
    } else {
        Write-Host "✗ Application not built (run: .\uwp-setup.ps1 -Action build)" -ForegroundColor Yellow
    }
    
    # Check if server is running
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:3000" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "✓ Server is running on http://localhost:3000" -ForegroundColor Green
    } catch {
        Write-Host "✗ Server is not running (run: .\uwp-setup.ps1 -Action start)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Configuration:" -ForegroundColor Cyan
    Write-Host "  Web App URL: http://localhost:3000" -ForegroundColor White
    Write-Host "  Test Page: http://localhost:3000/uwp-test" -ForegroundColor White
    Write-Host "  UWP Config: uwp-config.json" -ForegroundColor White
}

# Main script logic
if ($Help) {
    Show-Help
    exit 0
}

Write-Host "PDF Preview UWP Setup" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan
Write-Host ""

switch ($Action.ToLower()) {
    "start" { Start-DevServer }
    "build" { Build-Application }
    "test" { Test-UWPIntegration }
    "clean" { Clean-Build }
    "install" { Install-Dependencies }
    "status" { Show-Status }
    default {
        Write-Host "Unknown action: $Action" -ForegroundColor Red
        Write-Host "Use -Help to see available actions" -ForegroundColor Yellow
        exit 1
    }
}
