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

        uint GetCompleteClassTypeIndex(ClassTypeDescriptor classTypeDescriptor, ClassFieldsTypeDescriptor classFieldsTypeDescriptor, DataFieldDescriptor[] fields);

        uint GetArrayTypeIndex(ClassTypeDescriptor classDescriptor, ArrayTypeDescriptor arrayTypeDescriprtor);

        string GetMangledName(TypeDesc type);
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
        public uint ElementType;
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
        public ulong Offset;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClassFieldsTypeDescriptor
    {
        public ulong Size;
        public int FieldsCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ArrayTypeDescriptor
    {
        public uint Rank;
        public uint ElementType;
        public uint Size;
        public int IsMultiDimensional;
    }
}
