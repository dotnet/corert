// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysis.X64;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial class JumpStubNode : AssemblyStubNode
    {
        private ISymbolNode _target;

        public JumpStubNode(ISymbolNode target)
        {
            _target = target;
        }

        public override string MangledName
        {
            get
            {
                return "jmpstub_" + _target.MangledName;
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
