// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ARM
{
    /// <summary>
    /// Maps logical registers to physical registers on a specified OS.
    /// </summary>
    public struct TargetRegisterMap
    {
        public readonly Register Arg0;
        public readonly Register Arg1;
        public readonly Register Result;
        public readonly Register InterproceduralScratch;

        public TargetRegisterMap(TargetOS os)
        {
            Arg0 = Register.R0;
            Arg1 = Register.R1;
            Result = Register.R0;
            InterproceduralScratch = Register.R12;
        }
    }
}
