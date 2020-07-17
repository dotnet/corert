// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.Text;

using BlobBuilder = System.Reflection.Metadata.BlobBuilder;
using Blob = System.Reflection.Metadata.Blob;

namespace Internal.TypeSystem.TypesDebugInfo
{
    class DebugInfoBlob
    {
        ArrayBuilder<byte> _data;

        public byte[] ToArray() => _data.ToArray();

        public uint Size() { return (uint)_data.Count; }

        public uint DWORDAlignedSize(uint size)
        {
            checked
            {
                if ((size + Size()) % 4 != 0)
                {
                    size = size + 4 - ((size + Size()) % 4);
                }
            }
            return size;
        }

        public void AlignToDWORD()
        {
            if ((Size() % 4) != 0)
            {
                uint pad = 4 - (Size() % 4);
                for (uint i = pad; i > 0; i--)
                {
                    WriteBYTE((byte)(0xF0 | i)); // Insert LF_PADX entries.
                }
            }
        }

        public void SetWORDAtBlobIndex(uint blobIndex, ushort src)
        {
            _data[(int)blobIndex] = (byte)src;
            _data[(int)blobIndex + 1] = (byte)(src >> 8);
        }

        public void SetDWORDAtBlobIndex(uint blobIndex, uint src)
        {
            _data[(int)blobIndex] = (byte)src;
            _data[(int)blobIndex + 1] = (byte)(src >> 8);
            _data[(int)blobIndex + 2] = (byte)(src >> 16);
            _data[(int)blobIndex + 3] = (byte)(src >> 24);
        }

        public void WriteBYTE(byte src)
        {
            _data.Add(src);
        }

        public void WriteWORD(ushort src)
        {
            _data.Add((byte)src);
            _data.Add((byte)(src >> 8));
        }

        public void WriteDWORD(uint src)
        {
            _data.Add((byte)src);
            _data.Add((byte)(src >> 8));
            _data.Add((byte)(src >> 16));
            _data.Add((byte)(src >> 24));
        }

        public void WriteString(Utf8String utf8String)
        {
            _data.Append(utf8String.UnderlyingArray);
            _data.Add(0);
        }

        public void WriteString(string str)
        {
            WriteString(new Utf8String(str));
        }

        public void WriteBuffer(byte[] data)
        {
            _data.Append(data);
        }

        public void WriteBuffer(DebugInfoBlob debugInfoBlob)
        {
            _data.Append(debugInfoBlob._data);
        }

        public void WriteBuffer(BlobBuilder blobBuilder)
        {
            foreach (Blob blob in blobBuilder.GetBlobs())
            {
                ArraySegment<byte> byteChunk = blob.GetBytes();
                this.WriteBuffer(byteChunk.Array, byteChunk.Offset, byteChunk.Count);
            }
        }

        public void WriteBuffer(byte[] data, int index, int length)
        {
            _data.Append(data, index, length);
        }

        public void WriteQWORD(ulong src)
        {
            _data.Add((byte)src);
            _data.Add((byte)(src >> 8));
            _data.Add((byte)(src >> 16));
            _data.Add((byte)(src >> 24));
            _data.Add((byte)(src >> 32));
            _data.Add((byte)(src >> 40));
            _data.Add((byte)(src >> 48));
            _data.Add((byte)(src >> 56));
        }

        public static uint StringLengthEncoded(Utf8String str)
        {
            return checked((uint)str.Length + 1);
        }
    }
    class DebugInfoWriter
    {
        enum LeafKind : ushort
        {
            // values used for type records
            LF_VTSHAPE = 0x000a,
            LF_POINTER = 0x1002,
            LF_PROCEDURE = 0x1008,
            LF_MFUNCTION = 0x1009,
            LF_ARGLIST = 0x1201,
            LF_FIELDLIST = 0x1203,
            LF_BCLASS = 0x1400,
            LF_INDEX = 0x1404,
            LF_VFUNCTAB = 0x1409,
            LF_ENUMERATE = 0x1502,
            LF_ARRAY = 0x1503,
            LF_CLASS = 0x1504,
            LF_STRUCTURE = 0x1505,
            LF_ENUM = 0x1507,
            LF_MEMBER = 0x150d,
            LF_STATICMEMBER = 0x150e,
            LF_MFUNC_ID = 0x1602,    // member func ID

            // values used for numeric leafs
            LF_CHAR = 0x8000,
            LF_SHORT = 0x8001,
            LF_LONG = 0x8003,
            LF_ULONG = 0x8004,
            LF_QUADWORD = 0x8009
        };

        enum LF_CLASS_Properties : ushort
        {
            None = 0x0000,
            ForwardReference = 0x0080
        }

        enum CV_Visibility : ushort
        {
            Private = 0x1,
            Protected = 0x2,
            Public = 0x3
        }

        class TypeRecordsBlob : DebugInfoBlob
        {
            uint _currentTypeIndex = 0x1000;

            public uint GetNextTypeIndex()
            {
                Debug.Assert((Size() % 4) == 0);
                return _currentTypeIndex++;
            }

            public void WriteCV_Visibility(CV_Visibility src)
            {
                WriteWORD((ushort)src);
            }

            public void WriteLeafKind(LeafKind src)
            {
                WriteWORD((ushort)src);
            }

            public void WriteLF_CLASS_Properties(LF_CLASS_Properties src)
            {
                WriteWORD((ushort)src);
            }

            public void WriteNumericLeaf(ulong value)
            {
                long signedValue = (long)value;
                if (signedValue < 0)
                {
                    if (signedValue >= -0x80)
                    {
                        WriteLeafKind(LeafKind.LF_CHAR);
                        WriteBYTE((byte)value);
                    }
                    else if (signedValue >= -0x8000)
                    {
                        WriteLeafKind(LeafKind.LF_SHORT);
                        WriteWORD((ushort)value);
                    }
                    else if (signedValue >= -0x80000000)
                    {
                        WriteLeafKind(LeafKind.LF_LONG);
                        WriteDWORD((uint)value);
                    }
                    else
                    {
                        WriteLeafKind(LeafKind.LF_QUADWORD);
                        WriteQWORD(value);
                    }
                }
                else
                {
                    if (value < 0x8000)
                    {
                        WriteWORD((ushort)value);
                    }
                    else if (value <= 0x7FFFFFFF)
                    {
                        WriteLeafKind(LeafKind.LF_LONG);
                        WriteDWORD((uint)value);
                    }
                    else if (value <= 0xFFFFFFFF)
                    {
                        WriteLeafKind(LeafKind.LF_ULONG);
                        WriteDWORD((uint)value);
                    }
                    else
                    {
                        WriteLeafKind(LeafKind.LF_QUADWORD);
                        WriteQWORD(value);
                    }
                }
            }

            public void WriteNumericLeaf(long value)
            {
                if (value < 0)
                {
                    if (value >= -0x80)
                    {
                        WriteLeafKind(LeafKind.LF_CHAR);
                        WriteBYTE((byte)value);
                    }
                    else if (value >= -0x8000)
                    {
                        WriteLeafKind(LeafKind.LF_SHORT);
                        WriteWORD((ushort)value);
                    }
                    else if (value >= -0x80000000)
                    {
                        WriteLeafKind(LeafKind.LF_LONG);
                        WriteDWORD((uint)value);
                    }
                    else
                    {
                        WriteLeafKind(LeafKind.LF_QUADWORD);
                        WriteQWORD((ulong)value);
                    }
                }
                else
                {
                    if (value < 0x8000)
                    {
                        WriteWORD((ushort)value);
                    }
                    else if (value <= 0x7FFFFFFF)
                    {
                        WriteLeafKind(LeafKind.LF_LONG);
                        WriteDWORD((uint)value);
                    }
                    else if (value <= 0xFFFFFFFF)
                    {
                        WriteLeafKind(LeafKind.LF_ULONG);
                        WriteDWORD((uint)value);
                    }
                    else
                    {
                        WriteLeafKind(LeafKind.LF_QUADWORD);
                        WriteQWORD((ulong)value);
                    }
                }
            }
            public static uint NumericLeafSize(long value)
            {
                if (value < 0)
                {
                    if (value >= -0x80)
                        return 2 + 1;
                    else if (value >= -0x8000)
                        return 2 + 2;
                    else if (value >= -0x80000000L)
                        return 2 + 4;
                    else
                        return 2 + 8;
                }
                else
                {
                    if (value < 0x8000)
                        return 2;
                    else if (value <= 0x7FFFFFFF || value <= 0xFFFFFFFF)
                        return 2 + 4;
                    else
                        return 2 + 8;
                }
            }

            public static uint NumericLeafSize(ulong value)
            {
                long signedValue = (long)value;
                if (signedValue < 0)
                {
                    if (signedValue >= -0x80)
                        return 2 + 1;
                    else if (signedValue >= -0x8000)
                        return 2 + 2;
                    else if (signedValue >= -0x80000000L)
                        return 2 + 4;
                    else
                        return 2 + 8;
                }
                else
                {
                    if (value < 0x8000)
                        return 2;
                    else if (value <= 0x7FFFFFFF || value <= 0xFFFFFFFF)
                        return 2 + 4;
                    else
                        return 2 + 8;
                }
            }
        }


        TypeRecordsBlob _blob = new TypeRecordsBlob();
        uint _tiVTShapePointer;

        public DebugInfoWriter()
        {
            // Write header
            _blob.WriteBYTE(0x04);
            _blob.WriteBYTE(0x00);
            _blob.WriteBYTE(0x00);
            _blob.WriteBYTE(0x00);

            // Write out vtable shape pointer. Various other contents of this file will refer to it.
            _tiVTShapePointer = EmitVFuncTableShapeDebugType();
        }

        private struct FieldListInProgress
        {
            public uint TypeIndexOfFieldList;
            public uint BlobOffsetCurrentFieldListChunk;
            public ushort FieldsCount;
        }

        public void VerifyBlobEligibleToBeBetweenRecords()
        {
            Debug.Assert((_blob.Size() % 4) == 0);
        }

        private FieldListInProgress StartFieldList()
        {
            Debug.Assert((_blob.Size() % 4) == 0);
            FieldListInProgress fieldListInProgress = new FieldListInProgress();
            fieldListInProgress.BlobOffsetCurrentFieldListChunk = _blob.Size();
            fieldListInProgress.TypeIndexOfFieldList = _blob.GetNextTypeIndex();
            fieldListInProgress.FieldsCount = 0;
            _blob.WriteWORD(0);
            _blob.WriteLeafKind(LeafKind.LF_FIELDLIST);

            return fieldListInProgress;
        }

        private void FinalizeFieldList(FieldListInProgress fieldListInProgress)
        {
            ushort length = checked((ushort)(_blob.Size() - fieldListInProgress.BlobOffsetCurrentFieldListChunk - 2));
            _blob.SetWORDAtBlobIndex(fieldListInProgress.BlobOffsetCurrentFieldListChunk, length);
        }

        private void ExtendFieldList(ref FieldListInProgress fieldListInProgress, uint newDataLength, out bool mustSkipEmission)
        {
            checked
            {
                if (fieldListInProgress.FieldsCount == 0xFFFF)
                {
                    mustSkipEmission = true;
                    return;
                }

                mustSkipEmission = false;

                fieldListInProgress.FieldsCount++;
                if ((_blob.Size() + newDataLength + 11/* size of LF_INDEX + maximum possible padding*/ - fieldListInProgress.BlobOffsetCurrentFieldListChunk) > 0xFF00)
                {
                    Debug.Assert((_blob.Size() % 4) == 0);

                    // Add LF_INDEX record to push forward
                    _blob.WriteLeafKind(LeafKind.LF_INDEX);
                    _blob.WriteWORD(0); // pad0
                    uint newFieldListTypeIndex = _blob.GetNextTypeIndex();
                    _blob.WriteDWORD(newFieldListTypeIndex);
                    FinalizeFieldList(fieldListInProgress);

                    Debug.Assert((_blob.Size() % 4) == 0);
                    fieldListInProgress.BlobOffsetCurrentFieldListChunk = _blob.Size();
                    _blob.WriteWORD(0);
                    _blob.WriteLeafKind(LeafKind.LF_FIELDLIST);
                }
            }
        }

        private void EmitBaseClass(ref FieldListInProgress fieldListInProgress, uint baseClassIndex)
        {
            Debug.Assert((_blob.Size() % 4) == 0);
            bool mustSkipEmission;
            ExtendFieldList(ref fieldListInProgress, 8 + TypeRecordsBlob.NumericLeafSize(0), out mustSkipEmission);
            if (mustSkipEmission)
                return;

            _blob.WriteLeafKind(LeafKind.LF_BCLASS);
            _blob.WriteCV_Visibility(CV_Visibility.Public);
            _blob.WriteDWORD(baseClassIndex);
            _blob.WriteNumericLeaf(0);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
        }

        private void EmitDataMember(ref FieldListInProgress fieldListInProgress, uint type, int offset, Utf8String name)
        {
            Debug.Assert((_blob.Size() % 4) == 0);

            bool isStaticField = (uint)offset == 0xFFFFFFFF;

            bool mustSkipEmission;
            uint recordSize = 8 + (isStaticField ? 0 : TypeRecordsBlob.NumericLeafSize(offset)) + DebugInfoBlob.StringLengthEncoded(name);
            ExtendFieldList(ref fieldListInProgress, recordSize, out mustSkipEmission);
            if (mustSkipEmission)
                return;

            _blob.WriteLeafKind(isStaticField ? LeafKind.LF_STATICMEMBER : LeafKind.LF_MEMBER);
            _blob.WriteCV_Visibility(CV_Visibility.Public);
            _blob.WriteDWORD(type);

            if (!isStaticField)
                _blob.WriteNumericLeaf(offset);

            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
        }


        private void EmitEnumerate(ref FieldListInProgress fieldListInProgress, ulong value, Utf8String name)
        {
            Debug.Assert((_blob.Size() % 4) == 0);
            bool mustSkipEmission;
            uint recordSize = 4 + TypeRecordsBlob.NumericLeafSize(value) + DebugInfoBlob.StringLengthEncoded(name);
            ExtendFieldList(ref fieldListInProgress, recordSize, out mustSkipEmission);
            if (mustSkipEmission)
                return;

            _blob.WriteLeafKind(LeafKind.LF_ENUMERATE);
            _blob.WriteCV_Visibility(CV_Visibility.Public);
            _blob.WriteNumericLeaf(value);
            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
        }

        private uint EmitVFuncTableShapeDebugType()
        {
            Debug.Assert((_blob.Size() % 4) == 0);
            uint vFuncTableShapeTypeIndex = _blob.GetNextTypeIndex();
            uint recordSize = 10;
            _blob.WriteWORD((ushort)(_blob.DWORDAlignedSize(recordSize) - 2));
            _blob.WriteLeafKind(LeafKind.LF_VTSHAPE);
            _blob.WriteWORD(0);
            _blob.WriteDWORD(0);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
            return vFuncTableShapeTypeIndex;
        }

        private void EmitVFuncTab(ref FieldListInProgress fieldListInProgress)
        {
            Debug.Assert((_blob.Size() % 4) == 0);
            bool mustSkipEmission;
            uint recordSize = 8;
            ExtendFieldList(ref fieldListInProgress, recordSize, out mustSkipEmission);
            if (mustSkipEmission)
                return;

            _blob.WriteLeafKind(LeafKind.LF_VFUNCTAB);
            _blob.WriteWORD(0);
            _blob.WriteDWORD(_tiVTShapePointer);
            VerifyBlobEligibleToBeBetweenRecords();
        }

        public uint GetClassTypeIndex(ClassTypeDescriptor classTypeDescriptor)
        {
            FieldListInProgress fieldList = default(FieldListInProgress);
            if (classTypeDescriptor.BaseClassId != 0)
            {
                fieldList = StartFieldList();
                EmitBaseClass(ref fieldList, classTypeDescriptor.BaseClassId);
                FinalizeFieldList(fieldList);
            }

            uint classTypeIndex = _blob.GetNextTypeIndex();
            Utf8String name = new Utf8String(classTypeDescriptor.Name);
            uint recordSize = 20 + DebugInfoBlob.StringLengthEncoded(name) + TypeRecordsBlob.NumericLeafSize(0) /*size of length */;
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(classTypeDescriptor.IsStruct != 0 ? LeafKind.LF_STRUCTURE : LeafKind.LF_CLASS);
            _blob.WriteWORD(fieldList.FieldsCount);
            _blob.WriteLF_CLASS_Properties(LF_CLASS_Properties.ForwardReference);
            _blob.WriteDWORD(fieldList.TypeIndexOfFieldList);
            _blob.WriteDWORD(0); // Derivation list is not filled in here
            _blob.WriteDWORD(0); // No vtable shape
            _blob.WriteNumericLeaf(0);
            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();

            return classTypeIndex;
        }

        public uint GetCompleteClassTypeIndex(ClassTypeDescriptor classTypeDescriptor, ClassFieldsTypeDescriptor classFieldsTypeDescriptior,
                                              DataFieldDescriptor[] fields, StaticDataFieldDescriptor[] statics)
        {
            FieldListInProgress fieldList = default(FieldListInProgress);
            if ((classTypeDescriptor.BaseClassId != 0) || (fields != null && fields.Length > 0) || (classTypeDescriptor.IsStruct == 0))
            {
                fieldList = StartFieldList();
                if (classTypeDescriptor.BaseClassId != 0)
                    EmitBaseClass(ref fieldList, classTypeDescriptor.BaseClassId);

                if (classTypeDescriptor.IsStruct == 0)
                    EmitVFuncTab(ref fieldList);

                if (fields != null)
                {
                    foreach (DataFieldDescriptor field in fields)
                    {
                        EmitDataMember(ref fieldList, field.FieldTypeIndex, (int)field.Offset, new Utf8String(field.Name));
                    }
                }
                FinalizeFieldList(fieldList);
            }

            uint classTypeIndex = _blob.GetNextTypeIndex();
            Utf8String name = new Utf8String(classTypeDescriptor.Name);
            uint recordSize = 20 + DebugInfoBlob.StringLengthEncoded(name) + TypeRecordsBlob.NumericLeafSize(classFieldsTypeDescriptior.Size) /*size of length */;
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(classTypeDescriptor.IsStruct != 0 ? LeafKind.LF_STRUCTURE : LeafKind.LF_CLASS);
            _blob.WriteWORD(fieldList.FieldsCount);
            _blob.WriteLF_CLASS_Properties(LF_CLASS_Properties.None);
            _blob.WriteDWORD(fieldList.TypeIndexOfFieldList);
            _blob.WriteDWORD(0); // Derivation list is not filled in here
            _blob.WriteDWORD(_tiVTShapePointer); // No vtable shape
            _blob.WriteNumericLeaf(classFieldsTypeDescriptior.Size);
            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
            return classTypeIndex;
        }

        public uint GetEnumTypeIndex(EnumTypeDescriptor enumTypeDescriptor, EnumRecordTypeDescriptor[] enumerates)
        {
            checked
            {
                FieldListInProgress fieldList = default(FieldListInProgress);
                if ((enumerates != null && enumerates.Length > 0))
                {
                    fieldList = StartFieldList();
                    foreach (EnumRecordTypeDescriptor enumerate in enumerates)
                    {
                        EmitEnumerate(ref fieldList, enumerate.Value, new Utf8String(enumerate.Name));
                    }
                    FinalizeFieldList(fieldList);
                }

                if (enumerates != null)
                    Debug.Assert(checked((int)enumTypeDescriptor.ElementCount == enumerates.Length));
                if (enumerates == null)
                    Debug.Assert(enumTypeDescriptor.ElementCount == 0);

                uint enumTypeIndex = _blob.GetNextTypeIndex();
                Utf8String name = new Utf8String(enumTypeDescriptor.Name);
                uint recordSize = 16 + DebugInfoBlob.StringLengthEncoded(name);
                _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
                _blob.WriteLeafKind(LeafKind.LF_ENUM);
                _blob.WriteWORD(fieldList.FieldsCount);
                _blob.WriteWORD(0);
                _blob.WriteDWORD((uint)enumTypeDescriptor.ElementType);
                _blob.WriteDWORD(fieldList.TypeIndexOfFieldList);
                _blob.WriteString(name);
                _blob.AlignToDWORD();
                VerifyBlobEligibleToBeBetweenRecords();
                return enumTypeIndex;
            }
        }

        public uint GetArgListTypeDescriptor(uint[] arguments)
        {
            uint argListTypeIndex = _blob.GetNextTypeIndex();
            ushort recordSizeEmit;
            uint argumentListEmit;

            try
            {
                checked
                {
                    uint recordSize = (ushort)(8 + (4 * arguments.Length));
                    recordSizeEmit = checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2));
                    argumentListEmit = (uint)arguments.Length;
                }
            }
            catch (OverflowException)
            {
                return 0;
            }

            _blob.WriteWORD(recordSizeEmit); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(LeafKind.LF_ARGLIST);
            _blob.WriteDWORD(argumentListEmit);
            foreach (uint argType in arguments)
            {
                _blob.WriteDWORD(argType);
            }
            VerifyBlobEligibleToBeBetweenRecords();
            return argListTypeIndex;
        }

        public uint GetMemberFunctionTypeIndex(MemberFunctionTypeDescriptor memberDescriptor, uint[] arguments)
        {
            uint argumentList = GetArgListTypeDescriptor(arguments);
            if (argumentList == 0)
                return 0;

            uint memberFunctionTypeIndex = _blob.GetNextTypeIndex();
            uint recordSize = 28;
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(LeafKind.LF_MFUNCTION);
            _blob.WriteDWORD(memberDescriptor.ReturnType);
            _blob.WriteDWORD(memberDescriptor.ContainingClass);
            _blob.WriteDWORD(memberDescriptor.TypeIndexOfThisPointer);
            _blob.WriteBYTE(checked((byte)memberDescriptor.CallingConvention));
            _blob.WriteBYTE(0);
            _blob.WriteWORD(checked((ushort)arguments.Length));
            _blob.WriteDWORD(argumentList);
            _blob.WriteDWORD(memberDescriptor.ThisAdjust);
            VerifyBlobEligibleToBeBetweenRecords();
            return memberFunctionTypeIndex;
        }

        public uint GetMemberFunctionId(MemberFunctionIdTypeDescriptor memberIdDescriptor)
        {
            uint memberFunctionIdTypeIndex = _blob.GetNextTypeIndex();

            Utf8String name = new Utf8String(memberIdDescriptor.Name);
            uint recordSize = 12 + DebugInfoBlob.StringLengthEncoded(name);
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(LeafKind.LF_MFUNC_ID);
            _blob.WriteDWORD(memberIdDescriptor.ParentClass);
            _blob.WriteDWORD(memberIdDescriptor.MemberFunction);
            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
            return memberFunctionIdTypeIndex;
        }

        public uint GetPointerTypeIndex(PointerTypeDescriptor pointerTypeDescriptor)
        {
            uint pointerTypeIndex = _blob.GetNextTypeIndex();
            uint recordSize = 12;
            uint attr = 0;
            if (pointerTypeDescriptor.IsReference != 0)
                attr |= 0x20;
            if (pointerTypeDescriptor.IsConst != 0)
                attr |= 0x400;
            if (pointerTypeDescriptor.Is64Bit != 0)
                attr |= 12;
            else
                attr |= 10;

            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(LeafKind.LF_POINTER);
            _blob.WriteDWORD(pointerTypeDescriptor.ElementType);
            _blob.WriteDWORD(attr);
            VerifyBlobEligibleToBeBetweenRecords();
            return pointerTypeIndex;
        }

        public uint GetSimpleArrayTypeIndex(uint elementType, uint elementSize)
        {
            uint simpleArrayTypeIndex = _blob.GetNextTypeIndex();

            TypeRecordsBlob simpleArrayDataBlob = new TypeRecordsBlob();
            simpleArrayDataBlob.WriteLeafKind(LeafKind.LF_ARRAY);
            simpleArrayDataBlob.WriteDWORD(elementType);
            simpleArrayDataBlob.WriteDWORD((uint)PrimitiveTypeDescriptor.TYPE_ENUM.T_INT4);
            simpleArrayDataBlob.WriteNumericLeaf(elementSize);
            simpleArrayDataBlob.WriteString("");

            uint recordSize = simpleArrayDataBlob.Size() + 2;
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteBuffer(simpleArrayDataBlob);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
            return simpleArrayTypeIndex;
        }

        public uint GetArrayTypeIndex(ClassTypeDescriptor classDescriptor, ArrayTypeDescriptor arrayTypeDescriptor, int targetPointerSize)
        {
            uint simpleArrayDebugType = GetSimpleArrayTypeIndex(arrayTypeDescriptor.ElementType, arrayTypeDescriptor.Size);

            FieldListInProgress fieldList = default(FieldListInProgress);

            fieldList = StartFieldList();
            EmitBaseClass(ref fieldList, classDescriptor.BaseClassId);
            EmitDataMember(ref fieldList, (uint)PrimitiveTypeDescriptor.TYPE_ENUM.T_INT4, targetPointerSize, new Utf8String("count"));
            int nextOffset = targetPointerSize * 2;
            if (arrayTypeDescriptor.IsMultiDimensional != 0)
            {
                for (uint i = 0; i < arrayTypeDescriptor.Rank; i++)
                {
                    EmitDataMember(ref fieldList, (uint)PrimitiveTypeDescriptor.TYPE_ENUM.T_INT4, nextOffset, new Utf8String("length" + i.ToStringInvariant()));
                    nextOffset += 4;
                }
                for (uint i = 0; i < arrayTypeDescriptor.Rank; i++)
                {
                    EmitDataMember(ref fieldList, (uint)PrimitiveTypeDescriptor.TYPE_ENUM.T_INT4, nextOffset, new Utf8String("bounds" + i.ToStringInvariant()));
                    nextOffset += 4;
                }
            }

            EmitDataMember(ref fieldList, simpleArrayDebugType, nextOffset, new Utf8String("values"));

            FinalizeFieldList(fieldList);

            uint classTypeIndex = _blob.GetNextTypeIndex();
            Utf8String name = new Utf8String(classDescriptor.Name);
            uint recordSize = 20 + DebugInfoBlob.StringLengthEncoded(name) + TypeRecordsBlob.NumericLeafSize(targetPointerSize) /*size of length */;
            _blob.WriteWORD(checked((ushort)(_blob.DWORDAlignedSize(recordSize) - 2))); // don't include size of 'length' in 'length'
            _blob.WriteLeafKind(classDescriptor.IsStruct != 0 ? LeafKind.LF_STRUCTURE : LeafKind.LF_CLASS);
            _blob.WriteWORD(fieldList.FieldsCount);
            _blob.WriteLF_CLASS_Properties(LF_CLASS_Properties.None);
            _blob.WriteDWORD(fieldList.TypeIndexOfFieldList);
            _blob.WriteDWORD(0); // Derivation list is not filled in here
            _blob.WriteDWORD(_tiVTShapePointer); // No vtable shape
            _blob.WriteNumericLeaf(targetPointerSize);
            _blob.WriteString(name);
            _blob.AlignToDWORD();
            VerifyBlobEligibleToBeBetweenRecords();
            return classTypeIndex;
        }

        public DebugInfoBlob GetRawBlob()
        {
            return _blob;
        }
    }
}
