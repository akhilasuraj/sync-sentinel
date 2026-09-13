param(
    [Parameter(Mandatory = $true)] [string] $GeneratorPath,
    [Parameter(Mandatory = $true)] [string] $InstallerPath,
    [Parameter(Mandatory = $true)] [string] $FeedDirectory,
    [Parameter(Mandatory = $true)] [string] $ChangeLogDirectory,
    [Parameter(Mandatory = $true)] [string] $Version,
    [Parameter(Mandatory = $true)] [string] $BaseUrl,
    [Parameter(Mandatory = $true)] [string] $LatestAppcastUrl,
    [Parameter(Mandatory = $true)] [string] $RepositoryUrl,
    [string] $ProductName = 'SyncSentinel'
)

$ErrorActionPreference = 'Stop'
$appcastPath = Join-Path $FeedDirectory 'appcast.xml'
$normalizedBaseUrl = $BaseUrl.TrimEnd('/') + '/'
$normalizedRepositoryUrl = $RepositoryUrl.TrimEnd('/')
$requiredVersions = @()
$priorItemCount = 0

if (Test-Path $appcastPath) {
    [xml] $feed = Get-Content $appcastPath -Raw
    $sparkle = 'http://www.andymatuschak.org/xml-namespaces/sparkle'
    $ns = [System.Xml.XmlNamespaceManager]::new($feed.NameTable)
    $ns.AddNamespace('sparkle', $sparkle)

    foreach ($item in @($feed.SelectNodes('/rss/channel/item'))) {
        $versionNode = $item.SelectSingleNode('sparkle:version', $ns)
        if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
            throw 'The previous appcast contains an item without sparkle:version.'
        }

        $priorVersion = $versionNode.InnerText.Trim()
        $requiredVersions += $priorVersion
        $changeLogPath = Join-Path $ChangeLogDirectory "$priorVersion.md"
        if (-not (Test-Path $changeLogPath)) {
            throw "Release notes for historical version $priorVersion were not restored."
        }
        $changeLog = (Get-Content $changeLogPath -Raw).Trim()
        if ([string]::IsNullOrWhiteSpace($changeLog)) {
            throw "Release notes for historical version $priorVersion are empty."
        }

        $description = $item.SelectSingleNode('description')
        if ($null -eq $description) {
            $description = $feed.CreateElement('description')
            $item.AppendChild($description) | Out-Null
        }
        $description.InnerText = $changeLog

        $notesLink = $item.SelectSingleNode('sparkle:releaseNotesLink', $ns)
        if ($null -ne $notesLink) {
            $item.RemoveChild($notesLink) | Out-Null
        }
    }

    $priorItemCount = $requiredVersions.Count
    $feed.Save((Resolve-Path $appcastPath))
}

$generatorArguments = @(
    '--single-file', $InstallerPath,
    '--file-version', $Version,
    '--base-url', $normalizedBaseUrl,
    '--appcast-output-directory', $FeedDirectory,
    '--change-log-path', $ChangeLogDirectory,
    '--link-tag', $LatestAppcastUrl,
    '--product-name', $ProductName,
    '--human-readable', 'true'
)
if ($priorItemCount -gt 0) {
    $generatorArguments += '--reparse-existing'
}

& $GeneratorPath @generatorArguments
if (-not $?) {
    throw 'The signed NetSparkle appcast could not be generated.'
}

& (Join-Path $PSScriptRoot 'Validate-ReleaseFeed.ps1') `
    -AppcastPath $appcastPath `
    -ExpectedVersion $Version `
    -ExpectedInstallerUrl "${normalizedBaseUrl}SyncSentinel-Setup.exe" `
    -RepositoryUrl $normalizedRepositoryUrl `
    -RequiredVersions $requiredVersions `
    -MinimumItemCount ($priorItemCount + 1)
