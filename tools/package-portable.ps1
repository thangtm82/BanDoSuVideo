param(
    [Parameter(Mandatory=$true)][string]$EngineSource,
    [Parameter(Mandatory=$true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$source = [System.IO.Path]::GetFullPath($EngineSource).TrimEnd('\','/')
$targetRoot = [System.IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
$target = Join-Path $targetRoot 'studio'
foreach ($required in @('render.mjs', 'player.html')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) {
        throw "Engine invalid: missing $required"
    }
}
foreach ($required in @('tools','packs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Container)) {
        throw "Engine invalid: missing $required"
    }
}
if ($targetRoot.StartsWith($source + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
    $source.Equals($target, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The publish destination must not be inside the source engine.'
}
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
$excluded = @('.git','.venv','venv','node_modules','__pycache__','.pytest_cache','projects','build','out','dist','.next')
if (Test-Path -LiteralPath $target) {
    $entries = @(Get-ChildItem -LiteralPath $target -Force)
    if ($entries.Count -gt 0) { throw "Refusing to overwrite non-empty studio: $target" }
}
$stage = "$target.importing-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $stage | Out-Null
    function Copy-Engine([string]$src, [string]$dst) {
        foreach ($entry in Get-ChildItem -LiteralPath $src -Force) {
            if ($excluded -contains $entry.Name) { continue }
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            $next = Join-Path $dst $entry.Name
            if ($entry.PSIsContainer) {
                New-Item -ItemType Directory -Path $next | Out-Null
                Copy-Engine $entry.FullName $next
            } else {
                Copy-Item -LiteralPath $entry.FullName -Destination $next -ErrorAction Stop
            }
        }
    }
    Copy-Engine $source $stage
    foreach ($required in @('render.mjs', 'player.html', 'tools', 'packs')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stage $required))) { throw "Incomplete copy: $required" }
    }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    Move-Item -LiteralPath $stage -Destination $target
    Write-Host "Portable engine staged at $target"
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
