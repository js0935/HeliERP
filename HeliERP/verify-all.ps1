# HeliERP 一鍵總驗證腳本
# 用法:  powershell -ExecutionPolicy Bypass -File verify-all.ps1 [-SkipBuild] [-SkipPng] [-SkipGeometry]
# 依序驗證: 建置 -> 模組檢查 -> 各種檢查 -> 報表渲染(文字重疊/真實資料/PNG) -> 幾何重疊 -> 彙總
param(
    [switch]$SkipBuild,
    [switch]$SkipPng,
    [switch]$SkipGeometry
)

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Src  = Join-Path $Root "src"

$Results = New-Object System.Collections.Generic.List[string]

function Invoke-Check {
    param([string]$Name, [string]$Exe, [string[]]$ArgsList, [int]$TimeoutSec = 600)
    $log = Join-Path $Root "verify-logs"
    if (-not (Test-Path $log)) { New-Item -ItemType Directory -Path $log | Out-Null }
    $logFile = Join-Path $log ("{0}.txt" -f $Name)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    if (Test-Path $Exe) {
        & $Exe @ArgsList *> $logFile
        $code = $LASTEXITCODE
    } else {
        "EXE 不存在: $Exe" | Out-File $logFile
        $code = -1
    }
    $sw.Stop()
    $status = if ($code -eq 0) { "PASS" } else { "FAIL" }
    $secs = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
    $Results.Add("[$status] $Name (exit=$code, ${secs}s)")
    Write-Host "[$status] $Name (${secs}s)" -ForegroundColor $(if ($code -eq 0) { "Green" } else { "Red" })
    return $code
}

function Get-Exe {
    param([string]$Name)
    return (Join-Path $Src "$Name\bin\Release\net8.0-windows\$Name.exe")
}

Write-Host "=== HeliERP 一鍵總驗證 ===" -ForegroundColor Cyan
Write-Host ("根目錄: {0}  開始時間: {1}" -f $Root, (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Write-Host ""

# 1) 建置
if (-not $SkipBuild) {
    Write-Host "--- 建置全部專案 ---" -ForegroundColor Yellow
    $projs = Get-ChildItem $Src -Filter "*.csproj" -Recurse | Where-Object { $_.Name -match "Check|RtmRenderTest|HeliERP.App|DbSchemaFix" }
    $buildOk = $true
    foreach ($p in $projs) {
        dotnet build $p.FullName -c Release -v q --nologo *> $null
        if ($LASTEXITCODE -ne 0) { $buildOk = $false; $Results.Add("[FAIL] 建置: $($p.Directory.Name)") }
    }
    if ($buildOk) {
        $Results.Add("[PASS] 建置全部專案 (n=$($projs.Count))")
        Write-Host "[PASS] 建置全部專案" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] 有專案建置失敗" -ForegroundColor Red
    }
} else {
    Write-Host "--- 跳過建置 ---" -ForegroundColor Yellow
}

# 2) 模組檢查
Write-Host "`n--- 模組檢查 (ModuleCheck) ---" -ForegroundColor Yellow
$mc = Get-Exe "ModuleCheck"
foreach ($mod in @("trade","bill","payroll","acc","invoice","approval","audit","master")) {
    Invoke-Check -Name "ModuleCheck-$mod" -Exe $mc -ArgsList @($mod)
}

# 3) 各業務檢查工具
Write-Host "`n--- 業務檢查工具 ---" -ForegroundColor Yellow
foreach ($n in @("AdjustmentCheck","ARCheck","CrudCheck","DashboardCheck","PayCheck","PoCheck","UiLayoutCheck")) {
    Invoke-Check -Name $n -Exe (Get-Exe $n)
}

# 4) 報表渲染驗證
Write-Host "`n--- 報表渲染 (RtmRenderTest) ---" -ForegroundColor Yellow
$rtm = Get-Exe "RtmRenderTest"
$logDir = Join-Path $Root "verify-logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# 4a) oneall：像素級文字重疊 + 線條切穿文字（141 檔假資料渲染）
$sw = [System.Diagnostics.Stopwatch]::StartNew()
& $rtm oneall *> (Join-Path $logDir "Rtm-oneall.txt")
$sw.Stop()
$rep = "D:\HeliAcc\shots\oneall_report.txt"
if (Test-Path $rep) {
    $head = [System.IO.File]::ReadAllLines($rep, [System.Text.Encoding]::UTF8)[0]
    if ($head -match "0 有重疊") { $Results.Add("[PASS] Rtm-oneall (全部報表無字疊字/線疊字，$head)") ; Write-Host "[PASS] Rtm-oneall  $head" -ForegroundColor Green }
    else { $Results.Add("[FAIL] Rtm-oneall ($head)") ; Write-Host "[FAIL] Rtm-oneall  $head" -ForegroundColor Red }
} else {
    $Results.Add("[FAIL] Rtm-oneall (找不到報告檔)") ; Write-Host "[FAIL] Rtm-oneall 報告檔不存在" -ForegroundColor Red
}
Write-Host "        (耗時 $([Math]::Round($sw.Elapsed.TotalSeconds,1))s)"

Invoke-Check -Name "Rtm-mreport"    -Exe $rtm -ArgsList @("mreport")
if (-not $SkipPng) {
    Invoke-Check -Name "Rtm-oneallpng" -Exe $rtm -ArgsList @("oneallpng")
}
if (-not $SkipGeometry) {
    Invoke-Check -Name "Rtm-overlap"   -Exe $rtm -ArgsList @("overlap")
}

# 5) 彙總
Write-Host "`n=== 驗證彙總 ===" -ForegroundColor Cyan
$pass = ($Results | Where-Object { $_ -match '^\[PASS\]' }).Count
$fail = ($Results | Where-Object { $_ -match '^\[FAIL\]' }).Count
$Results | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "PASS=$pass  FAIL=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
exit $(if ($fail -eq 0) { 0 } else { 1 })
