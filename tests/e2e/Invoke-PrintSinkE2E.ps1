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
        extension = ''
        requiresSaveAs = $false
        expectedText = ''
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
        $installedNames = @(Get-Printer | ForEach-Object Name)
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

function Get-PrintSinkQueueSnapshot {
    param(
        [string[]] $ExpectedQueues
    )

    $printers = @(Get-Printer)
    $installedNames = @($printers | ForEach-Object Name)
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

    $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.Print.$([Guid]::NewGuid()).ps1"
    $escapedPrinterName = $printerName.Replace("'", "''")
    $printScript = @"
Add-Type -AssemblyName System.Drawing
`$document = [System.Drawing.Printing.PrintDocument]::new()
`$document.DocumentName = 'PrintSink E2E Real Print'
`$document.PrinterSettings.PrinterName = '$escapedPrinterName'
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
    $process = Start-Process -FilePath powershell.exe -ArgumentList @('-Sta', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) -PassThru

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

        if (-not $process.WaitForExit(30000)) {
            throw "Print process did not exit for $printerName."
        }

        if ($process.ExitCode -ne 0) {
            throw "Print process for $printerName exited with $($process.ExitCode)."
        }

        if ($PrintCase.requiresSaveAs) {
            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            do {
                if (Test-Path -LiteralPath $outputPath) {
                    $file = Get-Item -LiteralPath $outputPath
                    if ($file.Length -gt 0) {
                        break
                    }
                }

                Start-Sleep -Milliseconds 500
            }
            while ([DateTime]::UtcNow -lt $deadline)

            if (-not (Test-Path -LiteralPath $outputPath)) {
                throw "Output was not written for ${printerName}: $outputPath"
            }

            $file = Get-Item -LiteralPath $outputPath
            if ($file.Length -le 0) {
                throw "Output is empty for ${printerName}: $outputPath"
            }

            Assert-DocumentOutput -PrintCase $PrintCase -OutputPath $outputPath
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

        return [ordered]@{
            queue = $printerName
            format = $PrintCase.format
            outputPath = $null
            bytes = 0
            diagnostic = $diagnostic
            ticketValidation = $ticketValidation
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Close-SavePrintOutputDialogs
        Remove-Item -LiteralPath $scriptPath -ErrorAction SilentlyContinue
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

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath
        Assert-FileBytesEqual -ExpectedPath $sourcePath -ActualPath $outputPath

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute

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

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath

        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printCase.queue `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute

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
            'features=PageMediaSize,PageResolution,JobWatermarkMode',
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
        -Message 'Virtual printer attribute read succeeded' `
        -StartedUtc $StartedUtc `
        -DetailContains @(
            'Virtual printer attribute read succeeded',
            'document-format-default=',
            'document-format-supported=')
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
        expectedRoute = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'
    }
    $printerName = $printCase.queue
    $startedUtc = [DateTimeOffset]::UtcNow
    $outputPath = Join-Path $OutputDirectory 'PrintSink-JobUI-Watermark.pdf'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $scriptPath = Join-Path $env:TEMP "PrintSink.E2E.JobUI.$([Guid]::NewGuid()).ps1"
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
    $process = Start-Process -FilePath powershell.exe -ArgumentList @('-Sta', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) -PassThru

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

        Set-ToggleSwitch -Root $jobWindow -Name 'Text watermark' -ExpectedState $true
        Set-TextBoxValue -Root $jobWindow -Name 'Watermark text' -Value 'CI WATERMARK'
        Invoke-Button -Root $jobWindow -Name 'Continue' -TimeoutSeconds 30

        if (-not $process.WaitForExit(30000)) {
            throw 'Job UI watermark print process did not exit.'
        }

        if ($process.ExitCode -ne 0) {
            throw "Job UI watermark print process exited with $($process.ExitCode)."
        }

        Wait-ForNonEmptyFile -Path $outputPath -TimeoutSeconds 45
        Assert-DocumentOutput -PrintCase $printCase -OutputPath $outputPath
        $diagnostic = Wait-ForPrintSinkJobCompleted `
            -PackageFamilyName $PackageFamilyName `
            -Endpoint $printerName `
            -StartedUtc $startedUtc `
            -ExpectedRouteDetail $printCase.expectedRoute

        $file = Get-Item -LiteralPath $outputPath
        return [ordered]@{
            queue = $printerName
            format = 'pdf'
            outputPath = $outputPath
            bytes = $file.Length
            mode = 'job-ui-watermark'
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
    $process = Start-Process -FilePath powershell.exe -ArgumentList @('-Sta', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) -PassThru

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

        Invoke-Button -Root $jobWindow -Name 'Cancel' -TimeoutSeconds 30

        if (-not $process.WaitForExit(30000)) {
            throw 'Job UI cancel print process did not exit.'
        }

        if ($process.ExitCode -ne 0) {
            throw "Job UI cancel print process exited with $($process.ExitCode)."
        }

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

    if ($PrintCase.Contains('requiresImage') -and $PrintCase.requiresImage) {
        $arguments += @('--requires-image', 'true')
    }

    $assertionOutput = & dotnet @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Document assertion failed for $($PrintCase.queue). $($assertionOutput -join [Environment]::NewLine)"
    }
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
                $events = @(Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json)
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
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
                } |
                Select-Object -Last 1

            $match = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job completed' `
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $match) {
                if (-not [string]::IsNullOrWhiteSpace($ExpectedRouteDetail)) {
                    if ($null -eq $route) {
                        throw "PrintSink route diagnostic was not recorded for $Endpoint."
                    }

                    if ($route.detail -ne $ExpectedRouteDetail) {
                        throw "PrintSink route diagnostic differed for ${Endpoint}. Expected '$ExpectedRouteDetail'; actual '$($route.detail)'."
                    }
                }

                return [ordered]@{
                    timestamp = $match.timestamp
                    message = $match.message
                    detail = $match.detail
                    route = $route.detail
                }
            }

            $failure = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job failed' `
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
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
                $events = @(Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json)
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
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
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
                $events = @(Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json)
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
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
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
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
                } |
                Select-Object -Last 1
            if ($null -ne $completion) {
                throw "PrintSink job completed instead of canceling for ${Endpoint}: $($completion.detail)"
            }

            $failure = $events |
                Where-Object {
                    $_.endpoint -eq $Endpoint `
                        -and $_.message -eq 'Job failed' `
                        -and ([DateTimeOffset]::Parse($_.timestamp) -ge $StartedUtc)
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

    Get-AppxPackage -Name $PackageName | Remove-AppxPackage -ErrorAction Stop
    Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

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

Invoke-PrintSinkAppCommand -Arguments @('--disable-job-ui') -Description 'Disabling foreground job UI'
try {
    $cliQueueLifecycle = Invoke-PrintSinkCliQueueLifecycle -ExpectedQueues $expectedQueues

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

    $extensionCapabilitiesResult = Invoke-PrintSinkExtensionCapabilities `
        -PackageFamilyName $package.PackageFamilyName `
        -StartedUtc $e2eStartedUtc
    $queueSnapshots.Add([ordered]@{
        context = 'after extension capability refresh'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after extension capability refresh'
    })

    $userDefaultPrintTicketResult = Invoke-PrintSinkUserDefaultPrintTicket `
        -PackageFamilyName $package.PackageFamilyName `
        -StartedUtc $e2eStartedUtc
    $queueSnapshots.Add([ordered]@{
        context = 'after user default print ticket update'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after user default print ticket update'
    })

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

    $pdfPassthroughResult = Invoke-PrintSinkPdfPassthroughPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after PDF passthrough'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after PDF passthrough'
    })

    $winRtSourceResult = Invoke-PrintSinkWinRtSourcePrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after WinRT source print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after WinRT source print'
    })

    $settingsUiOwnerResult = Invoke-PrintSinkSettingsUiOwner `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings UI owner check'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings UI owner check'
    })

    $settingsWatermarkResult = Invoke-PrintSinkSettingsWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings text watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings text watermark print'
    })

    $settingsImageWatermarkResult = Invoke-PrintSinkSettingsImageWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after settings image watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after settings image watermark print'
    })

    Invoke-PrintSinkAppCommand -Arguments @('--enable-job-ui') -Description 'Enabling foreground job UI for the Job UI E2E path'
    $jobUiResult = Invoke-PrintSinkJobUiWatermarkPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after job UI watermark print'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after job UI watermark print'
    })
    $jobUiCancelResult = Invoke-PrintSinkJobUiCancelPrint `
        -OutputDirectory $OutputDirectory `
        -PackageFamilyName $package.PackageFamilyName
    $queueSnapshots.Add([ordered]@{
        context = 'after job UI cancel'
        queues = Assert-PrintSinkQueuesInstalled `
            -ExpectedQueues $expectedQueues `
            -Context 'after job UI cancel'
    })
    Invoke-PrintSinkAppCommand -Arguments @('--disable-job-ui') -Description 'Disabling foreground job UI after the Job UI E2E path'

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
        cliQueueLifecycle = $cliQueueLifecycle
        outputDirectory = $OutputDirectory
        extensionCapabilities = $extensionCapabilitiesResult
        userDefaultPrintTicket = $userDefaultPrintTicketResult
        virtualAttributeRead = $virtualAttributeReadResult
        realPrints = $realPrintResults
        pdfPassthrough = $pdfPassthroughResult
        winRtSource = $winRtSourceResult
        settingsUiOwner = $settingsUiOwnerResult
        settingsWatermark = $settingsWatermarkResult
        settingsImageWatermark = $settingsImageWatermarkResult
        jobUiWatermark = $jobUiResult
        jobUiCancel = $jobUiCancelResult
    }

    $result | ConvertTo-Json -Depth 8
    $completedSuccessfully = $true
}
finally {
    $cleanupFailures = [System.Collections.Generic.List[string]]::new()

    if ($Cleanup) {
        try {
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
