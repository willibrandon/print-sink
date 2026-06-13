param(
    [string] $PackageName = 'PrintSink',
    [string] $PackagePath,
    [switch] $SkipPackageInstall,
    [string] $OutputDirectory = (Join-Path $env:TEMP "PrintSink.E2E.$([Guid]::NewGuid())"),
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'

$isWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

if (-not $isWindowsPlatform) {
    throw 'PrintSink E2E tests require Windows.'
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

$expectedQueues = @(
    'PrintSink - PDF',
    'PrintSink - XPS',
    'PrintSink - PostScript',
    'PrintSink - Cloud',
    'PrintSink - PWG Raster',
    'PrintSink - PCLm'
)

$realPrintCases = @(
    [ordered]@{
        queue = 'PrintSink - PDF'
        format = 'pdf'
        extension = '.pdf'
        requiresSaveAs = $true
        expectedText = 'foo'
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    },
    [ordered]@{
        queue = 'PrintSink - XPS'
        format = 'oxps'
        extension = '.oxps'
        requiresSaveAs = $true
        expectedText = 'foo'
        expectedRoute = 'application/oxps -> Oxps; Copy; Endpoint supports passthrough.'
    },
    [ordered]@{
        queue = 'PrintSink - PostScript'
        format = 'postscript'
        extension = '.ps'
        requiresSaveAs = $true
        expectedText = ''
        expectedRoute = 'application/postscript -> PostScript; Copy; Endpoint supports passthrough.'
    },
    [ordered]@{
        queue = 'PrintSink - PWG Raster'
        format = 'pwg'
        extension = '.pwg'
        requiresSaveAs = $true
        expectedText = ''
        expectedRoute = 'application/oxps -> PwgRaster; Convert; Convert XPS to PWG Raster.'
    },
    [ordered]@{
        queue = 'PrintSink - PCLm'
        format = 'pclm'
        extension = '.pclm'
        requiresSaveAs = $true
        expectedText = ''
        expectedRoute = 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.'
    },
    [ordered]@{
        queue = 'PrintSink - Cloud'
        format = 'cloud'
        sinkFormat = 'pdf'
        extension = ''
        requiresSaveAs = $false
        expectedText = 'foo'
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    }
)

$expectedVirtualPrinters = @(
    [ordered]@{
        printerUri = 'printsink:print-to-pdf'
        displayNameResource = 'ms-resource:PdfPrintDisplayName'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'pdf'
        pdcFile = 'Config\PrinterPdf.pdc.xml'
        pdrFile = 'Config\PrinterPdf.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/pdf'; maxVersion = '1.7' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-xps'
        displayNameResource = 'ms-resource:XpsPrintDisplayName'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'xps;oxps'
        pdcFile = 'Config\PrinterXps.pdc.xml'
        pdrFile = 'Config\PrinterXps.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/oxps'; maxVersion = '1.0' },
            [ordered]@{ type = 'application/vnd.ms-xpsdocument'; maxVersion = '1.0' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-ps'
        displayNameResource = 'ms-resource:PostScriptPrintDisplayName'
        preferredInputFormat = 'application/postscript'
        outputFileTypes = 'ps'
        pdcFile = 'Config\PrinterPostScript.pdc.xml'
        pdrFile = 'Config\PrinterPostScript.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/postscript'; maxVersion = '3.0' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-cloud'
        displayNameResource = 'ms-resource:CloudPrintDisplayName'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = ''
        pdcFile = 'Config\PrinterCloud.pdc.xml'
        pdrFile = 'Config\PrinterCloud.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/pdf'; maxVersion = '1.7' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-pwgr'
        displayNameResource = 'ms-resource:PwgRasterPrintDisplayName'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'pwg'
        pdcFile = 'Config\PrinterPwgRaster.pdc.xml'
        pdrFile = 'Config\PrinterPwgRaster.pdr.xml'
        supportedFormats = @()
    },
    [ordered]@{
        printerUri = 'printsink:print-to-pclm'
        displayNameResource = 'ms-resource:PclmPrintDisplayName'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'pclm'
        pdcFile = 'Config\PrinterPclm.pdc.xml'
        pdrFile = 'Config\PrinterPclm.pdr.xml'
        supportedFormats = @()
    }
)

function Get-InstalledPackage {
    param(
        [string] $Name
    )

    $package = Get-AppxPackage -Name $Name |
        Sort-Object -Property Version -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        throw "Package '$Name' is not installed for the current user."
    }

    return $package
}

function Join-PackagePath {
    param(
        [string] $PackageRoot,
        [string] $RelativePath
    )

    $fullRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    $fullRootWithSeparator = $fullRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $PackageRoot $RelativePath))
    if (-not $candidate.StartsWith($fullRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package-relative path escapes the package root: $RelativePath"
    }

    return $candidate
}

function Assert-PackageFile {
    param(
        [string] $PackageRoot,
        [string] $RelativePath
    )

    $path = Join-PackagePath -PackageRoot $PackageRoot -RelativePath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Package file is missing: $RelativePath"
    }
}

function New-AppxNamespaceManager {
    param(
        [xml] $Manifest
    )

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($Manifest.NameTable)
    $namespaceManager.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10') | Out-Null
    $namespaceManager.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3') | Out-Null
    $namespaceManager.AddNamespace('uap10', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/10') | Out-Null
    $namespaceManager.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10') | Out-Null
    $namespaceManager.AddNamespace('printsupport', 'http://schemas.microsoft.com/appx/manifest/printsupport/windows10') | Out-Null
    $namespaceManager.AddNamespace('printsupport2', 'http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2') | Out-Null
    return ,$namespaceManager
}

function Assert-ManifestNode {
    param(
        [xml] $Manifest,
        [System.Xml.XmlNamespaceManager] $NamespaceManager,
        [string] $XPath,
        [string] $Description
    )

    $node = $Manifest.SelectSingleNode($XPath, $NamespaceManager)
    if ($null -eq $node) {
        throw "Package manifest is missing $Description."
    }

    return $node
}

function Get-InstalledPackageManifestPath {
    param(
        [string] $PackageRoot
    )

    $candidateNames = @('Package.appxmanifest', 'AppxManifest.xml')
    foreach ($candidateName in $candidateNames) {
        $candidatePath = Join-PackagePath -PackageRoot $PackageRoot -RelativePath $candidateName
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    throw "Installed package manifest was not found under $PackageRoot."
}

function Get-ExpectedVirtualPrinterValue {
    param(
        [System.Collections.Specialized.OrderedDictionary] $ExpectedPrinter,
        [string] $AttributeName
    )

    switch ($AttributeName) {
        'PreferredInputFormat' { return $ExpectedPrinter.preferredInputFormat }
        'PdcFile' { return $ExpectedPrinter.pdcFile }
        'PdrFile' { return $ExpectedPrinter.pdrFile }
        default { throw "Unsupported virtual-printer attribute: $AttributeName" }
    }
}

function Assert-InstalledPackageShape {
    param(
        $Package,
        [object[]] $ExpectedVirtualPrinters
    )

    if ($Package.IsDevelopmentMode) {
        throw "Package '$($Package.PackageFullName)' is registered in development mode. Install a signed MSIX with -PackagePath for E2E provisioning."
    }

    $installLocation = $Package.InstallLocation
    if ([string]::IsNullOrWhiteSpace($installLocation) -or -not (Test-Path -LiteralPath $installLocation -PathType Container)) {
        throw "Package install location is unavailable for $($Package.PackageFullName)."
    }

    $manifestPath = Get-InstalledPackageManifestPath -PackageRoot $installLocation
    [xml] $manifest = Get-Content -LiteralPath $manifestPath -Raw
    [System.Xml.XmlNamespaceManager] $namespaceManager = New-AppxNamespaceManager -Manifest $manifest
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//uap3:Extension[@Category="windows.appExecutionAlias"]/uap3:AppExecutionAlias/desktop:ExecutionAlias[@Alias="printsink-app.exe"]' -Description 'the printsink-app.exe execution alias' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//appx:Application[@uap10:SupportsMultipleInstances="true"]' -Description 'multiple-instance application support' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//appx:Capability[@Name="privateNetworkClientServer"]' -Description 'private network client/server capability for IPP communication' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportWorkflow" and @EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask"]' -Description 'the print support workflow extension' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportExtension" and @EntryPoint="PrintSink.Tasks.PrintSupportExtensionBackgroundTask"]' -Description 'the print support extension background task' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportSettingsUI" and @EntryPoint="PrintSink.App.App"]' -Description 'the settings UI extension' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportJobUI" and @EntryPoint="PrintSink.App.App"]' -Description 'the job UI extension' | Out-Null

    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'WinRT.Host.dll'
    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'PrintSink.Tasks.winmd'
    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'PrintSink.Xps.dll'

    $activationClasses = @(
        'PrintSink.Tasks.PrintSupportWorkflowBackgroundTask',
        'PrintSink.Tasks.PrintSupportExtensionBackgroundTask',
        'PrintSink.Tasks.VirtualPrinterBackgroundTask',
        'PrintSink.Xps.XpsPageWatermarker',
        'PrintSink.Xps.XpsSequentialDocument'
    )

    foreach ($activationClass in $activationClasses) {
        Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath "//appx:ActivatableClass[@ActivatableClassId=`"$activationClass`"]" -Description "activatable class $activationClass" | Out-Null
    }

    $printerNodes = @($manifest.SelectNodes('//printsupport2:PrintSupportVirtualPrinter', $namespaceManager))
    if ($printerNodes.Count -ne $ExpectedVirtualPrinters.Count) {
        throw "Expected $($ExpectedVirtualPrinters.Count) virtual-printer manifest entries but found $($printerNodes.Count)."
    }

    $reportedPrinters = @()
    foreach ($expectedPrinter in $ExpectedVirtualPrinters) {
        $printerNode = $printerNodes |
            Where-Object { $_.GetAttribute('PrinterUri') -eq $expectedPrinter.printerUri } |
            Select-Object -First 1
        if ($null -eq $printerNode) {
            throw "Package manifest is missing virtual printer '$($expectedPrinter.printerUri)'."
        }

        $actualDisplayName = $printerNode.GetAttribute('DisplayName')
        if ($actualDisplayName -ne $expectedPrinter.displayNameResource) {
            throw "Virtual printer '$($expectedPrinter.printerUri)' has DisplayName '$actualDisplayName'; expected '$($expectedPrinter.displayNameResource)'."
        }

        foreach ($attributeName in @('PreferredInputFormat', 'PdcFile', 'PdrFile')) {
            $actual = $printerNode.GetAttribute($attributeName)
            $expected = Get-ExpectedVirtualPrinterValue -ExpectedPrinter $expectedPrinter -AttributeName $attributeName
            if ($actual -ne $expected) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' has $attributeName '$actual'; expected '$expected'."
            }
        }

        $actualOutputFileTypes = $printerNode.GetAttribute('OutputFileTypes')
        if ($actualOutputFileTypes -ne $expectedPrinter.outputFileTypes) {
            throw "Virtual printer '$($expectedPrinter.printerUri)' has OutputFileTypes '$actualOutputFileTypes'; expected '$($expectedPrinter.outputFileTypes)'."
        }

        Assert-PackageFile -PackageRoot $installLocation -RelativePath $expectedPrinter.pdcFile
        Assert-PackageFile -PackageRoot $installLocation -RelativePath $expectedPrinter.pdrFile

        $supportedFormatNodes = @($printerNode.SelectNodes('printsupport2:SupportedFormats/printsupport2:SupportedFormat', $namespaceManager))
        $actualSupportedFormats = @($supportedFormatNodes | ForEach-Object {
            [pscustomobject]@{
                type = $_.GetAttribute('Type')
                maxVersion = $_.GetAttribute('MaxVersion')
            }
        } | Sort-Object -Property type)
        $expectedSupportedFormats = @($expectedPrinter.supportedFormats | ForEach-Object {
            [pscustomobject]@{
                type = $_['type']
                maxVersion = $_['maxVersion']
            }
        } | Sort-Object -Property type)

        if ($actualSupportedFormats.Count -ne $expectedSupportedFormats.Count) {
            throw "Virtual printer '$($expectedPrinter.printerUri)' supported format count differs. Actual: $($actualSupportedFormats.Count); expected: $($expectedSupportedFormats.Count)."
        }

        foreach ($expectedFormat in $expectedSupportedFormats) {
            $actualFormat = $actualSupportedFormats |
                Where-Object { $_.type -eq $expectedFormat.type } |
                Select-Object -First 1
            if ($null -eq $actualFormat) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' is missing supported format '$($expectedFormat.type)'."
            }

            if ($actualFormat.maxVersion -ne $expectedFormat.maxVersion) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' supported format '$($expectedFormat.type)' has MaxVersion '$($actualFormat.maxVersion)'; expected '$($expectedFormat.maxVersion)'."
            }
        }

        $reportedPrinters += [ordered]@{
            printerUri = $expectedPrinter.printerUri
            displayName = $actualDisplayName
            preferredInputFormat = $printerNode.GetAttribute('PreferredInputFormat')
            outputFileTypes = $actualOutputFileTypes
            pdcFile = $expectedPrinter.pdcFile
            pdrFile = $expectedPrinter.pdrFile
            supportedFormats = $actualSupportedFormats
        }
    }

    return [ordered]@{
        manifestPath = $manifestPath
        supportsMultipleInstances = $true
        virtualPrinters = $reportedPrinters
        activationClasses = $activationClasses
    }
}

function Invoke-PrintSinkAppCommand {
    param(
        [string[]] $Arguments,
        [string] $Description
    )

    $headlessLog = Join-Path $env:TEMP 'PrintSink.App.headless.log'
    $isProvisioningCommand = $Arguments -contains '--install-virtual-printers' `
        -or $Arguments -contains '--remove-virtual-printers'
    $maxAttempts = if ($isProvisioningCommand) {
        4
    }
    else {
        1
    }

    $diagnostic = 'No headless diagnostic log was written.'
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        Remove-Item $headlessLog -ErrorAction SilentlyContinue
        & printsink-app.exe @Arguments
        if ($LASTEXITCODE -eq 0) {
            return
        }

        $diagnostic = if (Test-Path $headlessLog) {
            Get-Content $headlessLog -Raw
        }
        else {
            'No headless diagnostic log was written.'
        }

        if ($attempt -lt $maxAttempts) {
            Start-Sleep -Seconds 5
        }
    }

    throw "$Description failed after $maxAttempts attempt(s). $diagnostic"
}

function Close-SavePrintOutputDialogs {
    try {
        Add-Type -AssemblyName UIAutomationClient
        Add-DialogNativeMethods

        $dialogs = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As'))

        foreach ($dialog in $dialogs) {
            try {
                [object] $windowPattern = $null
                if ($dialog.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$windowPattern)) {
                    $windowPattern.Close()
                    continue
                }

                $dialogHandle = [IntPtr]$dialog.Current.NativeWindowHandle
                if ($dialogHandle -ne [IntPtr]::Zero) {
                    [PrintSinkE2E.DialogNativeMethods]::SendMessage($dialogHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                }
            }
            catch {
                Write-Verbose "Failed to close a Save Print Output As dialog: $($_.Exception.Message)"
            }
        }
    }
    catch {
        Write-Verbose "Failed to inspect Save Print Output As dialogs: $($_.Exception.Message)"
    }
}

function Stop-PrintSinkE2ERuntime {
    Close-SavePrintOutputDialogs

    Get-Process -Name 'PrintSink*', 'PrintDialog' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Stop-PrintSinkProcess {
    param(
        [System.Diagnostics.Process] $Process
    )

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    try {
        $Process.Kill($true)
    }
    catch [System.Management.Automation.MethodException] {
        $Process.Kill()
    }

    $Process.WaitForExit(5000) | Out-Null
}

function Import-PrintSinkPackageCertificate {
    param(
        [string] $PackagePath
    )

    $packageDirectory = Split-Path -Parent $PackagePath
    $packageBaseName = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
    $certificatePath = Join-Path $packageDirectory "$packageBaseName.cer"
    if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
        return
    }

    Write-E2EProgress "Trusting package certificate $certificatePath"
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
    Add-PrintSinkPackageCertificateToStore `
        -Certificate $certificate `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
        -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    Add-PrintSinkPackageCertificateToStore `
        -Certificate $certificate `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
        -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
}

function Add-PrintSinkPackageCertificateToStore {
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

function Add-MediumIntegrityProcessLauncher {
    if ('PrintSinkE2E.MediumIntegrityProcessLauncher' -as [type]) {
        return
    }

    Add-Type -TypeDefinition @'
namespace PrintSinkE2E
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class MediumIntegrityProcessLauncher
    {
        private const uint TokenAssignPrimary = 0x0001;
        private const uint TokenDuplicate = 0x0002;
        private const uint TokenQuery = 0x0008;
        private const uint TokenAdjustDefault = 0x0080;
        private const uint TokenAdjustSessionId = 0x0100;

        private enum SecurityImpersonationLevel
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation,
        }

        private enum TokenType
        {
            TokenPrimary = 1,
            TokenImpersonation,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfo
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            SecurityImpersonationLevel impersonationLevel,
            TokenType tokenType,
            out IntPtr newToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr token,
            uint logonFlags,
            string applicationName,
            string commandLine,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public static int Start(
            IntPtr sourceProcessHandle,
            string applicationPath,
            string[] arguments,
            string workingDirectory)
        {
            IntPtr token;
            if (!OpenProcessToken(
                sourceProcessHandle,
                TokenAssignPrimary | TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAdjustSessionId,
                out token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                IntPtr primaryToken;
                if (!DuplicateTokenEx(
                    token,
                    TokenAssignPrimary | TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAdjustSessionId,
                    IntPtr.Zero,
                    SecurityImpersonationLevel.SecurityImpersonation,
                    TokenType.TokenPrimary,
                    out primaryToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    StartupInfo startupInfo = new StartupInfo
                    {
                        cb = (uint)Marshal.SizeOf<StartupInfo>(),
                    };
                    ProcessInformation processInformation;
                    string commandLine = BuildCommandLine(applicationPath, arguments);
                    if (!CreateProcessWithTokenW(
                        primaryToken,
                        0,
                        null,
                        commandLine,
                        0,
                        IntPtr.Zero,
                        workingDirectory,
                        ref startupInfo,
                        out processInformation))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    CloseHandle(processInformation.hThread);
                    CloseHandle(processInformation.hProcess);
                    return (int)processInformation.dwProcessId;
                }
                finally
                {
                    CloseHandle(primaryToken);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        private static string BuildCommandLine(string applicationPath, string[] arguments)
        {
            StringBuilder builder = new StringBuilder(QuoteArgument(applicationPath));
            foreach (string argument in arguments)
            {
                builder.Append(' ');
                builder.Append(QuoteArgument(argument));
            }

            return builder.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length == 0)
            {
                return "\"\"";
            }

            bool requiresQuoting = false;
            foreach (char value in argument)
            {
                if (char.IsWhiteSpace(value) || value == '"')
                {
                    requiresQuoting = true;
                    break;
                }
            }

            if (!requiresQuoting)
            {
                return argument;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            foreach (char value in argument)
            {
                if (value == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (value == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                builder.Append('\\', backslashes);
                builder.Append(value);
                backslashes = 0;
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}
'@
}

function Start-MediumIntegrityProcess {
    param(
        [string] $FilePath,
        [string[]] $ArgumentList
    )

    Add-MediumIntegrityProcessLauncher

    $explorer = Get-Process -Name explorer -ErrorAction SilentlyContinue |
        Where-Object { $_.Handle -ne [IntPtr]::Zero } |
        Select-Object -First 1
    if ($null -eq $explorer) {
        return Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru
    }

    try {
        $processId = [PrintSinkE2E.MediumIntegrityProcessLauncher]::Start(
            $explorer.Handle,
            $FilePath,
            $ArgumentList,
            (Get-Location).Path)
        return Get-Process -Id $processId -ErrorAction Stop
    }
    catch {
        Write-Verbose "Falling back to normal process launch after medium-integrity launch failed: $($_.Exception.Message)"
        return Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru
    }
}

function Invoke-PrintSinkCliCommand {
    param(
        [string[]] $Arguments,
        [string] $Description
    )

    $projectPath = Join-Path $PSScriptRoot '..\..\src\PrintSink.Cli\PrintSink.Cli.csproj'
    $output = & dotnet run --project $projectPath --configuration Debug -- @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE. $($output -join [Environment]::NewLine)"
    }

    return $output -join [Environment]::NewLine
}

function Assert-QueuesCliOutput {
    param(
        [string] $Output,
        [string[]] $ExpectedQueues,
        [bool] $ExpectedInstalled
    )

    $expectedStatus = if ($ExpectedInstalled) {
        'yes'
    }
    else {
        'no'
    }

    $lines = @($Output -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    foreach ($queue in $ExpectedQueues) {
        $row = $lines |
            Where-Object { $_.StartsWith($queue, [System.StringComparison]::Ordinal) } |
            Select-Object -First 1
        if ($null -eq $row) {
            throw "CLI queues output did not contain '$queue'. Output:$([Environment]::NewLine)$Output"
        }

        $cells = @($row -split '\s{2,}' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($cells.Count -lt 5) {
            throw "CLI queues row for '$queue' was not parseable: $row"
        }

        $actualStatus = $cells[$cells.Count - 1]
        if ($actualStatus -ne $expectedStatus) {
            throw "CLI queues row for '$queue' reported Installed '$actualStatus'; expected '$expectedStatus'."
        }
    }
}

function Wait-ForQueueInstalledState {
    param(
        [string[]] $ExpectedQueues,
        [bool] $ExpectedInstalled,
        [int] $TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $installedNames = @(Get-PrintSinkUsablePrinterNames)
        $matchesExpectedState = $true
        foreach ($queue in $ExpectedQueues) {
            if (($installedNames -contains $queue) -ne $ExpectedInstalled) {
                $matchesExpectedState = $false
                break
            }
        }

        if ($matchesExpectedState) {
            return
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    $expectedStatus = if ($ExpectedInstalled) {
        'installed'
    }
    else {
        'removed'
    }

    throw "Timed out waiting for PrintSink queues to be $expectedStatus."
}

function Test-PrintSinkPrinterIsUsablyInstalled {
    param(
        [object] $Printer
    )

    if ($null -eq $Printer) {
        return $false
    }

    return -not ([string]$Printer.PrinterStatus).Contains('PendingDeletion')
}

function Get-PrintSinkUsablePrinterNames {
    return @(
        Get-Printer |
            Where-Object { Test-PrintSinkPrinterIsUsablyInstalled -Printer $_ } |
            ForEach-Object Name
    )
}

function Clear-PrintSinkQueueJobs {
    param(
        [string[]] $ExpectedQueues
    )

    foreach ($queue in $ExpectedQueues) {
        Get-PrintJob -PrinterName $queue -ErrorAction SilentlyContinue |
            ForEach-Object {
                Remove-PrintJob -PrinterName $queue -ID $_.ID -ErrorAction SilentlyContinue
            }
    }
}

function Get-PrintSinkQueueSnapshot {
    param(
        [string[]] $ExpectedQueues
    )

    $installedNames = @(Get-PrintSinkUsablePrinterNames)
    return @($ExpectedQueues | ForEach-Object {
        [ordered]@{
            name = $_
            installed = $installedNames -contains $_
        }
    })
}

function Assert-PrintSinkQueuesInstalled {
    param(
        [string[]] $ExpectedQueues,
        [string] $Context
    )

    $snapshot = @(Get-PrintSinkQueueSnapshot -ExpectedQueues $ExpectedQueues)
    $missingQueues = @($snapshot | Where-Object { -not $_.installed } | ForEach-Object { $_.name })
    if ($missingQueues.Count -gt 0) {
        throw "Missing PrintSink queues ${Context}: $($missingQueues -join ', ')"
    }

    return $snapshot
}

function Invoke-PrintSinkCliQueueLifecycle {
    param(
        [string[]] $ExpectedQueues
    )

    Clear-PrintSinkQueueJobs -ExpectedQueues $ExpectedQueues

    $installOutput = Invoke-PrintSinkCliCommand `
        -Arguments @('queues', 'install') `
        -Description 'CLI queue installation'
    Assert-QueuesCliOutput `
        -Output $installOutput `
        -ExpectedQueues $ExpectedQueues `
        -ExpectedInstalled $true
    Wait-ForQueueInstalledState `
        -ExpectedQueues $ExpectedQueues `
        -ExpectedInstalled $true `
        -TimeoutSeconds 30

    $listInstalledOutput = Invoke-PrintSinkCliCommand `
        -Arguments @('queues') `
        -Description 'CLI installed queue listing'
    Assert-QueuesCliOutput `
        -Output $listInstalledOutput `
        -ExpectedQueues $ExpectedQueues `
        -ExpectedInstalled $true

    $removeOutput = Invoke-PrintSinkCliCommand `
        -Arguments @('queues', 'remove') `
        -Description 'CLI queue removal'
    Assert-QueuesCliOutput `
        -Output $removeOutput `
        -ExpectedQueues $ExpectedQueues `
        -ExpectedInstalled $false
    Wait-ForQueueInstalledState `
        -ExpectedQueues $ExpectedQueues `
        -ExpectedInstalled $false `
        -TimeoutSeconds 30

    return [ordered]@{
        install = $installOutput
        listInstalled = $listInstalledOutput
        remove = $removeOutput
    }
}

function Wait-ForAutomationElement {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [System.Windows.Automation.TreeScope] $Scope,
        [System.Windows.Automation.Condition] $Condition,
        [int] $TimeoutSeconds,
        [string] $Description
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindFirst($Scope, $Condition)
        if ($null -ne $element) {
            return $element
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Description."
}

function Find-EnabledDescendant {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [System.Windows.Automation.Condition] $Condition,
        [int] $TimeoutSeconds,
        [string] $Description
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $elements = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $Condition)
        foreach ($element in $elements) {
            if ($element.Current.IsEnabled) {
                return $element
            }
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Description."
}

function Find-EnabledDescendantByFilter {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [scriptblock] $Predicate,
        [int] $TimeoutSeconds,
        [string] $Description
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $elements = $Root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($element in $elements) {
            if ($element.Current.IsEnabled -and (& $Predicate $element)) {
                return $element
            }
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Description."
}

function Find-DescendantByFilter {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [scriptblock] $Predicate,
        [int] $TimeoutSeconds,
        [string] $Description
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $elements = $Root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($element in $elements) {
            if (& $Predicate $element) {
                return $element
            }
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Description."
}

function Add-DialogNativeMethods {
    if ('PrintSinkE2E.DialogNativeMethods' -as [type]) {
        return
    }

    Add-Type -Namespace PrintSinkE2E -Name DialogNativeMethods -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
public static extern bool SetWindowText(System.IntPtr hWnd, string lpString);

[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern System.IntPtr SendMessage(System.IntPtr hWnd, uint msg, System.IntPtr wParam, System.IntPtr lParam);

[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern System.IntPtr SendMessage(System.IntPtr hWnd, uint msg, System.IntPtr wParam, string lParam);

[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int GetWindowText(System.IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

[System.Runtime.InteropServices.DllImport("user32.dll")]
[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
public static extern bool IsWindow(System.IntPtr hWnd);
'@
}

function Get-DefaultWindowsPrinterName {
    $printer = Get-CimInstance Win32_Printer |
        Where-Object Default |
        Select-Object -First 1

    if ($null -eq $printer) {
        return ''
    }

    return [string]$printer.Name
}

function Set-DefaultWindowsPrinter {
    param(
        [string] $PrinterName
    )

    if ([string]::IsNullOrWhiteSpace($PrinterName)) {
        throw 'Printer name is required.'
    }

    $network = New-Object -ComObject WScript.Network
    $network.SetDefaultPrinter($PrinterName)

    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        if ((Get-DefaultWindowsPrinterName) -eq $PrinterName) {
            return
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out setting the default printer to '$PrinterName'."
}

function Stop-NotepadProcessesStartedAfter {
    param(
        [DateTimeOffset] $StartedUtc
    )

    Get-Process -Name Notepad -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                $_.StartTime.ToUniversalTime() -ge $StartedUtc.UtcDateTime.AddSeconds(-2)
            }
            catch {
                $false
            }
        } |
        ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
}

function Set-DialogEditText {
    param(
        [System.Windows.Automation.AutomationElement] $Dialog,
        [System.Windows.Automation.AutomationElement] $Element,
        [string] $Text
    )

    Add-DialogNativeMethods
    $windowHandle = [IntPtr]$Element.Current.NativeWindowHandle

    [object] $valuePattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$valuePattern)) {
        $valuePattern.SetValue($Text)
        return
    }

    $legacyAccessiblePatternType = 'System.Windows.Automation.LegacyIAccessiblePattern' -as [type]
    if ($null -ne $legacyAccessiblePatternType) {
        [object] $legacyPattern = $null
        $legacyPatternIdentifier = $legacyAccessiblePatternType.GetField('Pattern').GetValue($null)
        if ($Element.TryGetCurrentPattern($legacyPatternIdentifier, [ref]$legacyPattern)) {
            $legacyPattern.SetValue($Text)
            return
        }
    }

    if ($windowHandle -eq [IntPtr]::Zero) {
        throw 'The dialog edit control does not expose ValuePattern or a native window handle.'
    }

    if (-not [PrintSinkE2E.DialogNativeMethods]::SetWindowText($windowHandle, $Text)) {
        throw "SetWindowText failed for the dialog edit control. Win32 error: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }

    [PrintSinkE2E.DialogNativeMethods]::SendMessage($windowHandle, 0x000C, [IntPtr]::Zero, $Text) | Out-Null
    $actualText = [System.Text.StringBuilder]::new([Math]::Max(1024, $Text.Length + 1))
    [PrintSinkE2E.DialogNativeMethods]::GetWindowText($windowHandle, $actualText, $actualText.Capacity) | Out-Null
    if ($actualText.ToString() -ne $Text) {
        throw "The dialog edit control did not accept the output path. Current value: '$($actualText.ToString())'"
    }
}

function Invoke-DialogButton {
    param(
        [System.Windows.Automation.AutomationElement] $Dialog,
        [System.Windows.Automation.AutomationElement] $Element
    )

    Add-DialogNativeMethods
    $dialogHandle = [IntPtr]$Dialog.Current.NativeWindowHandle
    $buttonHandle = [IntPtr]$Element.Current.NativeWindowHandle

    if ($dialogHandle -ne [IntPtr]::Zero -and $buttonHandle -ne [IntPtr]::Zero) {
        [PrintSinkE2E.DialogNativeMethods]::SendMessage($dialogHandle, 0x0111, [IntPtr]1, $buttonHandle) | Out-Null
        Start-Sleep -Milliseconds 250
        [PrintSinkE2E.DialogNativeMethods]::SendMessage($dialogHandle, 0x0111, [IntPtr]1, [IntPtr]::Zero) | Out-Null
        return
    }

    [object] $invokePattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        $invokePattern.Invoke()
    }
    elseif ($buttonHandle -ne [IntPtr]::Zero) {
        $legacyAccessiblePatternType = 'System.Windows.Automation.LegacyIAccessiblePattern' -as [type]
        if ($null -ne $legacyAccessiblePatternType) {
            $legacyPatternIdentifier = $legacyAccessiblePatternType.GetField('Pattern').GetValue($null)
        }

        if ($null -ne $legacyAccessiblePatternType -and $Element.TryGetCurrentPattern($legacyPatternIdentifier, [ref]$invokePattern)) {
            $invokePattern.DoDefaultAction()
        }

        [PrintSinkE2E.DialogNativeMethods]::SendMessage($buttonHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    }
    else {
        throw 'The dialog button does not expose InvokePattern or a native window handle.'
    }
}

function Wait-ForDialogClosed {
    param(
        [System.Windows.Automation.AutomationElement] $Dialog,
        [int] $TimeoutSeconds
    )

    Add-DialogNativeMethods
    $dialogHandle = [IntPtr]$Dialog.Current.NativeWindowHandle
    if ($dialogHandle -eq [IntPtr]::Zero) {
        return
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (-not [PrintSinkE2E.DialogNativeMethods]::IsWindow($dialogHandle)) {
            return
        }

        $currentDialog = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As'))
        if ($null -eq $currentDialog) {
            return
        }

        if ([IntPtr]$currentDialog.Current.NativeWindowHandle -ne $dialogHandle) {
            return
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw 'The Save Print Output As dialog did not close after accepting the file path.'
}

function Set-FileDialogPath {
    param(
        [System.Windows.Automation.AutomationElement] $Dialog,
        [string] $OutputPath
    )

    try {
        $fileNameEdit = Find-EnabledDescendantByFilter `
            -Root $Dialog `
            -Predicate {
                param($element)

                ($element.Current.AutomationId -eq '1001' -and $element.Current.ClassName -eq 'Edit') `
                    -or ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit `
                        -and $element.Current.Name -eq 'File name:')
            } `
            -TimeoutSeconds 15 `
            -Description 'the Save As file name field'

        Set-DialogEditText -Dialog $Dialog -Element $fileNameEdit -Text $OutputPath

        $saveButton = Find-EnabledDescendantByFilter `
            -Root $Dialog `
            -Predicate {
                param($element)

                ($element.Current.AutomationId -eq '1' -and $element.Current.ClassName -eq 'Button') `
                    -or ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button `
                        -and $element.Current.Name -eq 'Save')
            } `
            -TimeoutSeconds 15 `
            -Description 'the Save button'
        Invoke-DialogButton -Dialog $Dialog -Element $saveButton
        Wait-ForDialogClosed -Dialog $Dialog -TimeoutSeconds 5
        return
    }
    catch [System.Exception] {
        $primaryError = $_.Exception.Message
        $snapshot = Format-AutomationSnapshot -Root $Dialog
        throw "Unable to set Save Print Output As path. $primaryError`n$snapshot"
    }
}

function Format-AutomationSnapshot {
    param(
        [System.Windows.Automation.AutomationElement] $Root
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("Window '$($Root.Current.Name)' class '$($Root.Current.ClassName)' type '$($Root.Current.ControlType.ProgrammaticName)'")
    $elements = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements | Select-Object -First 160) {
        $lines.Add("  Name='$($element.Current.Name)' AutomationId='$($element.Current.AutomationId)' Class='$($element.Current.ClassName)' Type='$($element.Current.ControlType.ProgrammaticName)' Enabled=$($element.Current.IsEnabled)")
    }

    return $lines -join [Environment]::NewLine
}

function New-PrintSinkSourcePdf {
    param(
        [string] $Path,
        [string] $Text
    )

    $encoding = [System.Text.Encoding]::ASCII
    $escapedText = $Text.Replace('\', '\\').Replace('(', '\(').Replace(')', '\)')
    $contentStream = "BT /F1 24 Tf 96 696 Td ($escapedText) Tj ET`n"
    $objects = @(
        '<< /Type /Catalog /Pages 2 0 R >>',
        '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
        '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
        '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
        "<< /Length $($encoding.GetByteCount($contentStream)) >>`nstream`n$contentStream`nendstream"
    )
    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.Append("%PDF-1.4`n")
    $offsets = [System.Collections.Generic.List[int]]::new()

    for ($index = 0; $index -lt $objects.Count; $index++) {
        $offsets.Add($encoding.GetByteCount($builder.ToString()))
        [void] $builder.Append("$($index + 1) 0 obj`n$($objects[$index])`nendobj`n")
    }

    $xrefOffset = $encoding.GetByteCount($builder.ToString())
    [void] $builder.Append("xref`n0 $($objects.Count + 1)`n")
    [void] $builder.Append("0000000000 65535 f `n")
    foreach ($offset in $offsets) {
        [void] $builder.Append($offset.ToString('0000000000') + " 00000 n `n")
    }

    [void] $builder.Append("trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`n")
    [void] $builder.Append("startxref`n$xrefOffset`n%%EOF`n")

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [System.IO.File]::WriteAllBytes($Path, $encoding.GetBytes($builder.ToString()))
}

function Assert-FileBytesEqual {
    param(
        [string] $ExpectedPath,
        [string] $ActualPath
    )

    $expected = [System.IO.File]::ReadAllBytes($ExpectedPath)
    $actual = [System.IO.File]::ReadAllBytes($ActualPath)
    if ($actual.Length -ne $expected.Length) {
        throw "Output bytes differ. Expected $($expected.Length) byte(s) from '$ExpectedPath'; actual $($actual.Length) byte(s) from '$ActualPath'."
    }

    for ($index = 0; $index -lt $expected.Length; $index++) {
        if ($expected[$index] -ne $actual[$index]) {
            throw "Output bytes differ at offset $index."
        }
    }
}

function Start-PrintSinkWin32PrintProcess {
    param(
        [string] $PrinterName,
        [string] $DocumentName,
        [string] $Text,
        [int] $PageCount = 1
    )

    $id = [Guid]::NewGuid()
    $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.Print.$id.ps1"
    $stdoutPath = Join-Path $env:TEMP "PrintSink.E2E.Print.$id.out.log"
    $stderrPath = Join-Path $env:TEMP "PrintSink.E2E.Print.$id.err.log"
    $escapedPrinterName = $PrinterName.Replace("'", "''")
    $escapedDocumentName = $DocumentName.Replace("'", "''")
    $escapedText = $Text.Replace("'", "''")
    $printScript = @"
`$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
`$document = [System.Drawing.Printing.PrintDocument]::new()
`$document.DocumentName = '$escapedDocumentName'
`$document.PrinterSettings.PrinterName = '$escapedPrinterName'
`$document.PrintController = [System.Drawing.Printing.StandardPrintController]::new()
if (-not `$document.PrinterSettings.IsValid) {
    throw "Printer '$escapedPrinterName' is not valid."
}
`$pageIndex = 0
`$pageCount = $PageCount
`$document.add_PrintPage({
    param(`$sender, `$eventArgs)
    `$font = [System.Drawing.Font]::new('Consolas', 16)
    try {
        `$pageNumber = `$script:pageIndex + 1
        `$eventArgs.Graphics.DrawString('$escapedText page ' + `$pageNumber, `$font, [System.Drawing.Brushes]::Black, 96, 96)
        `$script:pageIndex++
        `$eventArgs.HasMorePages = `$script:pageIndex -lt `$pageCount
    }
    finally {
        `$font.Dispose()
    }
})
try {
    `$document.Print()
}
finally {
    `$document.Dispose()
}
"@

    Set-Content -LiteralPath $scriptPath -Value $printScript -Encoding UTF8
    $process = Start-PrintSinkPowerShellProcess `
        -ScriptPath $scriptPath `
        -StdOutPath $stdoutPath `
        -StdErrPath $stderrPath

    return [ordered]@{
        process = $process
        scriptPath = $scriptPath
        stdoutPath = $stdoutPath
        stderrPath = $stderrPath
    }
}

function Start-PrintSinkPowerShellProcess {
    param(
        [string] $ScriptPath,
        [string] $StdOutPath,
        [string] $StdErrPath
    )

    $command = "& '$(ConvertTo-PowerShellSingleQuotedLiteral -Value $ScriptPath)'"
    if (-not [string]::IsNullOrWhiteSpace($StdOutPath)) {
        $command = "$command 1> '$(ConvertTo-PowerShellSingleQuotedLiteral -Value $StdOutPath)'"
    }

    if (-not [string]::IsNullOrWhiteSpace($StdErrPath)) {
        $command = "$command 2> '$(ConvertTo-PowerShellSingleQuotedLiteral -Value $StdErrPath)'"
    }

    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command powershell.exe).Source
    $startInfo.Arguments = "-Sta -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encodedCommand"
    $startInfo.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Failed to start PowerShell print process for '$ScriptPath'."
    }

    return $process
}

function ConvertTo-PowerShellSingleQuotedLiteral {
    param(
        [string] $Value
    )

    return $Value.Replace("'", "''")
}

function Get-PrintSinkProcessOutput {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintProcess
    )

    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @(
        [ordered]@{ name = 'stdout'; path = $PrintProcess.stdoutPath },
        [ordered]@{ name = 'stderr'; path = $PrintProcess.stderrPath })) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.path) -or -not (Test-Path -LiteralPath $entry.path)) {
            continue
        }

        $content = Get-Content -LiteralPath $entry.path -Raw
        if (-not [string]::IsNullOrWhiteSpace($content)) {
            $parts.Add("$($entry.name): $content")
        }
    }

    if ($parts.Count -eq 0) {
        return 'No print-process stdout/stderr was written.'
    }

    return $parts -join [Environment]::NewLine
}

function Wait-ForPrintSinkProcessSucceeded {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintProcess,
        [int] $TimeoutMilliseconds,
        [string] $Description
    )

    $process = $PrintProcess.process
    if (-not $process.WaitForExit($TimeoutMilliseconds)) {
        throw "$Description did not exit. $(Get-PrintSinkProcessOutput -PrintProcess $PrintProcess)"
    }

    $process.Refresh()
    $exitCode = $process.ExitCode
    if ($null -eq $exitCode) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        $exitCode = $process.ExitCode
    }

    if ($null -eq $exitCode) {
        throw "$Description exited but did not report an exit code. $(Get-PrintSinkProcessOutput -PrintProcess $PrintProcess)"
    }

    if ($exitCode -ne 0) {
        throw "$Description exited with $exitCode. $(Get-PrintSinkProcessOutput -PrintProcess $PrintProcess)"
    }
}

function Test-CurrentProcessIsElevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-WindowsSdkToolPath {
    param(
        [string] $ToolName
    )

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        throw "Windows SDK bin directory was not found: $kitsRoot"
    }

    $tool = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\[^\\]+$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $tool) {
        throw "$ToolName was not found under $kitsRoot."
    }

    return $tool.FullName
}

function Start-PrintSinkIppPrinterServer {
    param(
        [string] $PrinterName,
        [string] $HostName,
        [string] $OutputDirectory
    )

    $projectPath = Join-Path $PSScriptRoot '..\PrintSink.E2E.IppPrinter\PrintSink.E2E.IppPrinter.csproj'
    $assemblyPath = Join-Path $PSScriptRoot '..\PrintSink.E2E.IppPrinter\bin\Debug\net10.0\PrintSink.E2E.IppPrinter.dll'
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        $buildOutput = & dotnet build $projectPath --configuration Debug 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Building the E2E IPP printer failed. $($buildOutput -join [Environment]::NewLine)"
        }
    }

    $readyFile = Join-Path $OutputDirectory 'ipp-printer.ready'
    $argumentsFile = Join-Path $OutputDirectory 'ipp-printer.arguments.txt'
    $stdoutFile = Join-Path $OutputDirectory 'ipp-printer.stdout.log'
    $stderrFile = Join-Path $OutputDirectory 'ipp-printer.stderr.log'
    Remove-Item -LiteralPath $readyFile -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $argumentsFile,$stdoutFile,$stderrFile -ErrorAction SilentlyContinue

    $processStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processStartInfo.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $processArguments = @(
        $assemblyPath,
        '--printer-name',
        $PrinterName,
        '--port',
        '631',
        '--host',
        $HostName,
        '--output',
        $OutputDirectory,
        '--ready-file',
        $readyFile)
    $processStartInfo.Arguments = Join-PrintSinkProcessArguments -Arguments $processArguments
    Set-Content -LiteralPath $argumentsFile -Value $processStartInfo.Arguments -Encoding UTF8

    $processStartInfo.WorkingDirectory = (Get-Location).Path
    $processStartInfo.UseShellExecute = $false
    $processStartInfo.CreateNoWindow = $true
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($processStartInfo)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        if (Test-Path -LiteralPath $readyFile) {
            return $process
        }

        if ($process.HasExited) {
            Save-PrintSinkIppPrinterOutput -Process $process -StdoutFile $stdoutFile -StderrFile $stderrFile
            throw "The E2E IPP printer exited with $($process.ExitCode) before it became ready. $(Get-PrintSinkIppPrinterOutputSummary -StdoutFile $stdoutFile -StderrFile $stderrFile)"
        }

        Start-Sleep -Milliseconds 200
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    if (-not $process.HasExited) {
        Stop-PrintSinkProcess -Process $process
    }
    Save-PrintSinkIppPrinterOutput -Process $process -StdoutFile $stdoutFile -StderrFile $stderrFile

    throw "Timed out waiting for the E2E IPP printer to start. $(Get-PrintSinkIppPrinterOutputSummary -StdoutFile $stdoutFile -StderrFile $stderrFile)"
}

function Save-PrintSinkIppPrinterOutput {
    param(
        [System.Diagnostics.Process] $Process,
        [string] $StdoutFile,
        [string] $StderrFile
    )

    Set-Content -LiteralPath $StdoutFile -Value $Process.StandardOutput.ReadToEnd() -Encoding UTF8
    Set-Content -LiteralPath $StderrFile -Value $Process.StandardError.ReadToEnd() -Encoding UTF8
}

function Get-PrintSinkIppPrinterOutputSummary {
    param(
        [string] $StdoutFile,
        [string] $StderrFile
    )

    $stdout = if (Test-Path -LiteralPath $StdoutFile) {
        Get-Content -LiteralPath $StdoutFile -Raw
    }
    else {
        '<missing stdout>'
    }
    $stderr = if (Test-Path -LiteralPath $StderrFile) {
        Get-Content -LiteralPath $StderrFile -Raw
    }
    else {
        '<missing stderr>'
    }

    return "stdout=$stdout stderr=$stderr"
}

function Join-PrintSinkProcessArguments {
    param(
        [string[]] $Arguments
    )

    return ($Arguments | ForEach-Object { ConvertTo-PrintSinkProcessArgument $_ }) -join ' '
}

function ConvertTo-PrintSinkProcessArgument {
    param(
        [string] $Argument
    )

    if ($null -eq $Argument) {
        return '""'
    }

    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.Append('"')
    $backslashCount = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') {
            $backslashCount++
            continue
        }

        if ($character -eq '"') {
            [void] $builder.Append('\', ($backslashCount * 2) + 1)
            [void] $builder.Append('"')
            $backslashCount = 0
            continue
        }

        if ($backslashCount -gt 0) {
            [void] $builder.Append('\', $backslashCount)
            $backslashCount = 0
        }

        [void] $builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void] $builder.Append('\', $backslashCount * 2)
    }

    [void] $builder.Append('"')
    return $builder.ToString()
}

function Get-PrintSinkIppHost {
    $interfaces = @{}
    foreach ($interface in Get-NetIPInterface -AddressFamily IPv4 -ErrorAction SilentlyContinue) {
        $interfaces[$interface.InterfaceIndex] = $interface
    }

    $address = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike '127.*' `
                -and $_.AddressState -eq 'Preferred' `
                -and $_.IPAddress -notlike '169.254.*'
        } |
        Sort-Object `
            @{ Expression = { if ($_.PrefixOrigin -eq 'Dhcp') { 0 } else { 1 } } }, `
            @{ Expression = {
                if ($interfaces.ContainsKey($_.InterfaceIndex)) {
                    $interfaces[$_.InterfaceIndex].InterfaceMetric
                }
                else {
                    [int]::MaxValue
                }
            } }, `
            InterfaceIndex |
        Select-Object -First 1

    if ($null -eq $address) {
        return '127.0.0.1'
    }

    return [string]$address.IPAddress
}

function New-PrintSinkPsaExtensionInf {
    param(
        [string] $PackageFamilyName,
        [string] $Aumid,
        [string] $HardwareId,
        [string] $OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $infPath = Join-Path $OutputDirectory 'psa.inf'
    $catPath = Join-Path $OutputDirectory 'psa.cat'
    $inf = @"
[Version]
Signature = "`$WINDOWS NT`$"
Class = Extension
ClassGuid = {e2f84ce7-8efa-411c-aa69-97454ca4cb57}
Provider = %ManufacturerName%
ExtensionId = {6E660901-13C9-4436-A4E4-00000000E2E1}
CatalogFile = psa.cat
DriverVer = 06/13/2026,1.0.0.0
PnpLockdown = 1

[Manufacturer]
%ManufacturerName% = PrintSink, NTamd64.6.3

[PrintSink.NTamd64.6.3]
%Device.ExtensionDesc% = PSA-Install, %PrinterHardwareId%

[PSA-Install.NT]
AddProperty = Add-PSA-Property

[PSA-Install.NT.Software]
AddSoftware = %SoftwareName%,, Microsoft-PSA-SoftwareInstall

[Microsoft-PSA-SoftwareInstall]
SoftwareType = %MicrosoftStoreType%
SoftwareID = pfn://%PackageFamilyName%

[Add-PSA-Property]
{A925764B-88E0-426D-AFC5-B39768BE59EB}, 1, 0x12,, %AUMID%

[Strings]
ManufacturerName = "PrintSink"
SoftwareName = "PrintSink"
Device.ExtensionDesc = "PrintSink E2E PSA Extension"
MicrosoftStoreType = 2
PackageFamilyName = "$PackageFamilyName"
AUMID = "$Aumid"
PrinterHardwareId = "$HardwareId"
"@

    Set-Content -LiteralPath $infPath -Value $inf -Encoding ASCII
    $catalogScriptPath = Join-Path $OutputDirectory 'new-catalog.ps1'
    $catalogScript = @'
param(
    [string] $InfPath,
    [string] $CatalogPath
)

$ErrorActionPreference = 'Stop'
New-FileCatalog -Path $InfPath -CatalogFilePath $CatalogPath -CatalogVersion 2 | Out-Null
'@
    Set-Content -LiteralPath $catalogScriptPath -Value $catalogScript -Encoding UTF8
    $catalogHost = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    if ($null -eq $catalogHost) {
        $catalogHost = Get-Command powershell.exe -ErrorAction Stop
    }

    $catalogOutput = & $catalogHost.Source `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $catalogScriptPath `
        $infPath `
        $catPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Generating PSA extension catalog failed with exit code $LASTEXITCODE. $($catalogOutput -join [Environment]::NewLine)"
    }

    return [ordered]@{
        infPath = $infPath
        catPath = $catPath
    }
}

function Install-PrintSinkPsaExtensionInf {
    param(
        [string] $InfPath,
        [string] $CatalogPath,
        [string] $CertificateSubject
    )

    $certificatePath = Join-Path (Split-Path -Parent $InfPath) 'psa-signing.cer'
    $certificateThumbprint = New-PrintSinkPsaSigningCertificate `
        -CertificateSubject $CertificateSubject `
        -CertificatePath $certificatePath
    Add-PrintSinkCertificateToStore `
        -CertificatePath $certificatePath `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::Root)
    Add-PrintSinkCertificateToStore `
        -CertificatePath $certificatePath `
        -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher)

    $signToolPath = Get-WindowsSdkToolPath -ToolName 'signtool.exe'
    & $signToolPath sign /fd SHA256 /sha1 $certificateThumbprint /sm $CatalogPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Signing PSA extension catalog failed with exit code $LASTEXITCODE."
    }

    $pnputilOutput = & pnputil.exe /add-driver $InfPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Installing PSA extension INF failed with exit code $LASTEXITCODE. $($pnputilOutput -join [Environment]::NewLine)"
    }

    $publishedName = ($pnputilOutput |
        Select-String -Pattern 'Published Name:\s*(\S+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($publishedName)) {
        throw "Installing PSA extension INF did not report a published driver name. $($pnputilOutput -join [Environment]::NewLine)"
    }

    return [ordered]@{
        publishedName = $publishedName
        certificateThumbprint = $certificateThumbprint
    }
}

function Remove-PrintSinkPsaExtensionInf {
    param(
        [string] $PublishedName,
        [string] $CertificateThumbprint
    )

    if (-not [string]::IsNullOrWhiteSpace($PublishedName)) {
        & pnputil.exe /delete-driver $PublishedName /uninstall /force | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        Remove-PrintSinkCertificateFromStore `
            -CertificateThumbprint $CertificateThumbprint `
            -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::My)
        Remove-PrintSinkCertificateFromStore `
            -CertificateThumbprint $CertificateThumbprint `
            -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::Root)
        Remove-PrintSinkCertificateFromStore `
            -CertificateThumbprint $CertificateThumbprint `
            -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher)
    }
}

function New-PrintSinkPsaSigningCertificate {
    param(
        [string] $CertificateSubject,
        [string] $CertificatePath
    )

    $scriptPath = [System.IO.Path]::ChangeExtension([System.IO.Path]::GetTempFileName(), '.ps1')
    $certificateScript = @'
param(
    [string] $Subject,
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$certificateProvider = Get-PSProvider Certificate -ErrorAction SilentlyContinue
if ($null -eq $certificateProvider) {
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
    $certificateProvider = Get-PSProvider Certificate -ErrorAction SilentlyContinue
}

if ($null -eq $certificateProvider) {
    throw "The PowerShell Certificate provider is unavailable in the certificate helper process. PSModulePath=$env:PSModulePath"
}

if ($null -eq (Get-PSDrive Cert -ErrorAction SilentlyContinue)) {
    New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -ErrorAction Stop | Out-Null
}

Get-Command New-SelfSignedCertificate -ErrorAction Stop | Out-Null
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation Cert:\LocalMachine\My `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddDays(2)
Export-Certificate -Cert $certificate -FilePath $OutputPath | Out-Null
[Console]::Out.WriteLine($certificate.Thumbprint)
'@

    try {
        Set-Content -LiteralPath $scriptPath -Value $certificateScript -Encoding UTF8
        $certificateHost = Get-Command powershell.exe -ErrorAction Stop
        $previousModulePath = $env:PSModulePath
        $windowsPowerShellModulePath = @(
            (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)) 'WindowsPowerShell\Modules'),
            (Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules'),
            (Join-Path $env:WINDIR 'system32\WindowsPowerShell\v1.0\Modules')
        ) -join [System.IO.Path]::PathSeparator
        try {
            $env:PSModulePath = $windowsPowerShellModulePath
            $certificateOutput = & $certificateHost.Source `
                -NoProfile `
                -ExecutionPolicy Bypass `
                -File $scriptPath `
                $CertificateSubject `
                $CertificatePath 2>&1
        }
        finally {
            $env:PSModulePath = $previousModulePath
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Creating PSA signing certificate failed with exit code $LASTEXITCODE. $($certificateOutput -join [Environment]::NewLine)"
        }

        $thumbprint = $certificateOutput |
            ForEach-Object { [string] $_ } |
            Where-Object { $_ -match '^[0-9A-Fa-f]{40}$' } |
            Select-Object -Last 1
        if ([string]::IsNullOrWhiteSpace($thumbprint)) {
            throw "Creating PSA signing certificate did not return a thumbprint. $($certificateOutput -join [Environment]::NewLine)"
        }

        if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
            throw "Creating PSA signing certificate did not write $CertificatePath."
        }

        return ([string] $thumbprint).ToUpperInvariant()
    }
    finally {
        Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
    }
}

function Add-PrintSinkCertificateToStore {
    param(
        [string] $CertificatePath,
        [System.Security.Cryptography.X509Certificates.StoreName] $StoreName
    )

    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $existing = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $certificate.Thumbprint,
            $false)
        if ($existing.Count -eq 0) {
            $store.Add($certificate)
        }
    }
    finally {
        $store.Close()
        $certificate.Dispose()
    }
}

function Remove-PrintSinkCertificateFromStore {
    param(
        [string] $CertificateThumbprint,
        [System.Security.Cryptography.X509Certificates.StoreName] $StoreName
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $matches = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $CertificateThumbprint,
            $false)
        foreach ($certificate in $matches) {
            $store.Remove($certificate)
        }
    }
    finally {
        $store.Close()
    }
}

function Get-PrintSinkIppPrinterDevice {
    param(
        [string] $HardwareId
    )

    foreach ($device in Get-PnpDevice -Class Printer -ErrorAction SilentlyContinue) {
        $property = Get-PnpDeviceProperty `
            -InstanceId $device.InstanceId `
            -KeyName 'DEVPKEY_Device_HardwareIds' `
            -ErrorAction SilentlyContinue
        if (@($property.Data) -contains $HardwareId) {
            return $device
        }
    }

    return $null
}

function Invoke-PrintSinkIppWorkflowActivationPrint {
    param(
        [string] $PrinterName,
        [string] $PackageFamilyName
    )

    $startedUtc = [DateTimeOffset]::UtcNow
    $printProcess = Start-PrintSinkWin32PrintProcess `
        -PrinterName $PrinterName `
        -DocumentName 'PrintSink E2E IPP Workflow' `
        -Text 'foo workflow ipp'
    $process = $printProcess.process

    try {
        Wait-ForPrintSinkProcessSucceeded `
            -PrintProcess $printProcess `
            -TimeoutMilliseconds 45000 `
            -Description "IPP workflow print process for $PrinterName"

        $workflowStart = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint '' `
            -Message 'Workflow job starting' `
            -StartedUtc $startedUtc `
            -DetailContains @(
                'skipSystemRendering=default',
                'ippCompression=') `
            -TimeoutSeconds 60
        $workflow = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $PrinterName `
            -Message 'Workflow job passed through' `
            -StartedUtc $startedUtc `
            -DetailContains @('target=system') `
            -TimeoutSeconds 60
        return [ordered]@{
            printer = $PrinterName
            workflowStart = $workflowStart
            workflow = $workflow
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Remove-Item -LiteralPath $printProcess.scriptPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $printProcess.stdoutPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $printProcess.stderrPath -ErrorAction SilentlyContinue
    }
}

function Invoke-PrintSinkIppAssociation {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    if (-not (Test-CurrentProcessIsElevated)) {
        throw 'IPP association E2E requires an elevated shell to install the temporary signed extension INF.'
    }

    $probeId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $printerName = "PrintSink-E2E-IPP-$probeId"
    $hardwareId = 'PSA_PrintSinkE2E_IPP_Pri21CF'
    $aumid = "$PackageFamilyName!App"
    $testDirectory = Join-Path $OutputDirectory 'ipp-association'
    $infDirectory = Join-Path $testDirectory 'inf'
    $ippHost = Get-PrintSinkIppHost
    $serverProcess = $null
    $publishedName = $null
    $certificateThumbprint = $null

    Remove-Printer -Name $printerName -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $testDirectory -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $testDirectory | Out-Null

    try {
        $inf = New-PrintSinkPsaExtensionInf `
            -PackageFamilyName $PackageFamilyName `
            -Aumid $aumid `
            -HardwareId $hardwareId `
            -OutputDirectory $infDirectory
        $installedInf = Install-PrintSinkPsaExtensionInf `
            -InfPath $inf.infPath `
            -CatalogPath $inf.catPath `
            -CertificateSubject 'CN=PrintSink E2E Driver Signing'
        $publishedName = $installedInf.publishedName
        $certificateThumbprint = $installedInf.certificateThumbprint

        $serverProcess = Start-PrintSinkIppPrinterServer `
            -PrinterName $printerName `
            -HostName $ippHost `
            -OutputDirectory $testDirectory
        $startedUtc = [DateTimeOffset]::UtcNow
        try {
            Add-Printer -Name $printerName -IppURL "ipp://${ippHost}:631/ipp/printer/$printerName" -ErrorAction Stop
        }
        catch {
            $createdPrinter = Get-Printer -Name $printerName -ErrorAction SilentlyContinue
            if ($null -eq $createdPrinter) {
                throw
            }
        }

        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
        $device = $null
        do {
            $device = Get-PrintSinkIppPrinterDevice -HardwareId $hardwareId
            if ($null -ne $device) {
                break
            }

            Start-Sleep -Milliseconds 500
        }
        while ([DateTimeOffset]::UtcNow -lt $deadline)
        if ($null -eq $device) {
            throw "The IPP printer device with hardware ID '$hardwareId' was not found."
        }

        $psaProperty = Get-PnpDeviceProperty `
            -InstanceId $device.InstanceId `
            -KeyName '{A925764B-88E0-426D-AFC5-B39768BE59EB} 1' `
            -ErrorAction Stop
        if ([string]$psaProperty.Data -ne $aumid) {
            throw "PSA association property was '$($psaProperty.Data)'; expected '$aumid'."
        }

        $workflowPolicy = $null
        try {
            Set-Printer -Name $printerName -WorkflowPolicy Enabled -ErrorAction Stop
            $workflowPolicy = (Get-Printer -Name $printerName).WorkflowPolicy
        }
        catch {
            $workflowPolicy = "unsupported: $($_.Exception.Message)"
        }

        $ticketValidation = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -Message 'Print ticket validated' `
            -StartedUtc $startedUtc `
            -DetailContains @('status=Resolved')

        $evidencePath = Join-Path $testDirectory 'ipp-jobs.json'
        if (-not (Test-Path -LiteralPath $evidencePath)) {
            throw "The E2E IPP printer did not write evidence: $evidencePath"
        }

        $workflowActivationPrint = Invoke-PrintSinkIppWorkflowActivationPrint `
            -PrinterName $printerName `
            -PackageFamilyName $PackageFamilyName

        $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
        $requests = @($evidence.requests)
        if ($requests.Count -eq 0) {
            throw 'The E2E IPP printer did not receive any IPP requests.'
        }

        return [ordered]@{
            printer = $printerName
            hardwareId = $hardwareId
            ippHost = $ippHost
            aumid = $aumid
            deviceInstanceId = $device.InstanceId
            workflowPolicy = [string]$workflowPolicy
            publishedDriver = $publishedName
            certificateThumbprint = $certificateThumbprint
            ippEvidencePath = $evidencePath
            ippRequestCount = $requests.Count
            ippOperations = @($requests | ForEach-Object { $_.operation } | Select-Object -Unique)
            ippJobCount = @($evidence.jobs).Count
            ticketValidation = $ticketValidation
            workflowActivationPrint = $workflowActivationPrint
        }
    }
    finally {
        Remove-Printer -Name $printerName -ErrorAction SilentlyContinue
        if ($serverProcess -and -not $serverProcess.HasExited) {
            Stop-PrintSinkProcess -Process $serverProcess
        }

        Remove-PrintSinkPsaExtensionInf `
            -PublishedName $publishedName `
            -CertificateThumbprint $certificateThumbprint
    }
}

function Invoke-PrintSinkRealPrint {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintCase,
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $printerName = $PrintCase.queue
    $startedUtc = [DateTimeOffset]::UtcNow
    $outputPath = if ($PrintCase.requiresSaveAs) {
        $outputName = if ($PrintCase.Contains('outputName')) {
            $PrintCase.outputName
        }
        else {
            ($PrintCase.queue -replace '[^A-Za-z0-9]+', '-').Trim('-')
        }

        Join-Path $OutputDirectory "$outputName$($PrintCase.extension)"
    }
    else {
        ''
    }
    if ($PrintCase.requiresSaveAs) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
        Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue
    }

    $printProcess = Start-PrintSinkWin32PrintProcess `
        -PrinterName $printerName `
        -DocumentName 'PrintSink E2E Real Print' `
        -Text 'foo'
    $process = $printProcess.process

    try {
        if ($PrintCase.requiresSaveAs) {
            $dialog = Wait-ForAutomationElement `
                -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                -Scope ([System.Windows.Automation.TreeScope]::Children) `
                -Condition ([System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::NameProperty,
                    'Save Print Output As')) `
                -TimeoutSeconds 30 `
                -Description "the Save Print Output As dialog for $printerName"

            Set-FileDialogPath -Dialog $dialog -OutputPath $outputPath
        }

        Wait-ForPrintSinkProcessSucceeded `
            -PrintProcess $printProcess `
            -TimeoutMilliseconds 30000 `
            -Description "Print process for $printerName"

        if ($PrintCase.requiresSaveAs) {
            $diagnostic = Wait-ForPrintSinkJobCompleted `
                -PackageFamilyName $PackageFamilyName `
                -Endpoint $printerName `
                -StartedUtc $startedUtc `
                -ExpectedRouteDetail $PrintCase.expectedRoute
            $ticketValidation = Wait-ForPrintSinkDiagnostic `
                -PackageFamilyName $PackageFamilyName `
                -Endpoint $printerName `
                -Message 'Print ticket validated' `
                -StartedUtc $startedUtc `
                -DetailContains @('status=Resolved')

            Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
            Assert-DocumentOutput -PrintCase $PrintCase -OutputPath $outputPath
            $file = Get-Item -LiteralPath $outputPath

            return [ordered]@{
                queue = $printerName
                format = $PrintCase.format
                outputPath = $outputPath
                bytes = $file.Length
                diagnostic = $diagnostic
                ticketValidation = $ticketValidation
            }
        }

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $PrintCase.expectedRoute
        $ticketValidation = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -Message 'Print ticket validated' `
            -StartedUtc $startedUtc `
            -DetailContains @('status=Resolved')

        $sinkArtifact = $null
        if ($PrintCase.Contains('sinkFormat')) {
            $sinkDiagnostic = Wait-ForPrintSinkDiagnostic `
                -PackageFamilyName $PackageFamilyName `
                -Endpoint $printerName `
                -Message 'Cloud sink artifact written' `
                -StartedUtc $startedUtc `
                -DetailContains @(
                    'path=',
                    'bytes=',
                    'contentType=application/pdf')
            $sinkArtifact = Assert-CloudSinkArtifact `
                -Diagnostic $sinkDiagnostic `
                -PrintCase $PrintCase `
                -OutputDirectory $OutputDirectory
        }

        return [ordered]@{
            queue = $printerName
            format = $PrintCase.format
            outputPath = $null
            bytes = 0
            sinkArtifact = $sinkArtifact
            diagnostic = $diagnostic
            ticketValidation = $ticketValidation
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Remove-Item -LiteralPath $printProcess.scriptPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $printProcess.stdoutPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $printProcess.stderrPath -ErrorAction SilentlyContinue
    }
}

function Invoke-PrintSinkNotepadPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $printerName = 'PrintSink - PDF'
    $sourcePath = Join-Path $OutputDirectory 'PrintSink-Notepad-Source.txt'
    $outputPath = Join-Path $OutputDirectory 'PrintSink-Notepad-PDF.pdf'
    Set-Content -LiteralPath $sourcePath -Value 'foo' -Encoding UTF8
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $printCase = [ordered]@{
        queue = $printerName
        format = 'pdf'
        extension = '.pdf'
        requiresSaveAs = $true
        expectedText = 'foo'
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    }

    $notepadPath = Join-Path $env:WINDIR 'System32\notepad.exe'
    if (-not (Test-Path -LiteralPath $notepadPath -PathType Leaf)) {
        throw "Notepad was not found at $notepadPath."
    }

    $previousDefaultPrinter = Get-DefaultWindowsPrinterName
    $startedUtc = [DateTimeOffset]::MinValue
    $starterProcess = $null
    $notepadProcess = $null

    try {
        Set-DefaultWindowsPrinter -PrinterName $printerName
        $startedUtc = [DateTimeOffset]::UtcNow

        $starterProcess = Start-MediumIntegrityProcess `
            -FilePath $notepadPath `
            -ArgumentList @('/p', $sourcePath)

        $saveDialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As')) `
            -TimeoutSeconds 45 `
            -Description 'the Save Print Output As dialog for the Notepad print'

        $notepadProcess = Get-Process -Id $saveDialog.Current.ProcessId -ErrorAction SilentlyContinue
        Set-FileDialogPath -Dialog $saveDialog -OutputPath $outputPath

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute
        $ticketValidation = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -Message 'Print ticket validated' `
            -StartedUtc $startedUtc `
            -DetailContains @('status=Resolved')

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath

        if ($notepadProcess -and -not $notepadProcess.HasExited) {
            $notepadProcess.WaitForExit(30000) | Out-Null
        }

        if ($notepadProcess -and -not $notepadProcess.HasExited) {
            throw 'Notepad print process did not exit after the print job completed.'
        }

        $file = Get-Item -LiteralPath $outputPath
        return [ordered]@{
            queue = $printCase.queue
            format = $printCase.format
            sourcePath = $sourcePath
            outputPath = $outputPath
            bytes = $file.Length
            mode = 'notepad-command-line-print'
            diagnostic = $diagnostic
            ticketValidation = $ticketValidation
        }
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($previousDefaultPrinter)) {
            Set-DefaultWindowsPrinter -PrinterName $previousDefaultPrinter
        }

        if ($notepadProcess -and -not $notepadProcess.HasExited) {
            Stop-Process -Id $notepadProcess.Id -Force
        }

        if ($starterProcess -and -not $starterProcess.HasExited) {
            Stop-Process -Id $starterProcess.Id -Force
        }

        if ($startedUtc -ne [DateTimeOffset]::MinValue) {
            Stop-NotepadProcessesStartedAfter -StartedUtc $startedUtc
        }

        Close-SavePrintOutputDialogs
    }
}

function Invoke-PrintSinkConcurrentPrints {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $concurrentCases = @(
        [ordered]@{
            queue = 'PrintSink - PCLm'
            format = 'pclm'
            extension = '.pclm'
            requiresSaveAs = $true
            expectedText = ''
            expectedRoute = 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.'
            outputName = 'PrintSink-Concurrent-PCLm'
            printText = 'foo concurrent pclm'
            pageCount = 48
        },
        [ordered]@{
            queue = 'PrintSink - Cloud'
            format = 'cloud'
            sinkFormat = 'pdf'
            extension = ''
            requiresSaveAs = $false
            expectedText = 'foo concurrent cloud'
            expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
            outputName = 'PrintSink-Concurrent-Cloud'
            printText = 'foo concurrent cloud'
            pageCount = 96
        }
    )

    $jobs = [System.Collections.Generic.List[object]]::new()
    $startedUtc = [DateTimeOffset]::UtcNow

    try {
        Get-ChildItem -LiteralPath $OutputDirectory -Filter 'PrintSink-Concurrent-*' -File -ErrorAction SilentlyContinue |
            Remove-Item -Force

        foreach ($printCase in $concurrentCases) {
            $outputPath = Join-Path $OutputDirectory "$($printCase.outputName)$($printCase.extension)"
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
            Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

            $printProcess = Start-PrintSinkWin32PrintProcess `
                -PrinterName $printCase.queue `
                -DocumentName "PrintSink E2E Concurrent $($printCase.format)" `
                -Text $printCase.printText `
                -PageCount $printCase.pageCount
            $jobs.Add([ordered]@{
                printCase = $printCase
                outputPath = $outputPath
                process = $printProcess.process
                scriptPath = $printProcess.scriptPath
                stdoutPath = $printProcess.stdoutPath
                stderrPath = $printProcess.stderrPath
            })
        }

        foreach ($job in @($jobs | Where-Object { $_.printCase.requiresSaveAs })) {
            $printCase = $job.printCase
            $dialog = Wait-ForAutomationElement `
                -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                -Scope ([System.Windows.Automation.TreeScope]::Children) `
                -Condition ([System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::NameProperty,
                    'Save Print Output As')) `
                -TimeoutSeconds 30 `
                -Description "the Save Print Output As dialog for $($printCase.queue)"

            Set-FileDialogPath -Dialog $dialog -OutputPath $job.outputPath
        }

        $diagnostics = @{}
        $sinkArtifacts = @{}
        foreach ($job in $jobs) {
            $process = $job.process
            $printCase = $job.printCase

            Wait-ForPrintSinkProcessSucceeded `
                -PrintProcess $job `
                -TimeoutMilliseconds 90000 `
                -Description "Concurrent print process for $($printCase.queue)"

            $diagnostic = Wait-ForPrintSinkJobCompleted `
                -PackageFamilyName $PackageFamilyName `
                -Endpoint $printCase.queue `
                -StartedUtc $startedUtc `
                -ExpectedRouteDetail $printCase.expectedRoute

            $diagnostics[$printCase.queue] = $diagnostic

            if (-not $printCase.requiresSaveAs -and $printCase.Contains('sinkFormat')) {
                $sinkDiagnostic = Wait-ForPrintSinkDiagnostic `
                    -PackageFamilyName $PackageFamilyName `
                    -Endpoint $printCase.queue `
                    -Message 'Cloud sink artifact written' `
                    -StartedUtc $startedUtc `
                    -DetailContains @(
                        'path=',
                        'bytes=',
                        'contentType=application/pdf')
                $sinkArtifacts[$printCase.queue] = Assert-CloudSinkArtifact `
                    -Diagnostic $sinkDiagnostic `
                    -PrintCase $printCase `
                    -OutputDirectory $OutputDirectory
            }
        }

        $fileBackedJobs = @($jobs | Where-Object { $_.printCase.requiresSaveAs })
        $candidateFiles = if ($fileBackedJobs.Count -gt 0) {
            Wait-ForNonEmptyFiles `
                -Directory $OutputDirectory `
                -Filter 'PrintSink-Concurrent-*' `
                -ExpectedCount $fileBackedJobs.Count `
                -TimeoutSeconds 60
        }
        else {
            @()
        }

        $matchedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $results = @()
        foreach ($job in $jobs) {
            $printCase = $job.printCase
            if (-not $printCase.requiresSaveAs) {
                $sinkArtifact = $sinkArtifacts[$printCase.queue]
                $results += [ordered]@{
                    queue = $printCase.queue
                    format = $printCase.format
                    outputPath = $null
                    bytes = 0
                    pageCount = $printCase.pageCount
                    sinkArtifact = $sinkArtifact
                    diagnostic = $diagnostics[$printCase.queue]
                }
                continue
            }

            $file = Find-MatchingDocumentOutput `
                -PrintCase $printCase `
                -CandidateFiles $candidateFiles `
                -MatchedPaths $matchedPaths

            $matchedPaths.Add($file.FullName) | Out-Null
            $results += [ordered]@{
                queue = $printCase.queue
                format = $printCase.format
                outputPath = $file.FullName
                bytes = $file.Length
                pageCount = $printCase.pageCount
                diagnostic = $diagnostics[$printCase.queue]
            }
        }

        $first = $results[0].diagnostic
        $second = $results[1].diagnostic
        $missingStartTiming = [string]::IsNullOrWhiteSpace([string]$first.routeTimestamp) `
            -or [string]::IsNullOrWhiteSpace([string]$second.routeTimestamp)
        if ($missingStartTiming) {
            throw "Concurrent print diagnostics did not include start timing. First: $(ConvertTo-Json $first -Compress); Second: $(ConvertTo-Json $second -Compress)"
        }

        $firstRouteUtc = [DateTimeOffset]::Parse($first.routeTimestamp)
        $firstCompletedUtc = [DateTimeOffset]::Parse($first.timestamp)
        $secondRouteUtc = [DateTimeOffset]::Parse($second.routeTimestamp)
        $secondCompletedUtc = [DateTimeOffset]::Parse($second.timestamp)
        $overlapped = $firstRouteUtc -lt $secondCompletedUtc -and $secondRouteUtc -lt $firstCompletedUtc
        if (-not $overlapped) {
            throw "Concurrent print jobs did not overlap. $($concurrentCases[0].queue): $firstRouteUtc -> $firstCompletedUtc; $($concurrentCases[1].queue): $secondRouteUtc -> $secondCompletedUtc."
        }

        return [ordered]@{
            startedUtc = $startedUtc.ToString('O')
            overlapped = $true
            jobs = $results
        }
    }
    finally {
        foreach ($job in $jobs) {
            $process = $job.process
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
            }

            Remove-Item -LiteralPath $job.scriptPath -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $job.stdoutPath -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $job.stderrPath -ErrorAction SilentlyContinue
        }

        Close-SavePrintOutputDialogs
    }
}

function Invoke-PrintSinkPdfPassthroughPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $sourcePath = Join-Path $OutputDirectory 'PrintSink-Pdf-Passthrough-Source.pdf'
    $outputPath = Join-Path $OutputDirectory 'PrintSink-Pdf-Passthrough.pdf'
    New-PrintSinkSourcePdf -Path $sourcePath -Text 'foo'
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $printCase = [ordered]@{
        queue = 'PrintSink - PDF'
        format = 'pdf'
        extension = '.pdf'
        requiresSaveAs = $true
        expectedText = 'foo'
        expectedRoute = 'application/pdf -> Pdf; Copy; Endpoint supports passthrough.'
    }
    $startedUtc = [DateTimeOffset]::UtcNow
    $process = Start-Process `
        -FilePath 'printsink-app.exe' `
        -ArgumentList @('--print-pdf-passthrough', '--endpoint', 'Pdf', '--source', $sourcePath) `
        -PassThru

    try {
        $dialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As')) `
            -TimeoutSeconds 30 `
            -Description 'the Save Print Output As dialog for PDF passthrough'
        Set-FileDialogPath -Dialog $dialog -OutputPath $outputPath

        if (-not $process.WaitForExit(45000)) {
            throw 'PDF passthrough command process did not exit.'
        }

        if ($process.ExitCode -ne 0) {
            throw "PDF passthrough command process exited with $($process.ExitCode)."
        }

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath
        Assert-FileBytesEqual -ExpectedPath $sourcePath -ActualPath $outputPath

        $file = Get-Item -LiteralPath $outputPath
        return [ordered]@{
            queue = $printCase.queue
            format = $printCase.format
            sourcePath = $sourcePath
            outputPath = $outputPath
            bytes = $file.Length
            mode = 'pdl-passthrough'
            diagnostic = $diagnostic
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Get-Process | Where-Object { $_.ProcessName -like 'PrintSink*' } | Stop-Process -Force
    }
}

function Invoke-PrintSinkWinRtSourcePrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $sourceText = 'foo winrt source e2e'
    $outputPath = Join-Path $OutputDirectory 'PrintSink-WinRT-Source.pdf'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $printCase = [ordered]@{
        queue = 'PrintSink - PDF'
        format = 'pdf'
        extension = '.pdf'
        requiresSaveAs = $true
        expectedText = $sourceText
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    }
    $startedUtc = [DateTimeOffset]::UtcNow
    $alias = Get-Command printsink-app.exe -ErrorAction Stop
    $headlessLog = Join-Path $env:TEMP 'PrintSink.App.headless.log'
    Remove-Item $headlessLog -ErrorAction SilentlyContinue
    $process = Start-MediumIntegrityProcess `
        -FilePath $alias.Source `
        -ArgumentList @('--winrt-source-print', '--text', $sourceText)

    try {
        $printDialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'PrintSink WinRT E2E Source - Print')) `
            -TimeoutSeconds 45 `
            -Description 'the WinRT source Windows print dialog'

        Select-WindowsPrintPrinter `
            -PrintDialog $printDialog `
            -PrinterName $printCase.queue

        Invoke-Button `
            -Root $printDialog `
            -Name 'Print' `
            -TimeoutSeconds 30

        $saveDialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As')) `
            -TimeoutSeconds 30 `
            -Description 'the Save Print Output As dialog for the WinRT source print'
        Set-FileDialogPath -Dialog $saveDialog -OutputPath $outputPath

        if (-not $process.WaitForExit(60000)) {
            throw 'WinRT source print process did not exit.'
        }

        $exitCode = $null
        try {
            $process.Refresh()
            $exitCode = $process.ExitCode
        }
        catch [InvalidOperationException] {
            $exitCode = $null
        }

        if ($null -ne $exitCode -and $exitCode -ne 0) {
            $diagnostic = if (Test-Path $headlessLog) {
                Get-Content $headlessLog -Raw
            }
            else {
                'No headless diagnostic log was written.'
            }

            throw "WinRT source print process exited with $($process.ExitCode). $diagnostic"
        }

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath

        $file = Get-Item -LiteralPath $outputPath
        return [ordered]@{
            queue = $printCase.queue
            format = $printCase.format
            outputPath = $outputPath
            bytes = $file.Length
            mode = 'winrt-source'
            diagnostic = $diagnostic
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Get-Process -Name 'PrintDialog' -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-PrintSinkSettingsUiOwner {
    param(
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $sourceText = 'foo settings ui owner e2e'
    $startedUtc = [DateTimeOffset]::UtcNow
    $alias = Get-Command printsink-app.exe -ErrorAction Stop
    $headlessLog = Join-Path $env:TEMP 'PrintSink.App.headless.log'
    Remove-Item $headlessLog -ErrorAction SilentlyContinue
    $process = Start-MediumIntegrityProcess `
        -FilePath $alias.Source `
        -ArgumentList @('--winrt-source-print', '--text', $sourceText)

    try {
        $printDialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'PrintSink WinRT E2E Source - Print')) `
            -TimeoutSeconds 45 `
            -Description 'the WinRT source Windows print dialog for Settings UI'

        Select-WindowsPrintPrinter `
            -PrintDialog $printDialog `
            -PrinterName 'PrintSink - PDF'

        $printerSelected = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint 'PrintSink - PDF' `
            -Message 'Printer selected' `
            -StartedUtc $startedUtc `
            -DetailContains @('adaptiveCard=set', 'additionalFields=')

        $moreSettings = Find-EnabledDescendantByFilter `
            -Root $printDialog `
            -Predicate {
                param($element)

                $element.Current.Name -eq 'More settings'
            } `
            -TimeoutSeconds 30 `
            -Description 'the Windows print More settings link'

        [object] $invokePattern = $null
        if ($moreSettings.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
            $invokePattern.Invoke()
        }
        else {
            throw 'The Windows print More settings link does not expose InvokePattern.'
        }

        $settingsWindow = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Print preferences')) `
            -TimeoutSeconds 45 `
            -Description 'the PrintSink Settings UI window'

        Wait-ForAutomationElementEnabledState `
            -Element $printDialog `
            -ExpectedEnabled $false `
            -TimeoutSeconds 30 `
            -Description 'the Windows print dialog while Settings UI is open'

        $renderError = $null
        $settingsElements = $settingsWindow.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($settingsElement in $settingsElements) {
            if ($settingsElement.Current.Name.StartsWith('⚠ Render error', [System.StringComparison]::Ordinal)) {
                $renderError = $settingsElement
                break
            }
        }

        if ($null -ne $renderError) {
            throw 'Settings UI rendered a Reactor error surface.'
        }

        Find-DescendantByFilter `
            -Root $settingsWindow `
            -Predicate {
                param($element)

                $element.Current.Name -eq 'Modal to print preferences owner.'
            } `
            -TimeoutSeconds 30 `
            -Description 'the Settings UI modal owner status' | Out-Null

        Invoke-Button `
            -Root $settingsWindow `
            -Name 'Close' `
            -TimeoutSeconds 30

        Wait-ForTopLevelWindowClosed `
            -Name 'Print preferences' `
            -TimeoutSeconds 30

        Wait-ForAutomationElementEnabledState `
            -Element $printDialog `
            -ExpectedEnabled $true `
            -TimeoutSeconds 30 `
            -Description 'the Windows print dialog after Settings UI closes'

        Invoke-Button `
            -Root $printDialog `
            -Name 'Cancel' `
            -TimeoutSeconds 30

        if (-not $process.WaitForExit(60000)) {
            throw 'Settings UI owner source process did not exit.'
        }

        $exitCode = $null
        try {
            $process.Refresh()
            $exitCode = $process.ExitCode
        }
        catch [InvalidOperationException] {
            $exitCode = $null
        }

        if ($null -ne $exitCode -and $exitCode -ne 0) {
            $diagnostic = if (Test-Path $headlessLog) {
                Get-Content $headlessLog -Raw
            }
            else {
                'No headless diagnostic log was written.'
            }

            throw "Settings UI owner source process exited with $exitCode. $diagnostic"
        }

        return [ordered]@{
            queue = 'PrintSink - PDF'
            mode = 'settings-ui-owner'
            ownerDisabled = $true
            modalStatus = 'Modal to print preferences owner.'
            packageFamilyName = $PackageFamilyName
            printerSelected = $printerSelected
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Get-Process -Name 'PrintSink*', 'PrintDialog' -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-PrintSinkExtensionCapabilities {
    param(
        [string] $PackageFamilyName,
        [DateTimeOffset] $StartedUtc
    )

    Invoke-PrintSinkAppCommand `
        -Arguments @('--refresh-capabilities', '--endpoint', 'Pdf') `
        -Description 'Refreshing PDF capabilities through the packaged app'

    return Wait-ForPrintSinkDiagnostic `
        -PackageFamilyName $PackageFamilyName `
        -Endpoint 'PrintSink - PDF' `
        -Message 'Capabilities updated' `
        -StartedUtc $StartedUtc `
        -DetailContains @(
            'features=PageMediaSize,PageMediaType,JobInputBin,JobOutputBin,JobPageOrder,JobStapleAllDocuments,PageResolution,JobWatermarkMode',
            'mxdc=configured',
            'pdr=updated',
            'pdrResources=') `
        -TimeoutSeconds 120
}

function Invoke-PrintSinkUserDefaultPrintTicket {
    param(
        [string] $PackageFamilyName,
        [DateTimeOffset] $StartedUtc
    )

    Invoke-PrintSinkAppCommand `
        -Arguments @('--set-default-copies', '--endpoint', 'Pdf', '--copies', '2') `
        -Description 'Setting PDF user default print ticket copies to 2'
    $setResult = Wait-ForPrintSinkDiagnostic `
        -PackageFamilyName $PackageFamilyName `
        -Endpoint 'PrintSink - PDF' `
        -Message 'User default print ticket updated' `
        -StartedUtc $StartedUtc `
        -DetailContains @('copies=2', 'verifiedCopies=2')

    Invoke-PrintSinkAppCommand `
        -Arguments @('--set-default-copies', '--endpoint', 'Pdf', '--copies', '1') `
        -Description 'Restoring PDF user default print ticket copies to 1'
    $restoreResult = Wait-ForPrintSinkDiagnostic `
        -PackageFamilyName $PackageFamilyName `
        -Endpoint 'PrintSink - PDF' `
        -Message 'User default print ticket updated' `
        -StartedUtc $StartedUtc `
        -DetailContains @('copies=1', 'verifiedCopies=1')

    return [ordered]@{
        set = $setResult
        restore = $restoreResult
    }
}

function Invoke-PrintSinkVirtualAttributeRead {
    param(
        [string] $PackageFamilyName,
        [DateTimeOffset] $StartedUtc
    )

    Invoke-PrintSinkAppCommand `
        -Arguments @('--assert-virtual-attribute-read', '--endpoint', 'Pdf') `
        -Description 'Asserting PDF virtual-printer attribute reads through the packaged app'

    return Wait-ForPrintSinkDiagnostic `
        -PackageFamilyName $PackageFamilyName `
        -Endpoint 'PrintSink - PDF' `
        -Message 'Virtual printer attribute read matched platform behavior' `
        -StartedUtc $StartedUtc `
        -DetailContains @(
            'Virtual printer attribute read matched platform behavior',
            'document-format-default=<unsupported>',
            'document-format-supported=<unsupported>')
}

function Invoke-PrintSinkSettingsWatermarkPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    $watermarkText = 'CI DEFAULT WATERMARK'
    Invoke-PrintSinkAppCommand `
        -Arguments @(
            '--set-text-watermark',
            '--endpoint',
            'Pdf',
            '--text',
            $watermarkText,
            '--refresh-capabilities') `
        -Description 'Setting default PDF watermark and refreshing capabilities'

    try {
        $printCase = [ordered]@{
            queue = 'PrintSink - PDF'
            format = 'pdf'
            extension = '.pdf'
            requiresSaveAs = $true
            expectedText = $watermarkText
            expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
            outputName = 'PrintSink-Settings-Watermark'
        }

        return Invoke-PrintSinkRealPrint `
            -PrintCase $printCase `
            -OutputDirectory $OutputDirectory `
            -PackageFamilyName $PackageFamilyName
    }
    finally {
        Invoke-PrintSinkAppCommand `
            -Arguments @('--clear-watermark', '--endpoint', 'Pdf', '--refresh-capabilities') `
            -Description 'Clearing default PDF watermark and refreshing capabilities'
    }
}

function Invoke-PrintSinkSettingsImageWatermarkPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    $watermarkImagePath = Join-Path $OutputDirectory 'PrintSink-Watermark.png'
    [System.IO.File]::WriteAllBytes(
        $watermarkImagePath,
        [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lX8xjAAAAABJRU5ErkJggg=='))

    Invoke-PrintSinkAppCommand `
        -Arguments @(
            '--set-image-watermark',
            '--endpoint',
            'Pdf',
            '--image',
            $watermarkImagePath,
            '--refresh-capabilities') `
        -Description 'Setting default PDF image watermark and refreshing capabilities'

    try {
        $printCase = [ordered]@{
            queue = 'PrintSink - PDF'
            format = 'pdf'
            extension = '.pdf'
            requiresSaveAs = $true
            requiresImage = $true
            expectedText = 'foo'
            expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
            outputName = 'PrintSink-Settings-Image-Watermark'
        }

        return Invoke-PrintSinkRealPrint `
            -PrintCase $printCase `
            -OutputDirectory $OutputDirectory `
            -PackageFamilyName $PackageFamilyName
    }
    finally {
        Invoke-PrintSinkAppCommand `
            -Arguments @('--clear-watermark', '--endpoint', 'Pdf', '--refresh-capabilities') `
            -Description 'Clearing default PDF image watermark and refreshing capabilities'
    }
}

function Invoke-PrintSinkFailedImageWatermarkPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $corruptImagePath = Join-Path $OutputDirectory 'PrintSink-Corrupt-Watermark.png'
    Set-Content -LiteralPath $corruptImagePath -Value 'This is not a PNG image.' -Encoding UTF8

    Invoke-PrintSinkAppCommand `
        -Arguments @(
            '--set-image-watermark',
            '--endpoint',
            'Pdf',
            '--image',
            $corruptImagePath,
            '--refresh-capabilities') `
        -Description 'Setting corrupt default PDF image watermark and refreshing capabilities'

    try {
        $printerName = 'PrintSink - PDF'
        $startedUtc = [DateTimeOffset]::UtcNow
        $outputPath = Join-Path $OutputDirectory 'PrintSink-Failed-Image-Watermark.pdf'
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
        Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

        $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.FailedImageWatermark.$([Guid]::NewGuid()).ps1"
        $stdoutPath = [System.IO.Path]::ChangeExtension($scriptPath, '.out.log')
        $stderrPath = [System.IO.Path]::ChangeExtension($scriptPath, '.err.log')
        $printScript = @"
Add-Type -AssemblyName System.Drawing
`$document = [System.Drawing.Printing.PrintDocument]::new()
`$document.DocumentName = 'PrintSink E2E Failed Image Watermark'
`$document.PrinterSettings.PrinterName = 'PrintSink - PDF'
`$document.PrintController = [System.Drawing.Printing.StandardPrintController]::new()
`$document.add_PrintPage({
    param(`$sender, `$eventArgs)
    `$font = [System.Drawing.Font]::new('Consolas', 16)
    try {
        `$eventArgs.Graphics.DrawString('foo', `$font, [System.Drawing.Brushes]::Black, 96, 96)
        `$eventArgs.HasMorePages = `$false
    }
    finally {
        `$font.Dispose()
    }
})
`$document.Print()
"@

        Set-Content -LiteralPath $scriptPath -Value $printScript -Encoding UTF8
        $process = Start-PrintSinkPowerShellProcess `
            -ScriptPath $scriptPath `
            -StdOutPath $stdoutPath `
            -StdErrPath $stderrPath
        $printProcess = [ordered]@{
            process = $process
            scriptPath = $scriptPath
            stdoutPath = $stdoutPath
            stderrPath = $stderrPath
        }

        try {
            $dialog = Wait-ForAutomationElement `
                -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
                -Scope ([System.Windows.Automation.TreeScope]::Children) `
                -Condition ([System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::NameProperty,
                    'Save Print Output As')) `
                -TimeoutSeconds 30 `
                -Description 'the Save Print Output As dialog for failed image watermark'

            Set-FileDialogPath -Dialog $dialog -OutputPath $outputPath

            Wait-ForPrintSinkProcessSucceeded `
                -PrintProcess $printProcess `
                -TimeoutMilliseconds 30000 `
                -Description 'Failed image watermark print process'

            $failure = Wait-ForPrintSinkJobFailed `
                -PackageFamilyName $PackageFamilyName `
                -Endpoint $printerName `
                -StartedUtc $startedUtc `
                -ExpectedRouteDetail 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'

            $outputExists = Test-Path -LiteralPath $outputPath
            $bytes = if ($outputExists) {
                (Get-Item -LiteralPath $outputPath).Length
            }
            else {
                0
            }

            if ($bytes -gt 0) {
                throw "Failed image watermark job produced non-empty output: $outputPath ($bytes byte(s))."
            }

            return [ordered]@{
                queue = $printerName
                format = 'pdf'
                outputPath = $outputPath
                outputExists = $outputExists
                bytes = $bytes
                mode = 'failed-image-watermark'
                diagnostic = $failure
            }
        }
        finally {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
            }

            Close-SavePrintOutputDialogs
            Remove-Item -LiteralPath $scriptPath -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $stdoutPath -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $stderrPath -ErrorAction SilentlyContinue
        }
    }
    finally {
        Invoke-PrintSinkAppCommand `
            -Arguments @('--clear-watermark', '--endpoint', 'Pdf', '--refresh-capabilities') `
            -Description 'Clearing corrupt default PDF image watermark and refreshing capabilities'
    }
}

function Invoke-PrintSinkJobUiWatermarkPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $printCase = [ordered]@{
        queue = 'PrintSink - PDF'
        format = 'pdf'
        extension = '.pdf'
        requiresSaveAs = $true
        expectedText = 'CI WATERMARK'
        notExpectedText = 'ci-password'
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    }
    $printerName = $printCase.queue
    $startedUtc = [DateTimeOffset]::UtcNow
    $outputPath = Join-Path $OutputDirectory 'PrintSink-JobUI-Watermark.pdf'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.JobUI.$([Guid]::NewGuid()).ps1"
    $stdoutPath = [System.IO.Path]::ChangeExtension($scriptPath, '.out.log')
    $stderrPath = [System.IO.Path]::ChangeExtension($scriptPath, '.err.log')
    $printScript = @"
Add-Type -AssemblyName System.Drawing
`$document = [System.Drawing.Printing.PrintDocument]::new()
`$document.DocumentName = 'PrintSink E2E Job UI Watermark'
`$document.PrinterSettings.PrinterName = 'PrintSink - PDF'
`$document.PrintController = [System.Drawing.Printing.StandardPrintController]::new()
`$document.add_PrintPage({
    param(`$sender, `$eventArgs)
    `$font = [System.Drawing.Font]::new('Consolas', 16)
    try {
        `$eventArgs.Graphics.DrawString('foo', `$font, [System.Drawing.Brushes]::Black, 96, 96)
        `$eventArgs.HasMorePages = `$false
    }
    finally {
        `$font.Dispose()
    }
})
`$document.Print()
"@

    Set-Content -LiteralPath $scriptPath -Value $printScript -Encoding UTF8
    $process = Start-PrintSinkPowerShellProcess `
        -ScriptPath $scriptPath `
        -StdOutPath $stdoutPath `
        -StdErrPath $stderrPath
    $printProcess = [ordered]@{
        process = $process
        scriptPath = $scriptPath
        stdoutPath = $stdoutPath
        stderrPath = $stderrPath
    }

    try {
        $dialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As')) `
            -TimeoutSeconds 30 `
            -Description 'the Save Print Output As dialog for the Job UI watermark test'
        Set-FileDialogPath -Dialog $dialog -OutputPath $outputPath

        $jobWindow = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Job preview')) `
            -TimeoutSeconds 45 `
            -Description 'the PrintSink Job preview window'

        $jobUiPdl = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint '' `
            -Message 'Job UI PDL received' `
            -StartedUtc $startedUtc `
            -DetailContains @(
                'kind=virtual-printer',
                'jobTitle=PrintSink E2E Job UI Watermark',
                'source=powershell.exe',
                'contentType=application/oxps')

        Set-ToggleSwitch -Root $jobWindow -Name 'Text watermark' -ExpectedState $true
        Set-TextBoxValue -Root $jobWindow -Name 'Watermark text' -Value 'CI WATERMARK'
        Set-TextBoxValue -Root $jobWindow -Name 'Job password' -Value 'ci-password'
        Invoke-Button -Root $jobWindow -Name 'Continue' -TimeoutSeconds 30

        Wait-ForPrintSinkProcessSucceeded `
            -PrintProcess $printProcess `
            -TimeoutMilliseconds 30000 `
            -Description 'Job UI watermark print process'

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute
        if ([string]$diagnostic.detail -notlike '*job-password=present-not-applicable*') {
            throw "Job UI password metadata was not consumed by the virtual-printer processor. Detail: $($diagnostic.detail)"
        }
        if ([string]$diagnostic.detail -like '*ci-password*') {
            throw 'Job UI password secret leaked into diagnostics.'
        }

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath

        $file = Get-Item -LiteralPath $outputPath
        return [ordered]@{
            queue = $printerName
            format = 'pdf'
            outputPath = $outputPath
            bytes = $file.Length
            mode = 'job-ui-watermark'
            jobPassword = 'present-not-applicable'
            jobPasswordSecretExposed = $false
            jobUiPdl = $jobUiPdl
            diagnostic = $diagnostic
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Get-Process | Where-Object { $_.ProcessName -like 'PrintSink*' } | Stop-Process -Force
        Remove-Item -LiteralPath $scriptPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stdoutPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -ErrorAction SilentlyContinue
    }
}

function Invoke-PrintSinkJobUiCancelPrint {
    param(
        [string] $OutputDirectory,
        [string] $PackageFamilyName
    )

    Add-Type -AssemblyName UIAutomationClient

    $printerName = 'PrintSink - PDF'
    $startedUtc = [DateTimeOffset]::UtcNow
    $outputPath = Join-Path $OutputDirectory 'PrintSink-JobUI-Cancel.pdf'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.JobUICancel.$([Guid]::NewGuid()).ps1"
    $stdoutPath = [System.IO.Path]::ChangeExtension($scriptPath, '.out.log')
    $stderrPath = [System.IO.Path]::ChangeExtension($scriptPath, '.err.log')
    $printScript = @"
Add-Type -AssemblyName System.Drawing
`$document = [System.Drawing.Printing.PrintDocument]::new()
`$document.DocumentName = 'PrintSink E2E Job UI Cancel'
`$document.PrinterSettings.PrinterName = 'PrintSink - PDF'
`$document.PrintController = [System.Drawing.Printing.StandardPrintController]::new()
`$document.add_PrintPage({
    param(`$sender, `$eventArgs)
    `$font = [System.Drawing.Font]::new('Consolas', 16)
    try {
        `$eventArgs.Graphics.DrawString('foo', `$font, [System.Drawing.Brushes]::Black, 96, 96)
        `$eventArgs.HasMorePages = `$false
    }
    finally {
        `$font.Dispose()
    }
})
`$document.Print()
"@

    Set-Content -LiteralPath $scriptPath -Value $printScript -Encoding UTF8
    $process = Start-PrintSinkPowerShellProcess `
        -ScriptPath $scriptPath `
        -StdOutPath $stdoutPath `
        -StdErrPath $stderrPath
    $printProcess = [ordered]@{
        process = $process
        scriptPath = $scriptPath
        stdoutPath = $stdoutPath
        stderrPath = $stderrPath
    }

    try {
        $dialog = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Save Print Output As')) `
            -TimeoutSeconds 30 `
            -Description 'the Save Print Output As dialog for the Job UI cancel test'
        Set-FileDialogPath -Dialog $dialog -OutputPath $outputPath

        $jobWindow = Wait-ForAutomationElement `
            -Root ([System.Windows.Automation.AutomationElement]::RootElement) `
            -Scope ([System.Windows.Automation.TreeScope]::Children) `
            -Condition ([System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Job preview')) `
            -TimeoutSeconds 45 `
            -Description 'the PrintSink Job preview window for cancel'

        $jobUiPdl = Wait-ForPrintSinkDiagnostic `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint '' `
            -Message 'Job UI PDL received' `
            -StartedUtc $startedUtc `
            -DetailContains @(
                'kind=virtual-printer',
                'jobTitle=PrintSink E2E Job UI Cancel',
                'source=powershell.exe',
                'contentType=application/oxps')

        Invoke-Button -Root $jobWindow -Name 'Cancel' -TimeoutSeconds 30

        Wait-ForPrintSinkProcessSucceeded `
            -PrintProcess $printProcess `
            -TimeoutMilliseconds 30000 `
            -Description 'Job UI cancel print process'

        $diagnostic = Wait-ForPrintSinkJobCanceled `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -StartedUtc $startedUtc

        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(10)
        do {
            if (Test-Path -LiteralPath $outputPath) {
                $file = Get-Item -LiteralPath $outputPath
                if ($file.Length -gt 0) {
                    throw "Canceled Job UI print wrote output: $outputPath"
                }
            }

            Start-Sleep -Milliseconds 500
        }
        while ([DateTimeOffset]::UtcNow -lt $deadline)

        $outputExists = Test-Path -LiteralPath $outputPath
        $bytes = if ($outputExists) {
            (Get-Item -LiteralPath $outputPath).Length
        }
        else {
            0
        }

        return [ordered]@{
            queue = $printerName
            format = 'pdf'
            outputPath = $outputPath
            outputExists = $outputExists
            bytes = $bytes
            mode = 'job-ui-cancel'
            jobUiPdl = $jobUiPdl
            diagnostic = $diagnostic
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Get-Process | Where-Object { $_.ProcessName -like 'PrintSink*' } | Stop-Process -Force
        Remove-Item -LiteralPath $scriptPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stdoutPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -ErrorAction SilentlyContinue
    }
}

function Wait-ForNonEmptyFile {
    param(
        [string] $Path,
        [int] $TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $Path) {
            $file = Get-Item -LiteralPath $Path
            if ($file.Length -gt 0) {
                return
            }
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for non-empty output file: $Path"
}

function Wait-ForNonEmptyFiles {
    param(
        [string] $Directory,
        [string] $Filter,
        [int] $ExpectedCount,
        [int] $TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $files = @(
            Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Length -gt 0 } |
                Sort-Object -Property Name
        )
        if ($files.Count -ge $ExpectedCount) {
            return @($files)
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    $found = @(
        Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
            ForEach-Object { "$($_.Name)=$($_.Length)" }
    )
    throw "Timed out waiting for $ExpectedCount non-empty output files matching $Filter. Found: $($found -join ', ')"
}

function Test-DocumentOutput {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintCase,
        [string] $OutputPath
    )

    try {
        Assert-DocumentOutput -PrintCase $PrintCase -OutputPath $OutputPath
        return $true
    }
    catch {
        return $false
    }
}

function Find-MatchingDocumentOutput {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintCase,
        [object[]] $CandidateFiles,
        [System.Collections.Generic.HashSet[string]] $MatchedPaths
    )

    foreach ($candidateFile in $CandidateFiles) {
        if ($MatchedPaths.Contains($candidateFile.FullName)) {
            continue
        }

        if (Test-DocumentOutput -PrintCase $PrintCase -OutputPath $candidateFile.FullName) {
            return $candidateFile
        }
    }

    $candidateSummary = @($CandidateFiles | ForEach-Object { "$($_.Name)=$($_.Length)" })
    throw "Could not match a valid $($PrintCase.format) output for $($PrintCase.queue). Candidates: $($candidateSummary -join ', ')"
}

function Set-ToggleSwitch {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [string] $Name,
        [bool] $ExpectedState
    )

    $toggle = Find-EnabledDescendant `
        -Root $Root `
        -Condition ([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name)) `
        -TimeoutSeconds 30 `
        -Description "the $Name toggle"

    [object] $togglePattern = $null
    if ($toggle.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$togglePattern)) {
        $targetState = if ($ExpectedState) {
            [System.Windows.Automation.ToggleState]::On
        }
        else {
            [System.Windows.Automation.ToggleState]::Off
        }

        if ($togglePattern.Current.ToggleState -ne $targetState) {
            $togglePattern.Toggle()
        }

        return
    }

    $invokePattern = $toggle.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invokePattern.Invoke()
}

function Set-TextBoxValue {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [string] $Name,
        [string] $Value
    )

    $textBox = Find-EnabledDescendant `
        -Root $Root `
        -Condition ([System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                $Name),
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Edit))) `
        -TimeoutSeconds 30 `
        -Description "the $Name text box"

    $valuePattern = $textBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $valuePattern.SetValue($Value)
}

function Invoke-Button {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [string] $Name,
        [int] $TimeoutSeconds
    )

    $button = Find-EnabledDescendant `
        -Root $Root `
        -Condition ([System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                $Name),
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Button))) `
        -TimeoutSeconds $TimeoutSeconds `
        -Description "the $Name button"

    $invokePattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invokePattern.Invoke()
}

function Select-WindowsPrintPrinter {
    param(
        [System.Windows.Automation.AutomationElement] $PrintDialog,
        [string] $PrinterName
    )

    $printerSelector = Find-EnabledDescendant `
        -Root $PrintDialog `
        -Condition ([System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                'printerSelector'),
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::ComboBox))) `
        -TimeoutSeconds 30 `
        -Description 'the Windows print printer selector'

    [object] $expandPattern = $null
    if ($printerSelector.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expandPattern)) {
        if ($expandPattern.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded) {
            $expandPattern.Expand()
        }
    }

    $printerItem = Find-EnabledDescendantByFilter `
        -Root $PrintDialog `
        -Predicate {
            param($element)

            $element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem `
                -and $element.Current.Name -eq $PrinterName
        } `
        -TimeoutSeconds 30 `
        -Description "the $PrinterName printer item"

    [object] $selectionPattern = $null
    if ($printerItem.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectionPattern)) {
        $selectionPattern.Select()
    }
    else {
        $invokePattern = $printerItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invokePattern.Invoke()
    }

    Start-Sleep -Milliseconds 500
}

function Wait-ForAutomationElementEnabledState {
    param(
        [System.Windows.Automation.AutomationElement] $Element,
        [bool] $ExpectedEnabled,
        [int] $TimeoutSeconds,
        [string] $Description
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            if ($Element.Current.IsEnabled -eq $ExpectedEnabled) {
                return
            }
        }
        catch [System.Windows.Automation.ElementNotAvailableException] {
            throw "$Description is no longer available."
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $Description to be enabled=$ExpectedEnabled."
}

function Wait-ForTopLevelWindowClosed {
    param(
        [string] $Name,
        [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                $Name))
        if ($null -eq $window) {
            return
        }

        Start-Sleep -Milliseconds 250
    }
    while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for the $Name window to close."
}

function Assert-DocumentOutput {
    param(
        [System.Collections.Specialized.OrderedDictionary] $PrintCase,
        [string] $OutputPath
    )

    $arguments = @(
        'run',
        '--project',
        (Join-Path $PSScriptRoot '..\PrintSink.E2E.Assertions\PrintSink.E2E.Assertions.csproj'),
        '--configuration',
        'Debug',
        '--',
        '--format',
        $PrintCase.format,
        '--path',
        $OutputPath
    )

    if (-not [string]::IsNullOrWhiteSpace($PrintCase.expectedText)) {
        $arguments += @('--contains', $PrintCase.expectedText)
    }

    if ($PrintCase.Contains('notExpectedText') -and -not [string]::IsNullOrWhiteSpace($PrintCase.notExpectedText)) {
        $arguments += @('--not-contains', $PrintCase.notExpectedText)
    }

    if ($PrintCase.Contains('requiresImage') -and $PrintCase.requiresImage) {
        $arguments += @('--requires-image', 'true')
    }

    $assertionOutput = & dotnet @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Document assertion failed for $($PrintCase.queue). $($assertionOutput -join [Environment]::NewLine)"
    }
}

function Assert-CloudSinkArtifact {
    param(
        [object] $Diagnostic,
        [System.Collections.Specialized.OrderedDictionary] $PrintCase,
        [string] $OutputDirectory
    )

    $detail = [string]$Diagnostic.detail
    if ($detail -notmatch '^path=(?<path>.+);\s*bytes=(?<bytes>\d+);\s*contentType=(?<contentType>[^;]+)$') {
        throw "Cloud sink artifact diagnostic had unexpected detail: $detail"
    }

    $artifactPath = $Matches.path
    $reportedBytes = [int64]$Matches.bytes
    $contentType = $Matches.contentType
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Cloud sink artifact was not written: $artifactPath"
    }

    $artifact = Get-Item -LiteralPath $artifactPath
    if ($artifact.Length -le 0) {
        throw "Cloud sink artifact was empty: $artifactPath"
    }

    if ($artifact.Length -ne $reportedBytes) {
        throw "Cloud sink artifact byte count differed. Reported $reportedBytes; actual $($artifact.Length)."
    }

    $extension = if ($PrintCase.Contains('sinkFormat') -and $PrintCase.sinkFormat -eq 'pdf') {
        '.pdf'
    }
    else {
        [System.IO.Path]::GetExtension($artifactPath)
    }

    $copyPath = Join-Path $OutputDirectory "PrintSink-Cloud-Sink$extension"
    Copy-Item -LiteralPath $artifactPath -Destination $copyPath -Force

    $assertionCase = [ordered]@{
        queue = $PrintCase.queue
        format = $PrintCase.sinkFormat
        expectedText = $PrintCase.expectedText
    }
    Assert-DocumentOutput -PrintCase $assertionCase -OutputPath $copyPath

    return [ordered]@{
        path = $artifactPath
        artifactCopyPath = $copyPath
        bytes = $artifact.Length
        contentType = $contentType
        diagnostic = $Diagnostic
    }
}

function Test-PrintSinkDiagnosticStartedAfter {
    param(
        [object] $Event,
        [DateTimeOffset] $StartedUtc,
        [double] $SkewSeconds = 0
    )

    $timestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$Event.timestamp, [ref]$timestamp)) {
        return $false
    }

    return $timestamp -ge $StartedUtc.AddSeconds(-$SkewSeconds)
}

function Test-AllQueuesInstalled {
    param(
        [object[]] $QueueSnapshot,
        [string[]] $ExpectedQueues
    )

    if ($QueueSnapshot.Count -ne $ExpectedQueues.Count) {
        return $false
    }

    foreach ($queue in $ExpectedQueues) {
        $entry = $QueueSnapshot |
            Where-Object { $_.name -eq $queue } |
            Select-Object -First 1
        if ($null -eq $entry -or -not [bool]$entry.installed) {
            return $false
        }
    }

    return $true
}

function Get-ObjectPropertyValue {
    param(
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    if ($Object -is [System.Collections.IDictionary]) {
        return $Object[$Name]
    }

    return $Object.$Name
}

function Get-ResultByQueue {
    param(
        [object[]] $Results,
        [string] $Queue
    )

    return $Results |
        Where-Object { (Get-ObjectPropertyValue -Object $_ -Name 'queue') -eq $Queue } |
        Select-Object -First 1
}

function Test-RouteContains {
    param(
        [object] $Result,
        [string] $ExpectedText
    )

    if ($null -eq $Result -or $null -eq $Result.diagnostic) {
        return $false
    }

    $diagnostic = Get-ObjectPropertyValue -Object $Result -Name 'diagnostic'
    $route = Get-ObjectPropertyValue -Object $diagnostic -Name 'route'
    return [string]$route -like "*$ExpectedText*"
}

function New-PrintResultSummary {
    param(
        [object[]] $Results,
        [switch] $IncludeRoute,
        [switch] $IncludeTicketValidation
    )

    return @($Results | ForEach-Object {
        $diagnostic = Get-ObjectPropertyValue -Object $_ -Name 'diagnostic'
        $summary = [ordered]@{
            queue = Get-ObjectPropertyValue -Object $_ -Name 'queue'
            format = Get-ObjectPropertyValue -Object $_ -Name 'format'
            outputPath = Get-ObjectPropertyValue -Object $_ -Name 'outputPath'
            bytes = Get-ObjectPropertyValue -Object $_ -Name 'bytes'
        }

        if ($IncludeRoute) {
            $summary.route = Get-ObjectPropertyValue -Object $diagnostic -Name 'route'
        }

        if ($IncludeTicketValidation) {
            $summary.ticketValidation = Get-ObjectPropertyValue -Object $_ -Name 'ticketValidation'
        }

        $sinkArtifact = Get-ObjectPropertyValue -Object $_ -Name 'sinkArtifact'
        if ($null -ne $sinkArtifact) {
            $summary.sinkArtifact = $sinkArtifact
        }

        $summary
    })
}

function New-VirtualPrinterSummary {
    param(
        [object[]] $VirtualPrinters
    )

    return @($VirtualPrinters | ForEach-Object {
        [ordered]@{
            printerUri = Get-ObjectPropertyValue -Object $_ -Name 'printerUri'
            displayName = Get-ObjectPropertyValue -Object $_ -Name 'displayName'
            preferredInputFormat = Get-ObjectPropertyValue -Object $_ -Name 'preferredInputFormat'
            outputFileTypes = Get-ObjectPropertyValue -Object $_ -Name 'outputFileTypes'
        }
    })
}

function Add-PrintSinkFeatureEvidence {
    param(
        [System.Collections.Generic.List[object]] $FeatureEvidence,

        [Parameter(Mandatory)]
        [int] $Number,

        [Parameter(Mandatory)]
        [string] $Feature,

        [Parameter(Mandatory)]
        [bool] $Passed,

        [Parameter(Mandatory)]
        [string] $Evidence,

        [Parameter(Mandatory)]
        [object] $Artifact
    )

    if (-not $Passed) {
        throw "Feature evidence missing for #${Number} ${Feature}: $Evidence"
    }

    if ($null -eq $Artifact) {
        throw "Feature evidence artifact missing for #${Number} ${Feature}."
    }

    if ($Artifact -is [System.Array] -and $Artifact.Length -eq 0) {
        throw "Feature evidence artifact empty for #${Number} ${Feature}."
    }

    $FeatureEvidence.Add([ordered]@{
        number = $Number
        feature = $Feature
        evidence = $Evidence
        artifact = $Artifact
    }) | Out-Null
}

function Assert-PrintSinkFeatureEvidenceComplete {
    param(
        [object[]] $FeatureEvidence
    )

    $supportedNumbers = @()
    $supportedNumbers += 1..21
    $supportedNumbers += 23
    $supportedNumbers += 24
    $supportedNumbers += 25
    $supportedNumbers += 27

    $actualNumbers = @($FeatureEvidence | ForEach-Object { [int](Get-ObjectPropertyValue -Object $_ -Name 'number') })
    $missingNumbers = @($supportedNumbers | Where-Object { $_ -notin $actualNumbers })
    $unexpectedNumbers = @($actualNumbers | Where-Object { $_ -notin $supportedNumbers })
    $duplicateNumbers = @(
        $actualNumbers |
            Group-Object |
            Where-Object { $_.Count -gt 1 } |
            ForEach-Object { [int]$_.Name }
    )

    if ($missingNumbers.Count -gt 0) {
        throw "Feature evidence is missing supported print-stack feature number(s): $($missingNumbers -join ', ')."
    }

    if ($unexpectedNumbers.Count -gt 0) {
        throw "Feature evidence contains unsupported feature number(s): $($unexpectedNumbers -join ', ')."
    }

    if ($duplicateNumbers.Count -gt 0) {
        throw "Feature evidence contains duplicate feature number(s): $($duplicateNumbers -join ', ')."
    }
}

function New-PrintSinkDeferredFeatureEvidence {
    return @(
        [ordered]@{
            number = 22
            feature = 'Job notification compatibility hook'
            status = 'deferred'
            evidence = 'Windows did not deliver a deterministic PrintWorkflowJobUISession.JobNotification activation in the supported virtual-printer E2E flow. The handler records diagnostics if the OS activates it, but PrintSink does not claim this as a supported feature until a real E2E can trigger it.'
        },
        [ordered]@{
            number = 26
            feature = 'IPP communication-error timeout recovery'
            status = 'deferred'
            evidence = 'Windows did not deliver a deterministic PrintSupportExtensionSession.CommunicationErrorDetected activation in the supported E2E flow. The extension handler configures IPP timeouts when the OS reports timeout errors, but PrintSink does not claim this as supported behavior until a real-device E2E can trigger it.'
        }
    )
}

function New-PrintSinkFeatureEvidence {
    param(
        [string[]] $ExpectedQueues,
        [object] $PackageShape,
        [object[]] $QueueSnapshots,
        [object] $CliQueueLifecycle,
        [object] $ExtensionCapabilities,
        [object] $UserDefaultPrintTicket,
        [object] $VirtualAttributeRead,
        [object] $IppAssociation,
        [object[]] $RealPrintResults,
        [object] $NotepadPrint,
        [object] $ConcurrentPrints,
        [object] $PdfPassthrough,
        [object] $WinRtSource,
        [object] $SettingsUiOwner,
        [object] $SettingsWatermark,
        [object] $SettingsImageWatermark,
        [object] $FailedImageWatermark,
        [object] $JobUiWatermark,
        [object] $JobUiCancel
    )

    $featureEvidence = [System.Collections.Generic.List[object]]::new()
    $realPrints = @($RealPrintResults)
    $virtualPrinters = @($PackageShape.virtualPrinters)
    $initialSnapshot = @(
        $QueueSnapshots |
            Where-Object { $_.context -eq 'after provisioning' } |
            Select-Object -First 1
    )

    $provisionedQueues = if ($initialSnapshot.Count -gt 0) {
        @($initialSnapshot[0].queues)
    }
    else {
        @()
    }

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 1 `
        -Feature 'Install N virtual print queues from one package' `
        -Passed (
            $virtualPrinters.Count -eq $ExpectedQueues.Count `
                -and (Test-AllQueuesInstalled -QueueSnapshot $provisionedQueues -ExpectedQueues $ExpectedQueues) `
                -and [string]$CliQueueLifecycle.install -like '*Installed*yes*') `
        -Evidence 'The signed package manifest declares all queues, headless provisioning installs them, and the CLI observes them as installed.' `
        -Artifact ([ordered]@{
            virtualPrinters = $virtualPrinters.Count
            provisionedQueues = $provisionedQueues
            cliInstall = $CliQueueLifecycle.install
        })

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 2 `
        -Feature 'Receive spooled PDL and content type' `
        -Passed (
            $realPrints.Count -eq $ExpectedQueues.Count `
                -and (@($realPrints | Where-Object {
                    $diagnostic = Get-ObjectPropertyValue -Object $_ -Name 'diagnostic'
                    [string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue -Object $diagnostic -Name 'route'))
                }).Count -eq 0)) `
        -Evidence 'Every real queue produced a route diagnostic with the source content type from the live workflow activation.' `
        -Artifact (New-PrintResultSummary -Results $realPrints -IncludeRoute)

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 3 `
        -Feature 'Preferred input format negotiation' `
        -Passed (
            (@($virtualPrinters | Where-Object { (Get-ObjectPropertyValue -Object $_ -Name 'preferredInputFormat') -eq 'application/oxps' }).Count -ge 5) `
                -and (@($virtualPrinters | Where-Object { (Get-ObjectPropertyValue -Object $_ -Name 'preferredInputFormat') -eq 'application/postscript' }).Count -eq 1) `
                -and (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PostScript') -ExpectedText 'application/postscript')) `
        -Evidence 'Manifest preferred formats include OXPS and PostScript, and the PostScript queue received PostScript in a real print job.' `
        -Artifact (New-VirtualPrinterSummary -VirtualPrinters $virtualPrinters)

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 4 `
        -Feature 'Passthrough formats without OS re-render' `
        -Passed (
            (Test-RouteContains -Result $PdfPassthrough -ExpectedText 'application/pdf -> Pdf; Copy') `
                -and (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - XPS') -ExpectedText 'Copy') `
                -and (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PostScript') -ExpectedText 'Copy')) `
        -Evidence 'PDF passthrough is byte-asserted; XPS and PostScript queues completed copy routes from real print jobs.' `
        -Artifact ([ordered]@{
            pdf = $PdfPassthrough.diagnostic
            xps = (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - XPS').diagnostic
            postScript = (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PostScript').diagnostic
        })

    $fileBackedPrints = @($realPrints | Where-Object { (Get-ObjectPropertyValue -Object $_ -Name 'queue') -ne 'PrintSink - Cloud' })
    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 5 `
        -Feature 'File-printer Save As target' `
        -Passed (
                $fileBackedPrints.Count -eq 5 `
                -and $NotepadPrint.bytes -gt 0 `
                -and [string]$NotepadPrint.mode -eq 'notepad-command-line-print' `
                -and (@($fileBackedPrints | Where-Object {
                    [string]::IsNullOrWhiteSpace([string](Get-ObjectPropertyValue -Object $_ -Name 'outputPath')) `
                        -or (Get-ObjectPropertyValue -Object $_ -Name 'bytes') -le 0
                }).Count -eq 0)) `
        -Evidence 'The live Save-As broker produced non-empty files for every file-backed queue, including a real Notepad /p text-document print to PDF.' `
        -Artifact ([ordered]@{
            harness = New-PrintResultSummary -Results $fileBackedPrints
            notepad = $NotepadPrint
        })

    $cloudPrint = Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - Cloud'
    $cloudArtifact = Get-ObjectPropertyValue -Object $cloudPrint -Name 'sinkArtifact'
    $cloudArtifactBytes = Get-ObjectPropertyValue -Object $cloudArtifact -Name 'bytes'
    $cloudArtifactContentType = Get-ObjectPropertyValue -Object $cloudArtifact -Name 'contentType'
    $cloudArtifactCopyPath = Get-ObjectPropertyValue -Object $cloudArtifact -Name 'artifactCopyPath'
    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 6 `
        -Feature 'Non-file sinks' `
        -Passed (
            $null -ne $cloudPrint `
                -and $null -ne $cloudArtifact `
                -and [string]::IsNullOrWhiteSpace([string]$cloudPrint.outputPath) `
                -and $cloudPrint.bytes -eq 0 `
                -and ($cloudArtifactBytes -gt 0) `
                -and [string]$cloudArtifactContentType -eq 'application/pdf' `
                -and (Test-Path -LiteralPath $cloudArtifactCopyPath) `
                -and [string]$cloudPrint.diagnostic.message -eq 'Job completed') `
        -Evidence 'The cloud endpoint omits Save-As output, writes a package-local sink artifact from a real print job, and validates that artifact as PDF output.' `
        -Artifact $cloudPrint

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 7 `
        -Feature 'OXPS conversion to PDF, PWG Raster, and PCLm' `
        -Passed (
            (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PDF') -ExpectedText 'Convert XPS to PDF') `
                -and (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PWG Raster') -ExpectedText 'Convert XPS to PWG Raster') `
                -and (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - PCLm') -ExpectedText 'Convert XPS to PCLm')) `
        -Evidence 'The Windows converter produced valid PDF, PWG Raster, and PCLm outputs from real OXPS jobs.' `
        -Artifact (New-PrintResultSummary `
            -Results @($realPrints | Where-Object { (Get-ObjectPropertyValue -Object $_ -Name 'queue') -in @('PrintSink - PDF', 'PrintSink - PWG Raster', 'PrintSink - PCLm') }) `
            -IncludeRoute)

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 8 `
        -Feature 'XPS/OXPS passthrough copy' `
        -Passed (Test-RouteContains -Result (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - XPS') -ExpectedText 'Copy') `
        -Evidence 'The XPS endpoint completed a copy route and produced a valid OXPS package.' `
        -Artifact (Get-ResultByQueue -Results $realPrints -Queue 'PrintSink - XPS')

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 9 `
        -Feature 'Watermark text and image on XPS pages' `
        -Passed (
            $SettingsWatermark.bytes -gt 0 `
                -and $SettingsImageWatermark.bytes -gt 0 `
                -and $JobUiWatermark.bytes -gt 0) `
        -Evidence 'Default text watermark, default image watermark, and per-job UI watermark each produced validated PDF output.' `
        -Artifact ([ordered]@{
            settingsText = $SettingsWatermark
            settingsImage = $SettingsImageWatermark
            jobUiText = $JobUiWatermark
        })

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 10 `
        -Feature 'Per-job UI preview launched from background' `
        -Passed (
            $JobUiWatermark.mode -eq 'job-ui-watermark' `
                -and $JobUiWatermark.bytes -gt 0 `
                -and [string]$JobUiWatermark.jobUiPdl.detail -like '*kind=virtual-printer*' `
                -and [string]$JobUiWatermark.jobUiPdl.detail -like '*jobTitle=PrintSink E2E Job UI Watermark*' `
                -and [string]$JobUiWatermark.jobUiPdl.detail -like '*source=powershell.exe*' `
                -and [string]$JobUiWatermark.jobUiPdl.detail -like '*contentType=application/oxps*') `
        -Evidence 'The E2E run opened the packaged Job UI, proved it received virtual-printer PDL metadata, changed the watermark through UI Automation, continued the job, and validated the output.' `
        -Artifact $JobUiWatermark

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 11 `
        -Feature 'Custom print-preferences UI' `
        -Passed ($SettingsUiOwner.ownerDisabled -and $SettingsUiOwner.modalStatus -eq 'Modal to print preferences owner.') `
        -Evidence 'The Windows print dialog launched PrintSink settings, the owner was disabled while modal, and restored after close.' `
        -Artifact $SettingsUiOwner

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 12 `
        -Feature 'Print-ticket validation and resolve' `
        -Passed (
            $realPrints.Count -eq $ExpectedQueues.Count `
                -and (@($realPrints | Where-Object {
                    $ticketValidation = Get-ObjectPropertyValue -Object $_ -Name 'ticketValidation'
                    [string](Get-ObjectPropertyValue -Object $ticketValidation -Name 'message') -ne 'Print ticket validated'
                }).Count -eq 0)) `
        -Evidence 'Every real queue recorded PrintSupportExtension ticket validation with status=Resolved.' `
        -Artifact (New-PrintResultSummary -Results $realPrints -IncludeTicketValidation)

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 13 `
        -Feature 'PDC regeneration and custom features' `
        -Passed ([string]$ExtensionCapabilities.detail -like '*features=PageMediaSize,PageMediaType,JobInputBin,JobOutputBin,JobPageOrder,JobStapleAllDocuments,PageResolution,JobWatermarkMode*') `
        -Evidence 'A real capability refresh updated the installed queue PDC with the built-in PrintSink feature set.' `
        -Artifact $ExtensionCapabilities

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 14 `
        -Feature 'PDR localization of custom features' `
        -Passed ([string]$ExtensionCapabilities.detail -like '*pdr=updated*' -and [string]$ExtensionCapabilities.detail -like '*pdrResources=*') `
        -Evidence 'The extension updated device resources and reported localized PDR resource count during a real refresh.' `
        -Artifact $ExtensionCapabilities

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 15 `
        -Feature 'Refresh PDC on settings change' `
        -Passed ([string]$ExtensionCapabilities.message -eq 'Capabilities updated') `
        -Evidence 'The packaged app invoked RefreshPrintDeviceCapabilities and the extension recorded Capabilities updated.' `
        -Artifact $ExtensionCapabilities

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 16 `
        -Feature 'Get and set user default print ticket' `
        -Passed (
            [string]$UserDefaultPrintTicket.set.detail -like '*copies=2*verifiedCopies=2*' `
                -and [string]$UserDefaultPrintTicket.restore.detail -like '*copies=1*verifiedCopies=1*') `
        -Evidence 'The packaged app changed the installed PDF queue default copies and restored it through IppPrintDevice.UserDefaultPrintTicket.' `
        -Artifact $UserDefaultPrintTicket

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 17 `
        -Feature 'Physical IPP PSA association and workflow activation' `
        -Passed (
            -not [string]::IsNullOrWhiteSpace([string]$IppAssociation.aumid) `
                -and $IppAssociation.ippRequestCount -gt 0 `
                -and [string]$IppAssociation.ticketValidation.message -eq 'Print ticket validated' `
                -and $null -ne $IppAssociation.workflowActivationPrint.workflow) `
        -Evidence 'A temporary signed INF associated the package with a real Microsoft IPP Class Driver queue and triggered extension plus workflow diagnostics.' `
        -Artifact $IppAssociation

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 18 `
        -Feature 'MXDC image quality per output quality' `
        -Passed ([string]$ExtensionCapabilities.detail -like '*mxdc=configured*') `
        -Evidence 'A real capability refresh configured PrintSupportMxdcImageQualityConfiguration.' `
        -Artifact $ExtensionCapabilities

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 19 `
        -Feature 'Printer-selected adaptive card in MPD' `
        -Passed ([string]$SettingsUiOwner.printerSelected.detail -like '*adaptiveCard=set*' -and [string]$SettingsUiOwner.printerSelected.detail -like '*additionalFields=*') `
        -Evidence 'The Windows print dialog selected a PrintSink queue and the extension set adaptive-card and additional-field metadata.' `
        -Artifact $SettingsUiOwner.printerSelected

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 20 `
        -Feature 'IPP attribute get behavior for installed virtual queues' `
        -Passed ([string]$VirtualAttributeRead.detail -like '*document-format-default=<unsupported>*' -and [string]$VirtualAttributeRead.detail -like '*document-format-supported=<unsupported>*') `
        -Evidence 'The packaged app proved installed virtual-printer IPP attribute reads expose no usable document-format values, matching the v4 platform behavior.' `
        -Artifact $VirtualAttributeRead

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 21 `
        -Feature 'Multiple instances for concurrent jobs' `
        -Passed ($PackageShape.supportsMultipleInstances -and $ConcurrentPrints.overlapped -and @($ConcurrentPrints.jobs).Count -eq 2) `
        -Evidence 'The manifest supports multiple instances and two live jobs overlapped while producing valid outputs.' `
        -Artifact $ConcurrentPrints

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 23 `
        -Feature 'Graceful cancel, abort, and fail' `
        -Passed (
            [string]$FailedImageWatermark.diagnostic.message -eq 'Job failed' `
                -and [string]$JobUiCancel.diagnostic.message -eq 'Job canceled' `
                -and $JobUiCancel.bytes -eq 0) `
        -Evidence 'A corrupt watermark aborts as failed with no output, and the Job UI cancel path records cancellation with no output.' `
        -Artifact ([ordered]@{
            failed = $FailedImageWatermark
            canceled = $JobUiCancel
        })

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 24 `
        -Feature 'Job password option model' `
        -Passed (
            [string]$JobUiWatermark.jobPassword -eq 'present-not-applicable' `
                -and [string]$JobUiWatermark.diagnostic.detail -like '*job-password=present-not-applicable*' `
                -and [string]$JobUiWatermark.diagnostic.detail -notlike '*ci-password*' `
                -and $JobUiWatermark.jobPasswordSecretExposed -eq $false) `
        -Evidence 'The real Job UI captured job-password metadata and the virtual-printer processor consumed it without applying it to virtual file output.' `
        -Artifact ([ordered]@{
            queue = $JobUiWatermark.queue
            mode = $JobUiWatermark.mode
            jobPassword = $JobUiWatermark.jobPassword
            jobPasswordSecretExposed = $JobUiWatermark.jobPasswordSecretExposed
            diagnostic = $JobUiWatermark.diagnostic
        })

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 25 `
        -Feature 'Localized printer queue display names' `
        -Passed (
            (@($virtualPrinters | Where-Object { [string](Get-ObjectPropertyValue -Object $_ -Name 'displayName') -like 'ms-resource:*' }).Count -eq $ExpectedQueues.Count) `
                -and (Test-AllQueuesInstalled -QueueSnapshot $provisionedQueues -ExpectedQueues $ExpectedQueues)) `
        -Evidence 'The signed manifest uses ms-resource display names and Windows reports the installed localized queue names.' `
        -Artifact ([ordered]@{
            manifestNames = New-VirtualPrinterSummary -VirtualPrinters $virtualPrinters
            installedQueues = $provisionedQueues
        })

    Add-PrintSinkFeatureEvidence `
        -FeatureEvidence $featureEvidence `
        -Number 27 `
        -Feature 'IPP compression compatibility handling' `
        -Passed (
            [string]$IppAssociation.workflowActivationPrint.workflowStart.message -eq 'Workflow job starting' `
                -and [string]$IppAssociation.workflowActivationPrint.workflowStart.detail -like '*skipSystemRendering=default*' `
                -and [string]$IppAssociation.workflowActivationPrint.workflowStart.detail -like '*ippCompression=*') `
        -Evidence 'A real IPP workflow activation entered JobStarting and recorded the platform compression state while leaving system rendering enabled.' `
        -Artifact $IppAssociation.workflowActivationPrint.workflowStart

    Assert-PrintSinkFeatureEvidenceComplete -FeatureEvidence @($featureEvidence)

    return @($featureEvidence)
}

function Write-E2EProgress {
    param(
        [string] $Message
    )

    Write-Host "[$([DateTimeOffset]::Now.ToString('O'))] $Message"
}

function Read-PrintSinkDiagnosticEvents {
    param(
        [string] $DiagnosticPath
    )

    $json = Get-Content -LiteralPath $DiagnosticPath -Raw | ConvertFrom-Json
    if ($null -eq $json) {
        return @()
    }

    $events = @()
    foreach ($event in $json) {
        $events += $event
    }

    return $events
}

function Get-PrintSinkRouteTimestamp {
    param(
        [object] $Route,
        [object] $Completion
    )

    if ($null -ne $Route -and -not [string]::IsNullOrWhiteSpace([string]$Route.timestamp)) {
        return [string]$Route.timestamp
    }

    $completedUtc = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$Completion.timestamp, [ref]$completedUtc)) {
        return $null
    }

    $detail = [string]$Completion.detail
    if ($detail -notmatch ';\s*(?<elapsed>\d+)\s*ms;') {
        return $null
    }

    return $completedUtc.AddMilliseconds(-[int64]$Matches.elapsed).ToString('O')
}

function Wait-ForPrintSinkJobCompleted {
    param(
        [string] $PackageFamilyName,
        [string] $Endpoint,
        [DateTimeOffset] $StartedUtc,
        [string] $ExpectedRouteDetail
    )

    $diagnosticPath = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState\Settings\diagnostic-events.json"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    do {
        if (Test-Path -LiteralPath $diagnosticPath) {
            try {
                $events = Read-PrintSinkDiagnosticEvents -DiagnosticPath $diagnosticPath
            }
            catch [System.IO.IOException] {
                Start-Sleep -Milliseconds 250
                continue
            }
            catch [System.UnauthorizedAccessException] {
                Start-Sleep -Milliseconds 250
                continue
            }

            $route = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Route resolved' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1

            $match = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job completed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $match) {
                if (-not [string]::IsNullOrWhiteSpace($ExpectedRouteDetail)) {
                    if ($null -eq $route) {
                        if ([string]$match.detail -notlike "*route=$ExpectedRouteDetail*") {
                            throw "PrintSink route diagnostic was not recorded for $Endpoint."
                        }
                    }
                    elseif ($route.detail -ne $ExpectedRouteDetail) {
                        throw "PrintSink route diagnostic differed for ${Endpoint}. Expected '$ExpectedRouteDetail'; actual '$($route.detail)'."
                    }
                }

                return [ordered]@{
                    timestamp = $match.timestamp
                    message = $match.message
                    detail = $match.detail
                    routeTimestamp = Get-PrintSinkRouteTimestamp -Route $route -Completion $match
                    route = if ($null -eq $route) { $ExpectedRouteDetail } else { $route.detail }
                }
            }

            $failure = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job failed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $failure) {
                throw "PrintSink job failed for ${Endpoint}: $($failure.detail)"
            }
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for PrintSink job completion diagnostic for $Endpoint."
}

function Wait-ForPrintSinkJobFailed {
    param(
        [string] $PackageFamilyName,
        [string] $Endpoint,
        [DateTimeOffset] $StartedUtc,
        [string] $ExpectedRouteDetail
    )

    $diagnosticPath = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState\Settings\diagnostic-events.json"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    do {
        if (Test-Path -LiteralPath $diagnosticPath) {
            try {
                $events = Read-PrintSinkDiagnosticEvents -DiagnosticPath $diagnosticPath
            }
            catch [System.IO.IOException] {
                Start-Sleep -Milliseconds 250
                continue
            }
            catch [System.UnauthorizedAccessException] {
                Start-Sleep -Milliseconds 250
                continue
            }

            $route = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Route resolved' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1

            $failure = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job failed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $failure) {
                if ([string]::IsNullOrWhiteSpace([string]$failure.detail)) {
                    throw "PrintSink failure diagnostic was empty for $Endpoint."
                }

                if ([string]$failure.detail -notlike '*0x*') {
                    throw "PrintSink failure diagnostic did not include an HRESULT for ${Endpoint}: $($failure.detail)"
                }

                if ($null -eq $route) {
                    if ([string]$failure.detail -notlike "*route=$ExpectedRouteDetail*") {
                        throw "PrintSink route diagnostic was not recorded for failed $Endpoint job."
                    }
                }
                elseif ($route.detail -ne $ExpectedRouteDetail) {
                    throw "PrintSink route diagnostic differed for failed ${Endpoint}. Expected '$ExpectedRouteDetail'; actual '$($route.detail)'."
                }

                return [ordered]@{
                    timestamp = $failure.timestamp
                    message = $failure.message
                    detail = $failure.detail
                    route = if ($null -eq $route) { $ExpectedRouteDetail } else { $route.detail }
                }
            }

            $completion = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job completed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $completion) {
                throw "PrintSink job completed instead of failing for ${Endpoint}: $($completion.detail)"
            }

            $cancellation = $events |
                Where-Object {
                    ($_.endpoint -eq $Endpoint -or [string]::IsNullOrWhiteSpace([string]$_.endpoint)) `
                        -and $_.message -eq 'Job canceled' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $cancellation) {
                throw "PrintSink job canceled instead of failing for ${Endpoint}: $($cancellation.detail)"
            }
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for PrintSink job failure diagnostic for $Endpoint."
}

function Wait-ForPrintSinkDiagnostic {
    param(
        [string] $PackageFamilyName,
        [string] $Endpoint,
        [string] $Message,
        [DateTimeOffset] $StartedUtc,
        [string[]] $DetailContains = @(),
        [int] $TimeoutSeconds = 45
    )

    $diagnosticPath = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState\Settings\diagnostic-events.json"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastCandidate = $null
    do {
        if (Test-Path -LiteralPath $diagnosticPath) {
            try {
                $events = Read-PrintSinkDiagnosticEvents -DiagnosticPath $diagnosticPath
            }
            catch [System.IO.IOException] {
                Start-Sleep -Milliseconds 250
                continue
            }
            catch [System.UnauthorizedAccessException] {
                Start-Sleep -Milliseconds 250
                continue
            }

            $candidates = @($events |
                Where-Object {
                    ($_.endpoint -eq $Endpoint -or [string]::IsNullOrWhiteSpace($Endpoint)) `
                        -and $_.message -eq $Message `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc -SkewSeconds 5)
                })

            foreach ($candidate in $candidates) {
                $lastCandidate = $candidate
                $detail = [string]$candidate.detail
                $missingDetail = @($DetailContains | Where-Object { $detail -notlike "*$_*" })
                if ($missingDetail.Count -eq 0) {
                    return [ordered]@{
                        timestamp = $candidate.timestamp
                        source = $candidate.source
                        message = $candidate.message
                        endpoint = $candidate.endpoint
                        detail = $candidate.detail
                    }
                }
            }
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ($null -ne $lastCandidate) {
        throw "Timed out waiting for diagnostic '$Message' on '$Endpoint' with details '$($DetailContains -join ', ')'. Last detail: $($lastCandidate.detail)"
    }

    throw "Timed out waiting for diagnostic '$Message' on '$Endpoint'."
}

function Wait-ForPrintSinkJobCanceled {
    param(
        [string] $PackageFamilyName,
        [string] $Endpoint,
        [DateTimeOffset] $StartedUtc
    )

    $diagnosticPath = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState\Settings\diagnostic-events.json"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    do {
        if (Test-Path -LiteralPath $diagnosticPath) {
            try {
                $events = Read-PrintSinkDiagnosticEvents -DiagnosticPath $diagnosticPath
            }
            catch [System.IO.IOException] {
                Start-Sleep -Milliseconds 250
                continue
            }
            catch [System.UnauthorizedAccessException] {
                Start-Sleep -Milliseconds 250
                continue
            }

            $match = $events |
                Where-Object {
                    ($_.endpoint -eq $Endpoint -or [string]::IsNullOrWhiteSpace([string]$_.endpoint)) `
                        -and $_.message -eq 'Job canceled' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $match) {
                return [ordered]@{
                    timestamp = $match.timestamp
                    source = $match.source
                    message = $match.message
                    endpoint = $match.endpoint
                    detail = $match.detail
                }
            }

            $completion = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job completed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $completion) {
                throw "PrintSink job completed instead of canceling for ${Endpoint}: $($completion.detail)"
            }

            $failure = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job failed' `
                        -and (Test-PrintSinkDiagnosticStartedAfter -Event $_ -StartedUtc $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $failure) {
                throw "PrintSink job failed instead of canceling for ${Endpoint}: $($failure.detail)"
            }
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for PrintSink job cancellation diagnostic for $Endpoint."
}

if (-not $SkipPackageInstall) {
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        throw 'Pass -PackagePath or use -SkipPackageInstall when the package is already installed.'
    }

    if (-not (Test-Path -LiteralPath $PackagePath)) {
        throw "Package path was not found: $PackagePath"
    }

    Import-PrintSinkPackageCertificate -PackagePath $PackagePath

    Write-E2EProgress "Installing package from $PackagePath"
    Get-AppxPackage -Name $PackageName | Remove-AppxPackage -ErrorAction Stop
    Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

Write-E2EProgress "Inspecting installed package $PackageName"
$package = Get-InstalledPackage -Name $PackageName
$packageShape = Assert-InstalledPackageShape -Package $package -ExpectedVirtualPrinters $expectedVirtualPrinters
$diagnosticPath = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalState\Settings\diagnostic-events.json"
Remove-Item -LiteralPath $diagnosticPath -ErrorAction SilentlyContinue

$alias = Get-Command printsink-app.exe -ErrorAction SilentlyContinue
if ($null -eq $alias) {
    throw 'printsink-app.exe was not registered. Install the signed MSIX package before running E2E.'
}

$completedSuccessfully = $false
$e2eStartedUtc = [DateTimeOffset]::UtcNow

Write-E2EProgress 'Disabling foreground job UI'
Invoke-PrintSinkAppCommand -Arguments @('--disable-job-ui') -Description 'Disabling foreground job UI'
try {
    Write-E2EProgress 'Verifying CLI queue lifecycle'
    $cliQueueLifecycle = Invoke-PrintSinkCliQueueLifecycle -ExpectedQueues $expectedQueues

    Write-E2EProgress 'Provisioning virtual printer queues'
    Invoke-PrintSinkAppCommand -Arguments @('--install-virtual-printers') -Description 'Headless virtual-printer provisioning'

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    Get-ChildItem -LiteralPath $OutputDirectory -File -ErrorAction SilentlyContinue | Remove-Item -Force

    $queueSnapshots = [System.Collections.Generic.List[object]]::new()
    $queueSnapshots.Add([ordered]@{
        context = 'after provisioning'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after provisioning'
    })

    Write-E2EProgress 'Verifying print support extension capabilities'
    $extensionCapabilitiesResult = Invoke-PrintSinkExtensionCapabilities `
        -PackageFamilyName $package.PackageFamilyName `
        -StartedUtc $e2eStartedUtc
    $queueSnapshots.Add([ordered]@{
        context = 'after extension capability refresh'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after extension capability refresh'
    })

    Write-E2EProgress 'Verifying user default print ticket activation'
    $userDefaultPrintTicketResult = Invoke-PrintSinkUserDefaultPrintTicket `
        -PackageFamilyName $package.PackageFamilyName `
        -StartedUtc $e2eStartedUtc
    $queueSnapshots.Add([ordered]@{
        context = 'after user default print ticket update'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after user default print ticket update'
    })

    Write-E2EProgress 'Verifying virtual-printer attribute reads'
    $virtualAttributeReadResult = Invoke-PrintSinkVirtualAttributeRead `
        -PackageFamilyName $package.PackageFamilyName `
        -StartedUtc $e2eStartedUtc
    $queueSnapshots.Add([ordered]@{
        context = 'after virtual-printer attribute-read assertion'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after virtual-printer attribute-read assertion'
    })

    $realPrintResults = @()
    foreach ($printCase in $realPrintCases) {
        Write-E2EProgress "Printing real document to $($printCase.queue)"
        $realPrintResults += Invoke-PrintSinkRealPrint `
            -PrintCase $printCase `
            -OutputDirectory $OutputDirectory `
            -PackageFamilyName $package.PackageFamilyName
        $queueSnapshots.Add([ordered]@{
            context = "after printing $($printCase.queue)"
            queues = Assert-PrintSinkQueuesInstalled `
                -ExpectedQueues $expectedQueues `
            -Context "after printing $($printCase.queue)"
        })
    }

    Write-E2EProgress 'Printing Notepad text document to PrintSink - PDF'
    $notepadPrintResult = Invoke-PrintSinkNotepadPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after Notepad PDF print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after Notepad PDF print'
    })

    Write-E2EProgress 'Printing concurrent real documents'
    $concurrentPrintResult = Invoke-PrintSinkConcurrentPrints `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after concurrent real prints'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after concurrent real prints'
    })

    Write-E2EProgress 'Verifying PDF passthrough'
    $pdfPassthroughResult = Invoke-PrintSinkPdfPassthroughPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after PDF passthrough'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after PDF passthrough'
    })

    Write-E2EProgress 'Verifying WinRT source print'
    $winRtSourceResult = Invoke-PrintSinkWinRtSourcePrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after WinRT source print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after WinRT source print'
    })

    Write-E2EProgress 'Verifying settings UI ownership'
    $settingsUiOwnerResult = Invoke-PrintSinkSettingsUiOwner `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings UI owner check'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings UI owner check'
    })

    Write-E2EProgress 'Verifying settings text watermark print'
    $settingsWatermarkResult = Invoke-PrintSinkSettingsWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings text watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings text watermark print'
    })

    Write-E2EProgress 'Verifying settings image watermark print'
    $settingsImageWatermarkResult = Invoke-PrintSinkSettingsImageWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings image watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings image watermark print'
    })

    Write-E2EProgress 'Verifying invalid image watermark failure path'
    $failedImageWatermarkResult = Invoke-PrintSinkFailedImageWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after failed image watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after failed image watermark print'
    })

    Write-E2EProgress 'Enabling foreground job UI'
    Invoke-PrintSinkAppCommand -Arguments @('--enable-job-ui') -Description 'Enabling foreground job UI for the Job UI E2E path'
    Write-E2EProgress 'Verifying job UI watermark print'
    $jobUiResult = Invoke-PrintSinkJobUiWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after job UI watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after job UI watermark print'
    })
    Write-E2EProgress 'Verifying job UI cancellation'
    $jobUiCancelResult = Invoke-PrintSinkJobUiCancelPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after job UI cancel'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after job UI cancel'
    })
    Write-E2EProgress 'Disabling foreground job UI after job UI checks'
    Invoke-PrintSinkAppCommand -Arguments @('--disable-job-ui') -Description 'Disabling foreground job UI after the Job UI E2E path'

    Write-E2EProgress 'Verifying IPP PSA association'
    $ippAssociationResult = Invoke-PrintSinkIppAssociation `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after IPP PSA association'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after IPP PSA association'
    })

    $featureEvidence = New-PrintSinkFeatureEvidence `
        -ExpectedQueues $expectedQueues `
        -PackageShape $packageShape `
        -QueueSnapshots @($queueSnapshots) `
        -CliQueueLifecycle $cliQueueLifecycle `
        -ExtensionCapabilities $extensionCapabilitiesResult `
        -UserDefaultPrintTicket $userDefaultPrintTicketResult `
        -VirtualAttributeRead $virtualAttributeReadResult `
        -IppAssociation $ippAssociationResult `
        -RealPrintResults $realPrintResults `
        -NotepadPrint $notepadPrintResult `
        -ConcurrentPrints $concurrentPrintResult `
        -PdfPassthrough $pdfPassthroughResult `
        -WinRtSource $winRtSourceResult `
        -SettingsUiOwner $settingsUiOwnerResult `
        -SettingsWatermark $settingsWatermarkResult `
        -SettingsImageWatermark $settingsImageWatermarkResult `
        -FailedImageWatermark $failedImageWatermarkResult `
        -JobUiWatermark $jobUiResult `
        -JobUiCancel $jobUiCancelResult
    $deferredFeatureEvidence = New-PrintSinkDeferredFeatureEvidence

    $resultPath = Join-Path $OutputDirectory 'e2e-result.json'
    $result = [ordered]@{
        windowsVersion = [Environment]::OSVersion.Version.ToString()
        architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        package = [ordered]@{
            name = $package.Name
            fullName = $package.PackageFullName
            familyName = $package.PackageFamilyName
            version = $package.Version.ToString()
            installLocation = $package.InstallLocation
        }
        packageShape = $packageShape
        queues = @($expectedQueues)
        queueSnapshots = @($queueSnapshots)
        resultPath = $resultPath
        cliQueueLifecycle = $cliQueueLifecycle
        outputDirectory = $OutputDirectory
        extensionCapabilities = $extensionCapabilitiesResult
        userDefaultPrintTicket = $userDefaultPrintTicketResult
        virtualAttributeRead = $virtualAttributeReadResult
        ippAssociation = $ippAssociationResult
        realPrints = $realPrintResults
        notepadPrint = $notepadPrintResult
        concurrentPrints = $concurrentPrintResult
        pdfPassthrough = $pdfPassthroughResult
        winRtSource = $winRtSourceResult
        settingsUiOwner = $settingsUiOwnerResult
        settingsWatermark = $settingsWatermarkResult
        settingsImageWatermark = $settingsImageWatermarkResult
        failedImageWatermark = $failedImageWatermarkResult
        jobUiWatermark = $jobUiResult
        jobUiCancel = $jobUiCancelResult
        featureEvidence = $featureEvidence
        deferredFeatureEvidence = $deferredFeatureEvidence
    }

    $resultJson = $result | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $resultPath -Value $resultJson -Encoding UTF8
    Write-E2EProgress "Wrote E2E result to $resultPath"
    $resultJson
    $completedSuccessfully = $true
}
finally {
    $cleanupFailures = [System.Collections.Generic.List[string]]::new()

    if ($Cleanup) {
        try {
            Write-E2EProgress 'Cleaning up virtual printer queues'
            Invoke-PrintSinkAppCommand -Arguments @('--remove-virtual-printers') -Description 'Headless virtual-printer cleanup'
            Wait-ForQueueInstalledState `
                -ExpectedQueues $expectedQueues `
                -ExpectedInstalled $false `
                -TimeoutSeconds 30
        }
        catch {
            $cleanupFailures.Add("Virtual-printer cleanup failed: $($_.Exception.Message)")
        }

        Stop-PrintSinkE2ERuntime
    }

    try {
        Write-E2EProgress 'Restoring foreground job UI'
        Invoke-PrintSinkAppCommand -Arguments @('--enable-job-ui') -Description 'Restoring foreground job UI'
    }
    catch {
        $cleanupFailures.Add("Restoring foreground job UI failed: $($_.Exception.Message)")
    }

    if ($cleanupFailures.Count -gt 0) {
        $message = $cleanupFailures -join [Environment]::NewLine
        if ($completedSuccessfully) {
            throw $message
        }

        Write-Warning $message
    }
}
