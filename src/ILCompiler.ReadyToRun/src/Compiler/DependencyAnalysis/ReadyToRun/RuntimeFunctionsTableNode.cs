// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class RuntimeFunctionsTableNode : HeaderTableNode
    {
        private readonly List<MethodWithGCInfo> _methodNodes;

        public RuntimeFunctionsTableNode(TargetDetails target)
            : base(target)
        {
            _methodNodes = new List<MethodWithGCInfo>();
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunRuntimeFunctionsTable");
        }

        public int Add(MethodWithGCInfo method)
        {
            _methodNodes.Add(method);
            return _methodNodes.Count - 1;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder runtimeFunctionsBuilder = new ObjectDataBuilder(factory, relocsOnly);

            // Add the symbol representing this object node
            runtimeFunctionsBuilder.AddSymbol(this);

            foreach (MethodWithGCInfo method in _methodNodes)
            {
                // StartOffset of the runtime function
                runtimeFunctionsBuilder.EmitReloc(method, RelocType.IMAGE_REL_BASED_ADDR32NB, delta: 0);
                if (!relocsOnly && Target.Architecture == TargetArchitecture.X64)
                {
                    // On Amd64, the 2nd word contains the EndOffset of the runtime function
                    int methodLength = method.GetData(factory, relocsOnly).Data.Length;
                    runtimeFunctionsBuilder.EmitReloc(method, RelocType.IMAGE_REL_BASED_ADDR32NB, delta: methodLength);
                }
                // Emit the GC info RVA
                runtimeFunctionsBuilder.EmitReloc(method.GCInfoNode, RelocType.IMAGE_REL_BASED_ADDR32NB);
            }

            return runtimeFunctionsBuilder.ToObjectData();
        }

        protected override int ClassCode => -855231428;
    }
}
