Push-Location $PSScriptRoot

# Prepare
$csc = "$PSScriptRoot\packages\Microsoft.Net.Compilers\tools\csc.exe"
$build = "$PSScriptRoot\build"
$dist = "$PSScriptRoot\dist"
$src = "$PSScriptRoot\src"
New-Item -Path $build, $dist -ItemType 'Directory' -ErrorAction 'SilentlyContinue' | Out-Null
Remove-Item "$build\*", "$dist\*" -Recurse -Force

if ((Get-ChildItem "$PSScriptRoot\packages" -Recurse).Count -eq 0) {
    Write-Host 'Dependencies are missing. Run ''install.ps1''' -ForegroundColor 'DarkRed'
    exit 258
}

# Build
Copy-Item "$PSScriptRoot\packages\Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll", "$PSScriptRoot\packages\Newtonsoft.Json.Schema\lib\net45\Newtonsoft.Json.Schema.dll", "$PSscriptRoot\packages\YamlDotNet\lib\net45\YamlDotNet.dll" $build

Write-Output 'Compiling Scoop.Validator.cs ...'
$ScoopValidatorCs = @(
    '/deterministic'
    '/nologo'
    '/optimize'
    '/platform:anycpu'
    '/target:library'
    "/reference:""$build\Newtonsoft.Json.dll"",""$build\Newtonsoft.Json.Schema.dll"",""$build\YamlDotNet.dll"""
    "/out:""$build\Scoop.Validator.dll"""
    "$src\Scoop.Validator.cs"
)
& $csc @ScoopValidatorCs
if ($LASTEXITCODE -gt 0) { exit 1 }

Write-Output 'Compiling validator.cs ...'
$ValidatorCs = @(
    '/deterministic'
    '/nologo'
    '/optimize'
    '/platform:anycpu'
    '/target:exe'
    "/reference:""$build\Scoop.Validator.dll"",""$build\Newtonsoft.Json.dll"",""$build\Newtonsoft.Json.Schema.dll"""
    "/out:""$build\validator.exe"""
    "$src\validator.cs"
)
& $csc @ValidatorCs
if ($LASTEXITCODE -gt 0) { exit 1 }

# Package
7z a "$dist\validator.zip" "$build\*"
if ($LASTEXITCODE -gt 0) { exit 1 }
Get-ChildItem "$dist\*" | ForEach-Object {
    $checksum = (Get-FileHash -Path $_.FullName -Algorithm 'SHA256').Hash.ToLower()
    "$checksum *$($_.Name)" | Tee-Object -FilePath "$dist\$($_.Name).sha256" -Append
}

Pop-Location
