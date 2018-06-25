// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class CoreCLRReadyToRunGCInfoNode : ObjectNode, ISymbolDefinitionNode
    {
        TargetDetails _target;
        ArrayBuilder<byte> _gcInfoBuilder;
        Dictionary<byte[], int> _gcInfoToOffset;
        
        public CoreCLRReadyToRunGCInfoNode(TargetDetails target, byte[] gcInfo)
        {
            _target = target;
            _gcInfoBuilder = new ArrayBuilder<byte>();
            _gcInfo = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
        }
        
        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__GCInfo");
        }
        
        public int Add(byte[] gcInfo)
        {
            int gcInfoOffset;
            if (!_gcInfoToOffset.TryGetValue(gcInfo, out gcInfoOffset))
            {
                gcInfoOffset = _gcInfoBuilder.Count;
                _gcInfoBuilder.Append(gcInfo);
                _gcInfoToOffset.Add(gcInfo, gcInfoOffset);
            }
            return gcInfoOffset;
        }

        public int Offset => 0;

        public override bool IsShareable => false;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            return new ObjectData(
                data: _gcInfoBuilder.ToArray(),
                relocs: null,
                alignment: 1,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        protected override int ClassCode => 348729518;
    }
}
