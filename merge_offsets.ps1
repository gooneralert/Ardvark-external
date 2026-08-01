param(
    [Parameter(Mandatory = $true)][string]$PrimaryCsPath,
    [Parameter(Mandatory = $true)][string]$FallbackHPath,
    [Parameter(Mandatory = $true)][string]$OutputCsPath
)

# merge_offsets.ps1
# -----------------
# Merges the primary offsets.cs with the jewsploit Offsets.h fallback.
# The primary source (imtheo) takes priority. Values from the jewsploit
# fallback are ONLY used to fill in members/classes that are missing from
# the primary offsets.cs.

$ErrorActionPreference = 'Stop'

function Parse-PrimaryCs {
    param([string]$Path)

    $classes = [ordered]@{}
    $current = $null

    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $t = $line.Trim()

        if ($t -match '^\}\s*$') {
            $current = $null
            continue
        }

        if ($t -match '^public\s+static\s+class\s+([\w.]+)\s*\{') {
            $current = $Matches[1]
            if (-not $classes.Contains($current)) {
                $classes[$current] = [ordered]@{}
            }
            continue
        }

        if ($null -ne $current) {
            if ($t -match '^public\s+const\s+long\s+(\w+)\s*=\s*(.+?);') {
                $name = $Matches[1]
                $value = $Matches[2].Trim()
                if (-not $classes[$current].Contains($name)) {
                    $classes[$current][$name] = $value
                }
                continue
            }
            if ($t -match '^public\s+static\s+string\s+(\w+)\s*=\s*"([^"]*)";') {
                $name = $Matches[1]
                if (-not $classes[$current].Contains($name)) {
                    $classes[$current][$name] = '"' + $Matches[2] + '"'
                }
            }
        }
    }

    return , $classes
}

function Parse-FallbackH {
    param([string]$Path)

    $root = @{
        Name     = ''
        Members  = [ordered]@{}
        Children = [ordered]@{}
    }
    $stack = New-Object System.Collections.Generic.Stack[object]
    $stack.Push($root)

    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $t = $line.Trim()

        if ($t -match '^namespace\s+([\w]+)\s*\{') {
            $name = $Matches[1]
            $node = @{
                Name     = $name
                Members  = [ordered]@{}
                Children = [ordered]@{}
            }
            $parent = $stack.Peek()
            if (-not $parent['Children'].Contains($name)) {
                $parent['Children'][$name] = $node
            }
            $stack.Push($node)
            continue
        }

        if ($t -match '^\}\s*$') {
            if ($stack.Count -gt 1) {
                [void]$stack.Pop()
            }
            continue
        }

        $node = $stack.Peek()
        if ($node['Name'] -eq '') { continue }

        if ($t -match '^inline\s+constexpr\s+(?:uintptr_t|std::uint32_t|std::uint64_t|std::int32_t|std::int64_t|int|long|bool|float)\s+(\w+)\s*=\s*(.+?);') {
            $name = $Matches[1]
            $value = $Matches[2].Trim()
            $value = $value -replace '[uUlL]+$', ''
            if (-not $node['Members'].Contains($name)) {
                $node['Members'][$name] = $value
            }
            continue
        }

        if ($t -match '^inline\s+std::string\s+(\w+)\s*=\s*"([^"]*)";') {
            $name = $Matches[1]
            if (-not $node['Members'].Contains($name)) {
                $node['Members'][$name] = '"' + $Matches[2] + '"'
            }
        }
    }

    return , $root
}

function Flatten-HTree {
    param($Node, [string]$ParentPath)

    $result = [ordered]@{}
    $nodePath = [string]$Node['Name']
    if ([string]::IsNullOrEmpty($nodePath)) { $nodePath = '' }
    if ($ParentPath) { $nodePath = "$ParentPath.$nodePath" }
    if ($nodePath -eq 'Offsets') { $nodePath = '' }

    $targetPath = $nodePath
    if ([string]::IsNullOrEmpty($targetPath)) { $targetPath = 'Info' }

    $members = $Node['Members']
    if ($members.Count -gt 0) {
        if (-not $result.Contains($targetPath)) {
            $result[$targetPath] = [ordered]@{}
            foreach ($m in @($members.Keys)) {
                $result[$targetPath][$m] = $members[$m]
            }
        } else {
            foreach ($m in @($members.Keys)) {
                if (-not $result[$targetPath].Contains($m)) {
                    $result[$targetPath][$m] = $members[$m]
                }
            }
        }
    }

    foreach ($childName in @($Node['Children'].Keys)) {
        $child = $Node['Children'][$childName]
        $childFlat = Flatten-HTree -Node $child -ParentPath $nodePath
        foreach ($k in @($childFlat.Keys)) {
            if (-not $result.Contains($k)) {
                $result[$k] = $childFlat[$k]
            } else {
                foreach ($m in @($childFlat[$k].Keys)) {
                    if (-not $result[$k].Contains($m)) {
                        $result[$k][$m] = $childFlat[$k][$m]
                    }
                }
            }
        }
    }

    return , $result
}

function Build-Tree {
    param($ClassDict)

    $tree = [ordered]@{}
    foreach ($path in @($ClassDict.Keys)) {
        $parts = $path.Split('.')
        $cur = $tree
        for ($i = 0; $i -lt $parts.Count; $i++) {
            $p = $parts[$i]
            if (-not $cur.Contains($p)) {
                $cur[$p] = @{
                    Members  = [ordered]@{}
                    Children = [ordered]@{}
                }
            }
            $node = $cur[$p]
            if ($i -eq $parts.Count - 1) {
                foreach ($m in @($ClassDict[$path].Keys)) {
                    if (-not $node['Members'].Contains($m)) {
                        $node['Members'][$m] = $ClassDict[$path][$m]
                    }
                }
            } else {
                $cur = $node['Children']
            }
        }
    }
    return , $tree
}

function Write-Node {
    param($Node, [int]$Depth)

    foreach ($name in @($Node.Keys)) {
        $child = $Node[$name]
        $classIndent = '    ' * $Depth
        $memberIndent = '    ' * ($Depth + 1)

        [void]$script:sb.AppendLine($classIndent + 'public static class ' + $name + ' {')

        foreach ($m in @($child['Members'].Keys)) {
            $v = $child['Members'][$m]
            if ($v -match '^"') {
                [void]$script:sb.AppendLine($memberIndent + 'public static string ' + $m + ' = ' + $v + ';')
            } else {
                [void]$script:sb.AppendLine($memberIndent + 'public const long ' + $m + ' = ' + $v + ';')
            }
        }

        if ($child['Children'].Count -gt 0) {
            Write-Node -Node $child['Children'] -Depth ($Depth + 1)
        }

        [void]$script:sb.AppendLine($classIndent + '}')
    }
}

try {
    $primary = Parse-PrimaryCs -Path $PrimaryCsPath
    $fallbackTree = Parse-FallbackH -Path $FallbackHPath
    $fallback = Flatten-HTree -Node $fallbackTree -ParentPath ''

    $merged = [ordered]@{}
    foreach ($k in @($primary.Keys)) {
        $merged[$k] = $primary[$k]
    }
    foreach ($k in @($fallback.Keys)) {
        if (-not $merged.Contains($k)) {
            $merged[$k] = $fallback[$k]
        } else {
            foreach ($m in @($fallback[$k].Keys)) {
                if (-not $merged[$k].Contains($m)) {
                    $merged[$k][$m] = $fallback[$k][$m]
                }
            }
        }
    }

    $tree = Build-Tree -ClassDict $merged

    $header = @'
/* =============================================================
/*                       Ardvark Offsets
/* -------------------------------------------------------------
/*  Primary source  : https://imtheo.lol/Offsets/Offsets.cs
/*  Fallback source : https://awaky1337.github.io/jewsploit-offsets/Offsets.h
/*  Fallback offsets are only used to fill in values that are
/*  not present in the primary source.
/* =============================================================
*/

namespace Offsets {
'@

    $script:sb = New-Object System.Text.StringBuilder
    [void]$script:sb.AppendLine($header)
    Write-Node -Node $tree -Depth 1
    [void]$script:sb.AppendLine('}')

    $tmp = $OutputCsPath + '.tmp'
    [System.IO.File]::WriteAllText($tmp, $script:sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -Force -Path $tmp -Destination $OutputCsPath

    Write-Host "[+] Merged offsets written to: $OutputCsPath"
} catch {
    Write-Error $_
    exit 1
}