using System.Runtime.InteropServices;

namespace PrintSink.Xps.Projections;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ActivationContext
{
    internal int Size;
    internal uint Flags;
    internal nint Source;
    internal ushort ProcessorArchitecture;
    internal ushort LanguageId;
    internal nint AssemblyDirectory;
    internal nint ResourceName;
    internal nint ApplicationName;
    internal nint Module;
}
