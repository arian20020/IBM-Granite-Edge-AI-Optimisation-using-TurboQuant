$modulePath = Join-Path $PSScriptRoot `
    '..\..\scripts\hardware-inspection\HardwareInspectionTrust.psm1'

Import-Module $modulePath -Force

Describe 'Hardware Inspection Code Integrity diagnostics' {
    It 'selects the policy-bearing event for the exact package and activation window' {
        $package = 'GraniteEdgeAI.WinUI.UnitTests_1.0.0.0_x64__ec7dbtmc7r07r'
        $started = [DateTime]::Parse('2026-08-24T16:31:35Z').ToUniversalTime()
        $message3033 = 'Code Integrity determined that a process ' +
            "(\Device\HarddiskVolume3\Program Files\WindowsApps\$package\GraniteEdgeAI.UnitTests.exe) " +
            "attempted to load \Device\HarddiskVolume3\Program Files\WindowsApps\$package\GraniteEdgeAI.HardwareInspection.Foundation.dll " +
            'that did not meet the Custom 1 signing level requirements.'
        $message3077 = $message3033.TrimEnd('.') +
            ' or violated code integrity policy ' +
            '(Policy ID:{0283ac0f-fff1-49ae-ada1-8a933130cad6}).'

        $events = @(
            [pscustomobject]@{
                TimeCreated = $started.AddSeconds(-2)
                Id = 3077
                Message = $message3077
            }
            [pscustomobject]@{
                TimeCreated = $started.AddSeconds(1)
                Id = 3033
                Message = $message3033
            }
            [pscustomobject]@{
                TimeCreated = $started.AddSeconds(1)
                Id = 3077
                Message = $message3077
            }
            [pscustomobject]@{
                TimeCreated = $started.AddSeconds(2)
                Id = 3077
                Message = $message3077.Replace($package, 'Other.Package_1.0.0.0_x64__other')
            }
        )

        $actual = Find-HardwareInspectionCodeIntegrityBlock `
            -Events $events `
            -PackageFullName $package `
            -NotBeforeUtc $started

        $actual.EventId | Should Be 3077
        $actual.ModuleName | Should Be 'GraniteEdgeAI.HardwareInspection.Foundation.dll'
        $actual.PolicyId | Should Be '0283ac0f-fff1-49ae-ada1-8a933130cad6'
    }

    It 'returns no diagnosis for unrelated events' {
        $actual = Find-HardwareInspectionCodeIntegrityBlock `
            -Events @([pscustomobject]@{
                    TimeCreated = [DateTime]::UtcNow
                    Id = 3077
                    Message = 'An unrelated Code Integrity event.'
                }) `
            -PackageFullName 'GraniteEdgeAI.WinUI.UnitTests_1.0.0.0_x64__ec7dbtmc7r07r' `
            -NotBeforeUtc ([DateTime]::UtcNow.AddSeconds(-1))

        $actual | Should BeNullOrEmpty
    }
}

Describe 'Hardware Inspection trusted-signing argument contract' {
    It 'replaces one whole file-path token without reparsing it' {
        $actual = Resolve-HardwareInspectionSigningArguments `
            -ArgumentTemplate @('sign', '/fd', 'SHA256', '{FilePath}') `
            -FilePath 'C:\staging root\GraniteEdgeAI.dll'

        $actual.Count | Should Be 4
        $actual[0] | Should Be 'sign'
        $actual[3] | Should Be 'C:\staging root\GraniteEdgeAI.dll'
    }

    It 'rejects an embedded file-path token' {
        {
            Resolve-HardwareInspectionSigningArguments `
                -ArgumentTemplate @('sign', '/file:{FilePath}') `
                -FilePath 'C:\staging\GraniteEdgeAI.dll'
        } | Should Throw
    }

    It 'rejects a template with more than one file-path token' {
        {
            Resolve-HardwareInspectionSigningArguments `
                -ArgumentTemplate @('{FilePath}', '{FilePath}') `
                -FilePath 'C:\staging\GraniteEdgeAI.dll'
        } | Should Throw
    }

    It 'fails closed before executing a provider whose hash is unexpected' {
        $testRoot = Join-Path $env:TEMP ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $testRoot | Out-Null
        try {
            $provider = Join-Path $testRoot 'provider.exe'
            $target = Join-Path $testRoot 'target.dll'
            [IO.File]::WriteAllBytes($provider, [byte[]] @(1, 2, 3))
            [IO.File]::WriteAllBytes($target, [byte[]] @(4, 5, 6))

            {
                Invoke-HardwareInspectionTrustedSigner `
                    -FilePath $target `
                    -ProviderPath $provider `
                    -ExpectedProviderSha256 ('0' * 64) `
                    -ArgumentTemplate @('{FilePath}') `
                    -ExpectedPublisher 'CN=GraniteEdgeAI'
            } | Should Throw 'The trusted-signing provider hash is invalid.'
        }
        finally {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
}

Describe 'Hardware Inspection signing entry point' {
    It 'exposes the complete trusted-signing parameter boundary' {
        $scriptPath = Join-Path $PSScriptRoot `
            '..\..\scripts\hardware-inspection\Invoke-SignedHardwareInspectionAcceptance.ps1'
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            $scriptPath,
            [ref] $tokens,
            [ref] $errors)
        $errors.Count | Should Be 0
        $parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object {
                $_.Name.VariablePath.UserPath
            })

        ($parameterNames -contains 'TrustedSignerPath') | Should Be $true
        ($parameterNames -contains 'ExpectedTrustedSignerSha256') | Should Be $true
        ($parameterNames -contains 'TrustedSignerArgumentTemplate') | Should Be $true
        ($parameterNames -contains 'TrustedBundleDirectory') | Should Be $true
    }
}
