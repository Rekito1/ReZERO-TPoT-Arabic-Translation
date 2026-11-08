# Run each invocation in a separate Windows PowerShell 5.1 process.
param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$Report,
    [string]$ExtractPayload
)
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path -LiteralPath $Installer).Path
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
foreach ($reference in $assembly.GetReferencedAssemblies()) {
    [void][Reflection.Assembly]::ReflectionOnlyLoad($reference.FullName)
}
$sha = [Security.Cryptography.SHA256]::Create()
function Get-HexHash([byte[]]$Bytes) {
    [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()
}
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
$types = foreach ($type in ($assembly.GetTypes() | Sort-Object FullName)) {
    $methods = @($type.GetMethods($flags)) + @($type.GetConstructors($flags))
    if ($type.TypeInitializer -and $type.TypeInitializer.MetadataToken -notin $methods.MetadataToken) {
        $methods += $type.TypeInitializer
    }
    [ordered]@{
        name = $type.FullName
        base = if ($type.BaseType) { $type.BaseType.FullName } else { $null }
        attributes = [string]$type.Attributes
        fields = @($type.GetFields($flags) | Sort-Object MetadataToken | ForEach-Object { $_.ToString() })
        methods = @(foreach ($method in ($methods | Sort-Object MetadataToken)) {
            $body = $method.GetMethodBody()
            [ordered]@{
                signature = $method.ToString()
                attributes = [string]$method.Attributes
                implementation = [string]$method.GetMethodImplementationFlags()
                customAttributes = @($method.GetCustomAttributesData() | ForEach-Object { $_.ToString() })
                il = if ($body) { Get-HexHash $body.GetILAsByteArray() } else { $null }
                locals = if ($body) { @($body.LocalVariables | ForEach-Object { $_.ToString() }) } else { $null }
                exceptionHandlers = if ($body) { @($body.ExceptionHandlingClauses | ForEach-Object { $_.ToString() }) } else { $null }
            }
        })
    }
}
$resource = $assembly.GetManifestResourceStream('payload.zip')
if (-not $resource) { throw 'Embedded payload.zip resource is missing.' }
try {
    $payloadHash = [BitConverter]::ToString($sha.ComputeHash($resource)).Replace('-', '').ToLowerInvariant()
    if ($ExtractPayload) {
        $resource.Position = 0
        $target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ExtractPayload)
        $stream = [IO.File]::Open($target, [IO.FileMode]::CreateNew)
        try { $resource.CopyTo($stream) } finally { $stream.Dispose() }
    }
} finally { $resource.Dispose(); $sha.Dispose() }
[ordered]@{
    fileHash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    bytes = (Get-Item -LiteralPath $path).Length
    moduleVersionId = [string]$assembly.ManifestModule.ModuleVersionId
    assembly = $assembly.FullName
    entryPoint = $assembly.EntryPoint.ToString()
    references = @($assembly.GetReferencedAssemblies() | ForEach-Object { $_.FullName } | Sort-Object)
    resources = @($assembly.GetManifestResourceNames() | Sort-Object)
    payloadHash = $payloadHash
    types = @($types)
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Report -Encoding UTF8
