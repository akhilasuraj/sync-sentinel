param(
    [Parameter(Mandatory)] [string] $AppcastPath,
    [Parameter(Mandatory)] [string] $ExpectedVersion,
    [Parameter(Mandatory)] [string] $ExpectedInstallerUrl,
    [Parameter(Mandatory)] [string] $RepositoryUrl,
    [string[]] $RequiredVersions = @(),
    [int] $MinimumItemCount = 1
)

$ErrorActionPreference = 'Stop'
$sparkleNamespace = 'http://www.andymatuschak.org/xml-namespaces/sparkle'

if (-not (Test-Path -LiteralPath $AppcastPath)) {
    throw "Appcast was not generated at $AppcastPath."
}
if (-not (Test-Path -LiteralPath "$AppcastPath.signature")) {
    throw 'The detached appcast signature is missing or empty.'
}
$detachedSignature = (Get-Content -LiteralPath "$AppcastPath.signature" -Raw).Trim()
try {
    if ([Convert]::FromBase64String($detachedSignature).Length -ne 64) { throw 'bad length' }
} catch {
    throw 'The detached appcast signature is not a valid Ed25519 signature.'
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
    if ($null -ne $releaseNotesLink -and
        $releaseNotesLink.InnerText -ne "$RepositoryUrl/releases/tag/v$version") {
        throw "Appcast version $version has an incorrect release-notes link."
    }

    $enclosure = $item.SelectSingleNode('enclosure')
    if ($null -eq $enclosure) {
        throw "Appcast version $version has no signed installer enclosure."
    }
    if ($enclosure.GetAttribute('version', $sparkleNamespace) -ne $version) {
        throw "Appcast version $version has mismatched enclosure version metadata."
    }
    if ($enclosure.GetAttribute('url') -ne "$RepositoryUrl/releases/download/v$version/SyncSentinel-Setup.exe") {
        throw "Appcast version $version has an incorrect installer URL."
    }
    try {
        if ([Convert]::FromBase64String($enclosure.GetAttribute('signature', $sparkleNamespace)).Length -ne 64) {
            throw 'bad length'
        }
    } catch {
        throw "Appcast version $version has no valid Ed25519 enclosure signature."
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
