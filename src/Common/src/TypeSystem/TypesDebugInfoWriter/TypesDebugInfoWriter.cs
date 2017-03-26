// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Internal.TypeSystem.TypesDebugInfo
{
    public interface ITypesDebugInfoWriter
    {
        uint GetEnumTypeIndex(EnumTypeDescriptor enumTypeDescriptor, EnumRecordTypeDescriptor[] typeRecords);

        uint GetClassTypeIndex(ClassTypeDescriptor classTypeDescriptor);

        void CompleteClassDescription(ClassTypeDescriptor classTypeDescriptor, ClassFieldsTypeDescriptior classFieldsTypeDescriptior, DataFieldDescriptor[] fields);

        uint GetVariableTypeIndex(TypeDesc type);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EnumRecordTypeDescriptor
    {
        public ulong Value;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EnumTypeDescriptor
    {
        public ulong ElementType;
        public ulong ElementCount;
        public string Name;
        public string UniqueName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClassTypeDescriptor
    {
        public int IsStruct;
        public string Name;
        public string UniqueName;
        public uint BaseClassId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DataFieldDescriptor
    {
        public uint FieldTypeIndex;
        public int Offset;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClassFieldsTypeDescriptior
    {
        public int Size;
        public int FieldsCount;
    }
}
