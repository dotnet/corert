// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using LLVMSharp;

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
