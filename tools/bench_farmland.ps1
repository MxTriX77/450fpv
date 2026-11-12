# The farmland's PLT-07 benchmark (w6-farmland-look 1.2). Runs a review player's -farmbench (FarmlandBench: Explore opened as
# the menu does, then the scripted path over the village, a tree belt and an open field, through the feed and in noclip, in a
# 1920x1080 window with vsync off) and adds what only the OS sees: AC power, the power plan and mode, the CPU's and GPU's
# clocks while it runs, the process's dedicated VRAM and its RAM, and the other heavy processes (a quiet machine has none).
# The player writes its window to HKCU\Software\450fpv on quit; that key is saved first and put back after, byte for byte.
# Usage: pwsh -File tools/bench_farmland.ps1 [-Exe <450fpv.exe>] [-Out <folder>] [-Runs feed,noclip] [-Extra '<player args>']
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\unity\Builds\Windows-Review\450fpv.exe"),
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) "450fpv-farmbench"),
    [string]$Runs = "feed,noclip",
    [string]$Extra = "",
    [int]$TimeoutS = 900
)
$ErrorActionPreference = "Stop"
function Fail($text) { Write-Host "BENCH FAILED: $text" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $Exe)) { Fail "no player at $Exe (build one with: Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -review)" }
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
$json = Join-Path $Out "farmbench-$stamp.json"
$log = Join-Path $Out "farmbench-$stamp.log"
$smi = Join-Path $Out "farmbench-$stamp-gpu.csv"
$regKey = "HKCU\Software\450fpv"
$regSaved = Join-Path $Out "farmbench-$stamp-hkcu.reg"

# The machine: power (PLT-07: AC only), the plan and Windows' power mode, the CPU and GPU, and who else is busy.
$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
$ac = if (-not $battery) { "no battery (desktop)" } elseif ($battery.BatteryStatus -eq 2) { "AC" } else { "BATTERY (status $($battery.BatteryStatus)): not budget evidence" }
$plan = ((powercfg /getactivescheme) -join " ") -replace "^Power Scheme GUID:\s*", ""
$overlay = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes" -ErrorAction SilentlyContinue).ActiveOverlayAcPowerScheme
$modes = @{ "961cc777-2547-4f9d-8174-7d86181b8a7a" = "Best power efficiency"; "00000000-0000-0000-0000-000000000000" = "Balanced";
            "3af9b8d9-7c97-431d-ad78-34a8bfea439f" = "Better performance"; "ded574b5-45a0-4f42-8737-46345c09c238" = "Best performance" }
$mode = if (-not $overlay) { "Balanced (no overlay set)" } elseif ($modes.ContainsKey($overlay)) { "$($modes[$overlay]) ($overlay)" } else { "unknown ($overlay)" }
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpu = (& nvidia-smi --query-gpu=name,driver_version,clocks.max.graphics,clocks.max.memory,enforced.power.limit,power.max_limit --format=csv,noheader,nounits 2>$null) -split ",\s*"
# Windows' power mode sets the laptop GPU's power limit: the dev machine's RTX 4050 is held at 20 W of its 65 W in
# "Best power efficiency" (measured 2026-11-12), so the limit in force is printed with the mode.
$gpuInfo = if ($gpu.Count -ge 6) { "$($gpu[0]), driver $($gpu[1]), max $($gpu[2]) / $($gpu[3]) MHz, power limit $($gpu[4]) W in force of $($gpu[5]) W" } else { "nvidia-smi gave nothing" }
$busy = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(Unity|blender|dotnet|450fpv|python|pwsh)$' -and $_.Id -ne $PID } |
          ForEach-Object { "$($_.ProcessName)#$($_.Id)" })
Write-Host "power:   $ac; plan $plan; mode $mode"
Write-Host "cpu:     $($cpu.Name.Trim()), $($cpu.MaxClockSpeed) MHz base"
Write-Host "gpu:     $gpuInfo"
Write-Host "others:  $(if ($busy.Count) { $busy -join ', ' } else { 'none (quiet)' })"

# The pilot's window prefs, put back after the run.
& reg export $regKey $regSaved /y 2>$null | Out-Null
$hadKey = $LASTEXITCODE -eq 0

$smiProc = Start-Process nvidia-smi -PassThru -WindowStyle Hidden -ArgumentList @(
    "--query-gpu=timestamp,pstate,clocks.gr,clocks.mem,power.draw,utilization.gpu,temperature.gpu",
    "--format=csv,noheader,nounits", "-lms", "500", "-f", $smi)
$playerArgs = @("-farmbench", $json, "-benchruns", $Runs, "-windowed", "-vsync", "0", "-logFile", $log) + ($Extra -split " " | Where-Object { $_ })
$t0 = Get-Date
$p = Start-Process -FilePath $Exe -ArgumentList $playerArgs -PassThru
$vramMax = 0.0; $wsMax = 0.0; $privMax = 0.0; $mhz = New-Object System.Collections.Generic.List[double]
while (-not $p.HasExited -and ((Get-Date) - $t0).TotalSeconds -lt $TimeoutS) {
    $c = Get-Counter -ErrorAction SilentlyContinue -SampleInterval 1 -MaxSamples 1 -Counter @(
        "\GPU Process Memory(pid_$($p.Id)_*)\Dedicated Usage",
        "\Processor Information(_Total)\% Processor Performance",
        "\Processor Information(_Total)\Processor Frequency")
    if ($c) {
        $s = $c.CounterSamples
        $v = ($s | Where-Object { $_.Path -like "*gpu process memory*dedicated usage" } | Measure-Object CookedValue -Sum).Sum
        if ($v -gt $vramMax) { $vramMax = $v }
        $perf = ($s | Where-Object { $_.Path -like "*% processor performance" }).CookedValue
        $freq = ($s | Where-Object { $_.Path -like "*processor frequency" }).CookedValue
        if ($perf -and $freq) { $mhz.Add($freq * $perf / 100) }
    }
    try { $p.Refresh(); if ($p.WorkingSet64 -gt $wsMax) { $wsMax = $p.WorkingSet64 }; if ($p.PrivateMemorySize64 -gt $privMax) { $privMax = $p.PrivateMemorySize64 } } catch { }
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; $exit = "TIMEOUT" } else { $exit = $p.ExitCode }
Stop-Process -Id $smiProc.Id -ErrorAction SilentlyContinue
$seconds = [int]((Get-Date) - $t0).TotalSeconds

if ($hadKey) { & reg delete $regKey /f 2>$null | Out-Null; & reg import $regSaved 2>$null | Out-Null }
else { & reg delete $regKey /f 2>$null | Out-Null }
$check = Join-Path $Out "farmbench-$stamp-hkcu-after.reg"
& reg export $regKey $check /y 2>$null | Out-Null
$prefsBack = if ($hadKey) { (Get-FileHash $check).Hash -eq (Get-FileHash $regSaved).Hash } else { $LASTEXITCODE -ne 0 }
Remove-Item $check -ErrorAction SilentlyContinue

Write-Host "player:  exit $exit after $seconds s; log $log; HKCU prefs put back: $prefsBack"
if ($exit -ne 0 -or -not (Test-Path $json)) { Fail "the player did not finish (exit $exit); see $log" }
$r = Get-Content -Raw $json | ConvertFrom-Json

# The clocks while it ran: the GPU's graphics clock, utilisation, power and temperature; the CPU's effective clock.
$g = @(Get-Content $smi -ErrorAction SilentlyContinue | ForEach-Object { $f = $_ -split ",\s*"; if ($f.Count -ge 7 -and $f[2] -match '^\d') {
        [pscustomobject]@{ pstate = $f[1]; gr = [double]$f[2]; mem = [double]$f[3]; w = [double]$f[4]; util = [double]$f[5]; temp = [double]$f[6] } } })
function Med($xs) { $a = @($xs | Sort-Object); if ($a.Count) { $a[[int][math]::Floor($a.Count / 2)] } else { 0 } }
$busyG = @($g | Where-Object { $_.util -ge 50 })
$gpuClock = if ($busyG.Count) { "graphics $(Med $busyG.gr) MHz median ($(($busyG.gr | Measure-Object -Minimum).Minimum)-$(($busyG.gr | Measure-Object -Maximum).Maximum)), memory $(Med $busyG.mem) MHz, $(Med $busyG.w) W, $(Med $busyG.util) % busy, $(($busyG.temp | Measure-Object -Maximum).Maximum) C max, $(($busyG.pstate | Sort-Object -Unique) -join '/')" } else { "no samples" }
$cpuClock = if ($mhz.Count) { "{0:0} MHz effective median ({1:0}-{2:0})" -f (Med $mhz), ($mhz | Measure-Object -Minimum).Minimum, ($mhz | Measure-Object -Maximum).Maximum } else { "no samples" }

$vramMB = $vramMax / 1MB; $wsMB = $wsMax / 1MB; $privMB = $privMax / 1MB
$lines = @(
    "# Farmland benchmark $stamp",
    "",
    "- power: $ac; plan $plan; mode $mode",
    "- cpu: $($cpu.Name.Trim()); $cpuClock",
    "- gpu: $gpuInfo; $gpuClock",
    "- others running: $(if ($busy.Count) { $busy -join ', ' } else { 'none (quiet)' })",
    "- player: $($r.gpu), $($r.screen), vsync $($r.vsync), quality $($r.quality), battery $($r.battery); path $($r.path_m) m",
    "- Explore to the first frame: $($r.load_s) s (PLT-06: 20 s)",
    "- peak dedicated VRAM $([math]::Round($vramMB)) MB (PLT-05: 4608), peak RAM $([math]::Round($wsMB)) MB working set, $([math]::Round($privMB)) MB private (PLT-05: 8192)",
    "",
    "| Run | Frames | Average | 1 % low | Worst frame | CPU main / render | Wait to present | GPU (FrameTimingManager) | Draw calls (max) | Triangles (max) | Unity system MB | Render |",
    "|---|---|---|---|---|---|---|---|---|---|---|---|")
foreach ($x in $r.runs) {
    $lines += "| $($x.name) | $($x.frames) in $($x.seconds) s | $($x.average_fps) fps ($($x.mean_ms) ms) | $($x.low1_fps) fps ($($x.p99_ms) ms) | $($x.worst_ms) ms | $($x.cpu_main_ms) / $($x.cpu_render_ms) ms | $($x.present_wait_ms) ms | $($x.gpu_ftm_ms) ms$(if ($x.feed) { ' (output camera only)' }) | $($x.draw_calls) ($($x.draw_calls_max)) | $($x.triangles_m) M ($([math]::Round($x.triangles_max / 1e6, 2)) M) | $($x.unity_system_mb) | $($x.render) |"
}
$lines += ""
foreach ($x in $r.runs) {
    $ok = if ($x.feed) { $x.low1_fps -ge 60 -and $x.average_fps -ge 100 } else { $x.low1_fps -ge 60 }
    $lines += "- PLT-02 $($x.name): $(if ($ok) { 'met' } else { 'NOT met' }) ($(if ($x.feed) { '1 % low >= 60 and average >= 100 fps' } else { '1 % low >= 60 fps' }))"
}
$lines += "- PLT-05: $(if ($vramMB -le 4608 -and $wsMB -le 8192) { 'met' } else { 'NOT met' }); PLT-06: $(if ($r.load_s -le 20) { 'met' } else { 'NOT met' })"
$report = Join-Path $Out "farmbench-$stamp.md"
$lines | Set-Content $report
$lines | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "report: $report"
