param(
    [Parameter(Mandatory)] [string] $AppcastPath,
    [Parameter(Mandatory)] [string] $ExpectedVersion,
    [Parameter(Mandatory)] [string] $ExpectedInstallerUrl,
    [string[]] $RequiredVersions = @(),
    [int] $MinimumItemCount = 1
)

$ErrorActionPreference = 'Stop'
$sparkleNamespace = 'http://www.andymatuschak.org/xml-namespaces/sparkle'

if (-not (Test-Path -LiteralPath $AppcastPath)) {
    throw "Appcast was not generated at $AppcastPath."
}
if (-not (Test-Path -LiteralPath "$AppcastPath.signature") -or
    [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath "$AppcastPath.signature" -Raw))) {
    throw 'The detached appcast signature is missing or empty.'
}

[xml] $appcast = Get-Content -LiteralPath $AppcastPath -Raw
$namespaces = [System.Xml.XmlNamespaceManager]::new($appcast.NameTable)
$namespaces.AddNamespace('sparkle', $sparkleNamespace)
$items = @($appcast.SelectNodes('/rss/channel/item'))
if ($items.Count -lt $MinimumItemCount) {
    throw "The appcast contains $($items.Count) item(s); expected at least $MinimumItemCount."
}

$versions = @()
foreach ($item in $items) {
    $versionNode = $item.SelectSingleNode('sparkle:version', $namespaces)
    $version = $versionNode.InnerText
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'Every appcast item must have a sparkle:version.'
    }
    $versions += $version

    $description = $item.SelectSingleNode('description')
    $releaseNotesLink = $item.SelectSingleNode('sparkle:releaseNotesLink', $namespaces)
    if (($null -eq $description -or [string]::IsNullOrWhiteSpace($description.InnerText)) -and
        ($null -eq $releaseNotesLink -or [string]::IsNullOrWhiteSpace($releaseNotesLink.InnerText))) {
        throw "Appcast version $version has no release notes description or release-notes link."
    }

    $enclosure = $item.SelectSingleNode('enclosure')
    if ($null -eq $enclosure -or
        [string]::IsNullOrWhiteSpace($enclosure.GetAttribute('signature', $sparkleNamespace))) {
        throw "Appcast version $version has no signed installer enclosure."
    }
}

$duplicates = @($versions | Group-Object | Where-Object Count -gt 1)
if ($duplicates.Count -gt 0) {
    throw "The appcast contains duplicate version(s): $($duplicates.Name -join ', ')."
}
for ($index = 1; $index -lt $versions.Count; $index++) {
    if ([System.Management.Automation.SemanticVersion] $versions[$index - 1] -lt
        [System.Management.Automation.SemanticVersion] $versions[$index]) {
        throw 'Appcast versions must be ordered newest first.'
    }
}

if ($versions[0] -ne $ExpectedVersion) {
    throw "The newest appcast version '$($versions[0])' does not match '$ExpectedVersion'."
}
$currentEnclosure = $items[0].SelectSingleNode('enclosure')
if ($currentEnclosure.GetAttribute('url') -ne $ExpectedInstallerUrl) {
    throw "The current installer URL does not match '$ExpectedInstallerUrl'."
}
if ($currentEnclosure.GetAttribute('version', $sparkleNamespace) -ne $ExpectedVersion) {
    throw 'The current enclosure version does not match the release version.'
}
foreach ($requiredVersion in $RequiredVersions) {
    if ($requiredVersion -notin $versions) {
        throw "Previously published appcast version '$requiredVersion' was not retained."
    }
}

Write-Output "Validated $($items.Count) signed appcast item(s); current version is $ExpectedVersion."
