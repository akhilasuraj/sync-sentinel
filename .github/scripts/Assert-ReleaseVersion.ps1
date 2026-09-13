param(
    [Parameter(Mandatory = $true)] [string] $ActualVersion,
    [Parameter(Mandatory = $true)] [string] $ExpectedVersion,
    [Parameter(Mandatory = $true)] [string] $ArtifactName
)

$normalizedActualVersion = $ActualVersion.Trim()
$normalizedExpectedVersion = $ExpectedVersion.Trim()

if ($normalizedActualVersion -ne $normalizedExpectedVersion) {
    throw "$ArtifactName reports '$normalizedActualVersion'; expected '$normalizedExpectedVersion'."
}

Write-Output "$ArtifactName version '$normalizedActualVersion' matches the release version."
