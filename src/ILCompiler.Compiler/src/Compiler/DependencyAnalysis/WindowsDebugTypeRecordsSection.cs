// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugTypeRecordsSection : ObjectNode, ISymbolDefinitionNode, ITypesDebugInfoWriter
    {
        DebugInfoWriter _dbgInfo; // Pointer to DebugInfoWriter used to write data
        DebugInfoWriter _dbgInfoWriter; // Pointer to DebugInfoWriter used to generate new entries
        NodeFactory _nodeFactory;

        public WindowsDebugTypeRecordsSection(DebugInfoWriter dbgInfo, NodeFactory factory)
        {
            _dbgInfoWriter = _dbgInfo = dbgInfo;
            _nodeFactory = factory;
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".debug$T", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WindowsDebugTypeRecordsSectionNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            byte[] typeRecords = _dbgInfo.GetRawBlob().ToArray();
            _dbgInfo = null; // Neuter the section so that it cannot grow any larger
            Neuter(); // Neuter the writer so that nothing else can attempt to add new types

            return new ObjectData(typeRecords, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugTypeRecordsSection";
        }

        public void Neuter()
        {
            _dbgInfoWriter = null;
        }

        uint ITypesDebugInfoWriter.GetEnumTypeIndex(EnumTypeDescriptor enumTypeDescriptor, EnumRecordTypeDescriptor[] typeRecords)
        {
            return _dbgInfoWriter.GetEnumTypeIndex(enumTypeDescriptor, typeRecords);
        }

        uint ITypesDebugInfoWriter.GetClassTypeIndex(ClassTypeDescriptor classTypeDescriptor)
        {
            return _dbgInfoWriter.GetClassTypeIndex(classTypeDescriptor);
        }

        uint ITypesDebugInfoWriter.GetCompleteClassTypeIndex(ClassTypeDescriptor classTypeDescriptor, ClassFieldsTypeDescriptor classFieldsTypeDescriptior,
                                                             DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            return _dbgInfoWriter.GetCompleteClassTypeIndex(classTypeDescriptor, classFieldsTypeDescriptior, fields, statics);
        }

        uint ITypesDebugInfoWriter.GetArrayTypeIndex(ClassTypeDescriptor classDescriptor, ArrayTypeDescriptor arrayTypeDescriptor)
        {
            return _dbgInfoWriter.GetArrayTypeIndex(classDescriptor, arrayTypeDescriptor, _nodeFactory.Target.PointerSize);
        }

        uint ITypesDebugInfoWriter.GetPointerTypeIndex(PointerTypeDescriptor pointerDescriptor)
        {
            return _dbgInfoWriter.GetPointerTypeIndex(pointerDescriptor);
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor memberDescriptor, uint[] argumentTypes)
        {
            return _dbgInfoWriter.GetMemberFunctionTypeIndex(memberDescriptor, argumentTypes);
        }

        uint ITypesDebugInfoWriter.GetMemberFunctionId(MemberFunctionIdTypeDescriptor memberIdDescriptor)
        {
            return _dbgInfoWriter.GetMemberFunctionId(memberIdDescriptor);
        }

        uint ITypesDebugInfoWriter.GetPrimitiveTypeIndex(TypeDesc type)
        {
            return PrimitiveTypeDescriptor.GetPrimitiveTypeIndex(type);
        }

        string ITypesDebugInfoWriter.GetMangledName(TypeDesc type)
        {
            return _nodeFactory.NameMangler.GetMangledTypeName(type);
        }
    }
}
