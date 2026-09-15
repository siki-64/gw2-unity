<#
.SYNOPSIS
Decode a MsgChannel static registration table into message records.

.DESCRIPTION
The Ghidra-side walk produced, per table, a 16-byte-stride array of
{u64 defArrayPtr, u64 handlerFn}. Each defArrayPtr points at a chain of
40-byte MsgPackFieldDef descriptors, and descriptor[0] of a message carries
the id at +0x10 with fieldType 1 (MP_MSGID).

This script parses a table dump plus the descriptors it references, so the id
extraction is reproducible rather than retyped by hand.

Input : a JSON file written by the Ghidra walk, one object per table:
        {
          "channelId": 14,
          "direction": "recv",
          "tableAddr": "142167030",
          "entries": [
            { "defArray": "1425B6300", "handler": "1410F1830",
              "descriptor0": "0100000000000000882a9241010000001c00000000000000" },
            ...
          ]
        }

Output: one record per entry with the decoded id, or a diagnosis naming the
        exact entry that failed.

.PARAMETER Path
JSON table dump to decode.

.EXAMPLE
Extract-MsgRegistry.ps1 -Path protocol/schema/207032/tables/ch14-recv.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'

# Descriptor layout taken from the instructions of MsgPack_ComputeMaxSize
# (140fe98b0), which walks the chain with a 0x28 stride:
#   +0x00 u32 fieldType   (1 == MP_MSGID)
#   +0x08 u64 nameOrRef
#   +0x10 u32 the value; for descriptor[0] of a message this is the id
#   +0x18 u64 refTypeDef
#   +0x20 u64 nextDef
#   +0x24 u32 sizeCache
$DefStride = 0x28
$FieldTypeMsgId = 1

# MP_ARRAY (0x0a) wraps a payload in a length-prefixed envelope; its outer
# descriptor is not an id, so the id lives one descriptor further in.
$FieldTypeArray = 0x0a

function ConvertFrom-HexBytes {
    param([string] $Hex)
    $bytes = [byte[]]::new($Hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Read-DescriptorId {
    <#
      Returns the id encoded in one 40-byte descriptor, or $null when the
      descriptor is not an id field. Malformed input returns a diagnosis
      rather than a guess.
    #>
    param([byte[]] $Bytes, [string] $Where)

    if ($Bytes.Length -lt $DefStride) {
        throw "$Where :: descriptor is $($Bytes.Length) bytes, need $DefStride"
    }
    $fieldType = [BitConverter]::ToUInt32($Bytes, 0)
    $value = [BitConverter]::ToUInt32($Bytes, 16)
    return [pscustomobject]@{
        FieldType = $fieldType
        Value     = $value
        IsMsgId   = ($fieldType -eq $FieldTypeMsgId)
        IsArray   = ($fieldType -eq $FieldTypeArray)
    }
}

$dump = Get-Content -Raw -Path $Path | ConvertFrom-Json
$records = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[string]]::new()

foreach ($entry in $dump.entries) {
    $where = "ch$($dump.channelId)/$($dump.direction)/defArray=$($entry.defArray)"
    if (-not $entry.descriptor0) {
        $skipped.Add("$where :: no descriptor bytes supplied")
        continue
    }
    $bytes = ConvertFrom-HexBytes -Hex $entry.descriptor0
    $desc = Read-DescriptorId -Bytes $bytes -Where $where

    if ($desc.IsMsgId) {
        $records.Add([pscustomobject]@{
            channelId = $dump.channelId
            direction = $dump.direction
            messageId = ('0x{0:X}' -f $desc.Value)
            idDecimal = $desc.Value
            fieldType = $desc.FieldType
            defArray  = $entry.defArray
            handler   = $entry.handler
        })
    }
    elseif ($desc.IsArray) {
        # The envelope form; the id sits in the descriptor the array wraps.
        $skipped.Add("$where :: MP_ARRAY envelope (fieldType 0x0a); inner descriptor not walked")
    }
    else {
        $skipped.Add("$where :: first descriptor fieldType=$($desc.FieldType), not MP_MSGID")
    }
}

Write-Output "channel=$($dump.channelId) direction=$($dump.direction) decoded=$($records.Count) skipped=$($skipped.Count)"
foreach ($r in ($records | Sort-Object idDecimal)) {
    Write-Output ("  {0,-5} {1,-5} id={2,-8} dec={3,-5} handler={4}" -f `
        $r.channelId, $r.direction, $r.messageId, $r.idDecimal, $r.handler)
}
if ($skipped.Count -gt 0) {
    Write-Output ''
    Write-Output 'Skipped (each named, nothing silently dropped):'
    $skipped | ForEach-Object { Write-Output "  $_" }
}

# Emit machine-readable output when a destination is supplied.
if ($dump.outputPath) {
    $records | ConvertTo-Json -Depth 5 | Set-Content -Path $dump.outputPath -Encoding UTF8
    Write-Output ''
    Write-Output "wrote $($records.Count) records to $($dump.outputPath)"
}
