$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $scriptDir "src"
$outExe = Join-Path $scriptDir "AntigravityTokenOrb.exe"

$netDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpfDir = Join-Path $netDir "WPF"
$csc = Join-Path $netDir "csc.exe"

if (-not (Test-Path $csc)) {
    throw "未找到 .NET Framework 4.x 编译器: $csc"
}

$sources = @(
    (Join-Path $srcDir "Models.cs"),
    (Join-Path $srcDir "JsonLineParser.cs"),
    (Join-Path $srcDir "ThemeManager.cs"),
    (Join-Path $srcDir "SettingsManager.cs"),
    (Join-Path $srcDir "TokenMonitorEngine.cs"),
    (Join-Path $srcDir "SettingsWindow.cs"),
    (Join-Path $srcDir "DashboardWindow.cs"),
    (Join-Path $srcDir "OrbWindow.cs"),
    (Join-Path $srcDir "TrayManager.cs"),
    (Join-Path $srcDir "Program.cs")
)

$refs = @(
    (Join-Path $wpfDir "PresentationFramework.dll"),
    (Join-Path $wpfDir "PresentationCore.dll"),
    (Join-Path $wpfDir "WindowsBase.dll"),
    (Join-Path $netDir "System.Xaml.dll"),
    (Join-Path $netDir "System.Web.Extensions.dll"),
    (Join-Path $netDir "System.Windows.Forms.dll"),
    (Join-Path $netDir "System.Drawing.dll")
)

$argsList = @(
    "/nologo",
    "/utf8output",
    "/optimize+",
    "/target:winexe",
    "/out:$outExe"
)

$icoPath = Join-Path $scriptDir "orb.ico"
if (Test-Path $icoPath) {
    $argsList += "/win32icon:$icoPath"
}

foreach ($r in $refs) {
    $argsList += "/reference:$r"
}

foreach ($s in $sources) {
    $argsList += $s
}

Write-Host "正在编译 AntigravityTokenOrb.exe ..." -ForegroundColor Cyan
& $csc @argsList
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码: $LASTEXITCODE"
}

Write-Host "编译成功: $outExe" -ForegroundColor Green
