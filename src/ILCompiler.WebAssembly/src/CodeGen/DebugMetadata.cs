// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LLVMSharp.Interop;

namespace ILCompiler.WebAssembly
{
    class DebugMetadata
    {
        public DebugMetadata(LLVMMetadataRef file, LLVMMetadataRef compileUnit)
        {
            File = file;
            CompileUnit = compileUnit;
        }

        public LLVMMetadataRef CompileUnit { get; }
        public LLVMMetadataRef File { get; }
    }
}
