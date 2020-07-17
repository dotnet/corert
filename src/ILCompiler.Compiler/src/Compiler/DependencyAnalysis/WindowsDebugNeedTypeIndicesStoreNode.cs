// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    public class WindowsDebugNeedTypeIndicesStoreNode : ObjectNode, ISymbolDefinitionNode
    {
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool IsShareable => true;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugNeedTypeIndicesStoreNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (!relocsOnly)
            {
                UserDefinedTypeDescriptor userDefinedTypeDescriptor = factory.WindowsDebugData.UserDefinedTypeDescriptor;
                if (userDefinedTypeDescriptor != null)
                {
                    List<TypeDesc> typesThatNeedIndices = new List<TypeDesc>();
                    foreach (TypeDesc type in factory.MetadataManager.GetTypesWithEETypes())
                    {
                        if (!type.IsGenericDefinition)
                        {
                            typesThatNeedIndices.Add(type);
                        }
                    }

                    typesThatNeedIndices.Sort(new TypeSystemComparer().Compare);

                    foreach (TypeDesc type in typesThatNeedIndices)
                    {
                        // Force creation of type descriptors for _ALL_ EETypes
                        userDefinedTypeDescriptor.GetVariableTypeIndex(type);
                    }
                }
            }

            // There isn't actually any data in this node. Its simply an ObjectNode as that allows this 
            // function to be executed in a defined time during object emission. This does imply embedding a bit of data
            // into the object file, but the linker should be able to strip it out of the final file, and even if its in the final file
            // it won't cost significant size.

            return new ObjectData(new byte[1], Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugNeedTypeIndicesStore";
        }
    }
}
