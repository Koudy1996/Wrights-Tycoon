#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Unity's runtime profile needs the same record/init shim as the Core assembly.
    internal static class IsExternalInit { }
}
#endif
