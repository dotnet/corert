using LLVMSharp.Interop;

namespace Internal.IL
{
    internal class LLVMSharpInterop
    {
        /// <summary>
        /// Workaround while waiting for https://github.com/microsoft/LLVMSharp/pull/141
        /// </summary>
        internal static unsafe uint ElementAtOffset(LLVMTargetDataRef targetDataRef, LLVMTypeRef structTypeRef, ulong offset)
        {
            return LLVM.ElementAtOffset(targetDataRef, structTypeRef, offset);
        }

        ///
        /// Wrapper while waiting for https://github.com/microsoft/LLVMSharp/pull/144
        /// 
        internal static unsafe void DISetSubProgram(LLVMValueRef function, LLVMMetadataRef diFunction)
        {
            LLVM.SetSubprogram(function, diFunction);
        }
    }
}
