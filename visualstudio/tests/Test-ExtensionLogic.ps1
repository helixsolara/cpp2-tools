# Tests the Visual Studio extension's logic that doesn't need Visual Studio:
# finding a file in a build's cpp2_sources.json, parsing cppfront's diagnostics,
# and running cppfront. It loads the built extension assembly, and uses the
# cpp2-tools build (configure, build and test it first, see README.md).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File visualstudio\tests\Test-ExtensionLogic.ps1

param(
    [string]$Assembly = (Join-Path $PSScriptRoot '..\bin\Release\Cpp2.VisualStudio.dll'),
    [string]$WorkDir = (Join-Path ([IO.Path]::GetTempPath()) 'cpp2-tools-extension-tests')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
# LoadFrom, not Add-Type: Add-Type loads every type, and the package's types
# need Visual Studio's assemblies, which only the tested types can do without
[Reflection.Assembly]::LoadFrom((Resolve-Path $Assembly).Path) | Out-Null

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

function Test-SourcesFile {
    $greeting = Join-Path $repoRoot 'tests\cmake\modules\greeting\greeting.cpp2'
    $found = [Cpp2.VisualStudio.SourcesFile]::Find($greeting)
    Assert-True ($null -ne $found) 'Finds a source the build lists'
    Assert-True ($found.BuildOutput -like '*\cpp2_greeting\greeting.ixx') 'Gives the build output of a module unit'
    Assert-True (Test-Path -LiteralPath $found.Cppfront) 'Gives a cppfront that exists'

    New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
    $unlisted = Join-Path $WorkDir 'unlisted.cpp2'
    Set-Content -LiteralPath $unlisted -Value 'main: () = { }'
    Assert-True ($null -eq [Cpp2.VisualStudio.SourcesFile]::Find($unlisted)) 'Finds nothing for a source no build lists'
    return $found.Cppfront
}

function Test-Diagnostics {
    $output = "C:\src\bad.cpp2...`nC:\src\bad.cpp2(3,14): error: empty statement`nsub\other.cpp2(7): warning: odd`n"
    $parsed = [Cpp2.VisualStudio.CppfrontDiagnostic]::Parse($output, 'D:\work')
    Assert-True ($parsed.Count -eq 2) 'Parses the error and warning lines only'
    Assert-True ($parsed[0].File -eq 'C:\src\bad.cpp2' -and $parsed[0].Line -eq 3 -and $parsed[0].Column -eq 14 -and $parsed[0].IsError) 'Reads a full path, line and column'
    Assert-True ($parsed[1].File -eq 'D:\work\sub\other.cpp2' -and $parsed[1].Column -eq 0 -and -not $parsed[1].IsError) 'Resolves a relative path, and allows no column'
}

function Test-Run {
    param([string]$Cppfront)
    $bad = Join-Path $WorkDir 'bad.cpp2'
    [IO.File]::WriteAllText($bad, "main: () =`n{`n    x: int = ;`n}`n")
    $good = Join-Path $WorkDir 'good.cpp2'
    [IO.File]::WriteAllText($good, "main: () = { }`n")
    $outDir = Join-Path $WorkDir 'out'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $none = [Threading.CancellationToken]::None

    $failed = [Cpp2.VisualStudio.Cppfront]::RunAsync($Cppfront, [string[]]@(), $bad, (Join-Path $outDir 'bad.cpp'), $none).GetAwaiter().GetResult()
    Assert-True (-not $failed.Succeeded) 'Reports failure for a file with an error'
    Assert-True ($failed.Output -like "*$bad(3,14): error:*") 'Captures the error, naming the full path'

    $goodOutput = Join-Path $outDir 'good.cpp'
    $passed = [Cpp2.VisualStudio.Cppfront]::RunAsync($Cppfront, [string[]]@('-pure-cpp2'), $good, $goodOutput, $none).GetAwaiter().GetResult()
    Assert-True ($passed.Succeeded -and (Test-Path -LiteralPath $goodOutput)) 'Transpiles a good file to the output asked for'
}

$cppfront = Test-SourcesFile
Test-Diagnostics
Test-Run -Cppfront $cppfront

if ($script:failures) {
    Write-Output "$script:failures failed"
    exit 1
}
Write-Output 'All passed'
