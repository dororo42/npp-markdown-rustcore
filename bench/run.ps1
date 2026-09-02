# bench/run.ps1 — Windows performance suite (route A' native renderer).
#
# Usage (from repo root, PowerShell):
#   powershell -ExecutionPolicy Bypass -File bench\run.ps1            # all sizes
#   powershell -File bench\run.ps1 -Sizes 1KB.md,100KB.md             # subset
#   powershell -File bench\run.ps1 -Iters 20                          # more iters
#
# Prereqs: cargo (stable), repo checked out. Builds the bench example from
# rustrender-native and runs it against generated samples.

param(
    [string[]]$Sizes = @("1KB.md", "100KB.md", "1MB.md", "10MB.md"),
    [int]$Iters = 10
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    # 1. Build the harness (release).
    cargo build --release -p rustrender-native --example bench
    if ($LASTEXITCODE -ne 0) { throw "cargo build failed" }

    # 2. Generate samples that are missing.
    foreach ($s in $Sizes) {
        $p = Join-Path "bench" $s
        if (-not (Test-Path $p)) {
            Write-Host "generating $s ..."
            python bench\generate_samples.py $s
        }
    }

    # 3. Run.
    $results = @()
    foreach ($s in $Sizes) {
        $exe = ".\target\release\examples\bench.exe"
        $out = & $exe (Join-Path "bench" $s) $Iters | Select-Object -Last 1
        $json = $out | ConvertFrom-Json
        $results += $json
        "{0,-10} avg {1,8:N0} us   worst {2,8:N0} us   ({3:N0} KB)" -f `
            $json.file, $json.avg_us, $json.worst_us, ($json.bytes / 1KB)
    }

    # 4. Machine-readable summary for CI artifacts.
    $results | ConvertTo-Json | Set-Content bench\results.json
    Write-Host "`nresults written to bench\results.json"
}
finally {
    Pop-Location
}
