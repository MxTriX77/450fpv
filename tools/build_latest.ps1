# Build the newest playable 450fpv in its own worktree, with the full compile log.
# What it builds comes from tools/build_latest.json on origin/unity/main, so a stale checkout still builds the newest state.
# Usage: pwsh -File tools/build_latest.ps1 [-Repo C:\.dev\450fpv] [-NoSmoke] [-DryRun]
param(
    [string]$Repo = "C:\.dev\450fpv",
    [switch]$NoSmoke,
    [switch]$DryRun
)
$ErrorActionPreference = "Stop"
$started = Get-Date

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host ""; Write-Host "BUILD FAILED: $text" -ForegroundColor Red; exit 1 }
function Invoke-Git { & git -C $script:Here @args; if ($LASTEXITCODE -ne 0) { Fail "git $($args -join ' ') exited $LASTEXITCODE" } }

$Worktree = Join-Path (Split-Path $Repo -Parent) "450fpv-latest"
$OutRoot = Join-Path $Repo "unity\Builds\Latest"
$LogDir = Join-Path $Repo "unity\Logs"
$stamp = $started.ToString("yyyyMMdd-HHmmss")
$UnityLog = Join-Path $LogDir "build-latest-$stamp.log"
$Unity = if ($env:UNITY) { $env:UNITY } else { "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" }

Step "Fetch"
$script:Here = $Repo
Invoke-Git fetch --prune origin
$planText = (& git -C $Repo show origin/unity/main:tools/build_latest.json 2>$null) -join "`n"
if (-not $planText) { $planText = Get-Content -Raw (Join-Path $Repo "tools\build_latest.json"); Write-Host "note: latest.json is not on origin/unity/main yet; using the local copy" -ForegroundColor Yellow }
$plan = $planText | ConvertFrom-Json
if (-not $plan) { Fail "could not read tools/build_latest.json" }
Write-Host "base:   $($plan.base) @ $(& git -C $Repo rev-parse --short origin/$($plan.base))"
foreach ($ref in $plan.merge) { Write-Host "merge:  $ref @ $(& git -C $Repo rev-parse --short origin/$ref)" }
Write-Host "method: $($plan.method)"
Write-Host "about:  $($plan.contents)"

Step "Worktree $Worktree"
if (-not (Test-Path $Worktree)) {
    Invoke-Git worktree add --detach $Worktree "origin/$($plan.base)"
}
$script:Here = $Worktree
if (Test-Path (Join-Path $Worktree "unity\Temp\UnityLockfile")) {
    if (Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Unity }) {
        Write-Host "note: a Unity editor is running; if it has $Worktree\unity open, close it first." -ForegroundColor Yellow
    }
}
if (Test-Path (Join-Path $Worktree ".git")) {
    & git -C $Worktree merge --abort 2>$null
    Invoke-Git checkout --force --detach "origin/$($plan.base)"
    Invoke-Git reset --hard "origin/$($plan.base)"
}
foreach ($ref in $plan.merge) {
    $subject = "chore: merge $ref for the latest build"
    & git -C $Worktree -c user.name="Build" -c user.email="build@450fpv.local" merge --no-ff --no-stat -m $subject "origin/$ref"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "conflicts:" -ForegroundColor Red
        & git -C $Worktree diff --name-only --diff-filter=U
        & git -C $Worktree merge --abort
        Fail "origin/$ref does not merge cleanly onto origin/$($plan.base); tell the orchestrator to update tools/build_latest.json"
    }
}
Invoke-Git log --oneline -1
Step "LFS"
Invoke-Git lfs pull

Step "Machine"
$power = (Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus -ErrorAction SilentlyContinue | Select-Object -First 1).PowerOnline
$freeGb = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
Write-Host "AC power: $(if ($power -eq $false) { 'NO (on battery: the laptop falls back to its iGPU)' } else { 'yes' })"
Write-Host "free RAM: $freeGb GB$(if ($freeGb -lt 2.5) { '  (under 2.5 GB: the build may fail)' })"
Write-Host "Unity:    $Unity"
if (-not (Test-Path $Unity)) { Fail "Unity not found at $Unity (set `$env:UNITY)" }
if (-not (Get-Process -Name "Unity Hub" -ErrorAction SilentlyContinue)) {
    Write-Host "starting Unity Hub (batchmode needs its licence)"
    Start-Process explorer.exe "shell:AppsFolder\UnityTechnologies.UnityHub_2vrhnee42bhxm!UnityHub"
    Start-Sleep -Seconds 20
}

$unityArgs = @("-batchmode", "-quit", "-projectPath", (Join-Path $Worktree "unity"), "-buildTarget", "Win64",
    "-executeMethod", $plan.method, "-stackTraceLogType", "Full", "-logFile", "-") + @($plan.unity_args)
if ($DryRun) { Step "Dry run"; Write-Host "& `"$Unity`" $($unityArgs -join ' ')"; exit 0 }

Step "Sim core (dotnet build, detailed)"
& dotnet build (Join-Path $Worktree "tools\simcore") -c Release -v normal -nologo -clp:Summary | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "the sim core does not compile (see above)" }

Step "Unity build (full log: $UnityLog)"
New-Item -ItemType Directory -Force $LogDir | Out-Null
& $Unity @unityArgs 2>&1 | Tee-Object -FilePath $UnityLog | Out-Host
$unityExit = $LASTEXITCODE

Step "Compile summary"
$log = Get-Content $UnityLog
$errors = $log | Select-String -Pattern "error CS\d+" | Select-Object -ExpandProperty Line -Unique
$warnings = $log | Select-String -Pattern "warning CS\d+" | Select-Object -ExpandProperty Line -Unique
Write-Host "C# errors:   $($errors.Count)"; $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
Write-Host "C# warnings: $($warnings.Count)"; $warnings | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
$log | Select-String -Pattern "Build Finished|Build completed|Build succeeded|Build failed|Result:|Total size|BuildPlayer|Exiting batchmode|exit code|Scripts have compiler errors|Exception" |
    Select-Object -Last 25 | ForEach-Object { Write-Host "  $($_.Line)" }
Write-Host "Unity exit code: $unityExit"
if ($unityExit -ne 0 -or $errors.Count -gt 0) { Fail "Unity build failed (exit $unityExit); full log: $UnityLog" }

Step "Copy to $OutRoot"
$built = Join-Path $Worktree "unity\$($plan.output_dir)"
if (-not (Test-Path (Join-Path $built $plan.exe))) { Fail "no $($plan.exe) in $built" }
if (Test-Path $OutRoot) { Remove-Item -Recurse -Force $OutRoot }
Copy-Item -Recurse $built $OutRoot
Copy-Item $UnityLog (Join-Path $OutRoot "build.log")
$info = @("built $($started.ToString('yyyy-MM-dd HH:mm'))", "base $($plan.base) @ $(& git -C $Repo rev-parse --short origin/$($plan.base))") +
    @($plan.merge | ForEach-Object { "merge $_ @ $(& git -C $Repo rev-parse --short origin/$_)" }) +
    @("tree $(& git -C $Worktree rev-parse --short HEAD)", "method $($plan.method)", $plan.contents)
$info | Set-Content (Join-Path $OutRoot "BUILD-INFO.txt")
$exe = Join-Path $OutRoot $plan.exe

if (-not $NoSmoke -and $plan.smoke_args.Count -gt 0) {
    Step "Smoke test"
    & $exe @($plan.smoke_args) -logFile - 2>&1 | Select-String -Pattern "Smoke|PASS|FAIL|Exception|error" | ForEach-Object { Write-Host "  $($_.Line)" }
    if ($LASTEXITCODE -ne 0) { Fail "the smoke test failed (exit $LASTEXITCODE)" }
}

Step "Done in $([math]::Round(((Get-Date) - $started).TotalMinutes, 1)) min"
Write-Host "Run it: & `"$exe`"" -ForegroundColor Green
