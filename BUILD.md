# Build and inspect v2.2

The shipping installer is a C# Windows Forms application targeting the installed .NET Framework 4.x runtime. The verified compiler was the Windows x64 .NET Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Its file version was `4.8.9221.0` (`NET481REL1LAST_25H2`) and its SHA-256 was `46809206887326d2d24db1eff1f3064de972c3451abe766b49111450a5e08e00`. The build references the framework assemblies `System.Windows.Forms`, `System.Drawing`, `System.IO.Compression`, `System.IO.Compression.FileSystem`, and `System.Web.Extensions`. There are no NuGet packages. Use Windows PowerShell 5.1 for the reflection-only inspection tool. Python 3.12.0 with zlib 1.2.13 was used to regenerate the payload. The Python tools use only the standard library.

## Obtain private inputs

Keep build inputs in a local, ignored `private` folder. Obtain the exact v2.2 `payload.zip` and `app.ico` from the maintainer where permitted, or extract them locally from a trusted copy of the reference installer. Check the EXE against `recorded_release_sha256` in `release.json` before using it as a reference. The extraction commands map the executable as data or read it in a reflection-only context. They do not invoke its entry point.

```powershell
New-Item -ItemType Directory -Force private, build
Get-FileHash .\private\Nexus-v2.2.exe -Algorithm SHA256
powershell.exe -NoProfile -File .\tools\Inspect-Installer.ps1 `
    -Installer .\private\Nexus-v2.2.exe -Report .\build\nexus.json `
    -ExtractPayload .\private\payload.zip
python .\tools\extract_icon.py .\private\Nexus-v2.2.exe .\private\app.ico
Get-FileHash .\private\payload.zip, .\private\app.ico -Algorithm SHA256
```

Match the input hashes in `release.json`. The icon extractor reproduced the exact original icon bytes when tested on the local GUI rebuild. Its behavior on the published EXE has not been independently tested. The exact payload is necessary to compare the released installer without introducing new ZIP metadata. Use a fresh extraction path because the extraction tools refuse to overwrite an existing output. On systems with a restrictive script policy, review the scripts and run them using your organization's approved PowerShell execution policy.

## Build the GUI executable

Run from the repository root in PowerShell.

```powershell
powershell.exe -NoProfile -File .\tools\Build.ps1 `
    -Payload .\private\payload.zip -Icon .\private\app.ico
Get-FileHash .\build\ReZero_Arabic_Patch_v2.2.exe -Algorithm SHA256
powershell.exe -NoProfile -File .\tools\Inspect-Installer.ps1 `
    -Installer .\build\ReZero_Arabic_Patch_v2.2.exe -Report .\build\rebuilt.json
```

`Build.ps1` uses the original GUI target, AnyCPU platform, release optimization, embedded `payload.zip` resource name, and native manifest and icon options. It uses the historical output basename by default. To compare a reference and a rebuild, compare the reports' `entryPoint`, `references`, `resources`, `payloadHash`, and `types` fields. The `types` field records method signatures, attributes, IL hashes, locals, and exception clauses. Compare full EXE hashes separately. Matching this inspection report does not by itself compare all native resources or all metadata tables. A different module version ID and PE timestamp are expected with the historical compiler.

## Regenerate the payload from matching inputs

This path is for reviewers with lawful access to the original and finalized localized files as well as the original installer artwork. The source generator produces the payload from those files. It does not recreate the translation, artwork, or localized video from an unmodified game. The exact hashes for all seven inputs are in `release.json`.

```powershell
python .\tools\prepare.py `
    --original-archive .\private\original\00000000.cfsi `
    --localized-archive .\private\localized\00000000.cfsi `
    --original-exe .\private\original\ReZERO-TPT.exe `
    --localized-exe .\private\localized\ReZERO-TPT.exe `
    --original-movie .\private\original\op_STEAM_en.webm `
    --localized-movie .\private\localized\op_STEAM_en.webm `
    --artwork .\private\trio-nintendo.jpg `
    --output .\build\regenerated-payload.zip
python .\tools\Compare-Payloads.py .\private\payload.zip .\build\regenerated-payload.zip
```

The ZIP records creation-time timestamps for generated members and a file modification timestamp for the artwork. Regenerating it at another time can change its SHA-256 even with identical member contents. For binary review, extract the historical payload rather than substituting the regenerated ZIP. Treat all private inputs and generated installers as excluded from publication.

## Isolated install and restore test

Use a disposable copy of the exact original archive, game executable, and opening video in their game-relative paths. Keep the live Steam installation separate. Compile the same source as a console executable to run its existing `--selftest` entry point without the release manifest. It installs the patch, verifies the applied resources, restores the originals, and checks the original hashes. `tools/FaultTest.cs` is the retained manual fault test from the project. It can be compiled alongside `Installer.cs` with `/main:FaultTest` to test an exception after writing the movie and rejection of an altered source movie.

```powershell
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /nowarn:0649 /target:exe /platform:anycpu /optimize+ `
    '/resource:private\payload.zip,payload.zip' /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll `
    /out:build\selftest.exe src\Installer.cs
if ($LASTEXITCODE -eq 0) { & .\build\selftest.exe --selftest .\private\test-game }
```
