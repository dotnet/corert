// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class RuntimeFunctionsTableNode : HeaderTableNode
    {
        List<MethodCodeNode> _methodNodes;
        
        public RuntimeFunctionsTableNode(TargetDetails target)
            : base(target)
        {
            _methodNodes = new List<MethodCodeNode>();
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunRuntimeFunctionsTable");
        }

        public int Add(MethodCodeNode method)
        {
            int methodIndex = _methodNodes.Count;
            _methodNodes.Add(method);
            return methodIndex;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder runtimeFunctionsBuilder = new ObjectDataBuilder(factory, relocsOnly);

            // Add the symbol representing this object node
            runtimeFunctionsBuilder.AddSymbol(this);

            int gcInfoGlobalOffset = (Target.Architecture == TargetArchitecture.X64 ? 3 : 2) * sizeof(int) * _methodNodes.Count;
            ArrayBuilder<byte> uniqueGCInfoBuilder = new ArrayBuilder<byte>();
            Dictionary<byte[], int> uniqueGCInfoOffsets = new Dictionary<byte[], int>(ByteArrayComparer.Instance);

            foreach (MethodCodeNode methodNode in _methodNodes)
            {
                // StartOffset of the runtime function
                runtimeFunctionsBuilder.EmitReloc(methodNode, RelocType.IMAGE_REL_BASED_ADDR32NB, delta: 0);
                if (Target.Architecture == TargetArchitecture.X64)
                {
                    // On Amd64, the 2nd word contains the EndOffset of the runtime function
                    int methodLength = methodNode.GetData(factory, relocsOnly).Data.Length;
                    runtimeFunctionsBuilder.EmitReloc(methodNode, RelocType.IMAGE_REL_BASED_ADDR32NB, delta: methodLength);
                }
                // Unify GC info of the runtime function
                byte[] gcInfo = methodNode.GCInfo;
                int gcInfoLocalOffset;
                if (!uniqueGCInfoOffsets.TryGetValue(gcInfo, out gcInfoLocalOffset))
                {
                    gcInfoLocalOffset = uniqueGCInfoBuilder.Count;
                    uniqueGCInfoBuilder.Append(gcInfo);
                    uniqueGCInfoOffsets.Add(gcInfo, gcInfoLocalOffset);
                }
                // Emit the GC info RVA
                runtimeFunctionsBuilder.EmitReloc(this, RelocType.IMAGE_REL_BASED_ADDR32NB, delta: gcInfoGlobalOffset + gcInfoLocalOffset);
            }

            // Note: this algorithm always emits the GC info "above" the runtime function
            // table. If we want to be able to separate these two tables completely, we'll
            // likely need a separate node for the GC info.
            Debug.Assert(runtimeFunctionsBuilder.CountBytes == gcInfoGlobalOffset);

            // For some weird reason, ObjectDataBuilder.EmitBytes(ArrayBuilder<byte>) is marked as internal.
            runtimeFunctionsBuilder.EmitBytes(uniqueGCInfoBuilder.ToArray());

            return runtimeFunctionsBuilder.ToObjectData();
        }

        protected override int ClassCode => -855231428;
    }
}
