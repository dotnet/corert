﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public abstract partial class JumpStubNode : AssemblyStubNode
    {
        private ISymbolNode _target;

        public ISymbolNode Target
        {
            get
            {
                return _target;
            }
        }
        
        public JumpStubNode(ISymbolNode target)
        {
            _target = target;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        protected internal override int ClassCode => 737788182;
    }
}
