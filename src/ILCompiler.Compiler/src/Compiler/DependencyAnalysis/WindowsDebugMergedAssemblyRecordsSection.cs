// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

using Internal.Text;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugMergedAssembliesSection : ObjectNode, ISymbolDefinitionNode
    {
        private MergedAssemblyRecords _mergedAssemblies;

        public WindowsDebugMergedAssembliesSection(MergedAssemblyRecords mergedAssemblies)
        {
            _mergedAssemblies = mergedAssemblies;
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".dbgmergedassemblyrecords", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public override int ClassCode => -1250136545;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            DebugInfoBlob debugBlob = new DebugInfoBlob();
            foreach (MergedAssemblyRecord record in _mergedAssemblies.MergedAssemblies)
                record.Encode(debugBlob);

            byte [] _pdbBlob = debugBlob.ToArray();
            Debug.Assert(_pdbBlob.Length > 0);
            
            return new ObjectData(_pdbBlob, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugMergedAssemblyRecordsSection";
        }
    }
}
