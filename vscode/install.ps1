# Installs the Cpp2 VS Code extension for the current user by assembling it in
# VS Code's extensions folder: this folder's manifest plus the shared grammar
# and language configuration from ..\grammar. Rerun it after changing either.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File vscode\install.ps1

param(
    [string]$ExtensionsDir = (Join-Path $env:USERPROFILE '.vscode\extensions')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Raw | ConvertFrom-Json
$target = Join-Path $ExtensionsDir "$($manifest.publisher).$($manifest.name)-$($manifest.version)"
$grammarDir = Join-Path $PSScriptRoot '..\grammar'

if (Test-Path -LiteralPath $target)
{
    Remove-Item -LiteralPath $target -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $target 'syntaxes') | Out-Null

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Destination $target
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $target
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\LICENSE') -Destination $target
Copy-Item -LiteralPath (Join-Path $grammarDir 'language-configuration.json') -Destination $target
Copy-Item -LiteralPath (Join-Path $grammarDir 'cpp2.tmLanguage.json') -Destination (Join-Path $target 'syntaxes')

Write-Output "Installed to $target. Reload VS Code to pick it up."
