using LLVMSharp.Interop;

namespace Internal.IL
{
    /// <summary>
    /// Workaround while waiting for https://github.com/microsoft/LLVMSharp/pull/141
    /// </summary>
    internal class LLVMSharpInterop
    {
        internal static unsafe uint ElementAtOffset(LLVMTargetDataRef targetDataRef, LLVMTypeRef structTypeRef, ulong offset)
        {
            return LLVM.ElementAtOffset(targetDataRef, structTypeRef, offset);
        }
    }
}
