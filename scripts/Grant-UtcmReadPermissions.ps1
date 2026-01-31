#Requires -Modules Microsoft.Graph.Applications

<#
.SYNOPSIS
    Grants read-only permissions to the UTCM service principal for Entra workload snapshots.

.DESCRIPTION
    This script grants the UTCM (Unified Tenant Configuration Management) service principal
    the necessary read permissions to capture configuration snapshots from Microsoft Entra ID.

.EXAMPLE
    .\Grant-UtcmReadPermissions.ps1

.NOTES
    Requires: Microsoft.Graph.Applications module
    Permissions: AppRoleAssignment.ReadWrite.All (to run this script)
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# UTCM Service Principal App ID
$UtcmAppId = '03b07b79-c5bc-4b5e-9bfa-13acf4a99998'

# Microsoft Graph App ID
$GraphAppId = '00000003-0000-0000-c000-000000000000'

# Read-only permissions for Entra workload
$ReadPermissions = @(
    'AdministrativeUnit.Read.All',
    'Agreement.Read.All',
    'Application.Read.All',
    'CustomSecAttributeDefinition.Read.All',
    'Device.Read.All',
    'Directory.Read.All',
    'EntitlementManagement.Read.All',
    'Group.Read.All',
    'Organization.Read.All',
    'Policy.Read.All',
    'Policy.Read.ConditionalAccess',
    'RoleManagement.Read.Directory',
    'RoleManagementPolicy.Read.Directory',
    'User.Read.All'
)

Write-Host "UTCM Read Permissions Setup" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Check if connected to Microsoft Graph
$context = Get-MgContext
if (-not $context) {
    Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
    Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All" -NoWelcome
}

Write-Host "Connected as: $($context.Account)" -ForegroundColor Green
Write-Host ""

# Get the service principals
Write-Host "Getting Microsoft Graph service principal..." -ForegroundColor Yellow
$Graph = Get-MgServicePrincipal -Filter "AppId eq '$GraphAppId'"
if (-not $Graph) {
    throw "Microsoft Graph service principal not found"
}

Write-Host "Getting UTCM service principal..." -ForegroundColor Yellow
$UTCM = Get-MgServicePrincipal -Filter "AppId eq '$UtcmAppId'"
if (-not $UTCM) {
    Write-Host "UTCM service principal not found. Creating it..." -ForegroundColor Yellow
    $UTCM = New-MgServicePrincipal -AppId $UtcmAppId
    Write-Host "Created UTCM service principal: $($UTCM.Id)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Granting read permissions to UTCM service principal..." -ForegroundColor Yellow
Write-Host ""

$granted = 0
$skipped = 0
$failed = 0

foreach ($permissionName in $ReadPermissions) {
    $AppRole = $Graph.AppRoles | Where-Object { $_.Value -eq $permissionName }

    if (-not $AppRole) {
        Write-Host "  [SKIP] $permissionName - Permission not found in Graph" -ForegroundColor DarkYellow
        $skipped++
        continue
    }

    try {
        # Check if already assigned
        $existing = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $UTCM.Id |
            Where-Object { $_.AppRoleId -eq $AppRole.Id }

        if ($existing) {
            Write-Host "  [EXISTS] $permissionName" -ForegroundColor DarkGray
            $skipped++
            continue
        }

        $body = @{
            AppRoleId   = $AppRole.Id
            ResourceId  = $Graph.Id
            PrincipalId = $UTCM.Id
        }

        New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $UTCM.Id -BodyParameter $body | Out-Null
        Write-Host "  [GRANTED] $permissionName" -ForegroundColor Green
        $granted++
    }
    catch {
        Write-Host "  [FAILED] $permissionName - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Granted: $granted" -ForegroundColor Green
Write-Host "  Skipped: $skipped" -ForegroundColor Yellow
Write-Host "  Failed:  $failed" -ForegroundColor $(if ($failed -gt 0) { 'Red' } else { 'Green' })
Write-Host ""
Write-Host "UTCM service principal now has read access to Entra configuration." -ForegroundColor Cyan
