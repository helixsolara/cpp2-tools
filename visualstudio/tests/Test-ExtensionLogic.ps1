# Tests the Visual Studio extension's logic that doesn't need Visual Studio:
# finding a file in a build's cpp2_sources.json, parsing cppfront's diagnostics,
# running cppfront, mapping source lines to generated ones, building a single
# file with the bundled cppfront and Visual Studio's compiler, and copying the
# MSBuild rules to their stable folder. It loads the built extension assembly, and uses the
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
    $parsed = [Cpp2.VisualStudio.Diagnostic]::Parse($output, 'D:\work')
    Assert-True ($parsed.Count -eq 2) 'Parses the error and warning lines only'
    Assert-True ($parsed[0].File -eq 'C:\src\bad.cpp2' -and $parsed[0].Line -eq 3 -and $parsed[0].Column -eq 14 -and $parsed[0].IsError) 'Reads a full path, line and column'
    Assert-True ($parsed[1].File -eq 'D:\work\sub\other.cpp2' -and $parsed[1].Column -eq 0 -and -not $parsed[1].IsError) 'Resolves a relative path, and allows no column'

    $compiler = "D:/src/a.cpp2(4): error C2065: 'y': undeclared identifier`nD:\src\b.cpp2(9,2): fatal error C1083: Cannot open include file`n"
    $parsed = [Cpp2.VisualStudio.Diagnostic]::Parse($compiler, 'D:\work')
    Assert-True ($parsed.Count -eq 2 -and $parsed[0].File -eq 'D:\src\a.cpp2' -and $parsed[0].Line -eq 4 -and $parsed[0].Message -eq "C2065: 'y': undeclared identifier") "Reads MSVC's errors, with their code"
    Assert-True ($parsed[1].IsError -and $parsed[1].Column -eq 2) "Reads MSVC's fatal errors"
}

# Builds source on its own as Build and Run Cpp2 File does; returns the build and its result
function Invoke-SingleFileBuild {
    param([string]$Source, [string[]]$Options, [string]$Cppfront)
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $visualStudio = & $vswhere -latest -property installationPath
    $vcvars = Join-Path $visualStudio 'VC\Auxiliary\Build\vcvars64.bat'
    $include = Join-Path $repoRoot 'external\cppfront\include'
    $work = Join-Path $WorkDir ('run-' + [IO.Path]::GetFileNameWithoutExtension($Source))
    $build = New-Object Cpp2.VisualStudio.SingleFileBuild($vcvars, $Cppfront, [string[]]$Options, $include, $Source, $work)
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    [IO.File]::WriteAllText($build.ScriptPath, $build.Script())
    $result = [Cpp2.VisualStudio.ToolRunner]::RunAsync('cmd.exe', "/d /s /c `"`"$($build.ScriptPath)`"`"", $work,
        [TimeSpan]::FromMinutes(5), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    return @{ Build = $build; Result = $result }
}

function Test-SingleFileBuild {
    param([string]$Cppfront)
    $mixed = Join-Path $WorkDir 'mixed.cpp2'
    [IO.File]::WriteAllText($mixed, "#include <string>`nmain: () =`n{`n    s: std::string = `"Built on its own`";`n    std::cout << s << `"\n`";`n}`n")
    $built = Invoke-SingleFileBuild -Source $mixed -Options @() -Cppfront $Cppfront
    Assert-True $built.Result.Succeeded 'Builds a mixed Cpp2 file on its own'
    Assert-True ((& $built.Build.Program | Out-String) -match 'Built on its own') 'Runs the program it built'

    $pure = Join-Path $WorkDir 'pure.cpp2'
    [IO.File]::WriteAllText($pure, "main: () =`n{`n    std::cout << `"Pure and import std`\n`";`n}`n")
    $built = Invoke-SingleFileBuild -Source $pure -Options @('-pure-cpp2') -Cppfront $Cppfront
    Assert-True $built.Result.Succeeded 'Builds a pure Cpp2 file, with the std modules'
    if ($built.Result.Succeeded) {
        Assert-True ((& $built.Build.Program | Out-String) -match 'Pure and import std') 'Runs the pure program'
    }

    $wrong = Join-Path $WorkDir 'wrong.cpp2'
    [IO.File]::WriteAllText($wrong, "main: () -> int =`n{`n    x: int = 1;`n    return y;`n}`n")
    $built = Invoke-SingleFileBuild -Source $wrong -Options @() -Cppfront $Cppfront
    $errors = [Cpp2.VisualStudio.Diagnostic]::Parse($built.Result.Output, $built.Build.WorkDir)
    Assert-True (-not $built.Result.Succeeded) 'Fails to build a file the compiler rejects'
    Assert-True (@($errors | Where-Object { $_.File -eq $wrong -and $_.Line -eq 4 -and $_.IsError }).Count -ge 1) "Reports the compiler's error at the .cpp2 line"
}

function Test-RulesDeployment {
    $from = Join-Path $WorkDir 'rules-from'
    $to = Join-Path $WorkDir 'rules-to'
    Remove-Item -LiteralPath $from, $to -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path (Join-Path $from 'cppfront') | Out-Null
    [IO.File]::WriteAllText((Join-Path $from 'Cpp2.targets'), 'first')
    [IO.File]::WriteAllText((Join-Path $from 'cppfront\cppfront.exe'), 'binary')

    [Cpp2.VisualStudio.MSBuildRules]::Deploy($from, $to)
    Assert-True ((Test-Path (Join-Path $to 'Cpp2.targets')) -and (Test-Path (Join-Path $to 'cppfront\cppfront.exe'))) 'Copies the rules and cppfront, keeping folders'

    [IO.File]::WriteAllText((Join-Path $from 'Cpp2.targets'), 'second, longer')
    [Cpp2.VisualStudio.MSBuildRules]::Deploy($from, $to)
    Assert-True ([IO.File]::ReadAllText((Join-Path $to 'Cpp2.targets')) -eq 'second, longer') 'Copies a file again when it changed'
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

function Test-LineMapping {
    # As cppfront lays it out: a declaration of f from line 4, then its definition
    $generated = [string[]]@(
        '#include "cpp2util.h"',        # 1
        '#line 4 "a.cpp2"',             # 2
        'auto f() -> int;',             # 3: from 4
        '',                             # 4: from 5
        '#line 4 "a.cpp2"',             # 5
        'auto f() -> int {',            # 6: from 4
        '    return 42;',               # 7: from 5
        '}'                             # 8: from 6
    )
    Assert-True ([Cpp2.VisualStudio.GeneratedCode]::FindLine($generated, 4) -eq 6) 'Maps a line to its definition, not its declaration'
    Assert-True ([Cpp2.VisualStudio.GeneratedCode]::FindLine($generated, 5) -eq 7) 'Counts lines after a #line directive'
    Assert-True ([Cpp2.VisualStudio.GeneratedCode]::FindLine($generated, 9) -eq 8) 'Falls back to the nearest earlier line'
    Assert-True ([Cpp2.VisualStudio.GeneratedCode]::FindLine($generated, 2) -eq 1) 'Falls back to the first line before any code'

    # And on real cppfront output: main is defined from line 1 of good.cpp2
    $real = [IO.File]::ReadAllLines((Join-Path $WorkDir 'out\good.cpp'))
    $line = [Cpp2.VisualStudio.GeneratedCode]::FindLine($real, 1)
    Assert-True ($real[$line - 1] -match 'auto main\(\) -> int') 'Finds main in real cppfront output'
}

$cppfront = Test-SourcesFile
Test-Diagnostics
Test-Run -Cppfront $cppfront
Test-LineMapping
Test-SingleFileBuild -Cppfront (Join-Path $PSScriptRoot '..\obj\cppfront\cppfront.exe')
Test-RulesDeployment

if ($script:failures) {
    Write-Output "$script:failures failed"
    exit 1
}
Write-Output 'All passed'
# Explicit, since $LASTEXITCODE may still hold a failure a test expected, and a
# caller such as GitHub's pwsh step would exit with it
exit 0
