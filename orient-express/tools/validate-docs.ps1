[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    $python = Get-Command py -ErrorAction Stop
}

& $python.Source (Join-Path $PSScriptRoot "generate_graphs.py") $Root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $python.Source (Join-Path $PSScriptRoot "validate_docs.py") $Root
exit $LASTEXITCODE
