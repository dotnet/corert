// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILToNative.DependencyAnalysisFramework;
using System.Diagnostics;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    public abstract class AssemblyStubNode : ObjectNode, ISymbolNode
    {
        public AssemblyStubNode()
        {
        }

        public ISymbolNode Symbol
        {
            get
            {
                return this;
            }
        }

        public override string Section
        {
            get
            {
                return "text";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public abstract string MangledName
        {
            get;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            switch (factory.Target.Architecture)
            {
                case TargetArchitecture.X64:
                    X64.X64Emitter x64Emitter = new X64.X64Emitter(factory);
                    EmitCode(factory, ref x64Emitter, relocsOnly);
                    x64Emitter.Builder.Alignment = factory.Target.MinimumFunctionAlignment;
                    x64Emitter.Builder.DefinedSymbols.Add(this);
                    return x64Emitter.Builder.ToObjectData();
                default:
                    throw new NotImplementedException();
            }
        }

        protected abstract void EmitCode(NodeFactory factory, ref X64.X64Emitter instructionEncoder, bool relocsOnly);
    }
}
