# https://github.com/edymtt/nugetstandalone

try {
	Get-Command -Name 'nuget' -Type 'Application' -ErrorAction 'Stop' | Out-Null
} catch {
	Write-Host '''nuget'' not installed.' -ForegoundColor DarkRed
	exit 258
}

$destinationFolder = "$PSScriptRoot\packages"
if (Test-Path $destinationFolder) { Remove-Item $destinationFolder -Recurse }
New-Item $destinationFolder -Type 'Directory' | Out-Null

nuget install packages.config -o $destinationFolder -ExcludeVersion
