# Copyright © Erickson Lopez. MIT License.
<#
.SYNOPSIS
    Detects which packages are affected by a Git diff (e.g. Pull Request vs main).
.DESCRIPTION
    Analyzes changed files between a base commit and head commit.
    Categorizes changes to determine:
    1. Whether production C# code or tests were modified (skipping Stryker if docs-only).
    2. Which specific package(s) were modified, building a targeted matrix for Stryker.
#>

[CmdletBinding()]
param (
    [string]$BaseRef = "origin/main",
    [string]$HeadRef = "HEAD",
    [switch]$RunAll,
    [string]$RunAllMode = "false"
)

$ErrorActionPreference = "Stop"

$shouldRunAll = $RunAll.IsPresent -or ($RunAllMode -eq "true" -or $RunAllMode -eq "True" -or $RunAllMode -eq "1" -or $RunAllMode -eq "all")

$packageMap = [ordered]@{
    "Printing" = @{
        patterns = @("src/EricksonLopez.Printing/", "tests/EricksonLopez.Printing.Tests/")
        config = "stryker-config.json"
        outputDir = "StrykerOutput/printing"
        artifact = "stryker-report-printing"
    }
    "EscPos" = @{
        patterns = @("src/EricksonLopez.Printing.EscPos/", "tests/EricksonLopez.Printing.EscPos.Tests/")
        config = "stryker-escpos-config.json"
        outputDir = "StrykerOutput/escpos"
        artifact = "stryker-report-escpos"
    }
    "EscPosImaging" = @{
        patterns = @("src/EricksonLopez.Printing.EscPos.Imaging/", "tests/EricksonLopez.Printing.EscPos.Imaging.Tests/")
        config = "stryker-escpos-imaging-config.json"
        outputDir = "StrykerOutput/escpos-imaging"
        artifact = "stryker-report-escpos-imaging"
    }
    "EscPosStatus" = @{
        patterns = @("src/EricksonLopez.Printing.EscPos.Status/", "tests/EricksonLopez.Printing.EscPos.Status.Tests/")
        config = "stryker-escpos-status-config.json"
        outputDir = "StrykerOutput/escpos-status"
        artifact = "stryker-report-escpos-status"
    }
    "Serial" = @{
        patterns = @("src/EricksonLopez.Printing.Serial/", "tests/EricksonLopez.Printing.Serial.Tests/")
        config = "stryker-serial-config.json"
        outputDir = "StrykerOutput/serial"
        artifact = "stryker-report-serial"
    }
    "SignalR" = @{
        patterns = @("src/EricksonLopez.Printing.SignalR/", "tests/EricksonLopez.Printing.SignalR.Tests/")
        config = "stryker-signalr-config.json"
        outputDir = "StrykerOutput/signalr"
        artifact = "stryker-report-signalr"
    }
    "Zpl" = @{
        patterns = @("src/EricksonLopez.Printing.Zpl/", "tests/EricksonLopez.Printing.Zpl.Tests/")
        config = "stryker-zpl-config.json"
        outputDir = "StrykerOutput/zpl"
        artifact = "stryker-report-zpl"
    }
    "ZplImaging" = @{
        patterns = @("src/EricksonLopez.Printing.Zpl.Imaging/", "tests/EricksonLopez.Printing.Zpl.Imaging.Tests/")
        config = "stryker-zpl-imaging-config.json"
        outputDir = "StrykerOutput/zpl-imaging"
        artifact = "stryker-report-zpl-imaging"
    }
}

$globalTriggers = @("Directory.Build.props", "Directory.Packages.props", "EricksonLopez.Printing.slnx", ".editorconfig")

$hasCodeChanges = $false
$allAffected = $false
$affectedPackages = [System.Collections.Generic.HashSet[string]]::new()

if ($shouldRunAll) {
    Write-Host "Forced RunAll mode enabled: running Stryker across all $($packageMap.Count) packages." -ForegroundColor Cyan
    $hasCodeChanges = $true
    $allAffected = $true
    foreach ($pkg in $packageMap.Keys) {
        [void]$affectedPackages.Add($pkg)
    }
} else {
    $changedFiles = @()
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try {
        $rawOutput = git diff --name-only "$BaseRef...$HeadRef" 2>&1
        $changedFiles = @($rawOutput | Where-Object { $_ -is [string] -and $_ -notmatch '^(warning:|fatal:)' -and $_.Trim() -ne "" })
        
        if ($LASTEXITCODE -ne 0 -or $changedFiles.Count -eq 0) {
            $rawOutput2 = git diff --name-only "$BaseRef" "$HeadRef" 2>&1
            $changedFiles = @($rawOutput2 | Where-Object { $_ -is [string] -and $_ -notmatch '^(warning:|fatal:)' -and $_.Trim() -ne "" })
        }
    } catch {
        $changedFiles = @()
    }

    if ($changedFiles.Count -eq 0) {
        try {
            $rawOutput3 = git diff --name-only HEAD 2>&1
            $changedFiles = @($rawOutput3 | Where-Object { $_ -is [string] -and $_ -notmatch '^(warning:|fatal:)' -and $_.Trim() -ne "" })
        } catch {
            $changedFiles = @()
        }
    }
    $ErrorActionPreference = $prevEap

    Write-Host "Evaluating $(($changedFiles | Measure-Object).Count) changed file(s)..." -ForegroundColor Cyan

    foreach ($file in $changedFiles) {
        $norm = $file.Replace("\", "/")
        
        foreach ($gt in $globalTriggers) {
            if ($norm -eq $gt) {
                $hasCodeChanges = $true
                $allAffected = $true
                Write-Host "  Trigger: Global file modified -> $norm" -ForegroundColor Yellow
            }
        }
        
        foreach ($pkg in $packageMap.Keys) {
            foreach ($pat in $packageMap[$pkg].patterns) {
                if ($norm.StartsWith($pat)) {
                    $hasCodeChanges = $true
                    [void]$affectedPackages.Add($pkg)
                    Write-Host "  Trigger: $pkg affected by -> $norm" -ForegroundColor Green
                }
            }
        }
    }

    if ($allAffected) {
        foreach ($pkg in $packageMap.Keys) {
            [void]$affectedPackages.Add($pkg)
        }
    }
}

$matrixInclude = @()
foreach ($pkg in $packageMap.Keys) {
    if ($affectedPackages.Contains($pkg)) {
        $info = $packageMap[$pkg]
        $matrixInclude += [ordered]@{
            name = $pkg
            config = $info.config
            "output-dir" = $info.outputDir
            "artifact-name" = $info.artifact
        }
    }
}

if ($matrixInclude.Count -eq 0) {
    $matrixJson = "[]"
} elseif ($matrixInclude.Count -eq 1) {
    $singleJson = $matrixInclude[0] | ConvertTo-Json -Compress
    $matrixJson = "[$singleJson]"
} else {
    $matrixJson = $matrixInclude | ConvertTo-Json -Compress
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Has code changes        : $hasCodeChanges"
Write-Host "  Affected packages count : $($affectedPackages.Count)"
Write-Host "  Matrix JSON             : $matrixJson"

if ($env:GITHUB_OUTPUT) {
    $hasChangesStr = if ($hasCodeChanges) { "true" } else { "false" }
    $hasPkgsStr = if ($affectedPackages.Count -gt 0) { "true" } else { "false" }
    
    "has_code_changes=$hasChangesStr" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "has_affected_packages=$hasPkgsStr" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "affected_count=$($affectedPackages.Count)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "matrix=$matrixJson" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
