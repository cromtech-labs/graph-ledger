#Requires -Modules Microsoft.Graph.Applications

<#
.SYNOPSIS
    Grants read/write permissions to the UTCM service principal for Entra workload modifications.

.DESCRIPTION
    This script grants the UTCM (Unified Tenant Configuration Management) service principal
    the necessary read/write permissions to apply configuration changes to Microsoft Entra ID.

    WARNING: These permissions allow UTCM to modify your tenant configuration.
    Only run this if you plan to use UTCM drift remediation features.

.EXAMPLE
    .\Grant-UtcmModifyPermissions.ps1

.NOTES
    Requires: Microsoft.Graph.Applications module
    Permissions: AppRoleAssignment.ReadWrite.All (to run this script)
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# UTCM Service Principal App ID
$UtcmAppId = '03b07b79-c5bc-4b5e-9bfa-13acf4a99998'

# Microsoft Graph App ID
$GraphAppId = '00000003-0000-0000-c000-000000000000'

# Read/Write permissions for Entra workload
$ModifyPermissions = @(
    'AdministrativeUnit.ReadWrite.All',
    'Application.ReadWrite.All',
    'Directory.ReadWrite.All',
    'EntitlementManagement.ReadWrite.All',
    'Organization.ReadWrite.All',
    'Policy.ReadWrite.ApplicationConfiguration',
    'Policy.ReadWrite.AuthenticationMethod',
    'Policy.ReadWrite.Authorization',
    'Policy.ReadWrite.ConditionalAccess',
    'Policy.ReadWrite.SecurityDefaults',
    'Policy.ReadWrite.TrustFramework',
    'RoleManagement.ReadWrite.Directory',
    'RoleManagementPolicy.ReadWrite.Directory'
)

Write-Host ""
Write-Host "WARNING: UTCM Modify Permissions Setup" -ForegroundColor Red
Write-Host "=======================================" -ForegroundColor Red
Write-Host ""
Write-Host "This script grants WRITE permissions to the UTCM service principal." -ForegroundColor Yellow
Write-Host "UTCM will be able to MODIFY your tenant's configuration including:" -ForegroundColor Yellow
Write-Host "  - Conditional Access policies" -ForegroundColor Yellow
Write-Host "  - Authentication methods" -ForegroundColor Yellow
Write-Host "  - Directory roles and permissions" -ForegroundColor Yellow
Write-Host "  - Applications and service principals" -ForegroundColor Yellow
Write-Host ""

if (-not $Force) {
    $confirmation = Read-Host "Are you sure you want to continue? (yes/no)"
    if ($confirmation -ne 'yes') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

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
Write-Host "Granting modify permissions to UTCM service principal..." -ForegroundColor Yellow
Write-Host ""

$granted = 0
$skipped = 0
$failed = 0

foreach ($permissionName in $ModifyPermissions) {
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
Write-Host "UTCM service principal now has read/write access to Entra configuration." -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT: Review the granted permissions in Azure Portal:" -ForegroundColor Yellow
Write-Host "  Azure Portal > Enterprise Applications > UTCM > Permissions" -ForegroundColor Yellow
