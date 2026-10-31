param([int]$Port=28779,[switch]$VisualClient)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$executable=Join-Path $projectRoot 'bin\Release\net8.0-windows\win-x64\IronNebula.exe'
if(!(Test-Path -LiteralPath $executable)) { throw 'Build the Release game before running this check.' }
$reportRoot=Join-Path $projectRoot ('bin\coop-smoke-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $reportRoot | Out-Null
$soloSave=Join-Path $env:LOCALAPPDATA 'IronNebula\run-save.json'
$before=if(Test-Path -LiteralPath $soloSave) { (Get-FileHash -LiteralPath $soloSave -Algorithm SHA256).Hash } else { 'ABSENT' }
$ownedProcesses=[Collections.Generic.List[Diagnostics.Process]]::new()
try {
    $hostLog=Join-Path $reportRoot 'host.json'
    $hostProcess=Start-Process -FilePath $executable -ArgumentList @('--coop-smoke','--coop-port',$Port,'--coop-players','6','--coop-log',('"'+$hostLog+'"')) -WorkingDirectory (Split-Path -Parent $executable) -WindowStyle Hidden -PassThru
    $ownedProcesses.Add($hostProcess)
    Start-Sleep -Seconds 3
    for($i=1;$i -le 5;$i++) {
        $botLog=Join-Path $reportRoot ('guest-'+$i+'.json')
        $clientMode=if($VisualClient -and $i -eq 1) { '--coop-visual-client' } else { '--coop-bot' }
        $process=Start-Process -FilePath $executable -ArgumentList @($clientMode,'--coop-port',$Port,'--name',('BOT'+$i),'--coop-log',('"'+$botLog+'"')) -WorkingDirectory (Split-Path -Parent $executable) -WindowStyle Hidden -PassThru
        $ownedProcesses.Add($process)
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(55)
    while(@($ownedProcesses | Where-Object { !$_.HasExited }).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    $reports=@(Get-ChildItem -LiteralPath $reportRoot -Filter '*.json' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    $after=if(Test-Path -LiteralPath $soloSave) { (Get-FileHash -LiteralPath $soloSave -Algorithm SHA256).Hash } else { 'ABSENT' }
    $success=$reports.Count -eq 6 -and @($reports | Where-Object { !$_.success }).Count -eq 0 -and $before -eq $after
    [pscustomobject]@{ Success=$success; ReportCount=$reports.Count; SoloSaveUnchanged=($before -eq $after); ReportDirectory=$reportRoot; Reports=$reports } | ConvertTo-Json -Depth 8
    if(!$success) { throw 'Co-op smoke failed; inspect the reports.' }
}
finally {
    # Only terminate helper processes created by this invocation, never an existing game.
    foreach($process in $ownedProcesses) { if(!$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }
}
