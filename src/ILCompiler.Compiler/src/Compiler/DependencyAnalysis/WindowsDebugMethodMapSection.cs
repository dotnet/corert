// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugMethodSignatureMapSection : ObjectNode, ISymbolDefinitionNode
    {
        public WindowsDebugMethodSignatureMapSection()
        {
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".dbgmethodsignaturemap", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugMethodSignatureMapSectionNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        struct EmittedMethodWithILToken : IComparable<EmittedMethodWithILToken>
        {
            public EmittedMethodWithILToken(IMethodBodyNode emittedMethod, uint ilTokenRid)
            {
                EmittedMethod = emittedMethod;
                IlTokenRid = ilTokenRid;
            }

            public IMethodBodyNode EmittedMethod;
            public uint IlTokenRid;

            int IComparable<EmittedMethodWithILToken>.CompareTo(EmittedMethodWithILToken other)
            {
                if (IlTokenRid == other.IlTokenRid)
                    return 0;
                if (IlTokenRid < other.IlTokenRid)
                    return -1;
                return 1;
            }
        }

        //
        // returns the DEBUG_S_FUNC_MDTOKEN_MAP subsection as a byte array
        // DEBUG_S_FUNC_MDTOKEN_MAP subsection contains method RVA to mdToken mapping
        // 
        // contents of subsection:
        // offset 0,     4   bytes: count of entries in the map
        // offset 4,     8*N bytes: 4 byte RVA + 4 byte 'offset' relative to the start of 'method data'
        // offset 4+8*N, *   bytes: all method data packed sequentially with no padding. method data consists of
        //                          1 byte 'count' of generic parameters, 3 bytes of method's rid and 'count'
        //                          variable sized ECMA formatted TypeSpec signatures for each generic parameter
        //
        // Compiler places the CTLToken (for a method) or the lexical funclet order (if a method has 1 or more funclets),
        // which binder uses to compute the RVA.
        //
        // all entries are sorted by 'offset' field.
        //
        // 'offset' optimization: if the method has no generic parameters, we don't need to pass in a signature
        //                        and can encode the mdToken of method in 'offset'
        //                        We do this by setting the high bit of 'offset' and then storing rid part of
        //                        token in last 3 bytes of 'offset'
        //
        internal DebugInfoBlob GetDebugMethodRVAToTokenMap(ManagedBinaryEmitter pseudoAssembly, IEnumerable<IMethodBodyNode> emittedMethods, out List<Relocation> debugRelocations)
        {
            DebugInfoBlob methodRVAToTokenMap = new DebugInfoBlob();
            DebugInfoBlob methodDataBlob = new DebugInfoBlob();
            debugRelocations = new List<Relocation>();
            BlobBuilder blobBuilder = new BlobBuilder();

            uint entryCount = 0;
            methodRVAToTokenMap.WriteDWORD(0); // Placeholder for count of entries in map. Will be udpated later.

            List<EmittedMethodWithILToken> tokenInOffsetEntries = new List<EmittedMethodWithILToken>();

            foreach (IMethodBodyNode emitted in emittedMethods)
            {
                if (!(emitted.Method.GetTypicalMethodDefinition() is Internal.TypeSystem.Ecma.EcmaMethod))
                {
                    continue;
                }

                EntityHandle methodHandle = pseudoAssembly.EmitMetadataHandleForTypeSystemEntity(emitted.Method.GetTypicalMethodDefinition());
                Debug.Assert(methodHandle.Kind == HandleKind.MemberReference);
                uint methodToken = (uint)MetadataTokens.GetToken(methodHandle);
                uint methodTokenRid = methodToken & 0xFFFFFF;

                if (!(emitted.Method.HasInstantiation || emitted.Method.OwningType.HasInstantiation))
                {
                    tokenInOffsetEntries.Add(new EmittedMethodWithILToken(emitted, methodTokenRid));
                    continue;
                }

                uint cGenericArguments = checked((uint)emitted.Method.Instantiation.Length + (uint)emitted.Method.OwningType.Instantiation.Length);

                // Debugger format does not allow the debugging of methods that have more than 255 generic parameters (spread between the type and method instantiation)
                if (cGenericArguments > 0xFF)
                    continue;

                blobBuilder.Clear();

                // write the signature for each generic parameter of class
                foreach (TypeDesc instantiationType in emitted.Method.OwningType.Instantiation)
                    pseudoAssembly.EncodeSignatureForType(instantiationType, blobBuilder);

                // write the signature for each generic parameter of the method
                foreach (TypeDesc instantiationType in emitted.Method.Instantiation)
                    pseudoAssembly.EncodeSignatureForType(instantiationType, blobBuilder);

                Add_DEBUG_S_FUNC_MDTOKEN_MAP_Entry(methodRVAToTokenMap, debugRelocations, emitted, methodDataBlob.Size(), ref entryCount);

                methodDataBlob.WriteDWORD(cGenericArguments << 24 | methodTokenRid);
                methodDataBlob.WriteBuffer(blobBuilder);
            }

            // sort tokenInOffsetEntries based on tokenInOffset
            tokenInOffsetEntries.Sort();

            foreach (EmittedMethodWithILToken emitted in tokenInOffsetEntries)
            {
                Add_DEBUG_S_FUNC_MDTOKEN_MAP_Entry(methodRVAToTokenMap, debugRelocations, emitted.EmittedMethod, emitted.IlTokenRid | 0x80000000, ref entryCount);
            }

            methodRVAToTokenMap.SetDWORDAtBlobIndex(0, entryCount); // // Update placeholder for count of entries in map
            methodRVAToTokenMap.WriteBuffer(methodDataBlob);

            return methodRVAToTokenMap;
        }

        private void Add_DEBUG_S_FUNC_MDTOKEN_MAP_Entry(DebugInfoBlob methodRVAToTokenMap, List<Relocation> debugRelocations, IMethodBodyNode method, uint methodDataOrOffsetToMethodData, ref uint entryCount)
        {
            debugRelocations.Add(new Relocation(RelocType.IMAGE_REL_BASED_ADDR32NB, checked((int)methodRVAToTokenMap.Size()), method));
            methodRVAToTokenMap.WriteDWORD(0);
            methodRVAToTokenMap.WriteDWORD(methodDataOrOffsetToMethodData);
            entryCount++;

            throw new NotImplementedException();
            //IMethodBodyNodeWithFuncletSymbols funcletSymbolsNode = method as IMethodBodyNodeWithFuncletSymbols;

            //if (funcletSymbolsNode != null)
            //{
            //    foreach (ISymbolNode funclet in funcletSymbolsNode.FuncletSymbols)
            //    {
            //        debugRelocations.Add(new Relocation(RelocType.IMAGE_REL_BASED_ADDR32NB, checked((int)methodRVAToTokenMap.Size()), funclet));
            //        methodRVAToTokenMap.WriteDWORD(0);
            //        methodRVAToTokenMap.WriteDWORD(methodDataOrOffsetToMethodData);
            //        entryCount++;
            //    }
            //}
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            List<Relocation> relocations = new List<Relocation>();
            DebugInfoBlob debugData = GetDebugMethodRVAToTokenMap(factory.WindowsDebugData.DebugPseudoAssemblySection.PseudoAssembly, factory.MetadataManager.GetCompiledMethodBodies(), out relocations);

            return new ObjectData(debugData.ToArray(), relocations.ToArray(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugMethodSignatureMapSection";
        }
    }
}
