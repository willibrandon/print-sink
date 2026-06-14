param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [string] $PackagePath,

    [switch] $BuildPackage,

    [switch] $SkipPackageInstall,

    [string] $OutputDirectory = (Join-Path $PSScriptRoot "artifacts\e2e\$Platform"),

    [switch] $KeepQueues,

    [switch] $KeepPackage
)

$ErrorActionPreference = 'Stop'

$PackageCertificateSubject = 'CN=PrintSink'
$PackageCertificateFriendlyName = 'PrintSink local E2E package signing'
$CodeSigningEnhancedKeyUsageOid = '1.3.6.1.5.5.7.3.3'

function Assert-Administrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
    $isAdministrator = $principal.IsInRole(
        [System.Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isAdministrator) {
        throw 'PrintSink E2E requires elevated PowerShell because the IPP association path installs a temporary signed INF.'
    }
}

function Add-CertificateToStore {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [System.Security.Cryptography.X509Certificates.StoreName] $StoreName,
        [System.Security.Cryptography.X509Certificates.StoreLocation] $StoreLocation
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        $StoreLocation)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $existing = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Certificate.Thumbprint,
            $false)
        if ($existing.Count -eq 0) {
            $store.Add($Certificate)
        }
    }
    finally {
        $store.Dispose()
    }
}

function Test-PrintSinkPackageCertificate {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )

    if ($Certificate.Subject -ne $PackageCertificateSubject) {
        return $false
    }

    if ($Certificate.FriendlyName -ne $PackageCertificateFriendlyName) {
        return $false
    }

    if (-not $Certificate.HasPrivateKey) {
        return $false
    }

    if ($Certificate.NotAfter -le (Get-Date).AddDays(1)) {
        return $false
    }

    $enhancedKeyUsageExtension = $Certificate.Extensions |
        Where-Object { $null -ne $_.Oid -and $_.Oid.Value -eq '2.5.29.37' } |
        Select-Object -First 1
    if ($null -eq $enhancedKeyUsageExtension) {
        return $false
    }

    $enhancedKeyUsages = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] $enhancedKeyUsageExtension
    $codeSigningUsage = $enhancedKeyUsages.EnhancedKeyUsages |
        Where-Object { $_.Value -eq $CodeSigningEnhancedKeyUsageOid } |
        Select-Object -First 1
    return $null -ne $codeSigningUsage
}

function Find-PrintSinkPackageCertificate {
    return Get-ChildItem -LiteralPath Cert:\CurrentUser\My |
        Where-Object { Test-PrintSinkPackageCertificate -Certificate $_ } |
        Sort-Object -Property NotAfter -Descending |
        Select-Object -First 1
}

function New-PrintSinkPackageCertificate {
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $PackageCertificateSubject `
        -FriendlyName $PackageCertificateFriendlyName `
        -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @("2.5.29.37={text}$CodeSigningEnhancedKeyUsageOid")

    return $cert
}

function Get-PrintSinkPackageCertificate {
    $cert = Find-PrintSinkPackageCertificate
    if ($null -eq $cert) {
        $cert = New-PrintSinkPackageCertificate
    }

    Add-CertificateToStore `
        -Certificate $cert `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
        -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    Add-CertificateToStore `
        -Certificate $cert `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
        -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)

    return $cert
}

function Find-LatestMsixPackage {
    param(
        [string[]] $SearchRoots
    )

    $packages = foreach ($searchRoot in $SearchRoots) {
        if (Test-Path -LiteralPath $searchRoot -PathType Container) {
            Get-ChildItem -LiteralPath $searchRoot -Recurse -Filter '*.msix'
        }
    }

    return $packages |
        Sort-Object -Property LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Build-PrintSinkPackage {
    param(
        [string] $PackageDirectory,
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )

    New-Item -ItemType Directory -Force -Path $PackageDirectory | Out-Null
    $packageDirectoryWithSlash = $PackageDirectory.TrimEnd('\') + '\'

    $arguments = @(
        'src\PrintSink.App\PrintSink.App.csproj',
        '/t:Restore,Build',
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxPackageSigningEnabled=true',
        "/p:PackageCertificateThumbprint=$($Certificate.Thumbprint)",
        '/p:AppxBundle=Never',
        '/p:UapAppxPackageBuildMode=SideloadOnly',
        "/p:AppxPackageDir=$packageDirectoryWithSlash",
        '/nologo',
        '/v:minimal'
    )

    & msbuild @arguments | ForEach-Object {
        Write-Host $_
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $package = Find-LatestMsixPackage -SearchRoots @($PackageDirectory)
    if ($null -eq $package) {
        throw "Signed MSIX package was not produced under $PackageDirectory."
    }

    $certificatePath = Join-Path `
        $package.DirectoryName `
        "$([System.IO.Path]::GetFileNameWithoutExtension($package.Name)).cer"
    Export-Certificate -Cert $Certificate -FilePath $certificatePath -Force | Out-Null

    return $package.FullName
}

function Remove-PrintSinkPackage {
    param(
        [string] $ResultPath
    )

    $packageFullName = $null
    if (Test-Path -LiteralPath $ResultPath -PathType Leaf) {
        try {
            $result = Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json
            $packageFullName = [string]$result.package.fullName
        }
        catch {
            $packageFullName = $null
        }
    }

    $packages = if ([string]::IsNullOrWhiteSpace($packageFullName)) {
        @(Get-AppxPackage -Name 'PrintSink')
    }
    else {
        @(Get-AppxPackage -Name 'PrintSink' | Where-Object { $_.PackageFullName -eq $packageFullName })
    }

    foreach ($package in $packages) {
        Write-Host "Removing package $($package.PackageFullName)"
        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
    }
}

Assert-Administrator

if ($SkipPackageInstall -and (-not [string]::IsNullOrWhiteSpace($PackagePath))) {
    throw 'Do not pass -PackagePath with -SkipPackageInstall.'
}

if ($SkipPackageInstall -and $BuildPackage) {
    throw 'Do not pass -BuildPackage with -SkipPackageInstall.'
}

if ($BuildPackage -and (-not [string]::IsNullOrWhiteSpace($PackagePath))) {
    throw 'Do not pass -PackagePath with -BuildPackage.'
}

if (-not $SkipPackageInstall) {
    if ($BuildPackage) {
        $packageDirectory = Join-Path $PSScriptRoot "artifacts\appxpackages\$Platform"
        $certificate = Get-PrintSinkPackageCertificate
        $PackagePath = Build-PrintSinkPackage `
            -PackageDirectory $packageDirectory `
            -Certificate $certificate
    }
    elseif ([string]::IsNullOrWhiteSpace($PackagePath)) {
        $package = Find-LatestMsixPackage -SearchRoots @(
            (Join-Path $PSScriptRoot "artifacts\appxpackages\$Platform"),
            (Join-Path $PSScriptRoot 'src\PrintSink.App\AppPackages')
        )

        if ($null -eq $package) {
            throw 'Pass -PackagePath, use -BuildPackage, or use -SkipPackageInstall when the signed package is already installed.'
        }

        $PackagePath = $package.FullName
    }
    else {
        $PackagePath = [System.IO.Path]::GetFullPath($PackagePath)
        if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
            throw "Package path was not found: $PackagePath"
        }
    }
}

$e2eScript = Join-Path $PSScriptRoot 'tests\e2e\Invoke-PrintSinkE2E.ps1'
$resultPath = Join-Path $OutputDirectory 'e2e-result.json'
$e2eParameters = @{
    OutputDirectory = $OutputDirectory
}

if ($SkipPackageInstall) {
    $e2eParameters.SkipPackageInstall = $true
}
else {
    $e2eParameters.PackagePath = $PackagePath
}

if (-not $KeepQueues) {
    $e2eParameters.Cleanup = $true
}

$shouldRemovePackageAfterRun = (-not $SkipPackageInstall) -and (-not $KeepPackage) -and (-not $KeepQueues)
try {
    & $e2eScript @e2eParameters

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resultAssertionScript = Join-Path $PSScriptRoot 'tests\e2e\Assert-PrintSinkE2EResult.ps1'
    $resultAssertionParameters = @{
        ResultPath = $resultPath
    }

    if (-not $KeepQueues) {
        $resultAssertionParameters.RequireCleanup = $true
    }

    & $resultAssertionScript @resultAssertionParameters

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    if ($shouldRemovePackageAfterRun) {
        Remove-PrintSinkPackage -ResultPath $resultPath
    }
}
