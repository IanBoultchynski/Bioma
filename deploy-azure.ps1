<#
.SYNOPSIS
    Met a jour les 3 services Bioma sur Azure Container Apps sans ouvrir le portail Azure.
    Utilise Azure CLI s'il est installe, sinon bascule automatiquement sur Docker (mcr.microsoft.com/azure-cli).
#>

$ErrorActionPreference = "Stop"

$ResourceGroup = "rg-bioma"
$RegistryOwner = "ianboultchynski"
$Apps = @("bioma-python", "bioma-api", "bioma-blazor")
$Timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$hasAz = $null -ne (Get-Command az -ErrorAction SilentlyContinue)
$hasDocker = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)

if (-not $hasAz -and -not $hasDocker) {
    Write-Error "Ni Azure CLI ('az') ni Docker ne sont installes sur cette machine."
    exit 1
}

function Invoke-Az {
    param([string[]]$Arguments)

    if ($hasAz) {
        & az @Arguments
    }
    else {
        $azureDir = Join-Path $env:USERPROFILE ".azure"
        if (-not (Test-Path $azureDir)) {
            New-Item -ItemType Directory -Path $azureDir | Out-Null
        }
        & docker run --rm -it -v "${azureDir}:/root/.azure" mcr.microsoft.com/azure-cli az @Arguments
    }

    if ($LASTEXITCODE -ne 0) {
        throw "La commande az a echoue (code $LASTEXITCODE)."
    }
}

Write-Host "Verification de la connexion Azure..." -ForegroundColor Cyan
try {
    if ($hasAz) {
        & az account show --only-show-errors *> $null
    }
    else {
        $azureDir = Join-Path $env:USERPROFILE ".azure"
        if (-not (Test-Path $azureDir)) {
            New-Item -ItemType Directory -Path $azureDir | Out-Null
        }
        & docker run --rm -v "${azureDir}:/root/.azure" mcr.microsoft.com/azure-cli az account show --only-show-errors *> $null
    }
    $isLoggedIn = ($LASTEXITCODE -eq 0)
}
catch {
    $isLoggedIn = $false
}

if (-not $isLoggedIn) {
    Write-Host "Connexion a Azure requise (une seule fois)..." -ForegroundColor Yellow
    Invoke-Az @("login", "--use-device-code")
}

foreach ($app in $Apps) {
    $image = "ghcr.io/$RegistryOwner/${app}:latest"
    Write-Host "Mise a jour de $app ($image)..." -ForegroundColor Green
    Invoke-Az @(
        "containerapp", "update",
        "--name", $app,
        "--resource-group", $ResourceGroup,
        "--image", $image,
        "--set-env-vars", "DEPLOY_TS=$Timestamp"
    )
}

Write-Host ""
Write-Host "Deploiement termine !" -ForegroundColor Green
Write-Host "Frontend Blazor : https://bioma-blazor.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io"
Write-Host "Backend C#      : https://bioma-api.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io/health"
Write-Host "Backend Python  : https://bioma-python.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io/docs"
