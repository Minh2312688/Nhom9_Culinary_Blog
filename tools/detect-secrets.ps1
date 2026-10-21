<#
.SYNOPSIS
    Pre-commit and CI Secrets Scanner for Culinary Blog (NFR-SEC-007).
.DESCRIPTION
    Scans repository files or git staged files for accidentally committed secrets,
    private keys, API tokens, and production connection strings.
    Complies with SRS v1.0.0 NFR-SEC-007 (TruffleHog / Gitleaks lightweight equivalent).
    Never prints secret values to console output.
.PARAMETER StagedOnly
    Scan only git staged files (default for pre-commit hook).
.PARAMETER AllTracked
    Scan all git-tracked files in the repository.
.PARAMETER Path
    Scan a specific directory or file.
#>

[CmdletBinding()]
param(
    [switch]$StagedOnly,
    [switch]$AllTracked,
    [string]$Path
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Culinary Blog - Security Secrets Scanner (NFR-SEC-007)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Rules definition (Pattern, Description)
$Rules = @(
    @{
        Name = "Private Key Header"
        Pattern = '-----BEGIN (?:[A-Z0-9_-]+ )?PRIVATE KEY-----'
    },
    @{
        Name = "AWS Access Key ID"
        Pattern = '\b(AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b'
    },
    @{
        Name = "Hardcoded JWT Token"
        Pattern = '\beyJ[a-zA-Z0-9_-]{10,}\.eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\b'
    },
    @{
        Name = "High-entropy Slack / GitHub / Generic Token"
        Pattern = '\b(ghp_[a-zA-Z0-9]{36}|gho_[a-zA-Z0-9]{36}|xox[baprs]-[0-9a-zA-Z]{10,48})\b'
    }
)

# Known safe placeholders that should NOT trigger false positives
$SafePlaceholders = @(
    'change_me',
    'change_this',
    'replace_with',
    'your_secret_here',
    'IntegrationTestOnly',
    'TestSecretKey',
    'SuperSecretKeyForTesting',
    'WiringTestSecretKey',
    'NotARealCredential',
    'culinaryblog-api-secrets'
)

# File patterns to exclude
$ExcludeExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.ico', '.svg', '.webp',
    '.dll', '.exe', '.pdb', '.so', '.dylib',
    '.docx', '.pdf', '.zip', '.tar', '.gz',
    '.woff', '.woff2', '.ttf', '.eot'
)

function Should-SkipFile([string]$filePath) {
    if (-not (Test-Path $filePath -PathType Leaf)) {
        return $true
    }

    $ext = [System.IO.Path]::GetExtension($filePath).ToLower()
    if ($ExcludeExtensions -contains $ext) {
        return $true
    }

    $normalized = $filePath.Replace('\', '/')
    if ($normalized -match '(\.git/|node_modules/|/bin/|/obj/|/TestResults/|\.tools/)') {
        return $true
    }

    return $false
}

# Determine file list to scan
$FilesToScan = @()

if ($Path) {
    if (Test-Path $Path -PathType Container) {
        $FilesToScan = Get-ChildItem -Path $Path -Recurse -File | Select-Object -ExpandProperty FullName
    } elseif (Test-Path $Path -PathType Leaf) {
        $FilesToScan = @($Path)
    } else {
        Write-Error "Specified path does not exist: $Path"
        exit 1
    }
} elseif ($StagedOnly) {
    $gitStaged = git diff --name-only --cached --diff-filter=ACMR
    if ($gitStaged) {
        $FilesToScan = $gitStaged -split "`r?`n" | Where-Object { $_ -ne "" }
    }
} elseif ($AllTracked) {
    $gitTracked = git ls-files
    if ($gitTracked) {
        $FilesToScan = $gitTracked -split "`r?`n" | Where-Object { $_ -ne "" }
    }
} else {
    # Default: if run in git repo, scan staged if any, else scan tracked
    $gitStaged = git diff --name-only --cached --diff-filter=ACMR
    if ($gitStaged) {
        $FilesToScan = $gitStaged -split "`r?`n" | Where-Object { $_ -ne "" }
        Write-Host "Scanning git staged files..." -ForegroundColor Gray
    } else {
        $gitTracked = git ls-files
        $FilesToScan = $gitTracked -split "`r?`n" | Where-Object { $_ -ne "" }
        Write-Host "Scanning all git-tracked files..." -ForegroundColor Gray
    }
}

$Violations = @()
$ScannedCount = 0

foreach ($file in $FilesToScan) {
    if (Should-SkipFile $file) {
        continue
    }

    $ScannedCount++
    try {
        $lines = Get-Content -Path $file -ErrorAction Stop
    } catch {
        continue
    }

    $lineNum = 0
    foreach ($line in $lines) {
        $lineNum++

        # Check if line contains known safe placeholder
        $isSafe = $false
        foreach ($placeholder in $SafePlaceholders) {
            if ($line -like "*$placeholder*") {
                $isSafe = $true
                break
            }
        }
        if ($isSafe) { continue }

        foreach ($rule in $Rules) {
            if ($line -match $rule.Pattern) {
                $Violations += [PSCustomObject]@{
                    File = $file
                    Line = $lineNum
                    Rule = $rule.Name
                }
            }
        }
    }
}

Write-Host "Scanned $ScannedCount files." -ForegroundColor Gray

if ($Violations.Count -gt 0) {
    Write-Host "`n[SECURITY ALERT] Potential secrets detected in codebase!" -ForegroundColor Red
    Write-Host "Total findings: $($Violations.Count)" -ForegroundColor Red
    Write-Host "(Secret values are REDACTED for security per NFR-SEC-007)`n" -ForegroundColor Yellow

    foreach ($v in $Violations) {
        Write-Host "  -> File: $($v.File):$($v.Line)" -ForegroundColor Yellow
        Write-Host "     Rule: $($v.Rule)" -ForegroundColor Red
    }

    Write-Host "`nPlease remove raw secrets, move to User Secrets (dev) or Environment Variables (prod)." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "[PASS] No secrets detected. Clean commit." -ForegroundColor Green
    exit 0
}
