param(
    [Parameter(Mandatory=$true)][string]$Payload,
    [Parameter(Mandatory=$true)][string]$Icon,
    [string]$Output
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (-not $Output) { $Output = Join-Path $root 'build/ReZero_Arabic_Patch_v2.2.exe' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
$iconPath = (Resolve-Path -LiteralPath $Icon).Path
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
New-Item -Force -ItemType Directory (Split-Path $outputPath) | Out-Null
$compilerArgs = @(
    '/nologo', '/nowarn:0649', '/target:winexe', '/platform:anycpu', '/optimize+',
    "/win32manifest:$root\src\app.manifest", "/win32icon:$iconPath",
    "/resource:$payloadPath,payload.zip", '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll', '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll', '/reference:System.Web.Extensions.dll',
    "/out:$outputPath", "$root\src\Installer.cs"
)
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "C# compiler failed with exit code $LASTEXITCODE." }
Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
