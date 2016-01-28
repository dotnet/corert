// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: This is a generated file - do not manually edit!

#pragma warning disable 649

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Internal.Metadata.NativeFormat.Writer;
using Internal.NativeFormat;
using HandleType = Internal.Metadata.NativeFormat.HandleType;
using Debug = System.Diagnostics.Debug;

namespace Internal.Metadata.NativeFormat.Writer
{
    /// <summary>
    /// ArraySignature
    /// </summary>
    public partial class ArraySignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ArraySignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            ElementType = visitor.Visit(this, ElementType);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ArraySignature;
            if (other == null) return false;
            if (!Object.Equals(ElementType, other.ElementType)) return false;
            if (Rank != other.Rank) return false;
            if (!Sizes.SequenceEqual(other.Sizes)) return false;
            if (!LowerBounds.SequenceEqual(other.LowerBounds)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1603870787;
            hash = ((hash << 13) - (hash >> 19)) ^ (ElementType == null ? 0 : ElementType.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ Rank.GetHashCode();
            if (Sizes != null)
            {
                for (int i = 0; i < Sizes.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Sizes[i].GetHashCode();
                }
            }
            if (LowerBounds != null)
            {
                for (int i = 0; i < LowerBounds.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ LowerBounds[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(ElementType == null ||
                ElementType.HandleType == HandleType.TypeDefinition ||
                ElementType.HandleType == HandleType.TypeReference ||
                ElementType.HandleType == HandleType.TypeSpecification);
            writer.Write(ElementType);
            writer.Write(Rank);
            writer.Write(Sizes);
            writer.Write(LowerBounds);
        } // Save

        internal static ArraySignatureHandle AsHandle(ArraySignature record)
        {
            if (record == null)
            {
                return new ArraySignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ArraySignatureHandle Handle
        {
            get
            {
                return new ArraySignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord ElementType;
        public int Rank;
        public int[] Sizes;
        public int[] LowerBounds;
    } // ArraySignature

    /// <summary>
    /// ByReferenceSignature
    /// </summary>
    public partial class ByReferenceSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ByReferenceSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ByReferenceSignature;
            if (other == null) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -695627658;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
        } // Save

        internal static ByReferenceSignatureHandle AsHandle(ByReferenceSignature record)
        {
            if (record == null)
            {
                return new ByReferenceSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ByReferenceSignatureHandle Handle
        {
            get
            {
                return new ByReferenceSignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Type;
    } // ByReferenceSignature

    /// <summary>
    /// ConstantBooleanArray
    /// </summary>
    public partial class ConstantBooleanArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantBooleanArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantBooleanArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1799185487;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantBooleanArrayHandle AsHandle(ConstantBooleanArray record)
        {
            if (record == null)
            {
                return new ConstantBooleanArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantBooleanArrayHandle Handle
        {
            get
            {
                return new ConstantBooleanArrayHandle(HandleOffset);
            }
        } // Handle

        public bool[] Value;
    } // ConstantBooleanArray

    /// <summary>
    /// ConstantBooleanValue
    /// </summary>
    public partial class ConstantBooleanValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantBooleanValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantBooleanValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 842390402;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantBooleanValueHandle AsHandle(ConstantBooleanValue record)
        {
            if (record == null)
            {
                return new ConstantBooleanValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantBooleanValueHandle Handle
        {
            get
            {
                return new ConstantBooleanValueHandle(HandleOffset);
            }
        } // Handle

        public bool Value;
    } // ConstantBooleanValue

    /// <summary>
    /// ConstantByteArray
    /// </summary>
    public partial class ConstantByteArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantByteArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantByteArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -216020972;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantByteArrayHandle AsHandle(ConstantByteArray record)
        {
            if (record == null)
            {
                return new ConstantByteArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantByteArrayHandle Handle
        {
            get
            {
                return new ConstantByteArrayHandle(HandleOffset);
            }
        } // Handle

        public byte[] Value;
    } // ConstantByteArray

    /// <summary>
    /// ConstantByteValue
    /// </summary>
    public partial class ConstantByteValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantByteValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantByteValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 229129511;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantByteValueHandle AsHandle(ConstantByteValue record)
        {
            if (record == null)
            {
                return new ConstantByteValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantByteValueHandle Handle
        {
            get
            {
                return new ConstantByteValueHandle(HandleOffset);
            }
        } // Handle

        public byte Value;
    } // ConstantByteValue

    /// <summary>
    /// ConstantCharArray
    /// </summary>
    public partial class ConstantCharArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantCharArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantCharArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 306354450;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantCharArrayHandle AsHandle(ConstantCharArray record)
        {
            if (record == null)
            {
                return new ConstantCharArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantCharArrayHandle Handle
        {
            get
            {
                return new ConstantCharArrayHandle(HandleOffset);
            }
        } // Handle

        public char[] Value;
    } // ConstantCharArray

    /// <summary>
    /// ConstantCharValue
    /// </summary>
    public partial class ConstantCharValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantCharValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantCharValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1395702369;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantCharValueHandle AsHandle(ConstantCharValue record)
        {
            if (record == null)
            {
                return new ConstantCharValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantCharValueHandle Handle
        {
            get
            {
                return new ConstantCharValueHandle(HandleOffset);
            }
        } // Handle

        public char Value;
    } // ConstantCharValue

    /// <summary>
    /// ConstantDoubleArray
    /// </summary>
    public partial class ConstantDoubleArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantDoubleArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantDoubleArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1926817667;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantDoubleArrayHandle AsHandle(ConstantDoubleArray record)
        {
            if (record == null)
            {
                return new ConstantDoubleArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantDoubleArrayHandle Handle
        {
            get
            {
                return new ConstantDoubleArrayHandle(HandleOffset);
            }
        } // Handle

        public double[] Value;
    } // ConstantDoubleArray

    /// <summary>
    /// ConstantDoubleValue
    /// </summary>
    public partial class ConstantDoubleValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantDoubleValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantDoubleValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1481871770;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantDoubleValueHandle AsHandle(ConstantDoubleValue record)
        {
            if (record == null)
            {
                return new ConstantDoubleValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantDoubleValueHandle Handle
        {
            get
            {
                return new ConstantDoubleValueHandle(HandleOffset);
            }
        } // Handle

        public double Value;
    } // ConstantDoubleValue

    /// <summary>
    /// ConstantHandleArray
    /// </summary>
    public partial class ConstantHandleArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantHandleArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Value = Value.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantHandleArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 245030796;
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantHandleArrayHandle AsHandle(ConstantHandleArray record)
        {
            if (record == null)
            {
                return new ConstantHandleArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantHandleArrayHandle Handle
        {
            get
            {
                return new ConstantHandleArrayHandle(HandleOffset);
            }
        } // Handle

        public List<MetadataRecord> Value = new List<MetadataRecord>();
    } // ConstantHandleArray

    /// <summary>
    /// ConstantInt16Array
    /// </summary>
    public partial class ConstantInt16Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt16Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt16Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 511421544;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt16ArrayHandle AsHandle(ConstantInt16Array record)
        {
            if (record == null)
            {
                return new ConstantInt16ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt16ArrayHandle Handle
        {
            get
            {
                return new ConstantInt16ArrayHandle(HandleOffset);
            }
        } // Handle

        public short[] Value;
    } // ConstantInt16Array

    /// <summary>
    /// ConstantInt16Value
    /// </summary>
    public partial class ConstantInt16Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt16Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt16Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 981319805;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt16ValueHandle AsHandle(ConstantInt16Value record)
        {
            if (record == null)
            {
                return new ConstantInt16ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt16ValueHandle Handle
        {
            get
            {
                return new ConstantInt16ValueHandle(HandleOffset);
            }
        } // Handle

        public short Value;
    } // ConstantInt16Value

    /// <summary>
    /// ConstantInt32Array
    /// </summary>
    public partial class ConstantInt32Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt32Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt32Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1821266972;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt32ArrayHandle AsHandle(ConstantInt32Array record)
        {
            if (record == null)
            {
                return new ConstantInt32ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt32ArrayHandle Handle
        {
            get
            {
                return new ConstantInt32ArrayHandle(HandleOffset);
            }
        } // Handle

        public int[] Value;
    } // ConstantInt32Array

    /// <summary>
    /// ConstantInt32Value
    /// </summary>
    public partial class ConstantInt32Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt32Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt32Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -586694639;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt32ValueHandle AsHandle(ConstantInt32Value record)
        {
            if (record == null)
            {
                return new ConstantInt32ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt32ValueHandle Handle
        {
            get
            {
                return new ConstantInt32ValueHandle(HandleOffset);
            }
        } // Handle

        public int Value;
    } // ConstantInt32Value

    /// <summary>
    /// ConstantInt64Array
    /// </summary>
    public partial class ConstantInt64Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt64Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt64Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -769479382;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt64ArrayHandle AsHandle(ConstantInt64Array record)
        {
            if (record == null)
            {
                return new ConstantInt64ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt64ArrayHandle Handle
        {
            get
            {
                return new ConstantInt64ArrayHandle(HandleOffset);
            }
        } // Handle

        public long[] Value;
    } // ConstantInt64Array

    /// <summary>
    /// ConstantInt64Value
    /// </summary>
    public partial class ConstantInt64Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantInt64Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantInt64Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1994441023;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantInt64ValueHandle AsHandle(ConstantInt64Value record)
        {
            if (record == null)
            {
                return new ConstantInt64ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantInt64ValueHandle Handle
        {
            get
            {
                return new ConstantInt64ValueHandle(HandleOffset);
            }
        } // Handle

        public long Value;
    } // ConstantInt64Value

    /// <summary>
    /// ConstantReferenceValue
    /// </summary>
    public partial class ConstantReferenceValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantReferenceValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantReferenceValue;
            if (other == null) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -498183990;
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {

        } // Save

        internal static ConstantReferenceValueHandle AsHandle(ConstantReferenceValue record)
        {
            if (record == null)
            {
                return new ConstantReferenceValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantReferenceValueHandle Handle
        {
            get
            {
                return new ConstantReferenceValueHandle(HandleOffset);
            }
        } // Handle
    } // ConstantReferenceValue

    /// <summary>
    /// ConstantSByteArray
    /// </summary>
    public partial class ConstantSByteArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantSByteArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantSByteArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1904334730;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantSByteArrayHandle AsHandle(ConstantSByteArray record)
        {
            if (record == null)
            {
                return new ConstantSByteArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantSByteArrayHandle Handle
        {
            get
            {
                return new ConstantSByteArrayHandle(HandleOffset);
            }
        } // Handle

        public sbyte[] Value;
    } // ConstantSByteArray

    /// <summary>
    /// ConstantSByteValue
    /// </summary>
    public partial class ConstantSByteValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantSByteValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantSByteValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1000778923;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantSByteValueHandle AsHandle(ConstantSByteValue record)
        {
            if (record == null)
            {
                return new ConstantSByteValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantSByteValueHandle Handle
        {
            get
            {
                return new ConstantSByteValueHandle(HandleOffset);
            }
        } // Handle

        public sbyte Value;
    } // ConstantSByteValue

    /// <summary>
    /// ConstantSingleArray
    /// </summary>
    public partial class ConstantSingleArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantSingleArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantSingleArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 212052597;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantSingleArrayHandle AsHandle(ConstantSingleArray record)
        {
            if (record == null)
            {
                return new ConstantSingleArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantSingleArrayHandle Handle
        {
            get
            {
                return new ConstantSingleArrayHandle(HandleOffset);
            }
        } // Handle

        public float[] Value;
    } // ConstantSingleArray

    /// <summary>
    /// ConstantSingleValue
    /// </summary>
    public partial class ConstantSingleValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantSingleValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantSingleValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1786883192;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantSingleValueHandle AsHandle(ConstantSingleValue record)
        {
            if (record == null)
            {
                return new ConstantSingleValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantSingleValueHandle Handle
        {
            get
            {
                return new ConstantSingleValueHandle(HandleOffset);
            }
        } // Handle

        public float Value;
    } // ConstantSingleValue

    /// <summary>
    /// ConstantStringArray
    /// </summary>
    public partial class ConstantStringArray : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantStringArray;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantStringArray;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 270977183;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantStringArrayHandle AsHandle(ConstantStringArray record)
        {
            if (record == null)
            {
                return new ConstantStringArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantStringArrayHandle Handle
        {
            get
            {
                return new ConstantStringArrayHandle(HandleOffset);
            }
        } // Handle

        public string[] Value;
    } // ConstantStringArray

    /// <summary>
    /// ConstantStringValue
    /// </summary>
    public partial class ConstantStringValue : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantStringValue;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantStringValue;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 280549046;
            hash = ((hash << 13) - (hash >> 19)) ^ (Value == null ? 0 : Value.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            if (Value == null)
                return;
            
            
            writer.Write(Value);
        } // Save

        internal static ConstantStringValueHandle AsHandle(ConstantStringValue record)
        {
            if (record == null)
            {
                return new ConstantStringValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantStringValueHandle Handle
        {
            get
            {
                if (Value == null)
                    return new ConstantStringValueHandle(0);
                else
                    return new ConstantStringValueHandle(HandleOffset);
            }
        } // Handle

        public string Value;
    } // ConstantStringValue

    /// <summary>
    /// ConstantUInt16Array
    /// </summary>
    public partial class ConstantUInt16Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt16Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt16Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -462047045;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt16ArrayHandle AsHandle(ConstantUInt16Array record)
        {
            if (record == null)
            {
                return new ConstantUInt16ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt16ArrayHandle Handle
        {
            get
            {
                return new ConstantUInt16ArrayHandle(HandleOffset);
            }
        } // Handle

        public ushort[] Value;
    } // ConstantUInt16Array

    /// <summary>
    /// ConstantUInt16Value
    /// </summary>
    public partial class ConstantUInt16Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt16Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt16Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1667604030;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt16ValueHandle AsHandle(ConstantUInt16Value record)
        {
            if (record == null)
            {
                return new ConstantUInt16ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt16ValueHandle Handle
        {
            get
            {
                return new ConstantUInt16ValueHandle(HandleOffset);
            }
        } // Handle

        public ushort Value;
    } // ConstantUInt16Value

    /// <summary>
    /// ConstantUInt32Array
    /// </summary>
    public partial class ConstantUInt32Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt32Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt32Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -453396483;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt32ArrayHandle AsHandle(ConstantUInt32Array record)
        {
            if (record == null)
            {
                return new ConstantUInt32ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt32ArrayHandle Handle
        {
            get
            {
                return new ConstantUInt32ArrayHandle(HandleOffset);
            }
        } // Handle

        public uint[] Value;
    } // ConstantUInt32Array

    /// <summary>
    /// ConstantUInt32Value
    /// </summary>
    public partial class ConstantUInt32Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt32Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt32Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1659477760;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt32ValueHandle AsHandle(ConstantUInt32Value record)
        {
            if (record == null)
            {
                return new ConstantUInt32ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt32ValueHandle Handle
        {
            get
            {
                return new ConstantUInt32ValueHandle(HandleOffset);
            }
        } // Handle

        public uint Value;
    } // ConstantUInt32Value

    /// <summary>
    /// ConstantUInt64Array
    /// </summary>
    public partial class ConstantUInt64Array : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt64Array;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt64Array;
            if (other == null) return false;
            if (!Value.SequenceEqual(other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -470567020;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ Value[i].GetHashCode();
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt64ArrayHandle AsHandle(ConstantUInt64Array record)
        {
            if (record == null)
            {
                return new ConstantUInt64ArrayHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt64ArrayHandle Handle
        {
            get
            {
                return new ConstantUInt64ArrayHandle(HandleOffset);
            }
        } // Handle

        public ulong[] Value;
    } // ConstantUInt64Array

    /// <summary>
    /// ConstantUInt64Value
    /// </summary>
    public partial class ConstantUInt64Value : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ConstantUInt64Value;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ConstantUInt64Value;
            if (other == null) return false;
            if (Value != other.Value) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1678745429;
            hash = ((hash << 13) - (hash >> 19)) ^ Value.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Value);
        } // Save

        internal static ConstantUInt64ValueHandle AsHandle(ConstantUInt64Value record)
        {
            if (record == null)
            {
                return new ConstantUInt64ValueHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ConstantUInt64ValueHandle Handle
        {
            get
            {
                return new ConstantUInt64ValueHandle(HandleOffset);
            }
        } // Handle

        public ulong Value;
    } // ConstantUInt64Value

    /// <summary>
    /// CustomAttribute
    /// </summary>
    public partial class CustomAttribute : MetadataRecord
    {
        public CustomAttribute()
        {
            _equalsReentrancyGuard = new ThreadLocal<ReentrancyGuardStack>(() => new ReentrancyGuardStack());
        }

        public override HandleType HandleType
        {
            get
            {
                return HandleType.CustomAttribute;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            FixedArguments = visitor.Visit(this, FixedArguments.AsEnumerable());
            NamedArguments = visitor.Visit(this, NamedArguments.AsEnumerable());
            Type = visitor.Visit(this, Type);
            Constructor = visitor.Visit(this, Constructor);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as CustomAttribute;
            if (other == null) return false;
            if (_equalsReentrancyGuard.Value.Contains(other))
                return true;
            _equalsReentrancyGuard.Value.Push(other);
            try
            {
            if (!Object.Equals(Type, other.Type)) return false;
            if (!Object.Equals(Constructor, other.Constructor)) return false;
            if (!FixedArguments.SequenceEqual(other.FixedArguments)) return false;
            if (!NamedArguments.SequenceEqual(other.NamedArguments)) return false;
            }
            finally
            {
                var popped = _equalsReentrancyGuard.Value.Pop();
                Debug.Assert(Object.ReferenceEquals(other, popped));
            }
            return true;
        } // Equals

        private ThreadLocal<ReentrancyGuardStack> _equalsReentrancyGuard;
        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1789737237;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Constructor == null ? 0 : Constructor.GetHashCode());
            if (FixedArguments != null)
            {
                for (int i = 0; i < FixedArguments.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (FixedArguments[i] == null ? 0 : FixedArguments[i].GetHashCode());
                }
            }
            if (NamedArguments != null)
            {
                for (int i = 0; i < NamedArguments.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (NamedArguments[i] == null ? 0 : NamedArguments[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference);
            writer.Write(Type);
            Debug.Assert(Constructor == null ||
                Constructor.HandleType == HandleType.Method ||
                Constructor.HandleType == HandleType.MemberReference);
            writer.Write(Constructor);
            writer.Write(FixedArguments);
            writer.Write(NamedArguments);
        } // Save

        internal static CustomAttributeHandle AsHandle(CustomAttribute record)
        {
            if (record == null)
            {
                return new CustomAttributeHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new CustomAttributeHandle Handle
        {
            get
            {
                return new CustomAttributeHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Type;
        public MetadataRecord Constructor;
        public List<FixedArgument> FixedArguments = new List<FixedArgument>();
        public List<NamedArgument> NamedArguments = new List<NamedArgument>();
    } // CustomAttribute

    /// <summary>
    /// CustomModifier
    /// </summary>
    public partial class CustomModifier : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.CustomModifier;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as CustomModifier;
            if (other == null) return false;
            if (IsOptional != other.IsOptional) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -227081198;
            hash = ((hash << 13) - (hash >> 19)) ^ IsOptional.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(IsOptional);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
        } // Save

        internal static CustomModifierHandle AsHandle(CustomModifier record)
        {
            if (record == null)
            {
                return new CustomModifierHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new CustomModifierHandle Handle
        {
            get
            {
                return new CustomModifierHandle(HandleOffset);
            }
        } // Handle

        public bool IsOptional;
        public MetadataRecord Type;
    } // CustomModifier

    /// <summary>
    /// Event
    /// </summary>
    public partial class Event : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.Event;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            MethodSemantics = visitor.Visit(this, MethodSemantics.AsEnumerable());
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as Event;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            if (!MethodSemantics.SequenceEqual(other.MethodSemantics)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1560445450;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            if (MethodSemantics != null)
            {
                for (int i = 0; i < MethodSemantics.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (MethodSemantics[i] == null ? 0 : MethodSemantics[i].GetHashCode());
                }
            }
            if (CustomAttributes != null)
            {
                for (int i = 0; i < CustomAttributes.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (CustomAttributes[i] == null ? 0 : CustomAttributes[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
            writer.Write(MethodSemantics);
            writer.Write(CustomAttributes);
        } // Save

        internal static EventHandle AsHandle(Event record)
        {
            if (record == null)
            {
                return new EventHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new EventHandle Handle
        {
            get
            {
                return new EventHandle(HandleOffset);
            }
        } // Handle

        public EventAttributes Flags;
        public ConstantStringValue Name;
        public MetadataRecord Type;
        public List<MethodSemantics> MethodSemantics = new List<MethodSemantics>();
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // Event

    /// <summary>
    /// Field
    /// </summary>
    public partial class Field : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.Field;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Signature = visitor.Visit(this, Signature.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            DefaultValue = visitor.Visit(this, DefaultValue);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as Field;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Signature, other.Signature)) return false;
            if (!Object.Equals(DefaultValue, other.DefaultValue)) return false;
            if (Offset != other.Offset) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1184596707;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Signature == null ? 0 : Signature.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (DefaultValue == null ? 0 : DefaultValue.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ Offset.GetHashCode();
            if (CustomAttributes != null)
            {
                for (int i = 0; i < CustomAttributes.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (CustomAttributes[i] == null ? 0 : CustomAttributes[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            writer.Write(Signature);
            Debug.Assert(DefaultValue == null ||
                DefaultValue.HandleType == HandleType.TypeDefinition ||
                DefaultValue.HandleType == HandleType.TypeReference ||
                DefaultValue.HandleType == HandleType.TypeSpecification ||
                DefaultValue.HandleType == HandleType.ConstantBooleanArray ||
                DefaultValue.HandleType == HandleType.ConstantBooleanValue ||
                DefaultValue.HandleType == HandleType.ConstantByteArray ||
                DefaultValue.HandleType == HandleType.ConstantByteValue ||
                DefaultValue.HandleType == HandleType.ConstantCharArray ||
                DefaultValue.HandleType == HandleType.ConstantCharValue ||
                DefaultValue.HandleType == HandleType.ConstantDoubleArray ||
                DefaultValue.HandleType == HandleType.ConstantDoubleValue ||
                DefaultValue.HandleType == HandleType.ConstantHandleArray ||
                DefaultValue.HandleType == HandleType.ConstantInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantInt64Value ||
                DefaultValue.HandleType == HandleType.ConstantReferenceValue ||
                DefaultValue.HandleType == HandleType.ConstantSByteArray ||
                DefaultValue.HandleType == HandleType.ConstantSByteValue ||
                DefaultValue.HandleType == HandleType.ConstantSingleArray ||
                DefaultValue.HandleType == HandleType.ConstantSingleValue ||
                DefaultValue.HandleType == HandleType.ConstantStringArray ||
                DefaultValue.HandleType == HandleType.ConstantStringValue ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Value);
            writer.Write(DefaultValue);
            writer.Write(Offset);
            writer.Write(CustomAttributes);
        } // Save

        internal static FieldHandle AsHandle(Field record)
        {
            if (record == null)
            {
                return new FieldHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new FieldHandle Handle
        {
            get
            {
                return new FieldHandle(HandleOffset);
            }
        } // Handle

        public FieldAttributes Flags;
        public ConstantStringValue Name;
        public FieldSignature Signature;
        public MetadataRecord DefaultValue;
        public uint Offset;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // Field

    /// <summary>
    /// FieldSignature
    /// </summary>
    public partial class FieldSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.FieldSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Type = visitor.Visit(this, Type);
            CustomModifiers = CustomModifiers.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as FieldSignature;
            if (other == null) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            if (!CustomModifiers.SequenceEqual(other.CustomModifiers)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -964924677;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
            writer.Write(CustomModifiers);
        } // Save

        internal static FieldSignatureHandle AsHandle(FieldSignature record)
        {
            if (record == null)
            {
                return new FieldSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new FieldSignatureHandle Handle
        {
            get
            {
                return new FieldSignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Type;
        public List<CustomModifier> CustomModifiers = new List<CustomModifier>();
    } // FieldSignature

    /// <summary>
    /// FixedArgument
    /// </summary>
    public partial class FixedArgument : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.FixedArgument;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Type = visitor.Visit(this, Type);
            Value = visitor.Visit(this, Value);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as FixedArgument;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            if (!Object.Equals(Value, other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -346484021;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Value == null ? 0 : Value.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
            Debug.Assert(Value == null ||
                Value.HandleType == HandleType.TypeDefinition ||
                Value.HandleType == HandleType.TypeReference ||
                Value.HandleType == HandleType.TypeSpecification ||
                Value.HandleType == HandleType.ConstantBooleanArray ||
                Value.HandleType == HandleType.ConstantBooleanValue ||
                Value.HandleType == HandleType.ConstantByteArray ||
                Value.HandleType == HandleType.ConstantByteValue ||
                Value.HandleType == HandleType.ConstantCharArray ||
                Value.HandleType == HandleType.ConstantCharValue ||
                Value.HandleType == HandleType.ConstantDoubleArray ||
                Value.HandleType == HandleType.ConstantDoubleValue ||
                Value.HandleType == HandleType.ConstantHandleArray ||
                Value.HandleType == HandleType.ConstantInt16Array ||
                Value.HandleType == HandleType.ConstantInt16Value ||
                Value.HandleType == HandleType.ConstantInt32Array ||
                Value.HandleType == HandleType.ConstantInt32Value ||
                Value.HandleType == HandleType.ConstantInt64Array ||
                Value.HandleType == HandleType.ConstantInt64Value ||
                Value.HandleType == HandleType.ConstantReferenceValue ||
                Value.HandleType == HandleType.ConstantSByteArray ||
                Value.HandleType == HandleType.ConstantSByteValue ||
                Value.HandleType == HandleType.ConstantSingleArray ||
                Value.HandleType == HandleType.ConstantSingleValue ||
                Value.HandleType == HandleType.ConstantStringArray ||
                Value.HandleType == HandleType.ConstantStringValue ||
                Value.HandleType == HandleType.ConstantUInt16Array ||
                Value.HandleType == HandleType.ConstantUInt16Value ||
                Value.HandleType == HandleType.ConstantUInt32Array ||
                Value.HandleType == HandleType.ConstantUInt32Value ||
                Value.HandleType == HandleType.ConstantUInt64Array ||
                Value.HandleType == HandleType.ConstantUInt64Value);
            writer.Write(Value);
        } // Save

        internal static FixedArgumentHandle AsHandle(FixedArgument record)
        {
            if (record == null)
            {
                return new FixedArgumentHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new FixedArgumentHandle Handle
        {
            get
            {
                return new FixedArgumentHandle(HandleOffset);
            }
        } // Handle

        public FixedArgumentAttributes Flags;
        public MetadataRecord Type;
        public MetadataRecord Value;
    } // FixedArgument

    /// <summary>
    /// GenericParameter
    /// </summary>
    public partial class GenericParameter : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.GenericParameter;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            Constraints = Constraints.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as GenericParameter;
            if (other == null) return false;
            if (Number != other.Number) return false;
            if (Flags != other.Flags) return false;
            if (Kind != other.Kind) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Constraints.SequenceEqual(other.Constraints)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1752735177;
            hash = ((hash << 13) - (hash >> 19)) ^ Number.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ Kind.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            if (Constraints != null)
            {
                for (int i = 0; i < Constraints.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (Constraints[i] == null ? 0 : Constraints[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Number);
            writer.Write(Flags);
            writer.Write(Kind);
            writer.Write(Name);
            Debug.Assert(Constraints.TrueForAll(handle => handle == null ||
                handle.HandleType == HandleType.TypeDefinition ||
                handle.HandleType == HandleType.TypeReference ||
                handle.HandleType == HandleType.TypeSpecification));
            writer.Write(Constraints);
            writer.Write(CustomAttributes);
        } // Save

        internal static GenericParameterHandle AsHandle(GenericParameter record)
        {
            if (record == null)
            {
                return new GenericParameterHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new GenericParameterHandle Handle
        {
            get
            {
                return new GenericParameterHandle(HandleOffset);
            }
        } // Handle

        public ushort Number;
        public GenericParameterAttributes Flags;
        public GenericParameterKind Kind;
        public ConstantStringValue Name;
        public List<MetadataRecord> Constraints = new List<MetadataRecord>();
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // GenericParameter

    /// <summary>
    /// MemberReference
    /// </summary>
    public partial class MemberReference : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MemberReference;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Signature = visitor.Visit(this, Signature.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            Parent = visitor.Visit(this, Parent);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MemberReference;
            if (other == null) return false;
            if (!Object.Equals(Parent, other.Parent)) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Signature, other.Signature)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -2088053629;
            hash = ((hash << 13) - (hash >> 19)) ^ (Parent == null ? 0 : Parent.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Signature == null ? 0 : Signature.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Parent == null ||
                Parent.HandleType == HandleType.Method ||
                Parent.HandleType == HandleType.TypeDefinition ||
                Parent.HandleType == HandleType.TypeReference ||
                Parent.HandleType == HandleType.TypeSpecification);
            writer.Write(Parent);
            writer.Write(Name);
            Debug.Assert(Signature == null ||
                Signature.HandleType == HandleType.MethodSignature ||
                Signature.HandleType == HandleType.FieldSignature);
            writer.Write(Signature);
            writer.Write(CustomAttributes);
        } // Save

        internal static MemberReferenceHandle AsHandle(MemberReference record)
        {
            if (record == null)
            {
                return new MemberReferenceHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MemberReferenceHandle Handle
        {
            get
            {
                return new MemberReferenceHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Parent;
        public ConstantStringValue Name;
        public MetadataRecord Signature;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // MemberReference

    /// <summary>
    /// MetadataWriter
    /// </summary>
    public partial class MetadataWriter
    {
    } // MetadataWriter

    /// <summary>
    /// Method
    /// </summary>
    public partial class Method : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.Method;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Signature = visitor.Visit(this, Signature.AsSingleEnumerable()).FirstOrDefault();
            Parameters = visitor.Visit(this, Parameters.AsEnumerable());
            GenericParameters = visitor.Visit(this, GenericParameters.AsEnumerable());
            MethodImpls = visitor.Visit(this, MethodImpls.AsEnumerable());
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as Method;
            if (other == null) return false;
            if (RVA != other.RVA) return false;
            if (Flags != other.Flags) return false;
            if (ImplFlags != other.ImplFlags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Signature, other.Signature)) return false;
            if (!Parameters.SequenceEqual(other.Parameters)) return false;
            if (!GenericParameters.SequenceEqual(other.GenericParameters)) return false;
            if (!MethodImpls.SequenceEqual(other.MethodImpls)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 414302350;
            hash = ((hash << 13) - (hash >> 19)) ^ RVA.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ ImplFlags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Signature == null ? 0 : Signature.GetHashCode());
            if (Parameters != null)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (Parameters[i] == null ? 0 : Parameters[i].GetHashCode());
                }
            }
            if (GenericParameters != null)
            {
                for (int i = 0; i < GenericParameters.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (GenericParameters[i] == null ? 0 : GenericParameters[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(RVA);
            writer.Write(Flags);
            writer.Write(ImplFlags);
            writer.Write(Name);
            writer.Write(Signature);
            writer.Write(Parameters);
            writer.Write(GenericParameters);
            writer.Write(MethodImpls);
            writer.Write(CustomAttributes);
        } // Save

        internal static MethodHandle AsHandle(Method record)
        {
            if (record == null)
            {
                return new MethodHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodHandle Handle
        {
            get
            {
                return new MethodHandle(HandleOffset);
            }
        } // Handle

        public uint RVA;
        public MethodAttributes Flags;
        public MethodImplAttributes ImplFlags;
        public ConstantStringValue Name;
        public MethodSignature Signature;
        public List<Parameter> Parameters = new List<Parameter>();
        public List<GenericParameter> GenericParameters = new List<GenericParameter>();
        public List<MethodImpl> MethodImpls = new List<MethodImpl>();
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // Method

    /// <summary>
    /// MethodImpl
    /// </summary>
    public partial class MethodImpl : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MethodImpl;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            MethodDeclaration = visitor.Visit(this, MethodDeclaration);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MethodImpl;
            if (other == null) return false;
            if (!Object.Equals(MethodDeclaration, other.MethodDeclaration)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -470854868;
            hash = ((hash << 13) - (hash >> 19)) ^ (MethodDeclaration == null ? 0 : MethodDeclaration.GetHashCode());
            if (CustomAttributes != null)
            {
                for (int i = 0; i < CustomAttributes.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (CustomAttributes[i] == null ? 0 : CustomAttributes[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(MethodDeclaration == null ||
                MethodDeclaration.HandleType == HandleType.Method ||
                MethodDeclaration.HandleType == HandleType.MemberReference);
            writer.Write(MethodDeclaration);
            writer.Write(CustomAttributes);
        } // Save

        internal static MethodImplHandle AsHandle(MethodImpl record)
        {
            if (record == null)
            {
                return new MethodImplHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodImplHandle Handle
        {
            get
            {
                return new MethodImplHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord MethodDeclaration;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // MethodImpl

    /// <summary>
    /// MethodInstantiation
    /// </summary>
    public partial class MethodInstantiation : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MethodInstantiation;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Method = visitor.Visit(this, Method);
            Instantiation = visitor.Visit(this, Instantiation);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MethodInstantiation;
            if (other == null) return false;
            if (!Object.Equals(Method, other.Method)) return false;
            if (!Object.Equals(Instantiation, other.Instantiation)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1994047112;
            hash = ((hash << 13) - (hash >> 19)) ^ (Method == null ? 0 : Method.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Instantiation == null ? 0 : Instantiation.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Method == null ||
                Method.HandleType == HandleType.Method ||
                Method.HandleType == HandleType.MemberReference);
            writer.Write(Method);
            writer.Write(Instantiation);
        } // Save

        internal static MethodInstantiationHandle AsHandle(MethodInstantiation record)
        {
            if (record == null)
            {
                return new MethodInstantiationHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodInstantiationHandle Handle
        {
            get
            {
                return new MethodInstantiationHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Method;
        public MethodSignature Instantiation;
    } // MethodInstantiation

    /// <summary>
    /// MethodSemantics
    /// </summary>
    public partial class MethodSemantics : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MethodSemantics;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            Method = visitor.Visit(this, Method);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MethodSemantics;
            if (other == null) return false;
            if (Attributes != other.Attributes) return false;
            if (!Object.Equals(Method, other.Method)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 849522319;
            hash = ((hash << 13) - (hash >> 19)) ^ Attributes.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Method == null ? 0 : Method.GetHashCode());
            if (CustomAttributes != null)
            {
                for (int i = 0; i < CustomAttributes.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (CustomAttributes[i] == null ? 0 : CustomAttributes[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Attributes);
            writer.Write(Method);
            writer.Write(CustomAttributes);
        } // Save

        internal static MethodSemanticsHandle AsHandle(MethodSemantics record)
        {
            if (record == null)
            {
                return new MethodSemanticsHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodSemanticsHandle Handle
        {
            get
            {
                return new MethodSemanticsHandle(HandleOffset);
            }
        } // Handle

        public MethodSemanticsAttributes Attributes;
        public Method Method;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // MethodSemantics

    /// <summary>
    /// MethodSignature
    /// </summary>
    public partial class MethodSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MethodSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            ReturnType = visitor.Visit(this, ReturnType);
            Parameters = Parameters.Select(value => visitor.Visit(this, value)).ToList();
            VarArgParameters = VarArgParameters.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MethodSignature;
            if (other == null) return false;
            if (CallingConvention != other.CallingConvention) return false;
            if (GenericParameterCount != other.GenericParameterCount) return false;
            if (!Object.Equals(ReturnType, other.ReturnType)) return false;
            if (!Parameters.SequenceEqual(other.Parameters)) return false;
            if (!VarArgParameters.SequenceEqual(other.VarArgParameters)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -177286454;
            hash = ((hash << 13) - (hash >> 19)) ^ CallingConvention.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ GenericParameterCount.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (ReturnType == null ? 0 : ReturnType.GetHashCode());
            if (Parameters != null)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (Parameters[i] == null ? 0 : Parameters[i].GetHashCode());
                }
            }
            if (VarArgParameters != null)
            {
                for (int i = 0; i < VarArgParameters.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (VarArgParameters[i] == null ? 0 : VarArgParameters[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(CallingConvention);
            writer.Write(GenericParameterCount);
            writer.Write(ReturnType);
            writer.Write(Parameters);
            writer.Write(VarArgParameters);
        } // Save

        internal static MethodSignatureHandle AsHandle(MethodSignature record)
        {
            if (record == null)
            {
                return new MethodSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodSignatureHandle Handle
        {
            get
            {
                return new MethodSignatureHandle(HandleOffset);
            }
        } // Handle

        public CallingConventions CallingConvention;
        public int GenericParameterCount;
        public ReturnTypeSignature ReturnType;
        public List<ParameterTypeSignature> Parameters = new List<ParameterTypeSignature>();
        public List<ParameterTypeSignature> VarArgParameters = new List<ParameterTypeSignature>();
    } // MethodSignature

    /// <summary>
    /// MethodTypeVariableSignature
    /// </summary>
    public partial class MethodTypeVariableSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.MethodTypeVariableSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as MethodTypeVariableSignature;
            if (other == null) return false;
            if (Number != other.Number) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -581196591;
            hash = ((hash << 13) - (hash >> 19)) ^ Number.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Number);
        } // Save

        internal static MethodTypeVariableSignatureHandle AsHandle(MethodTypeVariableSignature record)
        {
            if (record == null)
            {
                return new MethodTypeVariableSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new MethodTypeVariableSignatureHandle Handle
        {
            get
            {
                return new MethodTypeVariableSignatureHandle(HandleOffset);
            }
        } // Handle

        public int Number;
    } // MethodTypeVariableSignature

    /// <summary>
    /// NamedArgument
    /// </summary>
    public partial class NamedArgument : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.NamedArgument;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Value = visitor.Visit(this, Value.AsSingleEnumerable()).FirstOrDefault();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as NamedArgument;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Value, other.Value)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 745864020;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Value == null ? 0 : Value.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            writer.Write(Value);
        } // Save

        internal static NamedArgumentHandle AsHandle(NamedArgument record)
        {
            if (record == null)
            {
                return new NamedArgumentHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new NamedArgumentHandle Handle
        {
            get
            {
                return new NamedArgumentHandle(HandleOffset);
            }
        } // Handle

        public NamedArgumentMemberKind Flags;
        public ConstantStringValue Name;
        public FixedArgument Value;
    } // NamedArgument

    /// <summary>
    /// NamespaceDefinition
    /// </summary>
    public partial class NamespaceDefinition : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.NamespaceDefinition;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            TypeDefinitions = visitor.Visit(this, TypeDefinitions.AsEnumerable());
            TypeForwarders = visitor.Visit(this, TypeForwarders.AsEnumerable());
            NamespaceDefinitions = visitor.Visit(this, NamespaceDefinitions.AsEnumerable());
            ParentScopeOrNamespace = visitor.Visit(this, ParentScopeOrNamespace);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as NamespaceDefinition;
            if (other == null) return false;
            if (!Object.Equals(ParentScopeOrNamespace, other.ParentScopeOrNamespace)) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1498718221;
            hash = ((hash << 13) - (hash >> 19)) ^ (ParentScopeOrNamespace == null ? 0 : ParentScopeOrNamespace.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(ParentScopeOrNamespace == null ||
                ParentScopeOrNamespace.HandleType == HandleType.NamespaceDefinition ||
                ParentScopeOrNamespace.HandleType == HandleType.ScopeDefinition);
            writer.Write(ParentScopeOrNamespace);
            writer.Write(Name);
            writer.Write(TypeDefinitions);
            writer.Write(TypeForwarders);
            writer.Write(NamespaceDefinitions);
        } // Save

        internal static NamespaceDefinitionHandle AsHandle(NamespaceDefinition record)
        {
            if (record == null)
            {
                return new NamespaceDefinitionHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new NamespaceDefinitionHandle Handle
        {
            get
            {
                return new NamespaceDefinitionHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord ParentScopeOrNamespace;
        public ConstantStringValue Name;
        public List<TypeDefinition> TypeDefinitions = new List<TypeDefinition>();
        public List<TypeForwarder> TypeForwarders = new List<TypeForwarder>();
        public List<NamespaceDefinition> NamespaceDefinitions = new List<NamespaceDefinition>();
    } // NamespaceDefinition

    /// <summary>
    /// NamespaceReference
    /// </summary>
    public partial class NamespaceReference : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.NamespaceReference;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            ParentScopeOrNamespace = visitor.Visit(this, ParentScopeOrNamespace);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as NamespaceReference;
            if (other == null) return false;
            if (!Object.Equals(ParentScopeOrNamespace, other.ParentScopeOrNamespace)) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 895578098;
            hash = ((hash << 13) - (hash >> 19)) ^ (ParentScopeOrNamespace == null ? 0 : ParentScopeOrNamespace.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(ParentScopeOrNamespace == null ||
                ParentScopeOrNamespace.HandleType == HandleType.NamespaceReference ||
                ParentScopeOrNamespace.HandleType == HandleType.ScopeReference);
            writer.Write(ParentScopeOrNamespace);
            writer.Write(Name);
        } // Save

        internal static NamespaceReferenceHandle AsHandle(NamespaceReference record)
        {
            if (record == null)
            {
                return new NamespaceReferenceHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new NamespaceReferenceHandle Handle
        {
            get
            {
                return new NamespaceReferenceHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord ParentScopeOrNamespace;
        public ConstantStringValue Name;
    } // NamespaceReference

    /// <summary>
    /// Parameter
    /// </summary>
    public partial class Parameter : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.Parameter;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            DefaultValue = visitor.Visit(this, DefaultValue);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as Parameter;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (Sequence != other.Sequence) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(DefaultValue, other.DefaultValue)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -929818086;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ Sequence.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (DefaultValue == null ? 0 : DefaultValue.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Sequence);
            writer.Write(Name);
            Debug.Assert(DefaultValue == null ||
                DefaultValue.HandleType == HandleType.TypeDefinition ||
                DefaultValue.HandleType == HandleType.TypeReference ||
                DefaultValue.HandleType == HandleType.TypeSpecification ||
                DefaultValue.HandleType == HandleType.ConstantBooleanArray ||
                DefaultValue.HandleType == HandleType.ConstantBooleanValue ||
                DefaultValue.HandleType == HandleType.ConstantByteArray ||
                DefaultValue.HandleType == HandleType.ConstantByteValue ||
                DefaultValue.HandleType == HandleType.ConstantCharArray ||
                DefaultValue.HandleType == HandleType.ConstantCharValue ||
                DefaultValue.HandleType == HandleType.ConstantDoubleArray ||
                DefaultValue.HandleType == HandleType.ConstantDoubleValue ||
                DefaultValue.HandleType == HandleType.ConstantHandleArray ||
                DefaultValue.HandleType == HandleType.ConstantInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantInt64Value ||
                DefaultValue.HandleType == HandleType.ConstantReferenceValue ||
                DefaultValue.HandleType == HandleType.ConstantSByteArray ||
                DefaultValue.HandleType == HandleType.ConstantSByteValue ||
                DefaultValue.HandleType == HandleType.ConstantSingleArray ||
                DefaultValue.HandleType == HandleType.ConstantSingleValue ||
                DefaultValue.HandleType == HandleType.ConstantStringArray ||
                DefaultValue.HandleType == HandleType.ConstantStringValue ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Value);
            writer.Write(DefaultValue);
            writer.Write(CustomAttributes);
        } // Save

        internal static ParameterHandle AsHandle(Parameter record)
        {
            if (record == null)
            {
                return new ParameterHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ParameterHandle Handle
        {
            get
            {
                return new ParameterHandle(HandleOffset);
            }
        } // Handle

        public ParameterAttributes Flags;
        public ushort Sequence;
        public ConstantStringValue Name;
        public MetadataRecord DefaultValue;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // Parameter

    /// <summary>
    /// ParameterTypeSignature
    /// </summary>
    public partial class ParameterTypeSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ParameterTypeSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            CustomModifiers = CustomModifiers.Select(value => visitor.Visit(this, value)).ToList();
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ParameterTypeSignature;
            if (other == null) return false;
            if (!CustomModifiers.SequenceEqual(other.CustomModifiers)) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -898234212;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(CustomModifiers);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
        } // Save

        internal static ParameterTypeSignatureHandle AsHandle(ParameterTypeSignature record)
        {
            if (record == null)
            {
                return new ParameterTypeSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ParameterTypeSignatureHandle Handle
        {
            get
            {
                return new ParameterTypeSignatureHandle(HandleOffset);
            }
        } // Handle

        public List<CustomModifier> CustomModifiers = new List<CustomModifier>();
        public MetadataRecord Type;
    } // ParameterTypeSignature

    /// <summary>
    /// PointerSignature
    /// </summary>
    public partial class PointerSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.PointerSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as PointerSignature;
            if (other == null) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 335490965;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
        } // Save

        internal static PointerSignatureHandle AsHandle(PointerSignature record)
        {
            if (record == null)
            {
                return new PointerSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new PointerSignatureHandle Handle
        {
            get
            {
                return new PointerSignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Type;
    } // PointerSignature

    /// <summary>
    /// Property
    /// </summary>
    public partial class Property : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.Property;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Signature = visitor.Visit(this, Signature.AsSingleEnumerable()).FirstOrDefault();
            MethodSemantics = visitor.Visit(this, MethodSemantics.AsEnumerable());
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            DefaultValue = visitor.Visit(this, DefaultValue);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as Property;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(Signature, other.Signature)) return false;
            if (!MethodSemantics.SequenceEqual(other.MethodSemantics)) return false;
            if (!Object.Equals(DefaultValue, other.DefaultValue)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -101394342;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Signature == null ? 0 : Signature.GetHashCode());
            if (MethodSemantics != null)
            {
                for (int i = 0; i < MethodSemantics.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (MethodSemantics[i] == null ? 0 : MethodSemantics[i].GetHashCode());
                }
            }
            hash = ((hash << 13) - (hash >> 19)) ^ (DefaultValue == null ? 0 : DefaultValue.GetHashCode());
            if (CustomAttributes != null)
            {
                for (int i = 0; i < CustomAttributes.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (CustomAttributes[i] == null ? 0 : CustomAttributes[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            writer.Write(Signature);
            writer.Write(MethodSemantics);
            Debug.Assert(DefaultValue == null ||
                DefaultValue.HandleType == HandleType.TypeDefinition ||
                DefaultValue.HandleType == HandleType.TypeReference ||
                DefaultValue.HandleType == HandleType.TypeSpecification ||
                DefaultValue.HandleType == HandleType.ConstantBooleanArray ||
                DefaultValue.HandleType == HandleType.ConstantBooleanValue ||
                DefaultValue.HandleType == HandleType.ConstantByteArray ||
                DefaultValue.HandleType == HandleType.ConstantByteValue ||
                DefaultValue.HandleType == HandleType.ConstantCharArray ||
                DefaultValue.HandleType == HandleType.ConstantCharValue ||
                DefaultValue.HandleType == HandleType.ConstantDoubleArray ||
                DefaultValue.HandleType == HandleType.ConstantDoubleValue ||
                DefaultValue.HandleType == HandleType.ConstantHandleArray ||
                DefaultValue.HandleType == HandleType.ConstantInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantInt64Value ||
                DefaultValue.HandleType == HandleType.ConstantReferenceValue ||
                DefaultValue.HandleType == HandleType.ConstantSByteArray ||
                DefaultValue.HandleType == HandleType.ConstantSByteValue ||
                DefaultValue.HandleType == HandleType.ConstantSingleArray ||
                DefaultValue.HandleType == HandleType.ConstantSingleValue ||
                DefaultValue.HandleType == HandleType.ConstantStringArray ||
                DefaultValue.HandleType == HandleType.ConstantStringValue ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt16Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt32Value ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Array ||
                DefaultValue.HandleType == HandleType.ConstantUInt64Value);
            writer.Write(DefaultValue);
            writer.Write(CustomAttributes);
        } // Save

        internal static PropertyHandle AsHandle(Property record)
        {
            if (record == null)
            {
                return new PropertyHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new PropertyHandle Handle
        {
            get
            {
                return new PropertyHandle(HandleOffset);
            }
        } // Handle

        public PropertyAttributes Flags;
        public ConstantStringValue Name;
        public PropertySignature Signature;
        public List<MethodSemantics> MethodSemantics = new List<MethodSemantics>();
        public MetadataRecord DefaultValue;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // Property

    /// <summary>
    /// PropertySignature
    /// </summary>
    public partial class PropertySignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.PropertySignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            CustomModifiers = CustomModifiers.Select(value => visitor.Visit(this, value)).ToList();
            Type = visitor.Visit(this, Type);
            Parameters = Parameters.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as PropertySignature;
            if (other == null) return false;
            if (CallingConvention != other.CallingConvention) return false;
            if (!CustomModifiers.SequenceEqual(other.CustomModifiers)) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            if (!Parameters.SequenceEqual(other.Parameters)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -401451631;
            hash = ((hash << 13) - (hash >> 19)) ^ CallingConvention.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            if (Parameters != null)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ (Parameters[i] == null ? 0 : Parameters[i].GetHashCode());
                }
            }
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(CallingConvention);
            writer.Write(CustomModifiers);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
            writer.Write(Parameters);
        } // Save

        internal static PropertySignatureHandle AsHandle(PropertySignature record)
        {
            if (record == null)
            {
                return new PropertySignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new PropertySignatureHandle Handle
        {
            get
            {
                return new PropertySignatureHandle(HandleOffset);
            }
        } // Handle

        public CallingConventions CallingConvention;
        public List<CustomModifier> CustomModifiers = new List<CustomModifier>();
        public MetadataRecord Type;
        public List<ParameterTypeSignature> Parameters = new List<ParameterTypeSignature>();
    } // PropertySignature

    /// <summary>
    /// ReturnTypeSignature
    /// </summary>
    public partial class ReturnTypeSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ReturnTypeSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            CustomModifiers = CustomModifiers.Select(value => visitor.Visit(this, value)).ToList();
            Type = visitor.Visit(this, Type);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ReturnTypeSignature;
            if (other == null) return false;
            if (!CustomModifiers.SequenceEqual(other.CustomModifiers)) return false;
            if (!Object.Equals(Type, other.Type)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1808955079;
            hash = ((hash << 13) - (hash >> 19)) ^ (Type == null ? 0 : Type.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(CustomModifiers);
            Debug.Assert(Type == null ||
                Type.HandleType == HandleType.TypeDefinition ||
                Type.HandleType == HandleType.TypeReference ||
                Type.HandleType == HandleType.TypeSpecification);
            writer.Write(Type);
        } // Save

        internal static ReturnTypeSignatureHandle AsHandle(ReturnTypeSignature record)
        {
            if (record == null)
            {
                return new ReturnTypeSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ReturnTypeSignatureHandle Handle
        {
            get
            {
                return new ReturnTypeSignatureHandle(HandleOffset);
            }
        } // Handle

        public List<CustomModifier> CustomModifiers = new List<CustomModifier>();
        public MetadataRecord Type;
    } // ReturnTypeSignature

    /// <summary>
    /// SZArraySignature
    /// </summary>
    public partial class SZArraySignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.SZArraySignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            ElementType = visitor.Visit(this, ElementType);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as SZArraySignature;
            if (other == null) return false;
            if (!Object.Equals(ElementType, other.ElementType)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 1118159789;
            hash = ((hash << 13) - (hash >> 19)) ^ (ElementType == null ? 0 : ElementType.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(ElementType == null ||
                ElementType.HandleType == HandleType.TypeDefinition ||
                ElementType.HandleType == HandleType.TypeReference ||
                ElementType.HandleType == HandleType.TypeSpecification);
            writer.Write(ElementType);
        } // Save

        internal static SZArraySignatureHandle AsHandle(SZArraySignature record)
        {
            if (record == null)
            {
                return new SZArraySignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new SZArraySignatureHandle Handle
        {
            get
            {
                return new SZArraySignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord ElementType;
    } // SZArraySignature

    /// <summary>
    /// ScopeDefinition
    /// </summary>
    public partial class ScopeDefinition : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ScopeDefinition;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Culture = visitor.Visit(this, Culture.AsSingleEnumerable()).FirstOrDefault();
            RootNamespaceDefinition = visitor.Visit(this, RootNamespaceDefinition.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ScopeDefinition;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (HashAlgorithm != other.HashAlgorithm) return false;
            if (MajorVersion != other.MajorVersion) return false;
            if (MinorVersion != other.MinorVersion) return false;
            if (BuildNumber != other.BuildNumber) return false;
            if (RevisionNumber != other.RevisionNumber) return false;
            if (!PublicKey.SequenceEqual(other.PublicKey)) return false;
            if (!Object.Equals(Culture, other.Culture)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 13322760;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ HashAlgorithm.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ MajorVersion.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ MinorVersion.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ BuildNumber.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ RevisionNumber.GetHashCode();
            if (PublicKey != null)
            {
                for (int i = 0; i < PublicKey.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ PublicKey[i].GetHashCode();
                }
            }
            hash = ((hash << 13) - (hash >> 19)) ^ (Culture == null ? 0 : Culture.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            writer.Write(HashAlgorithm);
            writer.Write(MajorVersion);
            writer.Write(MinorVersion);
            writer.Write(BuildNumber);
            writer.Write(RevisionNumber);
            writer.Write(PublicKey);
            writer.Write(Culture);
            writer.Write(RootNamespaceDefinition);
            writer.Write(CustomAttributes);
        } // Save

        internal static ScopeDefinitionHandle AsHandle(ScopeDefinition record)
        {
            if (record == null)
            {
                return new ScopeDefinitionHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ScopeDefinitionHandle Handle
        {
            get
            {
                return new ScopeDefinitionHandle(HandleOffset);
            }
        } // Handle

        public AssemblyFlags Flags;
        public ConstantStringValue Name;
        public AssemblyHashAlgorithm HashAlgorithm;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort BuildNumber;
        public ushort RevisionNumber;
        public byte[] PublicKey;
        public ConstantStringValue Culture;
        public NamespaceDefinition RootNamespaceDefinition;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // ScopeDefinition

    /// <summary>
    /// ScopeReference
    /// </summary>
    public partial class ScopeReference : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.ScopeReference;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            Culture = visitor.Visit(this, Culture.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as ScopeReference;
            if (other == null) return false;
            if (Flags != other.Flags) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (MajorVersion != other.MajorVersion) return false;
            if (MinorVersion != other.MinorVersion) return false;
            if (BuildNumber != other.BuildNumber) return false;
            if (RevisionNumber != other.RevisionNumber) return false;
            if (!PublicKeyOrToken.SequenceEqual(other.PublicKeyOrToken)) return false;
            if (!Object.Equals(Culture, other.Culture)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -769863623;
            hash = ((hash << 13) - (hash >> 19)) ^ Flags.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ MajorVersion.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ MinorVersion.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ BuildNumber.GetHashCode();
            hash = ((hash << 13) - (hash >> 19)) ^ RevisionNumber.GetHashCode();
            if (PublicKeyOrToken != null)
            {
                for (int i = 0; i < PublicKeyOrToken.Length; i++)
                {
                    hash = ((hash << 13) - (hash >> 19)) ^ PublicKeyOrToken[i].GetHashCode();
                }
            }
            hash = ((hash << 13) - (hash >> 19)) ^ (Culture == null ? 0 : Culture.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            writer.Write(Name);
            writer.Write(MajorVersion);
            writer.Write(MinorVersion);
            writer.Write(BuildNumber);
            writer.Write(RevisionNumber);
            writer.Write(PublicKeyOrToken);
            writer.Write(Culture);
            writer.Write(CustomAttributes);
        } // Save

        internal static ScopeReferenceHandle AsHandle(ScopeReference record)
        {
            if (record == null)
            {
                return new ScopeReferenceHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new ScopeReferenceHandle Handle
        {
            get
            {
                return new ScopeReferenceHandle(HandleOffset);
            }
        } // Handle

        public AssemblyFlags Flags;
        public ConstantStringValue Name;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort BuildNumber;
        public ushort RevisionNumber;
        public byte[] PublicKeyOrToken;
        public ConstantStringValue Culture;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // ScopeReference

    /// <summary>
    /// TypeDefinition
    /// </summary>
    public partial class TypeDefinition : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeDefinition;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            NestedTypes = visitor.Visit(this, NestedTypes.AsEnumerable());
            Methods = visitor.Visit(this, Methods.AsEnumerable());
            Fields = visitor.Visit(this, Fields.AsEnumerable());
            Properties = visitor.Visit(this, Properties.AsEnumerable());
            Events = visitor.Visit(this, Events.AsEnumerable());
            GenericParameters = visitor.Visit(this, GenericParameters.AsEnumerable());
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            BaseType = visitor.Visit(this, BaseType);
            NamespaceDefinition = visitor.Visit(this, NamespaceDefinition);
            EnclosingType = visitor.Visit(this, EnclosingType);
            Interfaces = Interfaces.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeDefinition;
            if (other == null) return false;
            if (!Object.Equals(NamespaceDefinition, other.NamespaceDefinition)) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!Object.Equals(EnclosingType, other.EnclosingType)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -967212031;
            hash = ((hash << 13) - (hash >> 19)) ^ (NamespaceDefinition == null ? 0 : NamespaceDefinition.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (EnclosingType == null ? 0 : EnclosingType.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Flags);
            Debug.Assert(BaseType == null ||
                BaseType.HandleType == HandleType.TypeDefinition ||
                BaseType.HandleType == HandleType.TypeReference ||
                BaseType.HandleType == HandleType.TypeSpecification);
            writer.Write(BaseType);
            writer.Write(NamespaceDefinition);
            writer.Write(Name);
            writer.Write(Size);
            writer.Write(PackingSize);
            writer.Write(EnclosingType);
            writer.Write(NestedTypes);
            writer.Write(Methods);
            writer.Write(Fields);
            writer.Write(Properties);
            writer.Write(Events);
            writer.Write(GenericParameters);
            Debug.Assert(Interfaces.TrueForAll(handle => handle == null ||
                handle.HandleType == HandleType.TypeDefinition ||
                handle.HandleType == HandleType.TypeReference ||
                handle.HandleType == HandleType.TypeSpecification));
            writer.Write(Interfaces);
            writer.Write(CustomAttributes);
        } // Save

        internal static TypeDefinitionHandle AsHandle(TypeDefinition record)
        {
            if (record == null)
            {
                return new TypeDefinitionHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeDefinitionHandle Handle
        {
            get
            {
                return new TypeDefinitionHandle(HandleOffset);
            }
        } // Handle

        public TypeAttributes Flags;
        public MetadataRecord BaseType;
        public NamespaceDefinition NamespaceDefinition;
        public ConstantStringValue Name;
        public uint Size;
        public uint PackingSize;
        public TypeDefinition EnclosingType;
        public List<TypeDefinition> NestedTypes = new List<TypeDefinition>();
        public List<Method> Methods = new List<Method>();
        public List<Field> Fields = new List<Field>();
        public List<Property> Properties = new List<Property>();
        public List<Event> Events = new List<Event>();
        public List<GenericParameter> GenericParameters = new List<GenericParameter>();
        public List<MetadataRecord> Interfaces = new List<MetadataRecord>();
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // TypeDefinition

    /// <summary>
    /// TypeForwarder
    /// </summary>
    public partial class TypeForwarder : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeForwarder;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Name = visitor.Visit(this, Name.AsSingleEnumerable()).FirstOrDefault();
            NestedTypes = visitor.Visit(this, NestedTypes.AsEnumerable());
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            Scope = visitor.Visit(this, Scope);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeForwarder;
            if (other == null) return false;
            if (!Object.Equals(Scope, other.Scope)) return false;
            if (!Object.Equals(Name, other.Name)) return false;
            if (!NestedTypes.SequenceEqual(other.NestedTypes)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 690839467;
            hash = ((hash << 13) - (hash >> 19)) ^ (Scope == null ? 0 : Scope.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (Name == null ? 0 : Name.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Scope);
            writer.Write(Name);
            writer.Write(NestedTypes);
            writer.Write(CustomAttributes);
        } // Save

        internal static TypeForwarderHandle AsHandle(TypeForwarder record)
        {
            if (record == null)
            {
                return new TypeForwarderHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeForwarderHandle Handle
        {
            get
            {
                return new TypeForwarderHandle(HandleOffset);
            }
        } // Handle

        public ScopeReference Scope;
        public ConstantStringValue Name;
        public List<TypeForwarder> NestedTypes = new List<TypeForwarder>();
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // TypeForwarder

    /// <summary>
    /// TypeInstantiationSignature
    /// </summary>
    public partial class TypeInstantiationSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeInstantiationSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            GenericType = visitor.Visit(this, GenericType);
            GenericTypeArguments = GenericTypeArguments.Select(value => visitor.Visit(this, value)).ToList();
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeInstantiationSignature;
            if (other == null) return false;
            if (!Object.Equals(GenericType, other.GenericType)) return false;
            if (!GenericTypeArguments.SequenceEqual(other.GenericTypeArguments)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -1724439320;
            hash = ((hash << 13) - (hash >> 19)) ^ (GenericType == null ? 0 : GenericType.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(GenericType == null ||
                GenericType.HandleType == HandleType.TypeDefinition ||
                GenericType.HandleType == HandleType.TypeReference ||
                GenericType.HandleType == HandleType.TypeSpecification);
            writer.Write(GenericType);
            Debug.Assert(GenericTypeArguments.TrueForAll(handle => handle == null ||
                handle.HandleType == HandleType.TypeDefinition ||
                handle.HandleType == HandleType.TypeReference ||
                handle.HandleType == HandleType.TypeSpecification));
            writer.Write(GenericTypeArguments);
        } // Save

        internal static TypeInstantiationSignatureHandle AsHandle(TypeInstantiationSignature record)
        {
            if (record == null)
            {
                return new TypeInstantiationSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeInstantiationSignatureHandle Handle
        {
            get
            {
                return new TypeInstantiationSignatureHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord GenericType;
        public List<MetadataRecord> GenericTypeArguments = new List<MetadataRecord>();
    } // TypeInstantiationSignature

    /// <summary>
    /// TypeReference
    /// </summary>
    public partial class TypeReference : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeReference;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            TypeName = visitor.Visit(this, TypeName.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
            ParentNamespaceOrType = visitor.Visit(this, ParentNamespaceOrType);
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeReference;
            if (other == null) return false;
            if (!Object.Equals(ParentNamespaceOrType, other.ParentNamespaceOrType)) return false;
            if (!Object.Equals(TypeName, other.TypeName)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = -594854621;
            hash = ((hash << 13) - (hash >> 19)) ^ (ParentNamespaceOrType == null ? 0 : ParentNamespaceOrType.GetHashCode());
            hash = ((hash << 13) - (hash >> 19)) ^ (TypeName == null ? 0 : TypeName.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(ParentNamespaceOrType == null ||
                ParentNamespaceOrType.HandleType == HandleType.NamespaceReference ||
                ParentNamespaceOrType.HandleType == HandleType.TypeReference);
            writer.Write(ParentNamespaceOrType);
            writer.Write(TypeName);
            writer.Write(CustomAttributes);
        } // Save

        internal static TypeReferenceHandle AsHandle(TypeReference record)
        {
            if (record == null)
            {
                return new TypeReferenceHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeReferenceHandle Handle
        {
            get
            {
                return new TypeReferenceHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord ParentNamespaceOrType;
        public ConstantStringValue TypeName;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // TypeReference

    /// <summary>
    /// TypeSpecification
    /// </summary>
    public partial class TypeSpecification : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeSpecification;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {
            Signature = visitor.Visit(this, Signature.AsSingleEnumerable()).FirstOrDefault();
            CustomAttributes = visitor.Visit(this, CustomAttributes.AsEnumerable());
        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeSpecification;
            if (other == null) return false;
            if (!Object.Equals(Signature, other.Signature)) return false;
            if (!CustomAttributes.SequenceEqual(other.CustomAttributes)) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 263768524;
            hash = ((hash << 13) - (hash >> 19)) ^ (Signature == null ? 0 : Signature.GetHashCode());
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            Debug.Assert(Signature == null ||
                Signature.HandleType == HandleType.TypeDefinition ||
                Signature.HandleType == HandleType.TypeReference ||
                Signature.HandleType == HandleType.TypeInstantiationSignature ||
                Signature.HandleType == HandleType.SZArraySignature ||
                Signature.HandleType == HandleType.ArraySignature ||
                Signature.HandleType == HandleType.PointerSignature ||
                Signature.HandleType == HandleType.ByReferenceSignature ||
                Signature.HandleType == HandleType.TypeVariableSignature ||
                Signature.HandleType == HandleType.MethodTypeVariableSignature);
            writer.Write(Signature);
            writer.Write(CustomAttributes);
        } // Save

        internal static TypeSpecificationHandle AsHandle(TypeSpecification record)
        {
            if (record == null)
            {
                return new TypeSpecificationHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeSpecificationHandle Handle
        {
            get
            {
                return new TypeSpecificationHandle(HandleOffset);
            }
        } // Handle

        public MetadataRecord Signature;
        public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();
    } // TypeSpecification

    /// <summary>
    /// TypeVariableSignature
    /// </summary>
    public partial class TypeVariableSignature : MetadataRecord
    {
        public override HandleType HandleType
        {
            get
            {
                return HandleType.TypeVariableSignature;
            }
        } // HandleType

        internal override void Visit(IRecordVisitor visitor)
        {

        } // Visit

        public override sealed bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj)) return true;
            var other = obj as TypeVariableSignature;
            if (other == null) return false;
            if (Number != other.Number) return false;
            return true;
        } // Equals

        public override sealed int GetHashCode()
        {
            if (_hash != 0)
                return _hash;
            EnterGetHashCode();
            int hash = 2086004915;
            hash = ((hash << 13) - (hash >> 19)) ^ Number.GetHashCode();
            LeaveGetHashCode();
            _hash = hash;
            return _hash;
        } // GetHashCode

        internal override void Save(NativeWriter writer)
        {
            writer.Write(Number);
        } // Save

        internal static TypeVariableSignatureHandle AsHandle(TypeVariableSignature record)
        {
            if (record == null)
            {
                return new TypeVariableSignatureHandle(0);
            }
            else
            {
                return record.Handle;
            }
        } // AsHandle

        internal new TypeVariableSignatureHandle Handle
        {
            get
            {
                return new TypeVariableSignatureHandle(HandleOffset);
            }
        } // Handle

        public int Number;
    } // TypeVariableSignature
} // Internal.Metadata.NativeFormat.Writer
