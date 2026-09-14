<#
.SYNOPSIS
    Offline reverse-engineering workflow wrapper for the GW2 custom-client repository.

.DESCRIPTION
    Repository-local generator and consumer for maintained artifacts only:
      * catalog  validate | stamp   - protocol/catalog.json and protocol/messages/<build>/<id>.json
      * evidence import   | verify  - hashes and provenance for local, ignored captures and dumps
      * notes    crosscheck         - record-hash consistency across the reviewed findings

    It does not attach to a running process, does not scan an image, and does not implement an
    image-coordinate case. Offline PE/disassembly inspection belongs in Ghidra/MCP, and live
    tracing belongs in a debugger or an external inspector; see tools/README.md.

    Build-sensitive output records the game build it was produced against, so captured output stays
    tied to the binary it describes.

.EXAMPLE
    .\tools\gw2re.ps1 catalog validate
.EXAMPLE
    .\tools\gw2re.ps1 evidence import -Path .\captures\local\205780 -Build 205780 -BuildLabel 205.780
.EXAMPLE
    .\tools\gw2re.ps1 evidence verify
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('catalog', 'evidence', 'notes')]
    [string] $Area,

    [Parameter(Mandatory = $true, Position = 1)]
    [string] $Action,

    [string] $Path,
    [int] $Build = 0,
    [string] $BuildLabel,
    [string] $ImageSha256,
    [string] $Catalog,
    [string] $Schema,
    [string] $Manifest,
    [string] $NotesRoot,
    [string] $ProcessName,
    [string] $RuntimeBase,
    [string] $ImageSize,
    [string] $CapturedAtUtc,
    [string] $SourceNote,
    [string] $Out,
    [string] $RelativeTo,
    [string] $EntryNote,
    [string] $StampNote,
    [string[]] $Tags = @(),
    [string[]] $Exclude,
    [string[]] $Include,
    [switch] $WireEligible,
    [switch] $Force,
    [switch] $CheckGeneratedSchema,
    [switch] $Strict,
    [switch] $Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The operator wrapper must work both in Windows PowerShell 5.1 (no '??', no -AsHashtable) and in
# PowerShell 7, so it sticks to syntax both understand.

$script:ExitCode = 0
$script:Errors = [System.Collections.Generic.List[string]]::new()
$script:Warnings = [System.Collections.Generic.List[string]]::new()
$script:Infos = [System.Collections.Generic.List[string]]::new()

$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:ToolVersion = 'gw2re/1'
$script:DefaultCatalog = 'protocol/catalog.json'
$script:DefaultSchema = 'tools/schema/catalog.schema.json'
$script:DefaultNotesManifest = 'research/provenance/hashes.json'

# --- evidence accounting -------------------------------------------------------------

function Add-Error {
    param([Parameter(Mandatory = $true)][string] $Message)
    $script:Errors.Add($Message)
    $script:ExitCode = 1
}

function Add-Warning {
    param([Parameter(Mandatory = $true)][string] $Message)
    $script:Warnings.Add($Message)
}

function Add-Info {
    param([Parameter(Mandatory = $true)][string] $Message)
    $script:Infos.Add($Message)
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $Message,
        [Parameter(Mandatory = $true)][string] $Path
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Add-Error "$Message :: missing file '$Path'"
        return $false
    }
    return $true
}

# --- path and hash helpers -----------------------------------------------------------

function Get-RepoPath {
    param([Parameter(Mandatory = $true)][string] $Value)
    if ([System.IO.Path]::IsPathRooted($Value)) {
        return [System.IO.Path]::GetFullPath($Value)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $Value))
}

function Get-RelativeRepoPath {
    param(
        [Parameter(Mandatory = $true)][string] $FullPath,
        [string] $BasePath
    )
    $base = if ($BasePath) { $BasePath } else { $script:RepoRoot }
    $baseFull = [System.IO.Path]::GetFullPath($base)
    if (-not $baseFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFull += [System.IO.Path]::DirectorySeparatorChar
    }
    $uri = [System.Uri]::new($baseFull)
    $target = [System.Uri]::new([System.IO.Path]::GetFullPath($FullPath))
    return [System.Uri]::UnescapeDataString($uri.MakeRelativeUri($target).ToString()).Replace('\', '/')
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string] $FilePath)
    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-Hashtable {
    param($InputObject)
    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $result = @{}
        foreach ($key in $InputObject.Keys) {
            $result[[string] $key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
        }
        return $result
    }
    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $result = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $result[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
        }
        return $result
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $items = @()
        foreach ($item in $InputObject) { $items += , (ConvertTo-Hashtable -InputObject $item) }
        return $items
    }
    return $InputObject
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string] $FilePath)
    $raw = Get-Content -LiteralPath $FilePath -Raw
    $parsed = $raw | ConvertFrom-Json
    $node = ConvertTo-Hashtable -InputObject $parsed
    if ($null -eq $node) { $node = @{} }
    # ConvertFrom-Json yields $null for an empty array literal; restore the list shape.
    foreach ($listKey in @('buildStamps', 'messages', 'researchLeads', 'supportedWireBuilds', 'fields', 'sourceReferences', 'sessionPrerequisites', 'wireFixtures', 'unresolved', 'stages', 'entries', 'records', 'tags', 'kind')) {
        if ($node.ContainsKey($listKey) -and $null -eq $node[$listKey]) { $node[$listKey] = @() }
    }
    return @{ Raw = $raw; Node = $node }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)][string] $Content
    )
    $directory = Split-Path -Parent $FilePath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($FilePath, $Content, $encoding)
}
function ConvertTo-StableJson {
    param([Parameter(Mandatory = $true)] $InputObject)

    # ConvertTo-Json renders an empty or one-element collection as a bare object, so it cannot be used
    # to write an evidence list that must round-trip as []. This writer emits arrays and objects
    # explicitly and always ends the file with a newline.
    $builder = [System.Text.StringBuilder]::new()
    [void] (Write-JsonValue -Builder $builder -Value $InputObject -Indent 0)
    [void] $builder.Append("`n")
    return ($builder.ToString() -replace "`r`n", "`n")
}

function Write-JsonValue {
    param(
        [Parameter(Mandatory = $true)][System.Text.StringBuilder] $Builder,
        $Value,
        [Parameter(Mandatory = $true)][int] $Indent
    )
    if ($null -eq $Value) {
        [void] $Builder.Append('null')
        return
    }
    if ($Value -is [bool]) {
        [void] $Builder.Append($(if ($Value) { 'true' } else { 'false' }))
        return
    }
    if ($Value -is [int] -or $Value -is [long]) {
        [void] $Builder.Append([string] $Value)
        return
    }
    if ($Value -is [double] -or $Value -is [decimal]) {
        [void] $Builder.Append(([double] $Value).ToString([System.Globalization.CultureInfo]::InvariantCulture))
        return
    }
    if ($Value -is [System.Collections.IDictionary]) {
        Write-JsonObject -Builder $Builder -Map $Value -Indent $Indent
        return
    }
    if ($Value -is [string]) {
        [void] $Builder.Append((ConvertTo-JsonString -Text $Value))
        return
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        Write-JsonArray -Builder $Builder -Items @($Value) -Indent $Indent
        return
    }
    [void] $Builder.Append((ConvertTo-JsonString -Text ([string] $Value)))
}

function Write-JsonObject {
    param(
        [Parameter(Mandatory = $true)][System.Text.StringBuilder] $Builder,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary] $Map,
        [Parameter(Mandatory = $true)][int] $Indent
    )
    $keys = @($Map.Keys)
    if ($keys.Count -eq 0) {
        [void] $Builder.Append('{}')
        return
    }
    $pad = ' ' * ($Indent + 4)
    $closePad = ' ' * $Indent
    [void] $Builder.Append("{`n")
    for ($index = 0; $index -lt $keys.Count; $index++) {
        $key = $keys[$index]
        [void] $Builder.Append($pad)
        [void] $Builder.Append((ConvertTo-JsonString -Text ([string] $key)))
        [void] $Builder.Append(': ')
        Write-JsonValue -Builder $Builder -Value $Map[$key] -Indent ($Indent + 4)
        if ($index -lt ($keys.Count - 1)) {
            [void] $Builder.Append(',')
        }
        [void] $Builder.Append("`n")
    }
    [void] $Builder.Append($closePad)
    [void] $Builder.Append('}')
}

function Write-JsonArray {
    param(
        [Parameter(Mandatory = $true)][System.Text.StringBuilder] $Builder,
        [object[]] $Items = @(),
        [Parameter(Mandatory = $true)][int] $Indent
    )
    if ($Items.Count -eq 0) {
        [void] $Builder.Append('[]')
        return
    }
    $pad = ' ' * ($Indent + 4)
    $closePad = ' ' * $Indent
    [void] $Builder.Append("[`n")
    for ($index = 0; $index -lt $Items.Count; $index++) {
        [void] $Builder.Append($pad)
        Write-JsonValue -Builder $Builder -Value $Items[$index] -Indent ($Indent + 4)
        if ($index -lt ($Items.Count - 1)) {
            [void] $Builder.Append(',')
        }
        [void] $Builder.Append("`n")
    }
    [void] $Builder.Append($closePad)
    [void] $Builder.Append(']')
}

function ConvertTo-JsonString {
    param([Parameter(Mandatory = $true)][string] $Text)
    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.Append('"')
    foreach ($character in $Text.ToCharArray()) {
        switch ($character) {
            '"' { [void] $builder.Append('\"') }
            '\' { [void] $builder.Append('\\') }
            "`b" { [void] $builder.Append('\b') }
            "`f" { [void] $builder.Append('\f') }
            "`n" { [void] $builder.Append('\n') }
            "`r" { [void] $builder.Append('\r') }
            "`t" { [void] $builder.Append('\t') }
            default {
                if ([int] $character -lt 0x20) {
                    [void] $builder.Append('\u' + ([int] $character).ToString('x4'))
                }
                else {
                    [void] $builder.Append($character)
                }
            }
        }
    }
    [void] $builder.Append('"')
    return $builder.ToString()
}
function Get-SchemaVocabulary {
    param([Parameter(Mandatory = $true)][string] $SchemaPath)
    $vocabulary = @{}
    if (-not (Test-Path -LiteralPath $SchemaPath -PathType Leaf)) {
        Add-Error "schema not found :: '$SchemaPath'"
        return $vocabulary
    }
    try {
        $schema = ConvertTo-Hashtable -InputObject (Get-Content -LiteralPath $SchemaPath -Raw | ConvertFrom-Json)
    }
    catch {
        Add-Error "schema is not valid JSON :: $SchemaPath :: $($_.Exception.Message)"
        return $vocabulary
    }
    if (-not $schema.ContainsKey('$defs')) {
        Add-Error "schema has no defs section :: '$SchemaPath'"
        return $vocabulary
    }
    $defs = $schema['$defs']
    foreach ($key in $defs.Keys) {
        $definition = $defs[$key]
        if ($definition -is [System.Collections.IDictionary] -and $definition.ContainsKey('enum')) {
            $vocabulary[$key] = @($definition['enum'])
        }
        elseif ($definition -is [System.Collections.IDictionary] -and $definition.ContainsKey('$ref')) {
            $target = [string] $definition['$ref']
            if ($target.StartsWith('#/$defs/')) {
                $vocabulary[$key] = @($target.Substring('#/$defs/'.Length))
            }
        }
    }
    foreach ($key in @($vocabulary.Keys)) {
        $values = @($vocabulary[$key])
        if ($values.Count -eq 1 -and $values[0] -is [string] -and $vocabulary.ContainsKey([string] $values[0])) {
            $vocabulary[$key] = @($vocabulary[[string] $values[0]])
        }
    }
    return $vocabulary
}


# --- shared catalog checks -----------------------------------------------------------

function Test-BuildSection {
    param(
        [Parameter(Mandatory = $true)] $Node,
        [Parameter(Mandatory = $true)][string] $Where,
        [string[]] $Required = @('build'),
        [switch] $ShapeOnly
    )
    foreach ($member in $Required) {
        if (-not $Node.ContainsKey($member) -or $null -eq $Node[$member]) {
            Add-Error "$Where :: missing required property '$member'"
            return
        }
    }
    $build = $Node['build']
    if ($build -isnot [int] -and $build -isnot [long]) {
        Add-Error "$Where :: 'build' must be an integer, got '$build'"
        return
    }
    if ([int] $build -lt 1 -and -not $ShapeOnly) {
        Add-Error "$Where :: 'build' must be positive, got $build"
    }
    if ($Node.ContainsKey('imageSha256') -and $Node['imageSha256']) {
        $hash = [string] $Node['imageSha256']
        if ($hash -notmatch '^[0-9a-f]{64}$') {
            Add-Error "$Where :: imageSha256 must be 64 lowercase hex characters"
        }
    }
}

function Test-HexId {
    param(
        $Value,
        [string] $Where
    )
    $id = [string] $Value
    if ($id -notmatch '^0x[0-9A-Fa-f]{1,8}(\.\.0x[0-9A-Fa-f]{1,8})?$') {
        Add-Error "$Where :: messageId '$id' is not a canonical hex id or id range"
        return $false
    }
    return $true
}

function Test-VocabularyValue {
    param(
        [Parameter(Mandatory = $true)] $Vocabulary,
        [Parameter(Mandatory = $true)][string] $Def,
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)][string] $Where,
        [Parameter(Mandatory = $true)][string] $Property
    )
    if (-not $Vocabulary.ContainsKey($Def)) {
        return
    }
    $allowed = @($Vocabulary[$Def])
    $text = [string] $Value
    if ($allowed -notcontains $text) {
        Add-Error "$Where :: $Property '$text' is not one of: $([string]::Join(', ', $allowed))"
    }
}

function Test-FieldTree {
    param(
        [Parameter(Mandatory = $true)] $Fields,
        [Parameter(Mandatory = $true)][string] $Where,
        [Parameter(Mandatory = $true)] $Vocabulary,
        [string] $Prefix = ''
    )
    $ids = @{}
    foreach ($field in @($Fields)) {
        $label = if ($Prefix) { "$Prefix/$($field['id'])" } else { [string] $field['id'] }
        $fieldWhere = "$Where :: field '$label'"
        foreach ($member in @('id', 'kind', 'confidence')) {
            if (-not $field.ContainsKey($member)) {
                Add-Error "$fieldWhere :: missing required property '$member'"
            }
        }
        if (-not $field.ContainsKey('id')) { continue }
        $id = [string] $field['id']
        if ($ids.ContainsKey($id)) {
            Add-Error "$fieldWhere :: duplicate field id '$id' among siblings"
        }
        $ids[$id] = $true
        Test-VocabularyValue -Vocabulary $Vocabulary -Def 'fieldKindOverride' -Value $field['kind'] -Where $fieldWhere -Property 'kind'
        Test-VocabularyValue -Vocabulary $Vocabulary -Def 'confidenceOverride' -Value $field['confidence'] -Where $fieldWhere -Property 'confidence'
        if ($field.ContainsKey('nested')) {
            Test-FieldTree -Fields $field['nested'] -Where $fieldWhere -Vocabulary $Vocabulary -Prefix $label
        }
    }
}

function Test-CatalogNode {
    param(
        [Parameter(Mandatory = $true)] $Node,
        [Parameter(Mandatory = $true)][string] $Where,
        [Parameter(Mandatory = $true)] $Vocabulary,
        [switch] $ShapeOnly
    )
    $required = @('name', 'build', 'direction', 'messageId', 'representation', 'evidenceLevel', 'pipeline', 'fields', 'sessionPrerequisites', 'sourceReferences', 'wireFixtures', 'unresolved', 'validation')
    $missing = $false
    foreach ($member in $required) {
        if (-not $Node.ContainsKey($member)) {
            Add-Error "$Where :: missing required property '$member'"
            $missing = $true
        }
    }
    if ($missing) { return }

    Test-BuildSection -Node $Node -Where $Where -ShapeOnly:$ShapeOnly
    if ($Node.ContainsKey('connectionRole')) {
        Test-VocabularyValue -Vocabulary $Vocabulary -Def 'connectionRole' -Value $Node['connectionRole'] -Where $Where -Property 'connectionRole'
    }
    Test-VocabularyValue -Vocabulary $Vocabulary -Def 'direction' -Value $Node['direction'] -Where $Where -Property 'direction'
    Test-VocabularyValue -Vocabulary $Vocabulary -Def 'representation' -Value $Node['representation'] -Where $Where -Property 'representation'
    Test-VocabularyValue -Vocabulary $Vocabulary -Def 'evidenceLevel' -Value $Node['evidenceLevel'] -Where $Where -Property 'evidenceLevel'
    [void] (Test-HexId -Value $Node['messageId'] -Where $Where)

    $pipeline = $Node['pipeline']
    if ($pipeline -isnot [System.Collections.IDictionary] -or -not $pipeline.ContainsKey('id')) {
        Add-Error "$Where :: pipeline must be an object with an 'id'"
    }
    elseif ($pipeline.ContainsKey('opcodeEncoding')) {
        Test-VocabularyValue -Vocabulary $Vocabulary -Def 'opcodeEncoding' -Value $pipeline['opcodeEncoding'] -Where $Where -Property 'pipeline.opcodeEncoding'
    }

    $validation = $Node['validation']
    if ($validation -is [System.Collections.IDictionary]) {
        foreach ($member in @('static', 'synthetic', 'capturedReplay', 'unity', 'liveInterop')) {
            if (-not $validation.ContainsKey($member)) {
                Add-Error "$Where :: validation.$member is required"
            }
        }
        if ($validation.ContainsKey('liveInterop') -and $validation['liveInterop'] -eq $true -and $Node['representation'] -ne 'wire-bytes') {
            Add-Error "$Where :: liveInterop cannot be claimed for representation '$($Node['representation'])'"
        }
    }

    foreach ($fixture in @($Node['wireFixtures'])) {
        $fixtureWhere = "$Where :: wireFixtures['$($fixture['kind'])/$($fixture['sha256'])']"
        foreach ($member in @('kind', 'direction', 'sha256')) {
            if (-not $fixture.ContainsKey($member)) {
                Add-Error "$fixtureWhere :: missing required property '$member'"
            }
        }
        if ($fixture.ContainsKey('kind')) {
            $kind = [string] $fixture['kind']
            if (@('synthetic', 'sanitized-capture') -notcontains $kind) {
                Add-Error "$fixtureWhere :: kind '$kind' must be synthetic or sanitized-capture"
            }
            if ($kind -eq 'sanitized-capture' -and @('wire-bytes', 'transport-frame') -notcontains [string] $Node['representation']) {
                Add-Error "$fixtureWhere :: a sanitized capture fixture requires wire-bytes or transport-frame representation"
            }
        }
        if ($fixture.ContainsKey('sha256') -and [string] $fixture['sha256'] -notmatch '^[0-9a-f]{64}$') {
            Add-Error "$fixtureWhere :: sha256 must be 64 lowercase hex characters"
        }
    }

    foreach ($reference in @($Node['sourceReferences'])) {
        if (-not $reference.ContainsKey('kind')) {
            Add-Error "$Where :: a sourceReference is missing 'kind'"
            continue
        }
        $kind = [string] $reference['kind']
        if ($reference.ContainsKey('path') -and -not [string]::IsNullOrWhiteSpace([string] $reference['path'])) {
            $referenced = Get-RepoPath -Value ([string] $reference['path'])
            if (-not (Test-Path -LiteralPath $referenced -PathType Leaf)) {
                Add-Warning "$Where :: sourceReference path '$($reference['path'])' does not exist"
            }
            elseif ($reference.ContainsKey('sha256') -and $reference['sha256']) {
                $actual = Get-Sha256Hex -FilePath $referenced
                if ($actual -ne [string] $reference['sha256']) {
                    Add-Error "$Where :: sourceReference sha256 for '$($reference['path'])' does not match the file on disk"
                }
            }
        }
        elseif ($kind -eq 'note') {
            Add-Warning "$Where :: note sourceReference has no path, so it cannot be hash-checked"
        }
    }
}

function Invoke-CatalogValidate {
    param(
        [Parameter(Mandatory = $true)][string] $CatalogPath,
        [Parameter(Mandatory = $true)][string] $SchemaPath
    )

    $catalogFull = Get-RepoPath -Value $CatalogPath
    if (-not (Invoke-Checked -Message 'catalog' -Path $catalogFull)) { return }
    $schemaFull = Get-RepoPath -Value $SchemaPath
    $vocabulary = Get-SchemaVocabulary -SchemaPath $schemaFull
    if (-not $vocabulary.ContainsKey('direction')) {
        Add-Error 'schema does not define the shared value vocabulary; validate cannot continue'
        return
    }

    try {
        $catalog = (Read-JsonFile -FilePath $catalogFull).Node
    }
    catch {
        Add-Error "catalog is not valid JSON :: $CatalogPath :: $($_.Exception.Message)"
        return
    }

    $missing = $false
    foreach ($member in @('schemaVersion', 'template', 'supportedWireBuilds', 'messages', 'researchLeads')) {
        if (-not $catalog.ContainsKey($member)) {
            Add-Error "catalog :: missing required property '$member'"
            $missing = $true
        }
    }
    if ($missing) { return }

    if ([int] $catalog['schemaVersion'] -ne 1) {
        Add-Error "catalog :: schemaVersion must be 1, got '$($catalog['schemaVersion'])'"
    }

    $templatePath = Get-RepoPath -Value ([string] $catalog['template'])
    if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
        Add-Error "catalog :: template '$($catalog['template'])' does not exist"
    }
    else {
        try {
            $template = (Read-JsonFile -FilePath $templatePath).Node
            Test-CatalogNode -Node $template -Where ([string] $catalog['template']) -Vocabulary $vocabulary -ShapeOnly
        }
        catch {
            Add-Error "catalog :: template '$($catalog['template'])' is not valid JSON :: $($_.Exception.Message)"
        }
    }

    if (@($catalog['supportedWireBuilds']).Count -gt 0) {
        Add-Error 'catalog :: supportedWireBuilds must stay empty until a build is registered after capture-backed codec validation'
    }

    $stampedBuilds = @{}
    if (-not $catalog.ContainsKey('buildStamps')) {
        Add-Warning 'catalog :: no buildStamps recorded; every message entry needs a stamp for its build'
    }
    foreach ($stamp in @($catalog['buildStamps'] | Where-Object { $null -ne $_ })) {
        $stampBuild = if ($stamp.ContainsKey('build')) { $stamp['build'] } else { '?' }
        $where = "catalog :: buildStamps['$stampBuild']"
        Test-BuildSection -Node $stamp -Where $where -Required @('build', 'stampedAtUtc', 'tool')
        if ($stamp.ContainsKey('build')) {
            $key = [int] $stamp['build']
            if ($stampedBuilds.ContainsKey($key)) {
                Add-Error "$where :: duplicate stamp for build $key"
            }
            $stampedBuilds[$key] = $stamp
        }
        if ($stamp.ContainsKey('runtimeBase') -and $stamp['runtimeBase'] -and [string] $stamp['runtimeBase'] -notmatch '^0x[0-9A-Fa-f]+$') {
            Add-Error "$where :: runtimeBase must be hex, e.g. 0x7FF600000000"
        }
    }
    $seen = @{}
    foreach ($entry in @($catalog['messages'])) {
        $where = "catalog :: messages['$($entry['messageId'])']"
        Test-BuildSection -Node $entry -Where $where
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'direction' -Value $entry['direction'] -Where $where -Property 'direction'
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'representation' -Value $entry['representation'] -Where $where -Property 'representation'
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'evidenceLevel' -Value $entry['evidenceLevel'] -Where $where -Property 'evidenceLevel'
        if (-not $entry.ContainsKey('messageId')) { continue }
        [void] (Test-HexId -Value $entry['messageId'] -Where $where)

        $build = [int] $entry['build']
        if (-not $stampedBuilds.ContainsKey($build)) {
            Add-Error "$where :: build $build has no buildStamps entry; run 'catalog stamp' first"
        }
        elseif ($entry.ContainsKey('imageSha256') -and $entry['imageSha256'] -and $stampedBuilds[$build]['imageSha256'] -and [string] $entry['imageSha256'] -ne [string] $stampedBuilds[$build]['imageSha256']) {
            Add-Error "$where :: imageSha256 disagrees with the build stamp for build $build"
        }

        $key = "$build/$($entry['messageId'])/$($entry['name'])"
        if ($seen.ContainsKey($key)) {
            Add-Error "$where :: duplicate catalog entry for build $build, id $($entry['messageId']) and name '$($entry['name'])'"
        }
        $seen[$key] = $true

        $artifactRef = $entry['artifact']
        if ($null -eq $artifactRef -or [string]::IsNullOrWhiteSpace([string] $artifactRef)) {
            Add-Warning "$where :: no message artifact; this entry remains a lead and is not a registered contract"
            continue
        }

        $artifactPath = Get-RepoPath -Value ([string] $artifactRef)
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            Add-Error "$where :: artifact '$artifactRef' does not exist"
            continue
        }

        try {
            $artifact = (Read-JsonFile -FilePath $artifactPath).Node
        }
        catch {
            Add-Error "$artifactRef :: not valid JSON :: $($_.Exception.Message)"
            continue
        }

        Test-CatalogNode -Node $artifact -Where ([string] $artifactRef) -Vocabulary $vocabulary
        Test-FieldTree -Fields $artifact['fields'] -Where ([string] $artifactRef) -Vocabulary $vocabulary

        foreach ($member in @('build', 'messageId', 'direction', 'representation', 'evidenceLevel', 'name')) {
            if ($artifact.ContainsKey($member) -and $entry.ContainsKey($member) -and [string] $artifact[$member] -ne [string] $entry[$member]) {
                Add-Error "$artifactRef :: $member '$($artifact[$member])' disagrees with the catalog entry '$($entry[$member])'"
            }
        }

        foreach ($fixture in @($artifact['wireFixtures'])) {
            if (-not $fixture.ContainsKey('artifactRef')) { continue }
            $fixtureWhere = "$artifactRef :: wireFixtures['$($fixture['artifactRef'])']"
            $fixturePath = Get-RepoPath -Value ([string] $fixture['artifactRef'])
            if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
                Add-Warning "$fixtureWhere :: artifactRef does not exist locally (it may be an ignored private capture)"
                continue
            }
            if ($fixture.ContainsKey('sha256') -and $fixture['sha256']) {
                $actual = Get-Sha256Hex -FilePath $fixturePath
                if ($actual -ne [string] $fixture['sha256']) {
                    Add-Error "$fixtureWhere :: sha256 does not match the bytes at '$($fixture['artifactRef'])'"
                }
            }
        }
    }

    foreach ($lead in @($catalog['researchLeads'] | Where-Object { $null -ne $_ })) {
        $leadId = if ($lead.ContainsKey('messageId')) { $lead['messageId'] } else { '?' }
        $where = "catalog :: researchLeads['$leadId']"
        Test-BuildSection -Node $lead -Where $where
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'direction' -Value $lead['direction'] -Where $where -Property 'direction'
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'representation' -Value $lead['representation'] -Where $where -Property 'representation'
        Test-VocabularyValue -Vocabulary $vocabulary -Def 'evidenceLevel' -Value $lead['evidenceLevel'] -Where $where -Property 'evidenceLevel'
        [void] (Test-HexId -Value $lead['messageId'] -Where $where)
        if (-not $lead.ContainsKey('source') -or [string]::IsNullOrWhiteSpace([string] $lead['source'])) {
            Add-Error "$where :: source is required"
        }
        elseif (-not (Test-Path -LiteralPath (Get-RepoPath -Value ([string] $lead['source'])) -PathType Leaf)) {
            Add-Warning "$where :: source '$($lead['source'])' does not exist"
        }
        if ($lead.ContainsKey('wireVerified') -and $lead['wireVerified'] -eq $true) {
            Add-Error "$where :: a research lead cannot be marked wireVerified"
        }
    }

    if ($script:Errors.Count -eq 0) {
        Add-Info "catalog validate :: $(@($catalog['messages']).Count) message entries, $(@($catalog['researchLeads']).Count) research leads, $($stampedBuilds.Count) build stamps"
    }
}

function Invoke-CatalogStamp {
    param(
        [Parameter(Mandatory = $true)][string] $CatalogPath,
        [Parameter(Mandatory = $true)][int] $BuildNumber,
        [string] $Label,
        [string] $Hash,
        [string] $Process,
        [string] $Base,
        [string] $Size,
        [string] $CapturedAt,
        [string] $EntryNote,
        [string] $StampNote
    )
    if ($BuildNumber -le 0) {
        Add-Error 'catalog stamp :: -Build is required and must be positive'
        return
    }
    if ($Hash -and $Hash -notmatch '^[0-9a-f]{64}$') {
        Add-Error 'catalog stamp :: -ImageSha256 must be 64 lowercase hex characters'
        return
    }
    $catalogFull = Get-RepoPath -Value $CatalogPath
    if (-not (Invoke-Checked -Message 'catalog' -Path $catalogFull)) { return }

    $catalog = (Read-JsonFile -FilePath $catalogFull).Node
    if (-not $catalog.ContainsKey('buildStamps') -or $null -eq $catalog['buildStamps']) {
        $catalog['buildStamps'] = @()
    }

    $stamp = [ordered]@{
        build        = $BuildNumber
        stampedAtUtc = [System.DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        tool         = $script:ToolVersion
    }
    if ($Label) { $stamp['buildLabel'] = $Label }
    if ($Hash) { $stamp['imageSha256'] = $Hash }
    if ($Process) { $stamp['processName'] = $Process }
    if ($Base) {
        if ($Base -notmatch '^0x[0-9A-Fa-f]+$') {
            Add-Error 'catalog stamp :: -RuntimeBase must be hex, e.g. 0x7FF600000000'
            return
        }
        $stamp['runtimeBase'] = $Base
    }
    if ($Size) {
        $imageSize = 0
        if (-not [int]::TryParse($Size, [ref] $imageSize)) {
            Add-Error "catalog stamp :: -ImageSize must be an integer, got '$Size'"
            return
        }
        $stamp['imageSize'] = $imageSize
    }
    if ($CapturedAt) {
        $parsedTime = [System.DateTimeOffset]::MinValue
        if (-not [System.DateTimeOffset]::TryParse($CapturedAt, [ref] $parsedTime)) {
            Add-Error "catalog stamp :: -CapturedAtUtc must be an ISO-8601 timestamp, got '$CapturedAt'"
            return
        }
        $stamp['capturedAtUtc'] = $parsedTime.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    if ($StampNote) { $stamp['notes'] = $StampNote }

    $stamps = [System.Collections.Generic.List[object]]::new()
    foreach ($existing in @($catalog['buildStamps'])) {
        if ([int] $existing['build'] -eq $BuildNumber) {
            Add-Warning "catalog stamp :: replacing the existing stamp for build $BuildNumber"
            continue
        }
        $stamps.Add($existing)
    }
    $stamps.Add($stamp)
    $catalog['buildStamps'] = @($stamps | Sort-Object { [int] $_['build'] })

    Write-Utf8NoBom -FilePath $catalogFull -Content (ConvertTo-StableJson -InputObject $catalog)
    Add-Info "catalog stamp :: build $BuildNumber stamped in '$CatalogPath'"
}

# --- evidence manifest ----------------------------------------------------------------

function Get-EvidenceKinds {
    param([Parameter(Mandatory = $true)][string] $FileName)
    $extension = [System.IO.Path]::GetExtension($FileName).ToLowerInvariant()
    $kinds = [System.Collections.Generic.List[string]]::new()
    $kinds.Add('unknown')
    if ($FileName -match '(?i)\.(?:pcap|pcapng)$') { $kinds.Add('wire-capture') }
    if ($FileName -match '(?i)(?:memdump|memory|dump)') { $kinds.Add('module-snapshot') }
    if ($FileName -match '(?i)(?:\^|[-_.])(?:request|sent|cmsg|outbound)(?:[-_.]|$)') { $kinds.Add('outbound-payload') }
    if ($FileName -match '(?i)(?:\^|[-_.])(?:response|recv|received|smsg|inbound)(?:[-_.]|$)') { $kinds.Add('inbound-payload') }
    if ($FileName -match '(?i)(?:^|[-_.])(?:asset|blob|raw)(?:[-_.]|$)' -and @('.png', '.jpg', '.jpeg', '.bmp', '.dds', '.wav', '.mp3', '.ogg', '.bin') -contains $extension) { $kinds.Add('asset') }
    if ($extension -eq '.sessionkey') { $kinds.Add('session-key') }
    if ($extension -eq '.json' -or $extension -eq '.txt') { $kinds.Add('record') }
    return @($kinds)
}

function Test-EvidenceBundleMetadata {
    param(
        [Parameter(Mandatory = $true)] $Bundle,
        [string] $Hash,
        [string] $Base
    )
    if ($Hash) {
        if ($Hash -notmatch '^[0-9a-f]{64}$') {
            Add-Error 'evidence import :: -ImageSha256 must be 64 lowercase hex characters'
            return $false
        }
        $Bundle['imageSha256'] = $Hash
    }
    if ($Base) {
        if ($Base -notmatch '^0x[0-9A-Fa-f]+$') {
            Add-Error 'evidence import :: -RuntimeBase must be hex, e.g. 0x7FF600000000'
            return $false
        }
        $Bundle['runtimeBase'] = $Base
    }
    return $true
}

function Get-EvidenceFileList {
    param(
        [Parameter(Mandatory = $true)][string] $SourceFull,
        [Parameter(Mandatory = $true)][string] $RelativeBase,
        [string[]] $ExcludeList
    )
    $files = [System.Collections.Generic.List[object]]::new()
    if (Test-Path -LiteralPath $SourceFull -PathType Leaf) {
        $files.Add((Get-Item -LiteralPath $SourceFull))
        return $files
    }
    $excludePatterns = @('artifacts/manifests/**', 'artifacts/re-tooling/**')
    if ($ExcludeList) { $excludePatterns = @($excludePatterns + $ExcludeList) }
    foreach ($candidate in @(Get-ChildItem -LiteralPath $SourceFull -Recurse -File)) {
        $relative = Get-RelativeRepoPath -FullPath $candidate.FullName -BasePath $RelativeBase
        $skip = $false
        foreach ($pattern in $excludePatterns) {
            if ($relative -like $pattern) { $skip = $true; break }
        }
        if (-not $skip) { $files.Add($candidate) }
    }
    return $files
}

function New-EvidenceEntry {
    param(
        [Parameter(Mandatory = $true)] $File,
        [Parameter(Mandatory = $true)][string] $RelativeBase,
        [string] $EntryNote,
        [switch] $WireEligible
    )
    $relative = Get-RelativeRepoPath -FullPath $File.FullName -BasePath $RelativeBase
    $extension = [System.IO.Path]::GetExtension($File.Name).ToLowerInvariant()
    if ($extension -eq '.sessionkey') {
        Add-Error "evidence import :: refusing to import '$relative'; session keys are never committed"
        return $null
    }
    if ($extension -eq '.dat' -and $File.Name -match '(?i)^Gw2\.dat$') {
        Add-Error "evidence import :: refusing to import '$relative'; the game archive is never committed"
        return $null
    }
    $kinds = @(Get-EvidenceKinds -FileName $File.Name)
    $entry = [ordered]@{
        kind     = $kinds
        bytes    = $File.Length
        sha256   = Get-Sha256Hex -FilePath $File.FullName
        relative = $relative
        imported = [System.DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    if ($WireEligible) {
        if (@($kinds) -contains 'inbound-payload' -or @($kinds) -contains 'outbound-payload') {
            $entry['wireEligible'] = $true
        }
        else {
            Add-Error "evidence import :: -WireEligible cannot be applied to '$relative'; the representation is not a captured payload"
        }
    }
    if ($EntryNote) { $entry['note'] = $EntryNote }
    return $entry
}

function Invoke-EvidenceImport {
    param(
        [Parameter(Mandatory = $true)][string] $SourcePath,
        [Parameter(Mandatory = $true)][int] $BuildNumber,
        [string] $Label,
        [string] $Hash,
        [string] $Process,
        [string] $Base,
        [string] $Size,
        [string] $CapturedAt,
        [string[]] $TagList,
        [string[]] $ExcludeList,
        [string] $RelativeToPath,
        [string] $ManifestPath,
        [string] $EntryNote,
        [switch] $WireEligible
    )
    if ($BuildNumber -le 0) {
        Add-Error 'evidence import :: -Build is required and must be positive'
        return
    }
    $sourceFull = Get-RepoPath -Value $SourcePath
    if (-not (Test-Path -LiteralPath $sourceFull)) {
        Add-Error "evidence import :: source '$SourcePath' does not exist"
        return
    }
    $manifestFull = Get-RepoPath -Value $ManifestPath
    if (Test-Path -LiteralPath $manifestFull) {
        Add-Error "evidence import :: '$ManifestPath' already exists; choose another -Manifest path or delete it deliberately"
        return
    }
    # A single-file import is relative to its own directory so the recorded path stays repository-relative.
    $relativeBase = if ($RelativeToPath) { Get-RepoPath -Value $RelativeToPath }
    elseif (Test-Path -LiteralPath $sourceFull -PathType Leaf) { Split-Path -Parent $sourceFull }
    else { $sourceFull }

    $bundle = [ordered]@{
        schemaVersion  = 1
        bundle         = Split-Path -Leaf $sourceFull
        build          = $BuildNumber
        importedAtUtc  = [System.DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        tool           = $script:ToolVersion
        representation = 'local-private-input'
        classification = 'offline evidence; not wire compatibility'
        bundleRoot     = (Get-RelativeRepoPath -FullPath $relativeBase)
    }
    if ($Label) { $bundle['buildLabel'] = $Label }
    if (-not (Test-EvidenceBundleMetadata -Bundle $bundle -Hash $Hash -Base $Base)) { return }
    if ($Process) { $bundle['processName'] = $Process }
    if ($Size) { $bundle['imageSize'] = $Size }
    if ($CapturedAt) { $bundle['capturedAtUtc'] = $CapturedAt }
    if ($TagList) { $bundle['tags'] = @($TagList) }

    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($file in @(Get-EvidenceFileList -SourceFull $sourceFull -RelativeBase $relativeBase -ExcludeList $ExcludeList | Sort-Object FullName)) {
        $entry = New-EvidenceEntry -File $file -RelativeBase $relativeBase -EntryNote $EntryNote -WireEligible:$WireEligible
        if ($null -ne $entry) { $entries.Add($entry) }
    }
    if ($entries.Count -eq 0) {
        Add-Error "evidence import :: no importable files under '$SourcePath'"
        return
    }
    if ($script:Errors.Count -gt 0) {
        Add-Error 'evidence import :: refusing to write a manifest for an import that reported errors'
        return
    }
    $bundle['entries'] = @($entries)

    Write-Utf8NoBom -FilePath $manifestFull -Content (ConvertTo-StableJson -InputObject $bundle)
    Add-Info "evidence import :: $($entries.Count) entries written to '$ManifestPath' for build $BuildNumber"
    foreach ($kind in @('session-key', 'asset')) {
        if (@($entries | Where-Object { @($_.kind) -contains $kind }).Count -gt 0) {
            Add-Warning "evidence import :: bundle contains '$kind' entries; keep it out of Git and pass -Exclude next time"
        }
    }
}

function Invoke-EvidenceVerify {
    param([Parameter(Mandatory = $true)][string] $ManifestPath)
    $manifestFull = Get-RepoPath -Value $ManifestPath
    if (-not (Invoke-Checked -Message 'evidence manifest' -Path $manifestFull)) { return }

    $manifest = (Read-JsonFile -FilePath $manifestFull).Node
    if (-not $manifest.ContainsKey('entries')) {
        Add-Error "evidence verify :: '$ManifestPath' has no entries"
        return
    }
    $base = Split-Path -Parent $manifestFull
    if ($manifest.ContainsKey('bundleRoot') -and $manifest['bundleRoot']) {
        $resolved = Get-RepoPath -Value ([string] $manifest['bundleRoot'])
        if (Test-Path -LiteralPath $resolved -PathType Container) { $base = $resolved }
        else { Add-Warning "evidence verify :: recorded bundleRoot '$($manifest['bundleRoot'])' is not present; resolving next to the manifest" }
    }
    $verified = 0
    $absent = 0
    foreach ($entry in @($manifest['entries'])) {
        $relative = [string] $entry['relative']
        $candidate = [System.IO.Path]::GetFullPath((Join-Path $base ($relative -replace '/', '\')))
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            Add-Warning "evidence verify :: '$relative' is not present next to the manifest"
            $absent++
            continue
        }
        $actual = Get-Sha256Hex -FilePath $candidate
        if ($actual -ne [string] $entry['sha256']) {
            Add-Error "evidence verify :: '$relative' sha256 mismatch (recorded $($entry['sha256']), actual $actual)"
            continue
        }
        $verified++
    }
    Add-Info "evidence verify :: $verified entries verified, $absent absent from the bundle directory"
}

function Invoke-EvidenceProvenance {
    param(
        [Parameter(Mandatory = $true)][string] $TargetPath,
        [string] $ManifestPath
    )
    $targetFull = Get-RepoPath -Value $TargetPath
    if (-not (Invoke-Checked -Message 'evidence path' -Path $targetFull)) { return }
    $targetHash = Get-Sha256Hex -FilePath $targetFull
    Add-Info "evidence provenance :: '$(Get-RelativeRepoPath -FullPath $targetFull)' sha256 $targetHash"

    $manifests = @()
    if ($ManifestPath) {
        $manifests = @(Get-RepoPath -Value $ManifestPath)
    }
    else {
        $root = Join-Path $script:RepoRoot 'artifacts/manifests'
        if (Test-Path -LiteralPath $root) {
            $manifests = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.json' | Select-Object -ExpandProperty FullName)
        }
    }
    if ($manifests.Count -eq 0) {
        Add-Warning 'evidence provenance :: no evidence manifests found; the input has no recorded provenance'
    }
    foreach ($manifestFile in $manifests) {
        try {
            $manifest = (Read-JsonFile -FilePath $manifestFile).Node
        }
        catch {
            Add-Warning "evidence provenance :: skipping unreadable manifest '$manifestFile'"
            continue
        }
        if (-not $manifest.ContainsKey('entries')) { continue }
        foreach ($entry in @($manifest['entries'])) {
            if ([string] $entry['sha256'] -eq $targetHash) {
                Add-Info "evidence provenance :: imported from bundle '$($manifest['bundle'])' for build $($manifest['build']) as '$($entry['relative'])' (kinds: $([string]::Join(', ', @($entry['kind']))))"
            }
        }
    }

    $catalogPath = Get-RepoPath -Value $script:DefaultCatalog
    if (Test-Path -LiteralPath $catalogPath) {
        $catalog = (Read-JsonFile -FilePath $catalogPath).Node
        foreach ($entry in @($catalog['messages'])) {
            if ($null -eq $entry['artifact']) { continue }
            $artifactPath = Get-RepoPath -Value ([string] $entry['artifact'])
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) { continue }
            $artifact = (Read-JsonFile -FilePath $artifactPath).Node
            foreach ($fixture in @($artifact['wireFixtures'])) {
                if ([string] $fixture['sha256'] -eq $targetHash) {
                    Add-Info "evidence provenance :: cited as a wire fixture by $($entry['artifact']) (kind: $($fixture['kind']))"
                }
            }
        }
    }
}

# --- findings record hash cross-check -------------------------------------------------

function Invoke-NotesCrosscheck {
    param(
        [Parameter(Mandatory = $true)][string] $NotesRootPath,
        [Parameter(Mandatory = $true)][string] $ManifestPath
    )
    $root = Get-RepoPath -Value $NotesRootPath
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        Add-Error "notes crosscheck :: '$NotesRootPath' is not a directory"
        return
    }
    $manifestFull = Get-RepoPath -Value $ManifestPath
    $manifestFull = Get-RepoPath -Value $ManifestPath
    if (-not (Test-Path -LiteralPath $manifestFull -PathType Leaf)) {
        Add-Warning "notes crosscheck :: '$ManifestPath' does not exist; skipping the record-hash cross-check"
        return
    }
    $manifest = (Read-JsonFile -FilePath $manifestFull).Node
    $known = @{}
    $recordKey = if ($manifest.ContainsKey('records')) { 'records' } elseif ($manifest.ContainsKey('files')) { 'files' } else { $null }
    if ($null -eq $recordKey) {
        Add-Warning "notes crosscheck :: '$ManifestPath' has no records or files list; skipping the record-hash cross-check"
        return
    }
    foreach ($record in @($manifest[$recordKey] | Where-Object { $null -ne $_ })) {
        if (-not $record.ContainsKey('sha256')) { continue }
        $known[[string] $record['sha256']] = $record
    }

    $pattern = [regex]::new('(?<![0-9A-Fa-f])([0-9a-f]{64})(?![0-9A-Fa-f])')
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Include '*.md', '*.cs', '*.json')
    $total = 0
    $matched = 0
    $unmatched = 0
    foreach ($file in $files) {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in $pattern.Matches($text)) {
            $hash = $match.Groups[1].Value
            $total++
            if ($known.ContainsKey($hash)) {
                $matched++
            }
            else {
                $unmatched++
                $line = ($text.Substring(0, $match.Index) -split "`n").Count
                Add-Warning "notes crosscheck :: $($file.Name):$line records $hash, which is not a manifest record hash"
            }
        }
    }
    Add-Info "notes crosscheck :: $total record hashes found in $($files.Count) findings files, $matched resolve to a reviewed record, $unmatched unresolved"
}

# --- dispatch -------------------------------------------------------------------------

$catalogRelative = if ($Catalog) { $Catalog } else { $script:DefaultCatalog }
$schemaRelative = if ($Schema) { $Schema } else { $script:DefaultSchema }

switch ("$Area $Action") {
    'catalog validate' {
        Invoke-CatalogValidate -CatalogPath $catalogRelative -SchemaPath $schemaRelative
        if ($CheckGeneratedSchema) {
            # The hand-written schema is the reviewed authority; this only proves it still resolves
            # and still admits the current catalog vocabulary.
            $schemaFull = Get-RepoPath -Value $schemaRelative
            $requiredDefs = @('catalog', 'messageArtifact', 'messageEntry', 'researchLead', 'buildStamp', 'field')
            $defined = @{}
            $schemaNode = (Read-JsonFile -FilePath $schemaFull).Node
            if ($schemaNode.ContainsKey('$defs')) {
                foreach ($defName in $schemaNode['$defs'].Keys) { $defined[[string] $defName] = $true }
            }
            foreach ($def in $requiredDefs) {
                if (-not $defined.ContainsKey($def)) {
                    Add-Error "catalog validate :: -CheckGeneratedSchema found no '$def' definition in '$schemaRelative'"
                }
            }
            Add-Info 'catalog validate :: schema vocabulary resolved'
        }
    }
    'catalog stamp' {
        Invoke-CatalogStamp -CatalogPath $catalogRelative -BuildNumber $Build -Label $BuildLabel -Hash $ImageSha256 -Process $ProcessName -Base $RuntimeBase -Size $ImageSize -CapturedAt $CapturedAtUtc -EntryNote $EntryNote -StampNote $StampNote
    }
    'evidence import' {
        if (-not $Path) {
            Add-Error 'evidence import :: -Path is required'
        }
        elseif (-not $Manifest) {
            Add-Error 'evidence import :: -Manifest is required (a bundle manifest under artifacts/manifests)'
        }
        else {
            Invoke-EvidenceImport -SourcePath $Path -BuildNumber $Build -Label $BuildLabel -Hash $ImageSha256 -Process $ProcessName -Base $RuntimeBase -Size $ImageSize -CapturedAt $CapturedAtUtc -TagList $Tags -ExcludeList $Exclude -RelativeToPath $RelativeTo -ManifestPath $Manifest -EntryNote $EntryNote -WireEligible:$WireEligible
        }
    }
    'evidence verify' {
        if (-not $Manifest) {
            Add-Error 'evidence verify :: -Manifest is required'
        }
        else {
            Invoke-EvidenceVerify -ManifestPath $Manifest
        }
    }
    'evidence provenance' {
        if (-not $Path) {
            Add-Error 'evidence provenance :: -Path is required'
        }
        else {
            Invoke-EvidenceProvenance -TargetPath $Path -ManifestPath $Manifest
        }
    }
    'notes crosscheck' {
        $notesRootRelative = if ($NotesRoot) { $NotesRoot } else { 'research/contracts/notes' }
        $notesManifestRelative = if ($Manifest) { $Manifest } else { $script:DefaultNotesManifest }
        Invoke-NotesCrosscheck -NotesRootPath $notesRootRelative -ManifestPath $notesManifestRelative
    }
    default {
        Add-Error "unknown command '$Area $Action'; see tools/README.md for the supported surface"
    }
}

# --- reporting ------------------------------------------------------------------------

$result = [ordered]@{
    tool    = $script:ToolVersion
    command = "$Area $Action"
    build   = if ($Build -gt 0) { $Build } else { $null }
    exit    = $script:ExitCode
    errors  = @($script:Errors)
    warnings = @($script:Warnings)
    infos   = @($script:Infos)
}

if ($Json) {
    # Structured output mode: one JSON object, so callers never scrape prose.
    Write-Output (ConvertTo-StableJson -InputObject $result)
}
else {
    foreach ($line in $script:Infos) { Write-Host "info    $line" }
    foreach ($line in $script:Warnings) { Write-Host "warning $line" -ForegroundColor Yellow }
    foreach ($line in $script:Errors) { Write-Host "error   $line" -ForegroundColor Red }
    $summary = if ($script:ExitCode -eq 0) { 'OK' } else { 'FAILED' }
    Write-Host "$summary $Area $Action :: $($script:Errors.Count) error(s), $($script:Warnings.Count) warning(s)"
}

if ($Strict -and $script:Warnings.Count -gt 0) {
    $script:ExitCode = 1
}

# Setting $LASTEXITCODE as well as exiting keeps the status visible to a caller that invokes the
# script from another script block or through powershell -Command.
$global:LASTEXITCODE = $script:ExitCode
exit $script:ExitCode
