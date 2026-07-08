# Unified automated test pipeline with structured logs and CI artifacts.
param(
    [switch]$Quick,
    [switch]$SkipBuild,
    [switch]$SkipMeta
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$Artifacts = Join-Path $Root "artifacts\test"
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
$LogFile = Join-Path $Artifacts "test.log"
$SummaryFile = Join-Path $Artifacts "summary.json"
$StartedAt = Get-Date
$PowerShellExe = (Get-Process -Id $PID).Path

$TimeoutsSec = @{
    Build = 600
    Unit = 180
    Integration = 240
    Ui = 180
    Tools = 120
    Lifecycle = 240
    Meta = 60
}

if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
$suiteResults = [System.Collections.Generic.List[object]]::new()
$anyFailed = $false
$slowFilter = "Category!=Slow"

function Write-TestLog {
    param(
        [Parameter(Mandatory = $true)][string]$Tag,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $line = "[{0}] {1}" -f $Tag, $Message
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    switch ($Tag) {
        "FAIL" { Write-Host $line -ForegroundColor Red }
        "PASS" { Write-Host $line -ForegroundColor Green }
        "SUITE" { Write-Host $line -ForegroundColor Cyan }
        "ARTIFACT" { Write-Host $line -ForegroundColor DarkGray }
        default { Write-Host $line }
    }
}

function Stop-TestProcesses {
    $stopped = @()
    foreach ($name in @("testhost", "testhost.net", "BalanceBoardApp")) {
        foreach ($proc in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            try {
                if ($proc.HasExited) {
                    continue
                }

                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
                $stopped += "{0}({1})" -f $proc.ProcessName, $proc.Id
            }
            catch {
                Write-TestLog "WARN" "Failed to stop stale process $($proc.ProcessName) ($($proc.Id)): $($_.Exception.Message)"
            }
        }
    }

    if ($stopped.Count -gt 0) {
        Write-TestLog "TEST" "Stopped stale test processes: $($stopped -join ', ')"
    }
}

function Get-TrxFailures {
    param([string]$TrxPath)

    if (-not (Test-Path $TrxPath)) {
        return @()
    }

    try {
        [xml]$trx = Get-Content $TrxPath
        return @($trx.TestRun.Results.UnitTestResult |
            Where-Object { $_.outcome -ne "Passed" } |
            ForEach-Object {
                [ordered]@{
                    name = $_.testName
                    outcome = $_.outcome
                    message = $_.Output.ErrorInfo.Message
                }
            })
    }
    catch {
        return @([ordered]@{ name = "trx-parse"; outcome = "Error"; message = $_.Exception.Message })
    }
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$TimeoutSec,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FilePath
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = ($Arguments | ForEach-Object {
            if ($_ -match '[\s";]') {
                '"' + ($_ -replace '"', '\"') + '"'
            }
            else {
                $_
            }
        }) -join ' '

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi

    $timedOut = $false
    try {
        [void]$process.Start()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        if (-not $process.WaitForExit($TimeoutSec * 1000)) {
            $timedOut = $true
            Write-TestLog "FAIL" "Command timed out after ${TimeoutSec}s: $FilePath $($Arguments -join ' ')"
            try {
                & taskkill /PID $process.Id /T /F 2>$null | Out-Null
            }
            catch {
                try {
                    $process.Kill($true)
                }
                catch {
                    # Best-effort fallback.
                }
            }
        }

        $process.WaitForExit()
        [System.Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask))
        $stdout = @($stdoutTask.Result -split "`r?`n")
        $stderr = @($stderrTask.Result -split "`r?`n")
        $combined = @($stdout + $stderr)
        $combined | Set-Content -Path $OutputPath -Encoding UTF8

        return [ordered]@{
            ExitCode = if ($timedOut) { -1 } else { [int]$process.ExitCode }
            TimedOut = $timedOut
            Output = $combined
        }
    }
    finally {
        $process.Dispose()
    }
}

function Write-OutputLinesToTestLog {
    param([string[]]$Lines)

    $Lines | ForEach-Object {
        if (-not [string]::IsNullOrWhiteSpace($_)) {
            Write-TestLog "TEST" $_
        }
    }
}

function Invoke-DotNetTestSuite {
    param(
        [string]$Name,
        [string]$Project,
        [string]$Phase,
        [string]$Filter = "",
        [int]$TimeoutSec
    )

    Write-TestLog "SUITE" "Starting $Name ($Phase)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $trxName = ($Name -replace '[^A-Za-z0-9._-]', '_') + ".trx"
    $trxPath = Join-Path $Artifacts $trxName
    $logPath = Join-Path $Artifacts (($Name -replace '[^A-Za-z0-9._-]', '_') + ".log")
    Remove-Item $trxPath, $logPath -Force -ErrorAction SilentlyContinue

    $args = @(
        "test", $Project,
        "-c", "Release",
        "--no-build",
        "--results-directory", $Artifacts,
        "--logger", "trx;LogFileName=$trxName",
        "--logger", "console;verbosity=minimal"
    )
    if ($Filter) {
        $args += @("--filter", $Filter)
    }

    try {
        $commandResult = Invoke-ExternalCommand -FilePath "dotnet" -Arguments $args -TimeoutSec $TimeoutSec -OutputPath $logPath
    }
    finally {
        $sw.Stop()
    }

    Write-OutputLinesToTestLog -Lines $commandResult.Output
    $failures = Get-TrxFailures -TrxPath $trxPath

    $result = [ordered]@{
        name = $Name
        phase = $Phase
        project = $Project
        passed = ($commandResult.ExitCode -eq 0)
        durationSec = [math]::Round($sw.Elapsed.TotalSeconds, 2)
        exitCode = $commandResult.ExitCode
        trx = $trxPath
        log = $logPath
        timedOut = $commandResult.TimedOut
        failedTests = $failures
    }
    $script:suiteResults.Add($result) | Out-Null

    if ($commandResult.ExitCode -eq 0) {
        Write-TestLog "PASS" "$Name completed in $($result.durationSec)s"
        Write-TestLog "ARTIFACT" $trxPath
        Write-TestLog "ARTIFACT" $logPath
    }
    else {
        $script:anyFailed = $true
        $reason = if ($commandResult.TimedOut) { "timed out" } else { "failed (exit $($commandResult.ExitCode))" }
        Write-TestLog "FAIL" "$Name $reason"
        Write-TestLog "ARTIFACT" $trxPath
        Write-TestLog "ARTIFACT" $logPath
        foreach ($failure in $failures) {
            Write-TestLog "FAIL" "Test=$($failure.name) outcome=$($failure.outcome) message=$($failure.message)"
        }

        $commandResult.Output |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Last 20 |
            ForEach-Object { Write-TestLog "CONTEXT" $_ }
    }

    return $commandResult.ExitCode
}

function Invoke-CommandSuite {
    param(
        [string]$Name,
        [string]$Phase,
        [string]$FilePath,
        [string[]]$Arguments,
        [int]$TimeoutSec
    )

    Write-TestLog "SUITE" "Starting $Name ($Phase)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $logPath = Join-Path $Artifacts (($Name -replace '[^A-Za-z0-9._-]', '_') + ".log")
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue

    try {
        $commandResult = Invoke-ExternalCommand -FilePath $FilePath -Arguments $Arguments -TimeoutSec $TimeoutSec -OutputPath $logPath
    }
    finally {
        $sw.Stop()
    }

    Write-OutputLinesToTestLog -Lines $commandResult.Output
    $passed = $commandResult.ExitCode -eq 0
    if (-not $passed) {
        $script:anyFailed = $true
    }

    $result = [ordered]@{
        name = $Name
        phase = $Phase
        passed = $passed
        durationSec = [math]::Round($sw.Elapsed.TotalSeconds, 2)
        exitCode = $commandResult.ExitCode
        log = $logPath
        timedOut = $commandResult.TimedOut
        failedTests = @()
    }
    if (-not $passed) {
        $message = if ($commandResult.TimedOut) {
            "Timed out after ${TimeoutSec}s."
        }
        else {
            "Suite exited with code $($commandResult.ExitCode)."
        }

        $result.failedTests = @([ordered]@{ name = $Name; outcome = "Failed"; message = $message })
    }

    $script:suiteResults.Add($result) | Out-Null

    if ($passed) {
        Write-TestLog "PASS" "$Name completed in $($result.durationSec)s"
        Write-TestLog "ARTIFACT" $logPath
        return 0
    }

    Write-TestLog "FAIL" "$Name failed: $($result.failedTests[0].message)"
    Write-TestLog "ARTIFACT" $logPath
    $commandResult.Output |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Last 20 |
        ForEach-Object { Write-TestLog "CONTEXT" $_ }
    return $commandResult.ExitCode
}

Write-TestLog "SUITE" "Balance Board Controller test pipeline ($(if ($Quick) { 'Quick' } else { 'Full' }))"
Write-TestLog "ARTIFACT" $Artifacts
Write-TestLog "TEST" "Physical hardware validation is excluded by default. Run BalanceBoardApp.exe --physical-test connect-basic manually when hardware is available."

try {
    Stop-TestProcesses

    if (-not $SkipBuild) {
        Invoke-CommandSuite `
            -Name "Build gate" `
            -Phase "build" `
            -FilePath "dotnet" `
            -Arguments @("build", "BalanceBoard.sln", "-c", "Release", "-warnaserror") `
            -TimeoutSec $TimeoutsSec.Build | Out-Null

        if ($anyFailed) {
            throw "Build failed."
        }
    }

    & (Join-Path $Root "scripts\dev\stop.ps1") | Out-Null

    # Layer 1: unit + property tests
    Invoke-DotNetTestSuite -Name "BalanceBoard.Core.Tests" -Project "tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj" -Phase "unit" -TimeoutSec $TimeoutsSec.Unit | Out-Null
    if (-not $Quick) {
        Invoke-DotNetTestSuite -Name "BalanceBoard.Fuzz.Tests" -Project "tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj" -Phase "unit" -TimeoutSec $TimeoutsSec.Unit | Out-Null
    }

    # Layer 2: integration (session/connect fakes)
    $integrationFilter = if ($Quick) { $slowFilter } else { "" }
    Invoke-DotNetTestSuite -Name "BalanceBoard.Integration.Tests" -Project "tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj" -Phase "integration" -Filter $integrationFilter -TimeoutSec $TimeoutsSec.Integration | Out-Null

    # Layer 3: headless WPF UI tests
    Invoke-DotNetTestSuite -Name "BalanceBoard.App.Ui.Tests" -Project "tests/BalanceBoard.App.Ui.Tests/BalanceBoard.App.Ui.Tests.csproj" -Phase "ui" -TimeoutSec $TimeoutsSec.Ui | Out-Null

    # Layer 4: tools / diagnostics CLI
    Invoke-CommandSuite `
        -Name "BalanceBoard.Validate" `
        -Phase "tools" `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "tools/Validate/BalanceBoard.Validate.csproj", "-c", "Release", "--no-build") `
        -TimeoutSec $TimeoutsSec.Tools | Out-Null

    # Layer 5: lifecycle / process smoke
    $lifecycleFilter = if ($Quick) { $slowFilter } else { "" }
    Invoke-DotNetTestSuite -Name "BalanceBoard.Automation" -Project "tests/BalanceBoard.Automation/BalanceBoard.Automation.csproj" -Phase "lifecycle" -Filter $lifecycleFilter -TimeoutSec $TimeoutsSec.Lifecycle | Out-Null

    if (-not $SkipMeta) {
        Invoke-CommandSuite `
            -Name "verify-tests.ps1" `
            -Phase "meta" `
            -FilePath $PowerShellExe `
            -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $PSScriptRoot "verify-tests.ps1"), "-VerifyCountsOnly") `
            -TimeoutSec $TimeoutsSec.Meta | Out-Null
    }
}
finally {
    Stop-TestProcesses
    & (Join-Path $Root "scripts\dev\stop.ps1") | Out-Null
}

$endedAt = Get-Date
$summary = [ordered]@{
    mode = if ($Quick) { "quick" } else { "full" }
    startedAtUtc = $StartedAt.ToUniversalTime().ToString("o")
    endedAtUtc = $endedAt.ToUniversalTime().ToString("o")
    durationSec = [math]::Round(($endedAt - $StartedAt).TotalSeconds, 2)
    passed = -not $anyFailed
    artifactsDirectory = $Artifacts
    logFile = $LogFile
    suites = $suiteResults
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $SummaryFile -Encoding UTF8
Write-TestLog "ARTIFACT" $SummaryFile

if ($anyFailed) {
    Write-TestLog "FAIL" "Test pipeline failed"
    exit 1
}

Write-TestLog "PASS" "All test suites passed"
exit 0
