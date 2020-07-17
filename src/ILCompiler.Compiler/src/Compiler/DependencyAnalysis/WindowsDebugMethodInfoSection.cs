// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Internal.TypeSystem.Ecma;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugMethodInfoSection : ObjectNode, ISymbolDefinitionNode
    {
        private MergedAssemblyRecords _mergedAssemblies;

        public WindowsDebugMethodInfoSection(MergedAssemblyRecords mergedAssemblies)
        {
            _mergedAssemblies = mergedAssemblies;
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".mdinfo", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        private uint AdjustIndex(uint assemblyIndex, uint corLibIndex)
        {
            if (assemblyIndex == 0x7FFFFFFF)
                return corLibIndex;
            if (assemblyIndex < corLibIndex)
                return assemblyIndex;
            return assemblyIndex + 1;
        }

        //
        // returns the DebugInfoBlob containing the method token to virtual method slot mapping

        // .mdinfo format
        // offset 0,     4   bytes: version number
        // offset 4,     4   bytes: count of assemblies
        // 
        // for each assembly
        // offset 0,     4   bytes: count of methods with a virtual method slot
        //
        // for each method 
        // offset 0,     4   bytes: method def token (as they are in the input assembly)
        // offset 4,     2   bytes: length of the per method information [ currently always 4 ]
        // offset 6,     4   bytes: virtual slot number
        // methods are sorted by their method def token

        internal DebugInfoBlob GetDebugMethodInfoMap(NodeFactory factory)
        {
            Dictionary<EcmaAssembly, uint> originalAssemblyOrder = new Dictionary<EcmaAssembly, uint>();
            List<SortedDictionary<uint, int>> moduleMethods = new List<SortedDictionary<uint, int>>();

            // re-construct orginal assembly input order
            foreach (MergedAssemblyRecord mergedAssembly in _mergedAssemblies.MergedAssemblies)
            {
                uint assemblyIndex = AdjustIndex(mergedAssembly.AssemblyIndex, _mergedAssemblies.CorLibIndex);
                originalAssemblyOrder.Add(mergedAssembly.Assembly, assemblyIndex);
                moduleMethods.Add(new SortedDictionary<uint, int>());
            }

            foreach (TypeDesc type in factory.MetadataManager.GetTypesWithConstructedEETypes())
            {
                // skip if sealed
                if (type.IsSealed())
                    continue;

                // no generic support yet 
                if (type is EcmaType)
                {
                    EcmaType ecmaType = (EcmaType)type;
                    EcmaAssembly ecmaAssembly = (EcmaAssembly)ecmaType.EcmaModule;
                    int assemblyIndex = (int)originalAssemblyOrder[ecmaAssembly];
                    SortedDictionary<uint, int> methodList = moduleMethods[assemblyIndex];
                    foreach (MethodDesc md in type.GetAllMethods())
                    {
                        // skip non-virtual and final methods
                        if (!md.IsVirtual || md.IsFinal)
                            continue;
                        // skip generic 
                        if (md.HasInstantiation)
                            continue;
                        // method token.
                        EntityHandle methodHandle = ((EcmaMethod)md).Handle;
                        uint methodToken = (uint)MetadataTokens.GetToken(methodHandle);
                        // find virtual method slot.
                        MethodDesc declaringMethodForSlot =
                            MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(md.GetTypicalMethodDefinition());
                        int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, declaringMethodForSlot, type);
                        if (slot != -1 && !methodList.ContainsKey(methodToken))
                            methodList.Add(methodToken, slot);
                    }
                }
            }
            return ConvertToDebugInfoBlob(moduleMethods);
        }

        private DebugInfoBlob ConvertToDebugInfoBlob(List<SortedDictionary<uint, int>> assemblyMethods)
        {
            DebugInfoBlob debugInfoBlob = new DebugInfoBlob();
            // version
            debugInfoBlob.WriteDWORD(0);
            // number of assemblies
            debugInfoBlob.WriteDWORD((uint)assemblyMethods.Count);
            foreach (var methods in assemblyMethods)
            {
                debugInfoBlob.WriteDWORD((uint)methods.Count);
                foreach (var method in methods)
                {
                    // method token
                    debugInfoBlob.WriteDWORD(method.Key);
                    // method info length , now it's 4
                    debugInfoBlob.WriteWORD(4);
                    // method slot
                    debugInfoBlob.WriteDWORD((uint)method.Value);
                }
            }
            return debugInfoBlob;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
            {
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            }
            DebugInfoBlob debugData = GetDebugMethodInfoMap(factory);
            return new ObjectData(debugData.ToArray(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugMethodInfoSection";
        }

        public override int ClassCode => 513099721;
    }
}
