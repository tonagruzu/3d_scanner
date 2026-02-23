# Real-Camera Smoke Test (Windows)

Use this procedure to run a single non-UI smoke execution and verify that real-camera capture metadata is persisted in the validation artifact.

## Prerequisites
- Windows host
- USB camera connected and visible to the system
- .NET SDK (project uses `net8.0`)
- Repository root as current working directory

## Smoke Command
Run this one-liner from the repository root:

```powershell
$tmp = Join-Path $env:TEMP ("scanner3d-smoke-" + [Guid]::NewGuid().ToString("N")); dotnet new console -n Smoke -o $tmp | Out-Null; dotnet add (Join-Path $tmp "Smoke.csproj") reference ".\src\Scanner3D.Pipeline\Scanner3D.Pipeline.csproj" | Out-Null; @'
using System.Text.Json;
using Scanner3D.Core.Models;
using Scanner3D.Pipeline;

var orchestrator = new PipelineOrchestrator();
var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "test-device", "smoke");
var result = await orchestrator.ExecuteAsync(session);
Console.WriteLine($"ValidationPath={result.ValidationReportPath}");
using var stream = File.OpenRead(result.ValidationReportPath);
using var doc = await JsonDocument.ParseAsync(stream);
var caps = doc.RootElement.GetProperty("captureCapabilities");
var selected = caps.GetProperty("selectedCamera");
Console.WriteLine($"SelectedCamera.DeviceId={selected.GetProperty("deviceId").GetString()}");
Console.WriteLine($"SelectedCamera.DisplayName={selected.GetProperty("displayName").GetString()}");
Console.WriteLine($"BackendUsed={caps.GetProperty("backendUsed").GetString()}");
Console.WriteLine($"ModeList.Count={caps.GetProperty("modeList").GetArrayLength()}");
'@ | Set-Content -Path (Join-Path $tmp "Program.cs") -Encoding UTF8; dotnet run --project (Join-Path $tmp "Smoke.csproj") -c Release
```

## Expected Console Output
- `ValidationPath=output/<sessionId>/validation.json`
- `SelectedCamera.DeviceId=<non-empty>`
- `SelectedCamera.DisplayName=<non-empty>`
- `BackendUsed=<windows|opencv|mock|unknown>`
- `ModeList.Count=<>=1`

## Artifact Contract Check
Open the generated `validation.json` and verify `captureCapabilities` exists with:
- `selectedCamera.deviceId`
- `selectedCamera.displayName`
- `modeList[]`
- `backendUsed`

## Phase 2 Starter Checks (Calibration + Underlay)
In the same `validation.json`, verify the new Phase 2 starter outputs:

- `calibration.notes` contains `frame-derived` when real capture frames were available.
- `calibrationQuality.reprojectionResidualSamplesPx` has at least `3` values.
- `calibrationQuality.scaleResidualSamplesMm` has at least `3` values.
- `underlayVerification.measuredBoxSizesMm` has at least `3` values.
- `underlayVerification.expectedBoxSizeMm` is `10.0`.
- `underlayVerification.maxAbsoluteErrorMm <= 0.2` for a normal pass run.

Quick PowerShell check (replace `<path>` with your `validation.json`):

```powershell
$j = Get-Content "<path>" -Raw | ConvertFrom-Json
"CalibrationNotes={0}" -f $j.calibration.notes
"ReprojectionSamples={0}" -f $j.calibrationQuality.reprojectionResidualSamplesPx.Count
"ScaleSamples={0}" -f $j.calibrationQuality.scaleResidualSamplesMm.Count
"UnderlaySamples={0}" -f $j.underlayVerification.measuredBoxSizesMm.Count
"UnderlayExpected={0}" -f $j.underlayVerification.expectedBoxSizeMm
"UnderlayMaxError={0}" -f $j.underlayVerification.maxAbsoluteErrorMm
```

This smoke test is non-blocking for image quality; it validates capture pipeline plumbing and metadata persistence.
