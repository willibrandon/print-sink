$ErrorActionPreference = 'Stop'

function Split-PrintSinkMarkdownTableRow {
    param(
        [Parameter(Mandatory)]
        [string] $Line
    )

    $cells = [System.Collections.Generic.List[string]]::new()
    $cell = [System.Text.StringBuilder]::new()
    $escaped = $false

    foreach ($character in $Line.ToCharArray()) {
        if ($escaped) {
            $null = $cell.Append($character)
            $escaped = $false
            continue
        }

        if ($character -eq '\') {
            $null = $cell.Append($character)
            $escaped = $true
            continue
        }

        if ($character -eq '|') {
            $cells.Add($cell.ToString().Trim()) | Out-Null
            $null = $cell.Clear()
            continue
        }

        $null = $cell.Append($character)
    }

    $cells.Add($cell.ToString().Trim()) | Out-Null

    if ($cells.Count -gt 0 -and $cells[0] -eq '') {
        $cells.RemoveAt(0)
    }

    if ($cells.Count -gt 0 -and $cells[$cells.Count - 1] -eq '') {
        $cells.RemoveAt($cells.Count - 1)
    }

    return @($cells)
}

function Get-PrintSinkDesignFeatureMatrix {
    param(
        [string] $DesignPath = (Join-Path $PSScriptRoot '..\..\docs\DESIGN.md')
    )

    $fullPath = [System.IO.Path]::GetFullPath($DesignPath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "PrintSink design document was not found: $fullPath"
    }

    $features = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $fullPath) {
        if ($line -notmatch '^\|\s*\d+\s*\|') {
            continue
        }

        $cells = Split-PrintSinkMarkdownTableRow -Line $line
        if ($cells.Count -lt 5) {
            throw "Could not parse feature matrix row in ${fullPath}: $line"
        }

        $number = [int]$cells[0]
        $feature = $cells[1]
        $notes = $cells[4]
        $status = if ($notes -like '*Tracked only*') { 'deferred' } else { 'supported' }
        $key = [string]$number

        if ($features.Contains($key)) {
            throw "Feature matrix contains duplicate row #$number in $fullPath."
        }

        $features[$key] = [ordered]@{
            number = $number
            feature = $feature
            status = $status
        }
    }

    if ($features.Count -eq 0) {
        throw "Feature matrix was not found in $fullPath."
    }

    return $features
}

function Get-PrintSinkFeatureMap {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('supported', 'deferred')]
        [string] $Status
    )

    $features = Get-PrintSinkDesignFeatureMatrix
    $map = @{}
    foreach ($row in $features.Values) {
        if ([string]$row.status -eq $Status) {
            $map[[string]$row.number] = [string]$row.feature
        }
    }

    return $map
}

function Get-PrintSinkSupportedFeatureMap {
    return Get-PrintSinkFeatureMap -Status 'supported'
}

function Get-PrintSinkDeferredFeatureMap {
    return Get-PrintSinkFeatureMap -Status 'deferred'
}
