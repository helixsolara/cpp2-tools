# Tests msbuild\Cpp2.props, Cpp2.targets and Cpp2.xml: builds the projects in
# this folder with MSBuild, runs them and checks their output, checks that a
# second build doesn't run cppfront again, and that cppfront's errors name the
# .cpp2 file. Needs Visual Studio with the C++ workload, and a cppfront: by
# default the one the Visual Studio extension's build makes (build the VSIX
# first, see visualstudio\README.md).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\msbuild\Test-Cpp2MSBuild.ps1

param(
    [string]$CppfrontPath = (Join-Path $PSScriptRoot '..\..\visualstudio\obj\cppfront\cppfront.exe'),
    [string]$IncludeDir = (Join-Path $PSScriptRoot '..\..\external\cppfront\include')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$CppfrontPath = (Resolve-Path -LiteralPath $CppfrontPath).Path
$IncludeDir = (Resolve-Path -LiteralPath $IncludeDir).Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found; install Visual Studio with the C++ workload'
}

$script:failures = 0
function Assert-True {
    param([bool]$Condition, [string]$Description)
    if ($Condition) {
        Write-Host "Passed: $Description"
    }
    else {
        Write-Host "FAILED: $Description"
        $script:failures++
    }
}

# Builds a test project; returns MSBuild's exit code and output
function Invoke-Build {
    param([string]$Name, [string]$Target = 'Build', [string]$Verbosity = 'minimal')
    $project = Join-Path $PSScriptRoot "$Name\$Name.vcxproj"
    $output = & $msbuild $project "/t:$Target" /p:Configuration=Debug /p:Platform=x64 `
        "/p:Cpp2CppfrontPath=$CppfrontPath" "/p:Cpp2IncludeDir=$IncludeDir" /nologo "/v:$Verbosity" 2>&1 | Out-String
    return @{ ExitCode = $LASTEXITCODE; Output = $output }
}

function Test-Program {
    param([string]$Name, [string]$Expected)
    Invoke-Build -Name $Name -Target 'Clean' | Out-Null
    $built = Invoke-Build -Name $Name
    Assert-True ($built.ExitCode -eq 0) "Builds $Name"
    if ($built.ExitCode -ne 0) {
        Write-Host $built.Output
        return
    }
    $ran = & (Join-Path $PSScriptRoot "$Name\bin\$Name.exe") | Out-String
    Assert-True ($ran -match [regex]::Escape($Expected)) "Runs $Name and prints '$Expected'"

    $again = Invoke-Build -Name $Name -Verbosity 'normal'
    Assert-True ($again.ExitCode -eq 0 -and $again.Output -notmatch [regex]::Escape($CppfrontPath)) "Doesn't run cppfront again for unchanged $Name"
}

function Test-Errors {
    $broken = Invoke-Build -Name 'broken'
    $source = Join-Path $PSScriptRoot 'broken\broken.cpp2'
    Assert-True ($broken.ExitCode -ne 0) 'Fails to build a broken .cpp2'
    Assert-True ($broken.Output -match [regex]::Escape("$source(6,14): error")) 'Reports the error at the .cpp2 file, line and column'
}

Test-Program -Name 'plain' -Expected 'Hello from Cpp2, 42'
Test-Program -Name 'modules' -Expected 'Hello from a Cpp2 module'
Test-Program -Name 'pure' -Expected 'Hello from pure Cpp2'
Test-Errors

if ($script:failures) {
    Write-Output "$script:failures failed"
    exit 1
}
Write-Output 'All passed'
# Explicit, since $LASTEXITCODE may still hold a failure a test expected, and a
# caller such as GitHub's pwsh step would exit with it
exit 0
