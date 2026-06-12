param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64', 'X86')]
    [string] $Platform = 'x64',

    [string] $Target = 'Build'
)

$ErrorActionPreference = 'Stop'

msbuild .\PrintSink.slnx /t:$Target /p:Configuration=$Configuration /p:Platform=$Platform
