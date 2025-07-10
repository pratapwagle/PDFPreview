# PowerShell script to configure the UWP app for different environments

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Local", "Staging", "Production")]
    [string]$Environment,
    
    [Parameter(Mandatory=$false)]
    [string]$AwsProductionUrl = "",
    
    [Parameter(Mandatory=$false)]
    [string]$AwsStagingUrl = ""
)

$configFile = "AppConfig.cs"

if (!(Test-Path $configFile)) {
    Write-Error "AppConfig.cs file not found in current directory"
    exit 1
}

Write-Host "Configuring UWP app for $Environment environment..." -ForegroundColor Green

# Read the current config file
$content = Get-Content $configFile -Raw

# Update URLs if provided
if ($AwsProductionUrl) {
    $content = $content -replace 'public const string AWS_PRODUCTION_URL = ".*";', "public const string AWS_PRODUCTION_URL = `"$AwsProductionUrl`";"
    Write-Host "Updated production URL: $AwsProductionUrl" -ForegroundColor Yellow
}

if ($AwsStagingUrl) {
    $content = $content -replace 'public const string AWS_STAGING_URL = ".*";', "public const string AWS_STAGING_URL = `"$AwsStagingUrl`";"
    Write-Host "Updated staging URL: $AwsStagingUrl" -ForegroundColor Yellow
}

# Update environment
$content = $content -replace 'public static EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType\.\w+;', "public static EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType.$Environment;"

# Write back to file
Set-Content $configFile -Value $content

Write-Host "Configuration updated successfully!" -ForegroundColor Green
Write-Host "Current environment: $Environment" -ForegroundColor Cyan

# Show current configuration
$currentConfig = Get-Content $configFile | Where-Object { $_ -match "URL|CurrentEnvironment" }
Write-Host "`nCurrent configuration:" -ForegroundColor Cyan
$currentConfig | ForEach-Object { Write-Host "  $_" }

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Build the UWP application: msbuild PDFPreviewUWP.csproj /p:Configuration=Release /p:Platform=x64"
Write-Host "2. Deploy the application to the target device"
Write-Host "3. Ensure your MFE is deployed and accessible at the configured URL"
