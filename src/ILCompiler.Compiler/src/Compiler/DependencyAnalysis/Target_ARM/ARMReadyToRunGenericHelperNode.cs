﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ILCompiler.DependencyAnalysis.ARM;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    partial class ReadyToRunGenericHelperNode
    {       
        protected sealed override void EmitCode(NodeFactory factory, ref ARMEmitter encoder, bool relocsOnly)
        {
            var NYI_Assert = new ExternSymbolNode("NYI_Assert");
            encoder.EmitJMP(NYI_Assert);
        }
    }
}
