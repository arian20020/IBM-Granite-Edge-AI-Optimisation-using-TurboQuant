[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $RepositoryRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (
        Resolve-Path `
            -LiteralPath (Join-Path $PSScriptRoot '..\..\..') `
            -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop
    ).Path
}

$OptimumIntelCommit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
$OptimumCommit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'

$OptimumIntelSource = [ordered]@{
    name = 'optimum-intel'
    repository = 'huggingface/optimum-intel'
    origin = 'https://github.com/huggingface/optimum-intel.git'
    commit = $OptimumIntelCommit
    clean = $true
    aggregate_sha256 = ('3' * 64)
    fixture_mode = $true
}
$OptimumSource = [ordered]@{
    name = 'optimum'
    repository = 'huggingface/optimum'
    origin = 'https://github.com/huggingface/optimum.git'
    commit = $OptimumCommit
    clean = $true
    aggregate_sha256 = ('4' * 64)
    fixture_mode = $true
}

$LockedRows = @(
    [pscustomobject]@{
        name = 'transformers'; version = '5.5.0'; hash = ('a' * 64)
    },
    [pscustomobject]@{
        name = 'huggingface-hub'; version = '1.21.0'; hash = ('b' * 64)
    },
    [pscustomobject]@{
        name = 'nncf'; version = '3.2.0'; hash = ('c' * 64)
    },
    [pscustomobject]@{
        name = 'openvino'; version = '2026.2.1'; hash = ('d' * 64)
    },
    [pscustomobject]@{
        name = 'openvino-tokenizers'; version = '2026.2.1.0'; hash = ('e' * 64)
    },
    [pscustomobject]@{
        name = 'requests'; version = '2.33.0'; hash = ('f' * 64)
    }
)
$LockText = (
    $LockedRows |
    ForEach-Object {
        "$($_.name)==$($_.version) --hash=sha256:$($_.hash)"
    }
) -join "`n"
$LockText += "`n"
$LockHash = ('9' * 64)

$InstallRows = @(
    foreach ($Row in $LockedRows) {
        [ordered]@{
            download_info = [ordered]@{
                url = "https://files.pythonhosted.org/$($Row.name).whl"
                archive_info = [ordered]@{
                    hashes = [ordered]@{ sha256 = $Row.hash }
                }
            }
            is_direct = $Row.name -ne 'requests'
            requested = $Row.name -ne 'requests'
            metadata = [ordered]@{
                name = $Row.name
                version = $Row.version
            }
        }
    }
)
$InstallReport = [ordered]@{
    version = '1'
    pip_version = '25.2'
    install = $InstallRows
    fixture_mode = $true
}

$VcsPackages = @(
    [ordered]@{
        name = 'optimum-intel'
        version = '2.3.0.dev0'
        commit = $OptimumIntelCommit
    },
    [ordered]@{
        name = 'optimum'
        version = '2.3.0'
        commit = $OptimumCommit
    }
)
$Checks = @(
    [ordered]@{ name = 'resolver'; status = 'Passed'; exit_code = 0 },
    [ordered]@{ name = 'install'; status = 'Passed'; exit_code = 0 },
    [ordered]@{ name = 'imports'; status = 'Passed'; exit_code = 0 },
    [ordered]@{ name = 'cli_help'; status = 'Passed'; exit_code = 0 },
    [ordered]@{
        name = 'no_model_compatibility'; status = 'Passed'; exit_code = 0
    },
    [ordered]@{
        name = 'remote_code_disabled'; status = 'Passed'; exit_code = $null
    }
)

$DirectRequirementsPath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\requirements.phase3-assets.in'
$DirectRequirements = @(
    Get-Content -LiteralPath $DirectRequirementsPath -Encoding UTF8 |
    Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        -not $_.TrimStart().StartsWith('#')
    } |
    ForEach-Object {
        # Create a new CLR string rather than carrying Get-Content's adapted
        # PSPath/PSProvider/ReadCount properties into the JSON object graph.
        [string]::new(([string]$_).ToCharArray())
    }
)

# A requirement row must be plain string data. Get-Content can attach adapted
# file/provider metadata to emitted strings under Windows PowerShell 5.1; that
# object graph is not permitted to cross into the controlled JSON observation.
if ($DirectRequirements.Count -eq 0) {
    throw 'Dependency observation probe found no direct requirements.'
}
foreach ($Requirement in $DirectRequirements) {
    if ($Requirement -isnot [string]) {
        throw (
            'Direct requirement is not System.String: ' +
            $Requirement.GetType().FullName
        )
    }
    foreach ($AdaptedProperty in @(
        'PSPath'
        'PSParentPath'
        'PSChildName'
        'PSDrive'
        'PSProvider'
        'ReadCount'
    )) {
        if (
            $Requirement.PSObject.Properties.Match($AdaptedProperty).Count -ne 0
        ) {
            throw (
                'Direct requirement retained adapted file-content property: ' +
                $AdaptedProperty
            )
        }
    }
}

$FixtureIdentity = 'C:\w5c\dependency-preflight-offline-fixture-00000000000-1'
$Observation = [ordered]@{
    generated_at_utc = '2026-08-15T00:00:00Z'
    workspace_root = $FixtureIdentity
    workspace_is_normal_local_directory = $true
    workspace_is_fresh = $true
    python_version = '3.12.10'
    python_executable_path = "$FixtureIdentity\venv\Scripts\python.exe"
    python_executable_sha256 = ('1' * 64)
    pip_version = '25.2'
    pip_executable_path = "$FixtureIdentity\venv\Scripts\pip.exe"
    pip_executable_sha256 = ('2' * 64)
    source_trees = @($OptimumIntelSource, $OptimumSource) |
        ForEach-Object {
            [ordered]@{
                name = $_.name
                repository = $_.repository
                origin = $_.origin
                commit = $_.commit
                clean = $_.clean
                aggregate_sha256 = $_.aggregate_sha256
            }
        }
    direct_requirements = $DirectRequirements
    lock_path = 'locks/requirements.phase3-assets.txt'
    lock_text = $LockText
    lock_sha256 = $LockHash
    lock_generator = 'pip-tools==7.5.0'
    normal_install_report = $InstallReport
    vcs_packages = $VcsPackages
    checks = $Checks
    import_modules = @(
        'optimum',
        'optimum.intel',
        'transformers',
        'nncf',
        'openvino'
    )
    cli_help_exit_code = 0
    no_model_compatibility_exit_code = 0
}

$SerializationProbe = [ordered]@{}
foreach ($FieldName in @($Observation.Keys)) {
    $SerializationProbe[$FieldName] = $Observation[$FieldName]
    Write-Host ("WB05_DEP_SERIALIZE_FIELD:{0}:start" -f $FieldName)
    $null = ConvertTo-Json -InputObject $SerializationProbe -Depth 12
    Write-Host ("WB05_DEP_SERIALIZE_FIELD:{0}:return" -f $FieldName)
}

Write-Host 'Workbook 05 dependency observation serialization probe passed.'
