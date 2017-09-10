// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LLVMSharp;

namespace ILCompiler.CodeGen
{
    class LLVMMisc
    {
        public static LLVMBool False { get; } = new LLVMBool(0);

        public static LLVMBool True { get; } = new LLVMBool(1);
    }
}
