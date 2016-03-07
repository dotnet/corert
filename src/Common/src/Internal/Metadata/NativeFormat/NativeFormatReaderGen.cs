// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: This is a generated file - do not manually edit!

#pragma warning disable 649
#pragma warning disable 169
#pragma warning disable 282 // There is no defined ordering between fields in multiple declarations of partial class or struct

using System;
using System.Reflection;
using System.Collections.Generic;

namespace Internal.Metadata.NativeFormat
{
    /// <summary>
    /// ArraySignature
    /// </summary>
    public partial struct ArraySignature
    {
        internal MetadataReader _reader;
        internal ArraySignatureHandle _handle;
        public ArraySignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle ElementType
        {
            get
            {
                return _elementType;
            }
        } // ElementType

        internal Handle _elementType;
        public int Rank
        {
            get
            {
                return _rank;
            }
        } // Rank

        internal int _rank;
        public IEnumerable<int> Sizes
        {
            get
            {
                return (IEnumerable<int>)_sizes;
            }
        } // Sizes

        internal int[] _sizes;
        public IEnumerable<int> LowerBounds
        {
            get
            {
                return (IEnumerable<int>)_lowerBounds;
            }
        } // LowerBounds

        internal int[] _lowerBounds;
    } // ArraySignature

    /// <summary>
    /// ArraySignatureHandle
    /// </summary>
    public partial struct ArraySignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ArraySignatureHandle)
                return _value == ((ArraySignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ArraySignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ArraySignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal ArraySignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ArraySignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ArraySignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ArraySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ArraySignature GetArraySignature(MetadataReader reader)
        {
            return reader.GetArraySignature(this);
        } // GetArraySignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ArraySignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ArraySignatureHandle

    /// <summary>
    /// ByReferenceSignature
    /// </summary>
    public partial struct ByReferenceSignature
    {
        internal MetadataReader _reader;
        internal ByReferenceSignatureHandle _handle;
        public ByReferenceSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // ByReferenceSignature

    /// <summary>
    /// ByReferenceSignatureHandle
    /// </summary>
    public partial struct ByReferenceSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ByReferenceSignatureHandle)
                return _value == ((ByReferenceSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ByReferenceSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ByReferenceSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal ByReferenceSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ByReferenceSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ByReferenceSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ByReferenceSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ByReferenceSignature GetByReferenceSignature(MetadataReader reader)
        {
            return reader.GetByReferenceSignature(this);
        } // GetByReferenceSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ByReferenceSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ByReferenceSignatureHandle

    /// <summary>
    /// ConstantBooleanArray
    /// </summary>
    public partial struct ConstantBooleanArray
    {
        internal MetadataReader _reader;
        internal ConstantBooleanArrayHandle _handle;
        public ConstantBooleanArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<bool> Value
        {
            get
            {
                return (IEnumerable<bool>)_value;
            }
        } // Value

        internal bool[] _value;
    } // ConstantBooleanArray

    /// <summary>
    /// ConstantBooleanArrayHandle
    /// </summary>
    public partial struct ConstantBooleanArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantBooleanArrayHandle)
                return _value == ((ConstantBooleanArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantBooleanArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantBooleanArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantBooleanArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantBooleanArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantBooleanArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantBooleanArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantBooleanArray GetConstantBooleanArray(MetadataReader reader)
        {
            return reader.GetConstantBooleanArray(this);
        } // GetConstantBooleanArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantBooleanArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantBooleanArrayHandle

    /// <summary>
    /// ConstantBooleanValue
    /// </summary>
    public partial struct ConstantBooleanValue
    {
        internal MetadataReader _reader;
        internal ConstantBooleanValueHandle _handle;
        public ConstantBooleanValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public bool Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal bool _value;
    } // ConstantBooleanValue

    /// <summary>
    /// ConstantBooleanValueHandle
    /// </summary>
    public partial struct ConstantBooleanValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantBooleanValueHandle)
                return _value == ((ConstantBooleanValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantBooleanValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantBooleanValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantBooleanValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantBooleanValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantBooleanValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantBooleanValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantBooleanValue GetConstantBooleanValue(MetadataReader reader)
        {
            return reader.GetConstantBooleanValue(this);
        } // GetConstantBooleanValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantBooleanValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantBooleanValueHandle

    /// <summary>
    /// ConstantBoxedEnumValue
    /// </summary>
    public partial struct ConstantBoxedEnumValue
    {
        internal MetadataReader _reader;
        internal ConstantBoxedEnumValueHandle _handle;
        public ConstantBoxedEnumValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: ConstantByteValue, ConstantSByteValue, ConstantInt16Value, ConstantUInt16Value, ConstantInt32Value, ConstantUInt32Value, ConstantInt64Value, ConstantUInt64Value
        public Handle Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal Handle _value;
        
        /// One of: TypeDefinition, TypeReference
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // ConstantBoxedEnumValue

    /// <summary>
    /// ConstantBoxedEnumValueHandle
    /// </summary>
    public partial struct ConstantBoxedEnumValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantBoxedEnumValueHandle)
                return _value == ((ConstantBoxedEnumValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantBoxedEnumValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantBoxedEnumValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantBoxedEnumValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantBoxedEnumValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantBoxedEnumValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantBoxedEnumValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantBoxedEnumValue GetConstantBoxedEnumValue(MetadataReader reader)
        {
            return reader.GetConstantBoxedEnumValue(this);
        } // GetConstantBoxedEnumValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantBoxedEnumValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantBoxedEnumValueHandle

    /// <summary>
    /// ConstantByteArray
    /// </summary>
    public partial struct ConstantByteArray
    {
        internal MetadataReader _reader;
        internal ConstantByteArrayHandle _handle;
        public ConstantByteArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<byte> Value
        {
            get
            {
                return (IEnumerable<byte>)_value;
            }
        } // Value

        internal byte[] _value;
    } // ConstantByteArray

    /// <summary>
    /// ConstantByteArrayHandle
    /// </summary>
    public partial struct ConstantByteArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantByteArrayHandle)
                return _value == ((ConstantByteArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantByteArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantByteArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantByteArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantByteArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantByteArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantByteArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantByteArray GetConstantByteArray(MetadataReader reader)
        {
            return reader.GetConstantByteArray(this);
        } // GetConstantByteArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantByteArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantByteArrayHandle

    /// <summary>
    /// ConstantByteValue
    /// </summary>
    public partial struct ConstantByteValue
    {
        internal MetadataReader _reader;
        internal ConstantByteValueHandle _handle;
        public ConstantByteValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public byte Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal byte _value;
    } // ConstantByteValue

    /// <summary>
    /// ConstantByteValueHandle
    /// </summary>
    public partial struct ConstantByteValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantByteValueHandle)
                return _value == ((ConstantByteValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantByteValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantByteValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantByteValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantByteValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantByteValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantByteValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantByteValue GetConstantByteValue(MetadataReader reader)
        {
            return reader.GetConstantByteValue(this);
        } // GetConstantByteValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantByteValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantByteValueHandle

    /// <summary>
    /// ConstantCharArray
    /// </summary>
    public partial struct ConstantCharArray
    {
        internal MetadataReader _reader;
        internal ConstantCharArrayHandle _handle;
        public ConstantCharArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<char> Value
        {
            get
            {
                return (IEnumerable<char>)_value;
            }
        } // Value

        internal char[] _value;
    } // ConstantCharArray

    /// <summary>
    /// ConstantCharArrayHandle
    /// </summary>
    public partial struct ConstantCharArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantCharArrayHandle)
                return _value == ((ConstantCharArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantCharArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantCharArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantCharArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantCharArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantCharArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantCharArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantCharArray GetConstantCharArray(MetadataReader reader)
        {
            return reader.GetConstantCharArray(this);
        } // GetConstantCharArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantCharArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantCharArrayHandle

    /// <summary>
    /// ConstantCharValue
    /// </summary>
    public partial struct ConstantCharValue
    {
        internal MetadataReader _reader;
        internal ConstantCharValueHandle _handle;
        public ConstantCharValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public char Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal char _value;
    } // ConstantCharValue

    /// <summary>
    /// ConstantCharValueHandle
    /// </summary>
    public partial struct ConstantCharValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantCharValueHandle)
                return _value == ((ConstantCharValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantCharValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantCharValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantCharValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantCharValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantCharValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantCharValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantCharValue GetConstantCharValue(MetadataReader reader)
        {
            return reader.GetConstantCharValue(this);
        } // GetConstantCharValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantCharValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantCharValueHandle

    /// <summary>
    /// ConstantDoubleArray
    /// </summary>
    public partial struct ConstantDoubleArray
    {
        internal MetadataReader _reader;
        internal ConstantDoubleArrayHandle _handle;
        public ConstantDoubleArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<double> Value
        {
            get
            {
                return (IEnumerable<double>)_value;
            }
        } // Value

        internal double[] _value;
    } // ConstantDoubleArray

    /// <summary>
    /// ConstantDoubleArrayHandle
    /// </summary>
    public partial struct ConstantDoubleArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantDoubleArrayHandle)
                return _value == ((ConstantDoubleArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantDoubleArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantDoubleArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantDoubleArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantDoubleArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantDoubleArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantDoubleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantDoubleArray GetConstantDoubleArray(MetadataReader reader)
        {
            return reader.GetConstantDoubleArray(this);
        } // GetConstantDoubleArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantDoubleArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantDoubleArrayHandle

    /// <summary>
    /// ConstantDoubleValue
    /// </summary>
    public partial struct ConstantDoubleValue
    {
        internal MetadataReader _reader;
        internal ConstantDoubleValueHandle _handle;
        public ConstantDoubleValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public double Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal double _value;
    } // ConstantDoubleValue

    /// <summary>
    /// ConstantDoubleValueHandle
    /// </summary>
    public partial struct ConstantDoubleValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantDoubleValueHandle)
                return _value == ((ConstantDoubleValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantDoubleValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantDoubleValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantDoubleValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantDoubleValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantDoubleValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantDoubleValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantDoubleValue GetConstantDoubleValue(MetadataReader reader)
        {
            return reader.GetConstantDoubleValue(this);
        } // GetConstantDoubleValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantDoubleValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantDoubleValueHandle

    /// <summary>
    /// ConstantHandleArray
    /// </summary>
    public partial struct ConstantHandleArray
    {
        internal MetadataReader _reader;
        internal ConstantHandleArrayHandle _handle;
        public ConstantHandleArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<Handle> Value
        {
            get
            {
                return (IEnumerable<Handle>)_value;
            }
        } // Value

        internal Handle[] _value;
    } // ConstantHandleArray

    /// <summary>
    /// ConstantHandleArrayHandle
    /// </summary>
    public partial struct ConstantHandleArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantHandleArrayHandle)
                return _value == ((ConstantHandleArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantHandleArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantHandleArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantHandleArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantHandleArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantHandleArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantHandleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantHandleArray GetConstantHandleArray(MetadataReader reader)
        {
            return reader.GetConstantHandleArray(this);
        } // GetConstantHandleArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantHandleArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantHandleArrayHandle

    /// <summary>
    /// ConstantInt16Array
    /// </summary>
    public partial struct ConstantInt16Array
    {
        internal MetadataReader _reader;
        internal ConstantInt16ArrayHandle _handle;
        public ConstantInt16ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<short> Value
        {
            get
            {
                return (IEnumerable<short>)_value;
            }
        } // Value

        internal short[] _value;
    } // ConstantInt16Array

    /// <summary>
    /// ConstantInt16ArrayHandle
    /// </summary>
    public partial struct ConstantInt16ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt16ArrayHandle)
                return _value == ((ConstantInt16ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt16ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt16ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt16ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt16Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt16Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt16ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt16Array GetConstantInt16Array(MetadataReader reader)
        {
            return reader.GetConstantInt16Array(this);
        } // GetConstantInt16Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt16Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt16ArrayHandle

    /// <summary>
    /// ConstantInt16Value
    /// </summary>
    public partial struct ConstantInt16Value
    {
        internal MetadataReader _reader;
        internal ConstantInt16ValueHandle _handle;
        public ConstantInt16ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public short Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal short _value;
    } // ConstantInt16Value

    /// <summary>
    /// ConstantInt16ValueHandle
    /// </summary>
    public partial struct ConstantInt16ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt16ValueHandle)
                return _value == ((ConstantInt16ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt16ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt16ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt16ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt16Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt16Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt16ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt16Value GetConstantInt16Value(MetadataReader reader)
        {
            return reader.GetConstantInt16Value(this);
        } // GetConstantInt16Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt16Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt16ValueHandle

    /// <summary>
    /// ConstantInt32Array
    /// </summary>
    public partial struct ConstantInt32Array
    {
        internal MetadataReader _reader;
        internal ConstantInt32ArrayHandle _handle;
        public ConstantInt32ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<int> Value
        {
            get
            {
                return (IEnumerable<int>)_value;
            }
        } // Value

        internal int[] _value;
    } // ConstantInt32Array

    /// <summary>
    /// ConstantInt32ArrayHandle
    /// </summary>
    public partial struct ConstantInt32ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt32ArrayHandle)
                return _value == ((ConstantInt32ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt32ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt32ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt32ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt32Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt32Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt32ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt32Array GetConstantInt32Array(MetadataReader reader)
        {
            return reader.GetConstantInt32Array(this);
        } // GetConstantInt32Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt32Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt32ArrayHandle

    /// <summary>
    /// ConstantInt32Value
    /// </summary>
    public partial struct ConstantInt32Value
    {
        internal MetadataReader _reader;
        internal ConstantInt32ValueHandle _handle;
        public ConstantInt32ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public int Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal int _value;
    } // ConstantInt32Value

    /// <summary>
    /// ConstantInt32ValueHandle
    /// </summary>
    public partial struct ConstantInt32ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt32ValueHandle)
                return _value == ((ConstantInt32ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt32ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt32ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt32ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt32Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt32Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt32ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt32Value GetConstantInt32Value(MetadataReader reader)
        {
            return reader.GetConstantInt32Value(this);
        } // GetConstantInt32Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt32Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt32ValueHandle

    /// <summary>
    /// ConstantInt64Array
    /// </summary>
    public partial struct ConstantInt64Array
    {
        internal MetadataReader _reader;
        internal ConstantInt64ArrayHandle _handle;
        public ConstantInt64ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<long> Value
        {
            get
            {
                return (IEnumerable<long>)_value;
            }
        } // Value

        internal long[] _value;
    } // ConstantInt64Array

    /// <summary>
    /// ConstantInt64ArrayHandle
    /// </summary>
    public partial struct ConstantInt64ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt64ArrayHandle)
                return _value == ((ConstantInt64ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt64ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt64ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt64ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt64Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt64Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt64ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt64Array GetConstantInt64Array(MetadataReader reader)
        {
            return reader.GetConstantInt64Array(this);
        } // GetConstantInt64Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt64Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt64ArrayHandle

    /// <summary>
    /// ConstantInt64Value
    /// </summary>
    public partial struct ConstantInt64Value
    {
        internal MetadataReader _reader;
        internal ConstantInt64ValueHandle _handle;
        public ConstantInt64ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public long Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal long _value;
    } // ConstantInt64Value

    /// <summary>
    /// ConstantInt64ValueHandle
    /// </summary>
    public partial struct ConstantInt64ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantInt64ValueHandle)
                return _value == ((ConstantInt64ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantInt64ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantInt64ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantInt64ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantInt64Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantInt64Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantInt64ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantInt64Value GetConstantInt64Value(MetadataReader reader)
        {
            return reader.GetConstantInt64Value(this);
        } // GetConstantInt64Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantInt64Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantInt64ValueHandle

    /// <summary>
    /// ConstantReferenceValue
    /// </summary>
    public partial struct ConstantReferenceValue
    {
        internal MetadataReader _reader;
        internal ConstantReferenceValueHandle _handle;
        public ConstantReferenceValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle
    } // ConstantReferenceValue

    /// <summary>
    /// ConstantReferenceValueHandle
    /// </summary>
    public partial struct ConstantReferenceValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantReferenceValueHandle)
                return _value == ((ConstantReferenceValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantReferenceValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantReferenceValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantReferenceValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantReferenceValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantReferenceValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantReferenceValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantReferenceValue GetConstantReferenceValue(MetadataReader reader)
        {
            return reader.GetConstantReferenceValue(this);
        } // GetConstantReferenceValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantReferenceValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantReferenceValueHandle

    /// <summary>
    /// ConstantSByteArray
    /// </summary>
    public partial struct ConstantSByteArray
    {
        internal MetadataReader _reader;
        internal ConstantSByteArrayHandle _handle;
        public ConstantSByteArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<sbyte> Value
        {
            get
            {
                return (IEnumerable<sbyte>)_value;
            }
        } // Value

        internal sbyte[] _value;
    } // ConstantSByteArray

    /// <summary>
    /// ConstantSByteArrayHandle
    /// </summary>
    public partial struct ConstantSByteArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantSByteArrayHandle)
                return _value == ((ConstantSByteArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantSByteArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantSByteArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantSByteArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantSByteArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantSByteArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantSByteArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantSByteArray GetConstantSByteArray(MetadataReader reader)
        {
            return reader.GetConstantSByteArray(this);
        } // GetConstantSByteArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantSByteArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantSByteArrayHandle

    /// <summary>
    /// ConstantSByteValue
    /// </summary>
    public partial struct ConstantSByteValue
    {
        internal MetadataReader _reader;
        internal ConstantSByteValueHandle _handle;
        public ConstantSByteValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public sbyte Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal sbyte _value;
    } // ConstantSByteValue

    /// <summary>
    /// ConstantSByteValueHandle
    /// </summary>
    public partial struct ConstantSByteValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantSByteValueHandle)
                return _value == ((ConstantSByteValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantSByteValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantSByteValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantSByteValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantSByteValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantSByteValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantSByteValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantSByteValue GetConstantSByteValue(MetadataReader reader)
        {
            return reader.GetConstantSByteValue(this);
        } // GetConstantSByteValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantSByteValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantSByteValueHandle

    /// <summary>
    /// ConstantSingleArray
    /// </summary>
    public partial struct ConstantSingleArray
    {
        internal MetadataReader _reader;
        internal ConstantSingleArrayHandle _handle;
        public ConstantSingleArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<float> Value
        {
            get
            {
                return (IEnumerable<float>)_value;
            }
        } // Value

        internal float[] _value;
    } // ConstantSingleArray

    /// <summary>
    /// ConstantSingleArrayHandle
    /// </summary>
    public partial struct ConstantSingleArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantSingleArrayHandle)
                return _value == ((ConstantSingleArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantSingleArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantSingleArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantSingleArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantSingleArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantSingleArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantSingleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantSingleArray GetConstantSingleArray(MetadataReader reader)
        {
            return reader.GetConstantSingleArray(this);
        } // GetConstantSingleArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantSingleArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantSingleArrayHandle

    /// <summary>
    /// ConstantSingleValue
    /// </summary>
    public partial struct ConstantSingleValue
    {
        internal MetadataReader _reader;
        internal ConstantSingleValueHandle _handle;
        public ConstantSingleValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public float Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal float _value;
    } // ConstantSingleValue

    /// <summary>
    /// ConstantSingleValueHandle
    /// </summary>
    public partial struct ConstantSingleValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantSingleValueHandle)
                return _value == ((ConstantSingleValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantSingleValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantSingleValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantSingleValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantSingleValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantSingleValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantSingleValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantSingleValue GetConstantSingleValue(MetadataReader reader)
        {
            return reader.GetConstantSingleValue(this);
        } // GetConstantSingleValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantSingleValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantSingleValueHandle

    /// <summary>
    /// ConstantStringArray
    /// </summary>
    public partial struct ConstantStringArray
    {
        internal MetadataReader _reader;
        internal ConstantStringArrayHandle _handle;
        public ConstantStringArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<string> Value
        {
            get
            {
                return (IEnumerable<string>)_value;
            }
        } // Value

        internal string[] _value;
    } // ConstantStringArray

    /// <summary>
    /// ConstantStringArrayHandle
    /// </summary>
    public partial struct ConstantStringArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantStringArrayHandle)
                return _value == ((ConstantStringArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantStringArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantStringArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantStringArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantStringArray || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantStringArray) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantStringArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantStringArray GetConstantStringArray(MetadataReader reader)
        {
            return reader.GetConstantStringArray(this);
        } // GetConstantStringArray

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantStringArray)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantStringArrayHandle

    /// <summary>
    /// ConstantStringValue
    /// </summary>
    public partial struct ConstantStringValue
    {
        internal MetadataReader _reader;
        internal ConstantStringValueHandle _handle;
        public ConstantStringValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public string Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal string _value;
    } // ConstantStringValue

    /// <summary>
    /// ConstantStringValueHandle
    /// </summary>
    public partial struct ConstantStringValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantStringValueHandle)
                return _value == ((ConstantStringValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantStringValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantStringValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantStringValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantStringValue || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantStringValue) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantStringValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantStringValue GetConstantStringValue(MetadataReader reader)
        {
            return reader.GetConstantStringValue(this);
        } // GetConstantStringValue

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantStringValue)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantStringValueHandle

    /// <summary>
    /// ConstantUInt16Array
    /// </summary>
    public partial struct ConstantUInt16Array
    {
        internal MetadataReader _reader;
        internal ConstantUInt16ArrayHandle _handle;
        public ConstantUInt16ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<ushort> Value
        {
            get
            {
                return (IEnumerable<ushort>)_value;
            }
        } // Value

        internal ushort[] _value;
    } // ConstantUInt16Array

    /// <summary>
    /// ConstantUInt16ArrayHandle
    /// </summary>
    public partial struct ConstantUInt16ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt16ArrayHandle)
                return _value == ((ConstantUInt16ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt16ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt16ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt16ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt16Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt16Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt16ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt16Array GetConstantUInt16Array(MetadataReader reader)
        {
            return reader.GetConstantUInt16Array(this);
        } // GetConstantUInt16Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt16Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt16ArrayHandle

    /// <summary>
    /// ConstantUInt16Value
    /// </summary>
    public partial struct ConstantUInt16Value
    {
        internal MetadataReader _reader;
        internal ConstantUInt16ValueHandle _handle;
        public ConstantUInt16ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public ushort Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal ushort _value;
    } // ConstantUInt16Value

    /// <summary>
    /// ConstantUInt16ValueHandle
    /// </summary>
    public partial struct ConstantUInt16ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt16ValueHandle)
                return _value == ((ConstantUInt16ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt16ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt16ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt16ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt16Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt16Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt16ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt16Value GetConstantUInt16Value(MetadataReader reader)
        {
            return reader.GetConstantUInt16Value(this);
        } // GetConstantUInt16Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt16Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt16ValueHandle

    /// <summary>
    /// ConstantUInt32Array
    /// </summary>
    public partial struct ConstantUInt32Array
    {
        internal MetadataReader _reader;
        internal ConstantUInt32ArrayHandle _handle;
        public ConstantUInt32ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<uint> Value
        {
            get
            {
                return (IEnumerable<uint>)_value;
            }
        } // Value

        internal uint[] _value;
    } // ConstantUInt32Array

    /// <summary>
    /// ConstantUInt32ArrayHandle
    /// </summary>
    public partial struct ConstantUInt32ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt32ArrayHandle)
                return _value == ((ConstantUInt32ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt32ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt32ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt32ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt32Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt32Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt32ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt32Array GetConstantUInt32Array(MetadataReader reader)
        {
            return reader.GetConstantUInt32Array(this);
        } // GetConstantUInt32Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt32Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt32ArrayHandle

    /// <summary>
    /// ConstantUInt32Value
    /// </summary>
    public partial struct ConstantUInt32Value
    {
        internal MetadataReader _reader;
        internal ConstantUInt32ValueHandle _handle;
        public ConstantUInt32ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public uint Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal uint _value;
    } // ConstantUInt32Value

    /// <summary>
    /// ConstantUInt32ValueHandle
    /// </summary>
    public partial struct ConstantUInt32ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt32ValueHandle)
                return _value == ((ConstantUInt32ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt32ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt32ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt32ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt32Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt32Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt32ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt32Value GetConstantUInt32Value(MetadataReader reader)
        {
            return reader.GetConstantUInt32Value(this);
        } // GetConstantUInt32Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt32Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt32ValueHandle

    /// <summary>
    /// ConstantUInt64Array
    /// </summary>
    public partial struct ConstantUInt64Array
    {
        internal MetadataReader _reader;
        internal ConstantUInt64ArrayHandle _handle;
        public ConstantUInt64ArrayHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<ulong> Value
        {
            get
            {
                return (IEnumerable<ulong>)_value;
            }
        } // Value

        internal ulong[] _value;
    } // ConstantUInt64Array

    /// <summary>
    /// ConstantUInt64ArrayHandle
    /// </summary>
    public partial struct ConstantUInt64ArrayHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt64ArrayHandle)
                return _value == ((ConstantUInt64ArrayHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt64ArrayHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt64ArrayHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt64ArrayHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt64Array || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt64Array) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt64ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt64Array GetConstantUInt64Array(MetadataReader reader)
        {
            return reader.GetConstantUInt64Array(this);
        } // GetConstantUInt64Array

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt64Array)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt64ArrayHandle

    /// <summary>
    /// ConstantUInt64Value
    /// </summary>
    public partial struct ConstantUInt64Value
    {
        internal MetadataReader _reader;
        internal ConstantUInt64ValueHandle _handle;
        public ConstantUInt64ValueHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public ulong Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal ulong _value;
    } // ConstantUInt64Value

    /// <summary>
    /// ConstantUInt64ValueHandle
    /// </summary>
    public partial struct ConstantUInt64ValueHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ConstantUInt64ValueHandle)
                return _value == ((ConstantUInt64ValueHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ConstantUInt64ValueHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ConstantUInt64ValueHandle(Handle handle) : this(handle._value)
        {

        }

        internal ConstantUInt64ValueHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ConstantUInt64Value || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ConstantUInt64Value) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ConstantUInt64ValueHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ConstantUInt64Value GetConstantUInt64Value(MetadataReader reader)
        {
            return reader.GetConstantUInt64Value(this);
        } // GetConstantUInt64Value

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ConstantUInt64Value)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ConstantUInt64ValueHandle

    /// <summary>
    /// CustomAttribute
    /// </summary>
    public partial struct CustomAttribute
    {
        internal MetadataReader _reader;
        internal CustomAttributeHandle _handle;
        public CustomAttributeHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: QualifiedMethod, MemberReference
        public Handle Constructor
        {
            get
            {
                return _constructor;
            }
        } // Constructor

        internal Handle _constructor;
        public IEnumerable<FixedArgumentHandle> FixedArguments
        {
            get
            {
                return (IEnumerable<FixedArgumentHandle>)_fixedArguments;
            }
        } // FixedArguments

        internal FixedArgumentHandle[] _fixedArguments;
        public IEnumerable<NamedArgumentHandle> NamedArguments
        {
            get
            {
                return (IEnumerable<NamedArgumentHandle>)_namedArguments;
            }
        } // NamedArguments

        internal NamedArgumentHandle[] _namedArguments;
    } // CustomAttribute

    /// <summary>
    /// CustomAttributeHandle
    /// </summary>
    public partial struct CustomAttributeHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is CustomAttributeHandle)
                return _value == ((CustomAttributeHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(CustomAttributeHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal CustomAttributeHandle(Handle handle) : this(handle._value)
        {

        }

        internal CustomAttributeHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.CustomAttribute || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.CustomAttribute) << 24);
            _Validate();
        }

        public static implicit operator  Handle(CustomAttributeHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public CustomAttribute GetCustomAttribute(MetadataReader reader)
        {
            return reader.GetCustomAttribute(this);
        } // GetCustomAttribute

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.CustomAttribute)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // CustomAttributeHandle

    /// <summary>
    /// CustomModifier
    /// </summary>
    public partial struct CustomModifier
    {
        internal MetadataReader _reader;
        internal CustomModifierHandle _handle;
        public CustomModifierHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public bool IsOptional
        {
            get
            {
                return _isOptional;
            }
        } // IsOptional

        internal bool _isOptional;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // CustomModifier

    /// <summary>
    /// CustomModifierHandle
    /// </summary>
    public partial struct CustomModifierHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is CustomModifierHandle)
                return _value == ((CustomModifierHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(CustomModifierHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal CustomModifierHandle(Handle handle) : this(handle._value)
        {

        }

        internal CustomModifierHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.CustomModifier || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.CustomModifier) << 24);
            _Validate();
        }

        public static implicit operator  Handle(CustomModifierHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public CustomModifier GetCustomModifier(MetadataReader reader)
        {
            return reader.GetCustomModifier(this);
        } // GetCustomModifier

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.CustomModifier)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // CustomModifierHandle

    /// <summary>
    /// Event
    /// </summary>
    public partial struct Event
    {
        internal MetadataReader _reader;
        internal EventHandle _handle;
        public EventHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public EventAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal EventAttributes _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
        public IEnumerable<MethodSemanticsHandle> MethodSemantics
        {
            get
            {
                return (IEnumerable<MethodSemanticsHandle>)_methodSemantics;
            }
        } // MethodSemantics

        internal MethodSemanticsHandle[] _methodSemantics;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // Event

    /// <summary>
    /// EventHandle
    /// </summary>
    public partial struct EventHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is EventHandle)
                return _value == ((EventHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(EventHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal EventHandle(Handle handle) : this(handle._value)
        {

        }

        internal EventHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.Event || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.Event) << 24);
            _Validate();
        }

        public static implicit operator  Handle(EventHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public Event GetEvent(MetadataReader reader)
        {
            return reader.GetEvent(this);
        } // GetEvent

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.Event)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // EventHandle

    /// <summary>
    /// Field
    /// </summary>
    public partial struct Field
    {
        internal MetadataReader _reader;
        internal FieldHandle _handle;
        public FieldHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public FieldAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal FieldAttributes _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public FieldSignatureHandle Signature
        {
            get
            {
                return _signature;
            }
        } // Signature

        internal FieldSignatureHandle _signature;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification, ConstantBooleanArray, ConstantBooleanValue, ConstantByteArray, ConstantByteValue, ConstantCharArray, ConstantCharValue, ConstantDoubleArray, ConstantDoubleValue, ConstantHandleArray, ConstantInt16Array, ConstantInt16Value, ConstantInt32Array, ConstantInt32Value, ConstantInt64Array, ConstantInt64Value, ConstantReferenceValue, ConstantSByteArray, ConstantSByteValue, ConstantSingleArray, ConstantSingleValue, ConstantStringArray, ConstantStringValue, ConstantUInt16Array, ConstantUInt16Value, ConstantUInt32Array, ConstantUInt32Value, ConstantUInt64Array, ConstantUInt64Value
        public Handle DefaultValue
        {
            get
            {
                return _defaultValue;
            }
        } // DefaultValue

        internal Handle _defaultValue;
        public uint Offset
        {
            get
            {
                return _offset;
            }
        } // Offset

        internal uint _offset;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // Field

    /// <summary>
    /// FieldHandle
    /// </summary>
    public partial struct FieldHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is FieldHandle)
                return _value == ((FieldHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(FieldHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal FieldHandle(Handle handle) : this(handle._value)
        {

        }

        internal FieldHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.Field || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.Field) << 24);
            _Validate();
        }

        public static implicit operator  Handle(FieldHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public Field GetField(MetadataReader reader)
        {
            return reader.GetField(this);
        } // GetField

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.Field)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // FieldHandle

    /// <summary>
    /// FieldSignature
    /// </summary>
    public partial struct FieldSignature
    {
        internal MetadataReader _reader;
        internal FieldSignatureHandle _handle;
        public FieldSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
        public IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get
            {
                return (IEnumerable<CustomModifierHandle>)_customModifiers;
            }
        } // CustomModifiers

        internal CustomModifierHandle[] _customModifiers;
    } // FieldSignature

    /// <summary>
    /// FieldSignatureHandle
    /// </summary>
    public partial struct FieldSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is FieldSignatureHandle)
                return _value == ((FieldSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(FieldSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal FieldSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal FieldSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.FieldSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.FieldSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(FieldSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public FieldSignature GetFieldSignature(MetadataReader reader)
        {
            return reader.GetFieldSignature(this);
        } // GetFieldSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.FieldSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // FieldSignatureHandle

    /// <summary>
    /// FixedArgument
    /// </summary>
    public partial struct FixedArgument
    {
        internal MetadataReader _reader;
        internal FixedArgumentHandle _handle;
        public FixedArgumentHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public FixedArgumentAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal FixedArgumentAttributes _flags;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification, ConstantBooleanArray, ConstantBooleanValue, ConstantByteArray, ConstantByteValue, ConstantCharArray, ConstantCharValue, ConstantDoubleArray, ConstantDoubleValue, ConstantHandleArray, ConstantInt16Array, ConstantInt16Value, ConstantInt32Array, ConstantInt32Value, ConstantInt64Array, ConstantInt64Value, ConstantReferenceValue, ConstantSByteArray, ConstantSByteValue, ConstantSingleArray, ConstantSingleValue, ConstantStringArray, ConstantStringValue, ConstantUInt16Array, ConstantUInt16Value, ConstantUInt32Array, ConstantUInt32Value, ConstantUInt64Array, ConstantUInt64Value
        public Handle Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal Handle _value;
    } // FixedArgument

    /// <summary>
    /// FixedArgumentHandle
    /// </summary>
    public partial struct FixedArgumentHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is FixedArgumentHandle)
                return _value == ((FixedArgumentHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(FixedArgumentHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal FixedArgumentHandle(Handle handle) : this(handle._value)
        {

        }

        internal FixedArgumentHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.FixedArgument || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.FixedArgument) << 24);
            _Validate();
        }

        public static implicit operator  Handle(FixedArgumentHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public FixedArgument GetFixedArgument(MetadataReader reader)
        {
            return reader.GetFixedArgument(this);
        } // GetFixedArgument

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.FixedArgument)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // FixedArgumentHandle

    /// <summary>
    /// GenericParameter
    /// </summary>
    public partial struct GenericParameter
    {
        internal MetadataReader _reader;
        internal GenericParameterHandle _handle;
        public GenericParameterHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public ushort Number
        {
            get
            {
                return _number;
            }
        } // Number

        internal ushort _number;
        public GenericParameterAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal GenericParameterAttributes _flags;
        public GenericParameterKind Kind
        {
            get
            {
                return _kind;
            }
        } // Kind

        internal GenericParameterKind _kind;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public IEnumerable<Handle> Constraints
        {
            get
            {
                return (IEnumerable<Handle>)_constraints;
            }
        } // Constraints

        internal Handle[] _constraints;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // GenericParameter

    /// <summary>
    /// GenericParameterHandle
    /// </summary>
    public partial struct GenericParameterHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is GenericParameterHandle)
                return _value == ((GenericParameterHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(GenericParameterHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal GenericParameterHandle(Handle handle) : this(handle._value)
        {

        }

        internal GenericParameterHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.GenericParameter || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.GenericParameter) << 24);
            _Validate();
        }

        public static implicit operator  Handle(GenericParameterHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public GenericParameter GetGenericParameter(MetadataReader reader)
        {
            return reader.GetGenericParameter(this);
        } // GetGenericParameter

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.GenericParameter)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // GenericParameterHandle

    /// <summary>
    /// Handle
    /// </summary>
    public partial struct Handle
    {
        public TypeDefinitionHandle ToTypeDefinitionHandle(MetadataReader reader)
        {
            return new TypeDefinitionHandle(this);
        } // ToTypeDefinitionHandle

        public TypeReferenceHandle ToTypeReferenceHandle(MetadataReader reader)
        {
            return new TypeReferenceHandle(this);
        } // ToTypeReferenceHandle

        public TypeSpecificationHandle ToTypeSpecificationHandle(MetadataReader reader)
        {
            return new TypeSpecificationHandle(this);
        } // ToTypeSpecificationHandle

        public ScopeDefinitionHandle ToScopeDefinitionHandle(MetadataReader reader)
        {
            return new ScopeDefinitionHandle(this);
        } // ToScopeDefinitionHandle

        public ScopeReferenceHandle ToScopeReferenceHandle(MetadataReader reader)
        {
            return new ScopeReferenceHandle(this);
        } // ToScopeReferenceHandle

        public NamespaceDefinitionHandle ToNamespaceDefinitionHandle(MetadataReader reader)
        {
            return new NamespaceDefinitionHandle(this);
        } // ToNamespaceDefinitionHandle

        public NamespaceReferenceHandle ToNamespaceReferenceHandle(MetadataReader reader)
        {
            return new NamespaceReferenceHandle(this);
        } // ToNamespaceReferenceHandle

        public MethodHandle ToMethodHandle(MetadataReader reader)
        {
            return new MethodHandle(this);
        } // ToMethodHandle

        public QualifiedMethodHandle ToQualifiedMethodHandle(MetadataReader reader)
        {
            return new QualifiedMethodHandle(this);
        } // ToQualifiedMethodHandle

        public QualifiedFieldHandle ToQualifiedFieldHandle(MetadataReader reader)
        {
            return new QualifiedFieldHandle(this);
        } // ToQualifiedFieldHandle

        public MethodInstantiationHandle ToMethodInstantiationHandle(MetadataReader reader)
        {
            return new MethodInstantiationHandle(this);
        } // ToMethodInstantiationHandle

        public MemberReferenceHandle ToMemberReferenceHandle(MetadataReader reader)
        {
            return new MemberReferenceHandle(this);
        } // ToMemberReferenceHandle

        public FieldHandle ToFieldHandle(MetadataReader reader)
        {
            return new FieldHandle(this);
        } // ToFieldHandle

        public PropertyHandle ToPropertyHandle(MetadataReader reader)
        {
            return new PropertyHandle(this);
        } // ToPropertyHandle

        public EventHandle ToEventHandle(MetadataReader reader)
        {
            return new EventHandle(this);
        } // ToEventHandle

        public CustomAttributeHandle ToCustomAttributeHandle(MetadataReader reader)
        {
            return new CustomAttributeHandle(this);
        } // ToCustomAttributeHandle

        public FixedArgumentHandle ToFixedArgumentHandle(MetadataReader reader)
        {
            return new FixedArgumentHandle(this);
        } // ToFixedArgumentHandle

        public NamedArgumentHandle ToNamedArgumentHandle(MetadataReader reader)
        {
            return new NamedArgumentHandle(this);
        } // ToNamedArgumentHandle

        public ConstantBoxedEnumValueHandle ToConstantBoxedEnumValueHandle(MetadataReader reader)
        {
            return new ConstantBoxedEnumValueHandle(this);
        } // ToConstantBoxedEnumValueHandle

        public GenericParameterHandle ToGenericParameterHandle(MetadataReader reader)
        {
            return new GenericParameterHandle(this);
        } // ToGenericParameterHandle

        public MethodImplHandle ToMethodImplHandle(MetadataReader reader)
        {
            return new MethodImplHandle(this);
        } // ToMethodImplHandle

        public ParameterHandle ToParameterHandle(MetadataReader reader)
        {
            return new ParameterHandle(this);
        } // ToParameterHandle

        public MethodSemanticsHandle ToMethodSemanticsHandle(MetadataReader reader)
        {
            return new MethodSemanticsHandle(this);
        } // ToMethodSemanticsHandle

        public TypeInstantiationSignatureHandle ToTypeInstantiationSignatureHandle(MetadataReader reader)
        {
            return new TypeInstantiationSignatureHandle(this);
        } // ToTypeInstantiationSignatureHandle

        public SZArraySignatureHandle ToSZArraySignatureHandle(MetadataReader reader)
        {
            return new SZArraySignatureHandle(this);
        } // ToSZArraySignatureHandle

        public ArraySignatureHandle ToArraySignatureHandle(MetadataReader reader)
        {
            return new ArraySignatureHandle(this);
        } // ToArraySignatureHandle

        public ByReferenceSignatureHandle ToByReferenceSignatureHandle(MetadataReader reader)
        {
            return new ByReferenceSignatureHandle(this);
        } // ToByReferenceSignatureHandle

        public PointerSignatureHandle ToPointerSignatureHandle(MetadataReader reader)
        {
            return new PointerSignatureHandle(this);
        } // ToPointerSignatureHandle

        public TypeVariableSignatureHandle ToTypeVariableSignatureHandle(MetadataReader reader)
        {
            return new TypeVariableSignatureHandle(this);
        } // ToTypeVariableSignatureHandle

        public MethodTypeVariableSignatureHandle ToMethodTypeVariableSignatureHandle(MetadataReader reader)
        {
            return new MethodTypeVariableSignatureHandle(this);
        } // ToMethodTypeVariableSignatureHandle

        public FieldSignatureHandle ToFieldSignatureHandle(MetadataReader reader)
        {
            return new FieldSignatureHandle(this);
        } // ToFieldSignatureHandle

        public PropertySignatureHandle ToPropertySignatureHandle(MetadataReader reader)
        {
            return new PropertySignatureHandle(this);
        } // ToPropertySignatureHandle

        public MethodSignatureHandle ToMethodSignatureHandle(MetadataReader reader)
        {
            return new MethodSignatureHandle(this);
        } // ToMethodSignatureHandle

        public ReturnTypeSignatureHandle ToReturnTypeSignatureHandle(MetadataReader reader)
        {
            return new ReturnTypeSignatureHandle(this);
        } // ToReturnTypeSignatureHandle

        public ParameterTypeSignatureHandle ToParameterTypeSignatureHandle(MetadataReader reader)
        {
            return new ParameterTypeSignatureHandle(this);
        } // ToParameterTypeSignatureHandle

        public TypeForwarderHandle ToTypeForwarderHandle(MetadataReader reader)
        {
            return new TypeForwarderHandle(this);
        } // ToTypeForwarderHandle

        public CustomModifierHandle ToCustomModifierHandle(MetadataReader reader)
        {
            return new CustomModifierHandle(this);
        } // ToCustomModifierHandle

        public ConstantBooleanArrayHandle ToConstantBooleanArrayHandle(MetadataReader reader)
        {
            return new ConstantBooleanArrayHandle(this);
        } // ToConstantBooleanArrayHandle

        public ConstantBooleanValueHandle ToConstantBooleanValueHandle(MetadataReader reader)
        {
            return new ConstantBooleanValueHandle(this);
        } // ToConstantBooleanValueHandle

        public ConstantByteArrayHandle ToConstantByteArrayHandle(MetadataReader reader)
        {
            return new ConstantByteArrayHandle(this);
        } // ToConstantByteArrayHandle

        public ConstantByteValueHandle ToConstantByteValueHandle(MetadataReader reader)
        {
            return new ConstantByteValueHandle(this);
        } // ToConstantByteValueHandle

        public ConstantCharArrayHandle ToConstantCharArrayHandle(MetadataReader reader)
        {
            return new ConstantCharArrayHandle(this);
        } // ToConstantCharArrayHandle

        public ConstantCharValueHandle ToConstantCharValueHandle(MetadataReader reader)
        {
            return new ConstantCharValueHandle(this);
        } // ToConstantCharValueHandle

        public ConstantDoubleArrayHandle ToConstantDoubleArrayHandle(MetadataReader reader)
        {
            return new ConstantDoubleArrayHandle(this);
        } // ToConstantDoubleArrayHandle

        public ConstantDoubleValueHandle ToConstantDoubleValueHandle(MetadataReader reader)
        {
            return new ConstantDoubleValueHandle(this);
        } // ToConstantDoubleValueHandle

        public ConstantHandleArrayHandle ToConstantHandleArrayHandle(MetadataReader reader)
        {
            return new ConstantHandleArrayHandle(this);
        } // ToConstantHandleArrayHandle

        public ConstantInt16ArrayHandle ToConstantInt16ArrayHandle(MetadataReader reader)
        {
            return new ConstantInt16ArrayHandle(this);
        } // ToConstantInt16ArrayHandle

        public ConstantInt16ValueHandle ToConstantInt16ValueHandle(MetadataReader reader)
        {
            return new ConstantInt16ValueHandle(this);
        } // ToConstantInt16ValueHandle

        public ConstantInt32ArrayHandle ToConstantInt32ArrayHandle(MetadataReader reader)
        {
            return new ConstantInt32ArrayHandle(this);
        } // ToConstantInt32ArrayHandle

        public ConstantInt32ValueHandle ToConstantInt32ValueHandle(MetadataReader reader)
        {
            return new ConstantInt32ValueHandle(this);
        } // ToConstantInt32ValueHandle

        public ConstantInt64ArrayHandle ToConstantInt64ArrayHandle(MetadataReader reader)
        {
            return new ConstantInt64ArrayHandle(this);
        } // ToConstantInt64ArrayHandle

        public ConstantInt64ValueHandle ToConstantInt64ValueHandle(MetadataReader reader)
        {
            return new ConstantInt64ValueHandle(this);
        } // ToConstantInt64ValueHandle

        public ConstantReferenceValueHandle ToConstantReferenceValueHandle(MetadataReader reader)
        {
            return new ConstantReferenceValueHandle(this);
        } // ToConstantReferenceValueHandle

        public ConstantSByteArrayHandle ToConstantSByteArrayHandle(MetadataReader reader)
        {
            return new ConstantSByteArrayHandle(this);
        } // ToConstantSByteArrayHandle

        public ConstantSByteValueHandle ToConstantSByteValueHandle(MetadataReader reader)
        {
            return new ConstantSByteValueHandle(this);
        } // ToConstantSByteValueHandle

        public ConstantSingleArrayHandle ToConstantSingleArrayHandle(MetadataReader reader)
        {
            return new ConstantSingleArrayHandle(this);
        } // ToConstantSingleArrayHandle

        public ConstantSingleValueHandle ToConstantSingleValueHandle(MetadataReader reader)
        {
            return new ConstantSingleValueHandle(this);
        } // ToConstantSingleValueHandle

        public ConstantStringArrayHandle ToConstantStringArrayHandle(MetadataReader reader)
        {
            return new ConstantStringArrayHandle(this);
        } // ToConstantStringArrayHandle

        public ConstantStringValueHandle ToConstantStringValueHandle(MetadataReader reader)
        {
            return new ConstantStringValueHandle(this);
        } // ToConstantStringValueHandle

        public ConstantUInt16ArrayHandle ToConstantUInt16ArrayHandle(MetadataReader reader)
        {
            return new ConstantUInt16ArrayHandle(this);
        } // ToConstantUInt16ArrayHandle

        public ConstantUInt16ValueHandle ToConstantUInt16ValueHandle(MetadataReader reader)
        {
            return new ConstantUInt16ValueHandle(this);
        } // ToConstantUInt16ValueHandle

        public ConstantUInt32ArrayHandle ToConstantUInt32ArrayHandle(MetadataReader reader)
        {
            return new ConstantUInt32ArrayHandle(this);
        } // ToConstantUInt32ArrayHandle

        public ConstantUInt32ValueHandle ToConstantUInt32ValueHandle(MetadataReader reader)
        {
            return new ConstantUInt32ValueHandle(this);
        } // ToConstantUInt32ValueHandle

        public ConstantUInt64ArrayHandle ToConstantUInt64ArrayHandle(MetadataReader reader)
        {
            return new ConstantUInt64ArrayHandle(this);
        } // ToConstantUInt64ArrayHandle

        public ConstantUInt64ValueHandle ToConstantUInt64ValueHandle(MetadataReader reader)
        {
            return new ConstantUInt64ValueHandle(this);
        } // ToConstantUInt64ValueHandle
    } // Handle

    /// <summary>
    /// MemberReference
    /// </summary>
    public partial struct MemberReference
    {
        internal MetadataReader _reader;
        internal MemberReferenceHandle _handle;
        public MemberReferenceHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Parent
        {
            get
            {
                return _parent;
            }
        } // Parent

        internal Handle _parent;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        
        /// One of: MethodSignature, FieldSignature
        public Handle Signature
        {
            get
            {
                return _signature;
            }
        } // Signature

        internal Handle _signature;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // MemberReference

    /// <summary>
    /// MemberReferenceHandle
    /// </summary>
    public partial struct MemberReferenceHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MemberReferenceHandle)
                return _value == ((MemberReferenceHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MemberReferenceHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MemberReferenceHandle(Handle handle) : this(handle._value)
        {

        }

        internal MemberReferenceHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MemberReference || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MemberReference) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MemberReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MemberReference GetMemberReference(MetadataReader reader)
        {
            return reader.GetMemberReference(this);
        } // GetMemberReference

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MemberReference)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MemberReferenceHandle

    /// <summary>
    /// MetadataReader
    /// </summary>
    public partial class MetadataReader : IMetadataReader
    {
        public TypeDefinition GetTypeDefinition(TypeDefinitionHandle handle)
        {
            var record = new TypeDefinition() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._baseType);
            offset = _streamReader.Read(offset, out record._namespaceDefinition);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._size);
            offset = _streamReader.Read(offset, out record._packingSize);
            offset = _streamReader.Read(offset, out record._enclosingType);
            offset = _streamReader.Read(offset, out record._nestedTypes);
            offset = _streamReader.Read(offset, out record._methods);
            offset = _streamReader.Read(offset, out record._fields);
            offset = _streamReader.Read(offset, out record._properties);
            offset = _streamReader.Read(offset, out record._events);
            offset = _streamReader.Read(offset, out record._genericParameters);
            offset = _streamReader.Read(offset, out record._interfaces);
            offset = _streamReader.Read(offset, out record._methodImpls);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetTypeDefinition

        public TypeReference GetTypeReference(TypeReferenceHandle handle)
        {
            var record = new TypeReference() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._parentNamespaceOrType);
            offset = _streamReader.Read(offset, out record._typeName);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetTypeReference

        public TypeSpecification GetTypeSpecification(TypeSpecificationHandle handle)
        {
            var record = new TypeSpecification() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._signature);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetTypeSpecification

        public ScopeDefinition GetScopeDefinition(ScopeDefinitionHandle handle)
        {
            var record = new ScopeDefinition() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._hashAlgorithm);
            offset = _streamReader.Read(offset, out record._majorVersion);
            offset = _streamReader.Read(offset, out record._minorVersion);
            offset = _streamReader.Read(offset, out record._buildNumber);
            offset = _streamReader.Read(offset, out record._revisionNumber);
            offset = _streamReader.Read(offset, out record._publicKey);
            offset = _streamReader.Read(offset, out record._culture);
            offset = _streamReader.Read(offset, out record._rootNamespaceDefinition);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetScopeDefinition

        public ScopeReference GetScopeReference(ScopeReferenceHandle handle)
        {
            var record = new ScopeReference() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._majorVersion);
            offset = _streamReader.Read(offset, out record._minorVersion);
            offset = _streamReader.Read(offset, out record._buildNumber);
            offset = _streamReader.Read(offset, out record._revisionNumber);
            offset = _streamReader.Read(offset, out record._publicKeyOrToken);
            offset = _streamReader.Read(offset, out record._culture);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetScopeReference

        public NamespaceDefinition GetNamespaceDefinition(NamespaceDefinitionHandle handle)
        {
            var record = new NamespaceDefinition() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._parentScopeOrNamespace);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._typeDefinitions);
            offset = _streamReader.Read(offset, out record._typeForwarders);
            offset = _streamReader.Read(offset, out record._namespaceDefinitions);
            return record;
        } // GetNamespaceDefinition

        public NamespaceReference GetNamespaceReference(NamespaceReferenceHandle handle)
        {
            var record = new NamespaceReference() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._parentScopeOrNamespace);
            offset = _streamReader.Read(offset, out record._name);
            return record;
        } // GetNamespaceReference

        public Method GetMethod(MethodHandle handle)
        {
            var record = new Method() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._implFlags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._signature);
            offset = _streamReader.Read(offset, out record._parameters);
            offset = _streamReader.Read(offset, out record._genericParameters);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetMethod

        public QualifiedMethod GetQualifiedMethod(QualifiedMethodHandle handle)
        {
            var record = new QualifiedMethod() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._method);
            offset = _streamReader.Read(offset, out record._enclosingType);
            return record;
        } // GetQualifiedMethod

        public QualifiedField GetQualifiedField(QualifiedFieldHandle handle)
        {
            var record = new QualifiedField() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._field);
            offset = _streamReader.Read(offset, out record._enclosingType);
            return record;
        } // GetQualifiedField

        public MethodInstantiation GetMethodInstantiation(MethodInstantiationHandle handle)
        {
            var record = new MethodInstantiation() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._method);
            offset = _streamReader.Read(offset, out record._genericTypeArguments);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetMethodInstantiation

        public MemberReference GetMemberReference(MemberReferenceHandle handle)
        {
            var record = new MemberReference() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._parent);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._signature);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetMemberReference

        public Field GetField(FieldHandle handle)
        {
            var record = new Field() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._signature);
            offset = _streamReader.Read(offset, out record._defaultValue);
            offset = _streamReader.Read(offset, out record._offset);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetField

        public Property GetProperty(PropertyHandle handle)
        {
            var record = new Property() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._signature);
            offset = _streamReader.Read(offset, out record._methodSemantics);
            offset = _streamReader.Read(offset, out record._defaultValue);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetProperty

        public Event GetEvent(EventHandle handle)
        {
            var record = new Event() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._type);
            offset = _streamReader.Read(offset, out record._methodSemantics);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetEvent

        public CustomAttribute GetCustomAttribute(CustomAttributeHandle handle)
        {
            var record = new CustomAttribute() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._constructor);
            offset = _streamReader.Read(offset, out record._fixedArguments);
            offset = _streamReader.Read(offset, out record._namedArguments);
            return record;
        } // GetCustomAttribute

        public FixedArgument GetFixedArgument(FixedArgumentHandle handle)
        {
            var record = new FixedArgument() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._type);
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetFixedArgument

        public NamedArgument GetNamedArgument(NamedArgumentHandle handle)
        {
            var record = new NamedArgument() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetNamedArgument

        public ConstantBoxedEnumValue GetConstantBoxedEnumValue(ConstantBoxedEnumValueHandle handle)
        {
            var record = new ConstantBoxedEnumValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetConstantBoxedEnumValue

        public GenericParameter GetGenericParameter(GenericParameterHandle handle)
        {
            var record = new GenericParameter() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._number);
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._kind);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._constraints);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetGenericParameter

        public MethodImpl GetMethodImpl(MethodImplHandle handle)
        {
            var record = new MethodImpl() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._methodBody);
            offset = _streamReader.Read(offset, out record._methodDeclaration);
            return record;
        } // GetMethodImpl

        public Parameter GetParameter(ParameterHandle handle)
        {
            var record = new Parameter() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._flags);
            offset = _streamReader.Read(offset, out record._sequence);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._defaultValue);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetParameter

        public MethodSemantics GetMethodSemantics(MethodSemanticsHandle handle)
        {
            var record = new MethodSemantics() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._attributes);
            offset = _streamReader.Read(offset, out record._method);
            return record;
        } // GetMethodSemantics

        public TypeInstantiationSignature GetTypeInstantiationSignature(TypeInstantiationSignatureHandle handle)
        {
            var record = new TypeInstantiationSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._genericType);
            offset = _streamReader.Read(offset, out record._genericTypeArguments);
            return record;
        } // GetTypeInstantiationSignature

        public SZArraySignature GetSZArraySignature(SZArraySignatureHandle handle)
        {
            var record = new SZArraySignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._elementType);
            return record;
        } // GetSZArraySignature

        public ArraySignature GetArraySignature(ArraySignatureHandle handle)
        {
            var record = new ArraySignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._elementType);
            offset = _streamReader.Read(offset, out record._rank);
            offset = _streamReader.Read(offset, out record._sizes);
            offset = _streamReader.Read(offset, out record._lowerBounds);
            return record;
        } // GetArraySignature

        public ByReferenceSignature GetByReferenceSignature(ByReferenceSignatureHandle handle)
        {
            var record = new ByReferenceSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetByReferenceSignature

        public PointerSignature GetPointerSignature(PointerSignatureHandle handle)
        {
            var record = new PointerSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetPointerSignature

        public TypeVariableSignature GetTypeVariableSignature(TypeVariableSignatureHandle handle)
        {
            var record = new TypeVariableSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._number);
            return record;
        } // GetTypeVariableSignature

        public MethodTypeVariableSignature GetMethodTypeVariableSignature(MethodTypeVariableSignatureHandle handle)
        {
            var record = new MethodTypeVariableSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._number);
            return record;
        } // GetMethodTypeVariableSignature

        public FieldSignature GetFieldSignature(FieldSignatureHandle handle)
        {
            var record = new FieldSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._type);
            offset = _streamReader.Read(offset, out record._customModifiers);
            return record;
        } // GetFieldSignature

        public PropertySignature GetPropertySignature(PropertySignatureHandle handle)
        {
            var record = new PropertySignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._callingConvention);
            offset = _streamReader.Read(offset, out record._customModifiers);
            offset = _streamReader.Read(offset, out record._type);
            offset = _streamReader.Read(offset, out record._parameters);
            return record;
        } // GetPropertySignature

        public MethodSignature GetMethodSignature(MethodSignatureHandle handle)
        {
            var record = new MethodSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._callingConvention);
            offset = _streamReader.Read(offset, out record._genericParameterCount);
            offset = _streamReader.Read(offset, out record._returnType);
            offset = _streamReader.Read(offset, out record._parameters);
            offset = _streamReader.Read(offset, out record._varArgParameters);
            return record;
        } // GetMethodSignature

        public ReturnTypeSignature GetReturnTypeSignature(ReturnTypeSignatureHandle handle)
        {
            var record = new ReturnTypeSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._customModifiers);
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetReturnTypeSignature

        public ParameterTypeSignature GetParameterTypeSignature(ParameterTypeSignatureHandle handle)
        {
            var record = new ParameterTypeSignature() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._customModifiers);
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetParameterTypeSignature

        public TypeForwarder GetTypeForwarder(TypeForwarderHandle handle)
        {
            var record = new TypeForwarder() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._scope);
            offset = _streamReader.Read(offset, out record._name);
            offset = _streamReader.Read(offset, out record._nestedTypes);
            offset = _streamReader.Read(offset, out record._customAttributes);
            return record;
        } // GetTypeForwarder

        public CustomModifier GetCustomModifier(CustomModifierHandle handle)
        {
            var record = new CustomModifier() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._isOptional);
            offset = _streamReader.Read(offset, out record._type);
            return record;
        } // GetCustomModifier

        public ConstantBooleanArray GetConstantBooleanArray(ConstantBooleanArrayHandle handle)
        {
            var record = new ConstantBooleanArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantBooleanArray

        public ConstantBooleanValue GetConstantBooleanValue(ConstantBooleanValueHandle handle)
        {
            var record = new ConstantBooleanValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantBooleanValue

        public ConstantByteArray GetConstantByteArray(ConstantByteArrayHandle handle)
        {
            var record = new ConstantByteArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantByteArray

        public ConstantByteValue GetConstantByteValue(ConstantByteValueHandle handle)
        {
            var record = new ConstantByteValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantByteValue

        public ConstantCharArray GetConstantCharArray(ConstantCharArrayHandle handle)
        {
            var record = new ConstantCharArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantCharArray

        public ConstantCharValue GetConstantCharValue(ConstantCharValueHandle handle)
        {
            var record = new ConstantCharValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantCharValue

        public ConstantDoubleArray GetConstantDoubleArray(ConstantDoubleArrayHandle handle)
        {
            var record = new ConstantDoubleArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantDoubleArray

        public ConstantDoubleValue GetConstantDoubleValue(ConstantDoubleValueHandle handle)
        {
            var record = new ConstantDoubleValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantDoubleValue

        public ConstantHandleArray GetConstantHandleArray(ConstantHandleArrayHandle handle)
        {
            var record = new ConstantHandleArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantHandleArray

        public ConstantInt16Array GetConstantInt16Array(ConstantInt16ArrayHandle handle)
        {
            var record = new ConstantInt16Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt16Array

        public ConstantInt16Value GetConstantInt16Value(ConstantInt16ValueHandle handle)
        {
            var record = new ConstantInt16Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt16Value

        public ConstantInt32Array GetConstantInt32Array(ConstantInt32ArrayHandle handle)
        {
            var record = new ConstantInt32Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt32Array

        public ConstantInt32Value GetConstantInt32Value(ConstantInt32ValueHandle handle)
        {
            var record = new ConstantInt32Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt32Value

        public ConstantInt64Array GetConstantInt64Array(ConstantInt64ArrayHandle handle)
        {
            var record = new ConstantInt64Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt64Array

        public ConstantInt64Value GetConstantInt64Value(ConstantInt64ValueHandle handle)
        {
            var record = new ConstantInt64Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantInt64Value

        public ConstantReferenceValue GetConstantReferenceValue(ConstantReferenceValueHandle handle)
        {
            var record = new ConstantReferenceValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            return record;
        } // GetConstantReferenceValue

        public ConstantSByteArray GetConstantSByteArray(ConstantSByteArrayHandle handle)
        {
            var record = new ConstantSByteArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantSByteArray

        public ConstantSByteValue GetConstantSByteValue(ConstantSByteValueHandle handle)
        {
            var record = new ConstantSByteValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantSByteValue

        public ConstantSingleArray GetConstantSingleArray(ConstantSingleArrayHandle handle)
        {
            var record = new ConstantSingleArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantSingleArray

        public ConstantSingleValue GetConstantSingleValue(ConstantSingleValueHandle handle)
        {
            var record = new ConstantSingleValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantSingleValue

        public ConstantStringArray GetConstantStringArray(ConstantStringArrayHandle handle)
        {
            var record = new ConstantStringArray() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantStringArray

        public ConstantStringValue GetConstantStringValue(ConstantStringValueHandle handle)
        {
            if (IsNull(handle))
                return new ConstantStringValue();
            var record = new ConstantStringValue() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantStringValue

        public ConstantUInt16Array GetConstantUInt16Array(ConstantUInt16ArrayHandle handle)
        {
            var record = new ConstantUInt16Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt16Array

        public ConstantUInt16Value GetConstantUInt16Value(ConstantUInt16ValueHandle handle)
        {
            var record = new ConstantUInt16Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt16Value

        public ConstantUInt32Array GetConstantUInt32Array(ConstantUInt32ArrayHandle handle)
        {
            var record = new ConstantUInt32Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt32Array

        public ConstantUInt32Value GetConstantUInt32Value(ConstantUInt32ValueHandle handle)
        {
            var record = new ConstantUInt32Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt32Value

        public ConstantUInt64Array GetConstantUInt64Array(ConstantUInt64ArrayHandle handle)
        {
            var record = new ConstantUInt64Array() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt64Array

        public ConstantUInt64Value GetConstantUInt64Value(ConstantUInt64ValueHandle handle)
        {
            var record = new ConstantUInt64Value() { _reader = this, _handle = handle };
            var offset = (uint)handle.Offset;
            offset = _streamReader.Read(offset, out record._value);
            return record;
        } // GetConstantUInt64Value

        internal TypeDefinitionHandle ToTypeDefinitionHandle(Handle handle)
        {
            return new TypeDefinitionHandle(handle._value);
        } // ToTypeDefinitionHandle

        internal Handle ToHandle(TypeDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(TypeReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(TypeSpecificationHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ScopeDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ScopeReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(NamespaceDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(NamespaceReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(QualifiedMethodHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(QualifiedFieldHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodInstantiationHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MemberReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(FieldHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(PropertyHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(EventHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(CustomAttributeHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(FixedArgumentHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(NamedArgumentHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantBoxedEnumValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(GenericParameterHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodImplHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ParameterHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodSemanticsHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(TypeInstantiationSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(SZArraySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ArraySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ByReferenceSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(PointerSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(TypeVariableSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodTypeVariableSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(FieldSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(PropertySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(MethodSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ReturnTypeSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ParameterTypeSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(TypeForwarderHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(CustomModifierHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantBooleanArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantBooleanValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantByteArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantByteValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantCharArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantCharValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantDoubleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantDoubleValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantHandleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt16ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt16ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt32ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt32ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt64ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantInt64ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantReferenceValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantSByteArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantSByteValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantSingleArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantSingleValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantStringArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantStringValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt16ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt16ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt32ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt32ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt64ArrayHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal Handle ToHandle(ConstantUInt64ValueHandle handle)
        {
            return new Handle(handle._value);
        } // ToHandle

        internal TypeReferenceHandle ToTypeReferenceHandle(Handle handle)
        {
            return new TypeReferenceHandle(handle._value);
        } // ToTypeReferenceHandle

        internal TypeSpecificationHandle ToTypeSpecificationHandle(Handle handle)
        {
            return new TypeSpecificationHandle(handle._value);
        } // ToTypeSpecificationHandle

        internal ScopeDefinitionHandle ToScopeDefinitionHandle(Handle handle)
        {
            return new ScopeDefinitionHandle(handle._value);
        } // ToScopeDefinitionHandle

        internal ScopeReferenceHandle ToScopeReferenceHandle(Handle handle)
        {
            return new ScopeReferenceHandle(handle._value);
        } // ToScopeReferenceHandle

        internal NamespaceDefinitionHandle ToNamespaceDefinitionHandle(Handle handle)
        {
            return new NamespaceDefinitionHandle(handle._value);
        } // ToNamespaceDefinitionHandle

        internal NamespaceReferenceHandle ToNamespaceReferenceHandle(Handle handle)
        {
            return new NamespaceReferenceHandle(handle._value);
        } // ToNamespaceReferenceHandle

        internal MethodHandle ToMethodHandle(Handle handle)
        {
            return new MethodHandle(handle._value);
        } // ToMethodHandle

        internal QualifiedMethodHandle ToQualifiedMethodHandle(Handle handle)
        {
            return new QualifiedMethodHandle(handle._value);
        } // ToQualifiedMethodHandle

        internal QualifiedFieldHandle ToQualifiedFieldHandle(Handle handle)
        {
            return new QualifiedFieldHandle(handle._value);
        } // ToQualifiedFieldHandle

        internal MethodInstantiationHandle ToMethodInstantiationHandle(Handle handle)
        {
            return new MethodInstantiationHandle(handle._value);
        } // ToMethodInstantiationHandle

        internal MemberReferenceHandle ToMemberReferenceHandle(Handle handle)
        {
            return new MemberReferenceHandle(handle._value);
        } // ToMemberReferenceHandle

        internal FieldHandle ToFieldHandle(Handle handle)
        {
            return new FieldHandle(handle._value);
        } // ToFieldHandle

        internal PropertyHandle ToPropertyHandle(Handle handle)
        {
            return new PropertyHandle(handle._value);
        } // ToPropertyHandle

        internal EventHandle ToEventHandle(Handle handle)
        {
            return new EventHandle(handle._value);
        } // ToEventHandle

        internal CustomAttributeHandle ToCustomAttributeHandle(Handle handle)
        {
            return new CustomAttributeHandle(handle._value);
        } // ToCustomAttributeHandle

        internal FixedArgumentHandle ToFixedArgumentHandle(Handle handle)
        {
            return new FixedArgumentHandle(handle._value);
        } // ToFixedArgumentHandle

        internal NamedArgumentHandle ToNamedArgumentHandle(Handle handle)
        {
            return new NamedArgumentHandle(handle._value);
        } // ToNamedArgumentHandle

        internal ConstantBoxedEnumValueHandle ToConstantBoxedEnumValueHandle(Handle handle)
        {
            return new ConstantBoxedEnumValueHandle(handle._value);
        } // ToConstantBoxedEnumValueHandle

        internal GenericParameterHandle ToGenericParameterHandle(Handle handle)
        {
            return new GenericParameterHandle(handle._value);
        } // ToGenericParameterHandle

        internal MethodImplHandle ToMethodImplHandle(Handle handle)
        {
            return new MethodImplHandle(handle._value);
        } // ToMethodImplHandle

        internal ParameterHandle ToParameterHandle(Handle handle)
        {
            return new ParameterHandle(handle._value);
        } // ToParameterHandle

        internal MethodSemanticsHandle ToMethodSemanticsHandle(Handle handle)
        {
            return new MethodSemanticsHandle(handle._value);
        } // ToMethodSemanticsHandle

        internal TypeInstantiationSignatureHandle ToTypeInstantiationSignatureHandle(Handle handle)
        {
            return new TypeInstantiationSignatureHandle(handle._value);
        } // ToTypeInstantiationSignatureHandle

        internal SZArraySignatureHandle ToSZArraySignatureHandle(Handle handle)
        {
            return new SZArraySignatureHandle(handle._value);
        } // ToSZArraySignatureHandle

        internal ArraySignatureHandle ToArraySignatureHandle(Handle handle)
        {
            return new ArraySignatureHandle(handle._value);
        } // ToArraySignatureHandle

        internal ByReferenceSignatureHandle ToByReferenceSignatureHandle(Handle handle)
        {
            return new ByReferenceSignatureHandle(handle._value);
        } // ToByReferenceSignatureHandle

        internal PointerSignatureHandle ToPointerSignatureHandle(Handle handle)
        {
            return new PointerSignatureHandle(handle._value);
        } // ToPointerSignatureHandle

        internal TypeVariableSignatureHandle ToTypeVariableSignatureHandle(Handle handle)
        {
            return new TypeVariableSignatureHandle(handle._value);
        } // ToTypeVariableSignatureHandle

        internal MethodTypeVariableSignatureHandle ToMethodTypeVariableSignatureHandle(Handle handle)
        {
            return new MethodTypeVariableSignatureHandle(handle._value);
        } // ToMethodTypeVariableSignatureHandle

        internal FieldSignatureHandle ToFieldSignatureHandle(Handle handle)
        {
            return new FieldSignatureHandle(handle._value);
        } // ToFieldSignatureHandle

        internal PropertySignatureHandle ToPropertySignatureHandle(Handle handle)
        {
            return new PropertySignatureHandle(handle._value);
        } // ToPropertySignatureHandle

        internal MethodSignatureHandle ToMethodSignatureHandle(Handle handle)
        {
            return new MethodSignatureHandle(handle._value);
        } // ToMethodSignatureHandle

        internal ReturnTypeSignatureHandle ToReturnTypeSignatureHandle(Handle handle)
        {
            return new ReturnTypeSignatureHandle(handle._value);
        } // ToReturnTypeSignatureHandle

        internal ParameterTypeSignatureHandle ToParameterTypeSignatureHandle(Handle handle)
        {
            return new ParameterTypeSignatureHandle(handle._value);
        } // ToParameterTypeSignatureHandle

        internal TypeForwarderHandle ToTypeForwarderHandle(Handle handle)
        {
            return new TypeForwarderHandle(handle._value);
        } // ToTypeForwarderHandle

        internal CustomModifierHandle ToCustomModifierHandle(Handle handle)
        {
            return new CustomModifierHandle(handle._value);
        } // ToCustomModifierHandle

        internal ConstantBooleanArrayHandle ToConstantBooleanArrayHandle(Handle handle)
        {
            return new ConstantBooleanArrayHandle(handle._value);
        } // ToConstantBooleanArrayHandle

        internal ConstantBooleanValueHandle ToConstantBooleanValueHandle(Handle handle)
        {
            return new ConstantBooleanValueHandle(handle._value);
        } // ToConstantBooleanValueHandle

        internal ConstantByteArrayHandle ToConstantByteArrayHandle(Handle handle)
        {
            return new ConstantByteArrayHandle(handle._value);
        } // ToConstantByteArrayHandle

        internal ConstantByteValueHandle ToConstantByteValueHandle(Handle handle)
        {
            return new ConstantByteValueHandle(handle._value);
        } // ToConstantByteValueHandle

        internal ConstantCharArrayHandle ToConstantCharArrayHandle(Handle handle)
        {
            return new ConstantCharArrayHandle(handle._value);
        } // ToConstantCharArrayHandle

        internal ConstantCharValueHandle ToConstantCharValueHandle(Handle handle)
        {
            return new ConstantCharValueHandle(handle._value);
        } // ToConstantCharValueHandle

        internal ConstantDoubleArrayHandle ToConstantDoubleArrayHandle(Handle handle)
        {
            return new ConstantDoubleArrayHandle(handle._value);
        } // ToConstantDoubleArrayHandle

        internal ConstantDoubleValueHandle ToConstantDoubleValueHandle(Handle handle)
        {
            return new ConstantDoubleValueHandle(handle._value);
        } // ToConstantDoubleValueHandle

        internal ConstantHandleArrayHandle ToConstantHandleArrayHandle(Handle handle)
        {
            return new ConstantHandleArrayHandle(handle._value);
        } // ToConstantHandleArrayHandle

        internal ConstantInt16ArrayHandle ToConstantInt16ArrayHandle(Handle handle)
        {
            return new ConstantInt16ArrayHandle(handle._value);
        } // ToConstantInt16ArrayHandle

        internal ConstantInt16ValueHandle ToConstantInt16ValueHandle(Handle handle)
        {
            return new ConstantInt16ValueHandle(handle._value);
        } // ToConstantInt16ValueHandle

        internal ConstantInt32ArrayHandle ToConstantInt32ArrayHandle(Handle handle)
        {
            return new ConstantInt32ArrayHandle(handle._value);
        } // ToConstantInt32ArrayHandle

        internal ConstantInt32ValueHandle ToConstantInt32ValueHandle(Handle handle)
        {
            return new ConstantInt32ValueHandle(handle._value);
        } // ToConstantInt32ValueHandle

        internal ConstantInt64ArrayHandle ToConstantInt64ArrayHandle(Handle handle)
        {
            return new ConstantInt64ArrayHandle(handle._value);
        } // ToConstantInt64ArrayHandle

        internal ConstantInt64ValueHandle ToConstantInt64ValueHandle(Handle handle)
        {
            return new ConstantInt64ValueHandle(handle._value);
        } // ToConstantInt64ValueHandle

        internal ConstantReferenceValueHandle ToConstantReferenceValueHandle(Handle handle)
        {
            return new ConstantReferenceValueHandle(handle._value);
        } // ToConstantReferenceValueHandle

        internal ConstantSByteArrayHandle ToConstantSByteArrayHandle(Handle handle)
        {
            return new ConstantSByteArrayHandle(handle._value);
        } // ToConstantSByteArrayHandle

        internal ConstantSByteValueHandle ToConstantSByteValueHandle(Handle handle)
        {
            return new ConstantSByteValueHandle(handle._value);
        } // ToConstantSByteValueHandle

        internal ConstantSingleArrayHandle ToConstantSingleArrayHandle(Handle handle)
        {
            return new ConstantSingleArrayHandle(handle._value);
        } // ToConstantSingleArrayHandle

        internal ConstantSingleValueHandle ToConstantSingleValueHandle(Handle handle)
        {
            return new ConstantSingleValueHandle(handle._value);
        } // ToConstantSingleValueHandle

        internal ConstantStringArrayHandle ToConstantStringArrayHandle(Handle handle)
        {
            return new ConstantStringArrayHandle(handle._value);
        } // ToConstantStringArrayHandle

        internal ConstantStringValueHandle ToConstantStringValueHandle(Handle handle)
        {
            return new ConstantStringValueHandle(handle._value);
        } // ToConstantStringValueHandle

        internal ConstantUInt16ArrayHandle ToConstantUInt16ArrayHandle(Handle handle)
        {
            return new ConstantUInt16ArrayHandle(handle._value);
        } // ToConstantUInt16ArrayHandle

        internal ConstantUInt16ValueHandle ToConstantUInt16ValueHandle(Handle handle)
        {
            return new ConstantUInt16ValueHandle(handle._value);
        } // ToConstantUInt16ValueHandle

        internal ConstantUInt32ArrayHandle ToConstantUInt32ArrayHandle(Handle handle)
        {
            return new ConstantUInt32ArrayHandle(handle._value);
        } // ToConstantUInt32ArrayHandle

        internal ConstantUInt32ValueHandle ToConstantUInt32ValueHandle(Handle handle)
        {
            return new ConstantUInt32ValueHandle(handle._value);
        } // ToConstantUInt32ValueHandle

        internal ConstantUInt64ArrayHandle ToConstantUInt64ArrayHandle(Handle handle)
        {
            return new ConstantUInt64ArrayHandle(handle._value);
        } // ToConstantUInt64ArrayHandle

        internal ConstantUInt64ValueHandle ToConstantUInt64ValueHandle(Handle handle)
        {
            return new ConstantUInt64ValueHandle(handle._value);
        } // ToConstantUInt64ValueHandle

        internal bool IsNull(TypeDefinitionHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(TypeReferenceHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(TypeSpecificationHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ScopeDefinitionHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ScopeReferenceHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(NamespaceDefinitionHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(NamespaceReferenceHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(QualifiedMethodHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(QualifiedFieldHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodInstantiationHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MemberReferenceHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(FieldHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(PropertyHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(EventHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(CustomAttributeHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(FixedArgumentHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(NamedArgumentHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantBoxedEnumValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(GenericParameterHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodImplHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ParameterHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodSemanticsHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(TypeInstantiationSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(SZArraySignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ArraySignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ByReferenceSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(PointerSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(TypeVariableSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodTypeVariableSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(FieldSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(PropertySignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(MethodSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ReturnTypeSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ParameterTypeSignatureHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(TypeForwarderHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(CustomModifierHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantBooleanArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantBooleanValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantByteArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantByteValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantCharArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantCharValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantDoubleArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantDoubleValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantHandleArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt16ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt16ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt32ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt32ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt64ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantInt64ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantReferenceValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantSByteArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantSByteValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantSingleArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantSingleValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantStringArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantStringValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt16ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt16ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt32ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt32ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt64ArrayHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull

        internal bool IsNull(ConstantUInt64ValueHandle handle)
        {
            return (handle._value & 0x00FFFFFF) == 0;
        } // IsNull
    } // MetadataReader

    /// <summary>
    /// Method
    /// </summary>
    public partial struct Method
    {
        internal MetadataReader _reader;
        internal MethodHandle _handle;
        public MethodHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public MethodAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal MethodAttributes _flags;
        public MethodImplAttributes ImplFlags
        {
            get
            {
                return _implFlags;
            }
        } // ImplFlags

        internal MethodImplAttributes _implFlags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public MethodSignatureHandle Signature
        {
            get
            {
                return _signature;
            }
        } // Signature

        internal MethodSignatureHandle _signature;
        public IEnumerable<ParameterHandle> Parameters
        {
            get
            {
                return (IEnumerable<ParameterHandle>)_parameters;
            }
        } // Parameters

        internal ParameterHandle[] _parameters;
        public IEnumerable<GenericParameterHandle> GenericParameters
        {
            get
            {
                return (IEnumerable<GenericParameterHandle>)_genericParameters;
            }
        } // GenericParameters

        internal GenericParameterHandle[] _genericParameters;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // Method

    /// <summary>
    /// MethodHandle
    /// </summary>
    public partial struct MethodHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodHandle)
                return _value == ((MethodHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.Method || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.Method) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public Method GetMethod(MetadataReader reader)
        {
            return reader.GetMethod(this);
        } // GetMethod

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.Method)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodHandle

    /// <summary>
    /// MethodImpl
    /// </summary>
    public partial struct MethodImpl
    {
        internal MetadataReader _reader;
        internal MethodImplHandle _handle;
        public MethodImplHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: QualifiedMethod, MemberReference
        public Handle MethodBody
        {
            get
            {
                return _methodBody;
            }
        } // MethodBody

        internal Handle _methodBody;
        
        /// One of: QualifiedMethod, MemberReference
        public Handle MethodDeclaration
        {
            get
            {
                return _methodDeclaration;
            }
        } // MethodDeclaration

        internal Handle _methodDeclaration;
    } // MethodImpl

    /// <summary>
    /// MethodImplHandle
    /// </summary>
    public partial struct MethodImplHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodImplHandle)
                return _value == ((MethodImplHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodImplHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodImplHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodImplHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MethodImpl || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MethodImpl) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodImplHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MethodImpl GetMethodImpl(MetadataReader reader)
        {
            return reader.GetMethodImpl(this);
        } // GetMethodImpl

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MethodImpl)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodImplHandle

    /// <summary>
    /// MethodInstantiation
    /// </summary>
    public partial struct MethodInstantiation
    {
        internal MetadataReader _reader;
        internal MethodInstantiationHandle _handle;
        public MethodInstantiationHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: QualifiedMethod, MemberReference
        public Handle Method
        {
            get
            {
                return _method;
            }
        } // Method

        internal Handle _method;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public IEnumerable<Handle> GenericTypeArguments
        {
            get
            {
                return (IEnumerable<Handle>)_genericTypeArguments;
            }
        } // GenericTypeArguments

        internal Handle[] _genericTypeArguments;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // MethodInstantiation

    /// <summary>
    /// MethodInstantiationHandle
    /// </summary>
    public partial struct MethodInstantiationHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodInstantiationHandle)
                return _value == ((MethodInstantiationHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodInstantiationHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodInstantiationHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodInstantiationHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MethodInstantiation || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MethodInstantiation) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodInstantiationHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MethodInstantiation GetMethodInstantiation(MetadataReader reader)
        {
            return reader.GetMethodInstantiation(this);
        } // GetMethodInstantiation

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MethodInstantiation)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodInstantiationHandle

    /// <summary>
    /// MethodSemantics
    /// </summary>
    public partial struct MethodSemantics
    {
        internal MetadataReader _reader;
        internal MethodSemanticsHandle _handle;
        public MethodSemanticsHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public MethodSemanticsAttributes Attributes
        {
            get
            {
                return _attributes;
            }
        } // Attributes

        internal MethodSemanticsAttributes _attributes;
        public MethodHandle Method
        {
            get
            {
                return _method;
            }
        } // Method

        internal MethodHandle _method;
    } // MethodSemantics

    /// <summary>
    /// MethodSemanticsHandle
    /// </summary>
    public partial struct MethodSemanticsHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodSemanticsHandle)
                return _value == ((MethodSemanticsHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodSemanticsHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodSemanticsHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodSemanticsHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MethodSemantics || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MethodSemantics) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodSemanticsHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MethodSemantics GetMethodSemantics(MetadataReader reader)
        {
            return reader.GetMethodSemantics(this);
        } // GetMethodSemantics

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MethodSemantics)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodSemanticsHandle

    /// <summary>
    /// MethodSignature
    /// </summary>
    public partial struct MethodSignature
    {
        internal MetadataReader _reader;
        internal MethodSignatureHandle _handle;
        public MethodSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public CallingConventions CallingConvention
        {
            get
            {
                return _callingConvention;
            }
        } // CallingConvention

        internal CallingConventions _callingConvention;
        public int GenericParameterCount
        {
            get
            {
                return _genericParameterCount;
            }
        } // GenericParameterCount

        internal int _genericParameterCount;
        public ReturnTypeSignatureHandle ReturnType
        {
            get
            {
                return _returnType;
            }
        } // ReturnType

        internal ReturnTypeSignatureHandle _returnType;
        public IEnumerable<ParameterTypeSignatureHandle> Parameters
        {
            get
            {
                return (IEnumerable<ParameterTypeSignatureHandle>)_parameters;
            }
        } // Parameters

        internal ParameterTypeSignatureHandle[] _parameters;
        public IEnumerable<ParameterTypeSignatureHandle> VarArgParameters
        {
            get
            {
                return (IEnumerable<ParameterTypeSignatureHandle>)_varArgParameters;
            }
        } // VarArgParameters

        internal ParameterTypeSignatureHandle[] _varArgParameters;
    } // MethodSignature

    /// <summary>
    /// MethodSignatureHandle
    /// </summary>
    public partial struct MethodSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodSignatureHandle)
                return _value == ((MethodSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MethodSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MethodSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MethodSignature GetMethodSignature(MetadataReader reader)
        {
            return reader.GetMethodSignature(this);
        } // GetMethodSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MethodSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodSignatureHandle

    /// <summary>
    /// MethodTypeVariableSignature
    /// </summary>
    public partial struct MethodTypeVariableSignature
    {
        internal MetadataReader _reader;
        internal MethodTypeVariableSignatureHandle _handle;
        public MethodTypeVariableSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public int Number
        {
            get
            {
                return _number;
            }
        } // Number

        internal int _number;
    } // MethodTypeVariableSignature

    /// <summary>
    /// MethodTypeVariableSignatureHandle
    /// </summary>
    public partial struct MethodTypeVariableSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is MethodTypeVariableSignatureHandle)
                return _value == ((MethodTypeVariableSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(MethodTypeVariableSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal MethodTypeVariableSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal MethodTypeVariableSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.MethodTypeVariableSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.MethodTypeVariableSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(MethodTypeVariableSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public MethodTypeVariableSignature GetMethodTypeVariableSignature(MetadataReader reader)
        {
            return reader.GetMethodTypeVariableSignature(this);
        } // GetMethodTypeVariableSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.MethodTypeVariableSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // MethodTypeVariableSignatureHandle

    /// <summary>
    /// NamedArgument
    /// </summary>
    public partial struct NamedArgument
    {
        internal MetadataReader _reader;
        internal NamedArgumentHandle _handle;
        public NamedArgumentHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public NamedArgumentMemberKind Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal NamedArgumentMemberKind _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public FixedArgumentHandle Value
        {
            get
            {
                return _value;
            }
        } // Value

        internal FixedArgumentHandle _value;
    } // NamedArgument

    /// <summary>
    /// NamedArgumentHandle
    /// </summary>
    public partial struct NamedArgumentHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is NamedArgumentHandle)
                return _value == ((NamedArgumentHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(NamedArgumentHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal NamedArgumentHandle(Handle handle) : this(handle._value)
        {

        }

        internal NamedArgumentHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.NamedArgument || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.NamedArgument) << 24);
            _Validate();
        }

        public static implicit operator  Handle(NamedArgumentHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public NamedArgument GetNamedArgument(MetadataReader reader)
        {
            return reader.GetNamedArgument(this);
        } // GetNamedArgument

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.NamedArgument)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // NamedArgumentHandle

    /// <summary>
    /// NamespaceDefinition
    /// </summary>
    public partial struct NamespaceDefinition
    {
        internal MetadataReader _reader;
        internal NamespaceDefinitionHandle _handle;
        public NamespaceDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: NamespaceDefinition, ScopeDefinition
        public Handle ParentScopeOrNamespace
        {
            get
            {
                return _parentScopeOrNamespace;
            }
        } // ParentScopeOrNamespace

        internal Handle _parentScopeOrNamespace;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public IEnumerable<TypeDefinitionHandle> TypeDefinitions
        {
            get
            {
                return (IEnumerable<TypeDefinitionHandle>)_typeDefinitions;
            }
        } // TypeDefinitions

        internal TypeDefinitionHandle[] _typeDefinitions;
        public IEnumerable<TypeForwarderHandle> TypeForwarders
        {
            get
            {
                return (IEnumerable<TypeForwarderHandle>)_typeForwarders;
            }
        } // TypeForwarders

        internal TypeForwarderHandle[] _typeForwarders;
        public IEnumerable<NamespaceDefinitionHandle> NamespaceDefinitions
        {
            get
            {
                return (IEnumerable<NamespaceDefinitionHandle>)_namespaceDefinitions;
            }
        } // NamespaceDefinitions

        internal NamespaceDefinitionHandle[] _namespaceDefinitions;
    } // NamespaceDefinition

    /// <summary>
    /// NamespaceDefinitionHandle
    /// </summary>
    public partial struct NamespaceDefinitionHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is NamespaceDefinitionHandle)
                return _value == ((NamespaceDefinitionHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(NamespaceDefinitionHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal NamespaceDefinitionHandle(Handle handle) : this(handle._value)
        {

        }

        internal NamespaceDefinitionHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.NamespaceDefinition || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.NamespaceDefinition) << 24);
            _Validate();
        }

        public static implicit operator  Handle(NamespaceDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public NamespaceDefinition GetNamespaceDefinition(MetadataReader reader)
        {
            return reader.GetNamespaceDefinition(this);
        } // GetNamespaceDefinition

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.NamespaceDefinition)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // NamespaceDefinitionHandle

    /// <summary>
    /// NamespaceReference
    /// </summary>
    public partial struct NamespaceReference
    {
        internal MetadataReader _reader;
        internal NamespaceReferenceHandle _handle;
        public NamespaceReferenceHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: NamespaceReference, ScopeReference
        public Handle ParentScopeOrNamespace
        {
            get
            {
                return _parentScopeOrNamespace;
            }
        } // ParentScopeOrNamespace

        internal Handle _parentScopeOrNamespace;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
    } // NamespaceReference

    /// <summary>
    /// NamespaceReferenceHandle
    /// </summary>
    public partial struct NamespaceReferenceHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is NamespaceReferenceHandle)
                return _value == ((NamespaceReferenceHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(NamespaceReferenceHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal NamespaceReferenceHandle(Handle handle) : this(handle._value)
        {

        }

        internal NamespaceReferenceHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.NamespaceReference || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.NamespaceReference) << 24);
            _Validate();
        }

        public static implicit operator  Handle(NamespaceReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public NamespaceReference GetNamespaceReference(MetadataReader reader)
        {
            return reader.GetNamespaceReference(this);
        } // GetNamespaceReference

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.NamespaceReference)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // NamespaceReferenceHandle

    /// <summary>
    /// Parameter
    /// </summary>
    public partial struct Parameter
    {
        internal MetadataReader _reader;
        internal ParameterHandle _handle;
        public ParameterHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public ParameterAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal ParameterAttributes _flags;
        public ushort Sequence
        {
            get
            {
                return _sequence;
            }
        } // Sequence

        internal ushort _sequence;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification, ConstantBooleanArray, ConstantBooleanValue, ConstantByteArray, ConstantByteValue, ConstantCharArray, ConstantCharValue, ConstantDoubleArray, ConstantDoubleValue, ConstantHandleArray, ConstantInt16Array, ConstantInt16Value, ConstantInt32Array, ConstantInt32Value, ConstantInt64Array, ConstantInt64Value, ConstantReferenceValue, ConstantSByteArray, ConstantSByteValue, ConstantSingleArray, ConstantSingleValue, ConstantStringArray, ConstantStringValue, ConstantUInt16Array, ConstantUInt16Value, ConstantUInt32Array, ConstantUInt32Value, ConstantUInt64Array, ConstantUInt64Value
        public Handle DefaultValue
        {
            get
            {
                return _defaultValue;
            }
        } // DefaultValue

        internal Handle _defaultValue;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // Parameter

    /// <summary>
    /// ParameterHandle
    /// </summary>
    public partial struct ParameterHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ParameterHandle)
                return _value == ((ParameterHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ParameterHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ParameterHandle(Handle handle) : this(handle._value)
        {

        }

        internal ParameterHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.Parameter || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.Parameter) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ParameterHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public Parameter GetParameter(MetadataReader reader)
        {
            return reader.GetParameter(this);
        } // GetParameter

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.Parameter)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ParameterHandle

    /// <summary>
    /// ParameterTypeSignature
    /// </summary>
    public partial struct ParameterTypeSignature
    {
        internal MetadataReader _reader;
        internal ParameterTypeSignatureHandle _handle;
        public ParameterTypeSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get
            {
                return (IEnumerable<CustomModifierHandle>)_customModifiers;
            }
        } // CustomModifiers

        internal CustomModifierHandle[] _customModifiers;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // ParameterTypeSignature

    /// <summary>
    /// ParameterTypeSignatureHandle
    /// </summary>
    public partial struct ParameterTypeSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ParameterTypeSignatureHandle)
                return _value == ((ParameterTypeSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ParameterTypeSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ParameterTypeSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal ParameterTypeSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ParameterTypeSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ParameterTypeSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ParameterTypeSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ParameterTypeSignature GetParameterTypeSignature(MetadataReader reader)
        {
            return reader.GetParameterTypeSignature(this);
        } // GetParameterTypeSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ParameterTypeSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ParameterTypeSignatureHandle

    /// <summary>
    /// PointerSignature
    /// </summary>
    public partial struct PointerSignature
    {
        internal MetadataReader _reader;
        internal PointerSignatureHandle _handle;
        public PointerSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // PointerSignature

    /// <summary>
    /// PointerSignatureHandle
    /// </summary>
    public partial struct PointerSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is PointerSignatureHandle)
                return _value == ((PointerSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(PointerSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal PointerSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal PointerSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.PointerSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.PointerSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(PointerSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public PointerSignature GetPointerSignature(MetadataReader reader)
        {
            return reader.GetPointerSignature(this);
        } // GetPointerSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.PointerSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // PointerSignatureHandle

    /// <summary>
    /// Property
    /// </summary>
    public partial struct Property
    {
        internal MetadataReader _reader;
        internal PropertyHandle _handle;
        public PropertyHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public PropertyAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal PropertyAttributes _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public PropertySignatureHandle Signature
        {
            get
            {
                return _signature;
            }
        } // Signature

        internal PropertySignatureHandle _signature;
        public IEnumerable<MethodSemanticsHandle> MethodSemantics
        {
            get
            {
                return (IEnumerable<MethodSemanticsHandle>)_methodSemantics;
            }
        } // MethodSemantics

        internal MethodSemanticsHandle[] _methodSemantics;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification, ConstantBooleanArray, ConstantBooleanValue, ConstantByteArray, ConstantByteValue, ConstantCharArray, ConstantCharValue, ConstantDoubleArray, ConstantDoubleValue, ConstantHandleArray, ConstantInt16Array, ConstantInt16Value, ConstantInt32Array, ConstantInt32Value, ConstantInt64Array, ConstantInt64Value, ConstantReferenceValue, ConstantSByteArray, ConstantSByteValue, ConstantSingleArray, ConstantSingleValue, ConstantStringArray, ConstantStringValue, ConstantUInt16Array, ConstantUInt16Value, ConstantUInt32Array, ConstantUInt32Value, ConstantUInt64Array, ConstantUInt64Value
        public Handle DefaultValue
        {
            get
            {
                return _defaultValue;
            }
        } // DefaultValue

        internal Handle _defaultValue;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // Property

    /// <summary>
    /// PropertyHandle
    /// </summary>
    public partial struct PropertyHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is PropertyHandle)
                return _value == ((PropertyHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(PropertyHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal PropertyHandle(Handle handle) : this(handle._value)
        {

        }

        internal PropertyHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.Property || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.Property) << 24);
            _Validate();
        }

        public static implicit operator  Handle(PropertyHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public Property GetProperty(MetadataReader reader)
        {
            return reader.GetProperty(this);
        } // GetProperty

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.Property)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // PropertyHandle

    /// <summary>
    /// PropertySignature
    /// </summary>
    public partial struct PropertySignature
    {
        internal MetadataReader _reader;
        internal PropertySignatureHandle _handle;
        public PropertySignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public CallingConventions CallingConvention
        {
            get
            {
                return _callingConvention;
            }
        } // CallingConvention

        internal CallingConventions _callingConvention;
        public IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get
            {
                return (IEnumerable<CustomModifierHandle>)_customModifiers;
            }
        } // CustomModifiers

        internal CustomModifierHandle[] _customModifiers;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
        public IEnumerable<ParameterTypeSignatureHandle> Parameters
        {
            get
            {
                return (IEnumerable<ParameterTypeSignatureHandle>)_parameters;
            }
        } // Parameters

        internal ParameterTypeSignatureHandle[] _parameters;
    } // PropertySignature

    /// <summary>
    /// PropertySignatureHandle
    /// </summary>
    public partial struct PropertySignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is PropertySignatureHandle)
                return _value == ((PropertySignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(PropertySignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal PropertySignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal PropertySignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.PropertySignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.PropertySignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(PropertySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public PropertySignature GetPropertySignature(MetadataReader reader)
        {
            return reader.GetPropertySignature(this);
        } // GetPropertySignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.PropertySignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // PropertySignatureHandle

    /// <summary>
    /// QualifiedField
    /// </summary>
    public partial struct QualifiedField
    {
        internal MetadataReader _reader;
        internal QualifiedFieldHandle _handle;
        public QualifiedFieldHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public FieldHandle Field
        {
            get
            {
                return _field;
            }
        } // Field

        internal FieldHandle _field;
        
        /// One of: TypeDefinition, TypeSpecification
        public Handle EnclosingType
        {
            get
            {
                return _enclosingType;
            }
        } // EnclosingType

        internal Handle _enclosingType;
    } // QualifiedField

    /// <summary>
    /// QualifiedFieldHandle
    /// </summary>
    public partial struct QualifiedFieldHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is QualifiedFieldHandle)
                return _value == ((QualifiedFieldHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(QualifiedFieldHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal QualifiedFieldHandle(Handle handle) : this(handle._value)
        {

        }

        internal QualifiedFieldHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.QualifiedField || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.QualifiedField) << 24);
            _Validate();
        }

        public static implicit operator  Handle(QualifiedFieldHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public QualifiedField GetQualifiedField(MetadataReader reader)
        {
            return reader.GetQualifiedField(this);
        } // GetQualifiedField

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.QualifiedField)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // QualifiedFieldHandle

    /// <summary>
    /// QualifiedMethod
    /// </summary>
    public partial struct QualifiedMethod
    {
        internal MetadataReader _reader;
        internal QualifiedMethodHandle _handle;
        public QualifiedMethodHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public MethodHandle Method
        {
            get
            {
                return _method;
            }
        } // Method

        internal MethodHandle _method;
        
        /// One of: TypeDefinition, TypeSpecification
        public Handle EnclosingType
        {
            get
            {
                return _enclosingType;
            }
        } // EnclosingType

        internal Handle _enclosingType;
    } // QualifiedMethod

    /// <summary>
    /// QualifiedMethodHandle
    /// </summary>
    public partial struct QualifiedMethodHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is QualifiedMethodHandle)
                return _value == ((QualifiedMethodHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(QualifiedMethodHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal QualifiedMethodHandle(Handle handle) : this(handle._value)
        {

        }

        internal QualifiedMethodHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.QualifiedMethod || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.QualifiedMethod) << 24);
            _Validate();
        }

        public static implicit operator  Handle(QualifiedMethodHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public QualifiedMethod GetQualifiedMethod(MetadataReader reader)
        {
            return reader.GetQualifiedMethod(this);
        } // GetQualifiedMethod

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.QualifiedMethod)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // QualifiedMethodHandle

    /// <summary>
    /// ReturnTypeSignature
    /// </summary>
    public partial struct ReturnTypeSignature
    {
        internal MetadataReader _reader;
        internal ReturnTypeSignatureHandle _handle;
        public ReturnTypeSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get
            {
                return (IEnumerable<CustomModifierHandle>)_customModifiers;
            }
        } // CustomModifiers

        internal CustomModifierHandle[] _customModifiers;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle Type
        {
            get
            {
                return _type;
            }
        } // Type

        internal Handle _type;
    } // ReturnTypeSignature

    /// <summary>
    /// ReturnTypeSignatureHandle
    /// </summary>
    public partial struct ReturnTypeSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ReturnTypeSignatureHandle)
                return _value == ((ReturnTypeSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ReturnTypeSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ReturnTypeSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal ReturnTypeSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ReturnTypeSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ReturnTypeSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ReturnTypeSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ReturnTypeSignature GetReturnTypeSignature(MetadataReader reader)
        {
            return reader.GetReturnTypeSignature(this);
        } // GetReturnTypeSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ReturnTypeSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ReturnTypeSignatureHandle

    /// <summary>
    /// SZArraySignature
    /// </summary>
    public partial struct SZArraySignature
    {
        internal MetadataReader _reader;
        internal SZArraySignatureHandle _handle;
        public SZArraySignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle ElementType
        {
            get
            {
                return _elementType;
            }
        } // ElementType

        internal Handle _elementType;
    } // SZArraySignature

    /// <summary>
    /// SZArraySignatureHandle
    /// </summary>
    public partial struct SZArraySignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is SZArraySignatureHandle)
                return _value == ((SZArraySignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(SZArraySignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal SZArraySignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal SZArraySignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.SZArraySignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.SZArraySignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(SZArraySignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public SZArraySignature GetSZArraySignature(MetadataReader reader)
        {
            return reader.GetSZArraySignature(this);
        } // GetSZArraySignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.SZArraySignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // SZArraySignatureHandle

    /// <summary>
    /// ScopeDefinition
    /// </summary>
    public partial struct ScopeDefinition
    {
        internal MetadataReader _reader;
        internal ScopeDefinitionHandle _handle;
        public ScopeDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public AssemblyFlags Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal AssemblyFlags _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public AssemblyHashAlgorithm HashAlgorithm
        {
            get
            {
                return _hashAlgorithm;
            }
        } // HashAlgorithm

        internal AssemblyHashAlgorithm _hashAlgorithm;
        public ushort MajorVersion
        {
            get
            {
                return _majorVersion;
            }
        } // MajorVersion

        internal ushort _majorVersion;
        public ushort MinorVersion
        {
            get
            {
                return _minorVersion;
            }
        } // MinorVersion

        internal ushort _minorVersion;
        public ushort BuildNumber
        {
            get
            {
                return _buildNumber;
            }
        } // BuildNumber

        internal ushort _buildNumber;
        public ushort RevisionNumber
        {
            get
            {
                return _revisionNumber;
            }
        } // RevisionNumber

        internal ushort _revisionNumber;
        public IEnumerable<byte> PublicKey
        {
            get
            {
                return (IEnumerable<byte>)_publicKey;
            }
        } // PublicKey

        internal byte[] _publicKey;
        public ConstantStringValueHandle Culture
        {
            get
            {
                return _culture;
            }
        } // Culture

        internal ConstantStringValueHandle _culture;
        public NamespaceDefinitionHandle RootNamespaceDefinition
        {
            get
            {
                return _rootNamespaceDefinition;
            }
        } // RootNamespaceDefinition

        internal NamespaceDefinitionHandle _rootNamespaceDefinition;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // ScopeDefinition

    /// <summary>
    /// ScopeDefinitionHandle
    /// </summary>
    public partial struct ScopeDefinitionHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ScopeDefinitionHandle)
                return _value == ((ScopeDefinitionHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ScopeDefinitionHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ScopeDefinitionHandle(Handle handle) : this(handle._value)
        {

        }

        internal ScopeDefinitionHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ScopeDefinition || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ScopeDefinition) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ScopeDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ScopeDefinition GetScopeDefinition(MetadataReader reader)
        {
            return reader.GetScopeDefinition(this);
        } // GetScopeDefinition

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ScopeDefinition)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ScopeDefinitionHandle

    /// <summary>
    /// ScopeReference
    /// </summary>
    public partial struct ScopeReference
    {
        internal MetadataReader _reader;
        internal ScopeReferenceHandle _handle;
        public ScopeReferenceHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public AssemblyFlags Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal AssemblyFlags _flags;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public ushort MajorVersion
        {
            get
            {
                return _majorVersion;
            }
        } // MajorVersion

        internal ushort _majorVersion;
        public ushort MinorVersion
        {
            get
            {
                return _minorVersion;
            }
        } // MinorVersion

        internal ushort _minorVersion;
        public ushort BuildNumber
        {
            get
            {
                return _buildNumber;
            }
        } // BuildNumber

        internal ushort _buildNumber;
        public ushort RevisionNumber
        {
            get
            {
                return _revisionNumber;
            }
        } // RevisionNumber

        internal ushort _revisionNumber;
        public IEnumerable<byte> PublicKeyOrToken
        {
            get
            {
                return (IEnumerable<byte>)_publicKeyOrToken;
            }
        } // PublicKeyOrToken

        internal byte[] _publicKeyOrToken;
        public ConstantStringValueHandle Culture
        {
            get
            {
                return _culture;
            }
        } // Culture

        internal ConstantStringValueHandle _culture;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // ScopeReference

    /// <summary>
    /// ScopeReferenceHandle
    /// </summary>
    public partial struct ScopeReferenceHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is ScopeReferenceHandle)
                return _value == ((ScopeReferenceHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(ScopeReferenceHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal ScopeReferenceHandle(Handle handle) : this(handle._value)
        {

        }

        internal ScopeReferenceHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.ScopeReference || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.ScopeReference) << 24);
            _Validate();
        }

        public static implicit operator  Handle(ScopeReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public ScopeReference GetScopeReference(MetadataReader reader)
        {
            return reader.GetScopeReference(this);
        } // GetScopeReference

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.ScopeReference)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // ScopeReferenceHandle

    /// <summary>
    /// TypeDefinition
    /// </summary>
    public partial struct TypeDefinition
    {
        internal MetadataReader _reader;
        internal TypeDefinitionHandle _handle;
        public TypeDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public TypeAttributes Flags
        {
            get
            {
                return _flags;
            }
        } // Flags

        internal TypeAttributes _flags;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle BaseType
        {
            get
            {
                return _baseType;
            }
        } // BaseType

        internal Handle _baseType;
        public NamespaceDefinitionHandle NamespaceDefinition
        {
            get
            {
                return _namespaceDefinition;
            }
        } // NamespaceDefinition

        internal NamespaceDefinitionHandle _namespaceDefinition;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public uint Size
        {
            get
            {
                return _size;
            }
        } // Size

        internal uint _size;
        public ushort PackingSize
        {
            get
            {
                return _packingSize;
            }
        } // PackingSize

        internal ushort _packingSize;
        public TypeDefinitionHandle EnclosingType
        {
            get
            {
                return _enclosingType;
            }
        } // EnclosingType

        internal TypeDefinitionHandle _enclosingType;
        public IEnumerable<TypeDefinitionHandle> NestedTypes
        {
            get
            {
                return (IEnumerable<TypeDefinitionHandle>)_nestedTypes;
            }
        } // NestedTypes

        internal TypeDefinitionHandle[] _nestedTypes;
        public IEnumerable<MethodHandle> Methods
        {
            get
            {
                return (IEnumerable<MethodHandle>)_methods;
            }
        } // Methods

        internal MethodHandle[] _methods;
        public IEnumerable<FieldHandle> Fields
        {
            get
            {
                return (IEnumerable<FieldHandle>)_fields;
            }
        } // Fields

        internal FieldHandle[] _fields;
        public IEnumerable<PropertyHandle> Properties
        {
            get
            {
                return (IEnumerable<PropertyHandle>)_properties;
            }
        } // Properties

        internal PropertyHandle[] _properties;
        public IEnumerable<EventHandle> Events
        {
            get
            {
                return (IEnumerable<EventHandle>)_events;
            }
        } // Events

        internal EventHandle[] _events;
        public IEnumerable<GenericParameterHandle> GenericParameters
        {
            get
            {
                return (IEnumerable<GenericParameterHandle>)_genericParameters;
            }
        } // GenericParameters

        internal GenericParameterHandle[] _genericParameters;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public IEnumerable<Handle> Interfaces
        {
            get
            {
                return (IEnumerable<Handle>)_interfaces;
            }
        } // Interfaces

        internal Handle[] _interfaces;
        public IEnumerable<MethodImplHandle> MethodImpls
        {
            get
            {
                return (IEnumerable<MethodImplHandle>)_methodImpls;
            }
        } // MethodImpls

        internal MethodImplHandle[] _methodImpls;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // TypeDefinition

    /// <summary>
    /// TypeDefinitionHandle
    /// </summary>
    public partial struct TypeDefinitionHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeDefinitionHandle)
                return _value == ((TypeDefinitionHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeDefinitionHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeDefinitionHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeDefinitionHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeDefinition || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeDefinition) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeDefinitionHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeDefinition GetTypeDefinition(MetadataReader reader)
        {
            return reader.GetTypeDefinition(this);
        } // GetTypeDefinition

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeDefinition)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeDefinitionHandle

    /// <summary>
    /// TypeForwarder
    /// </summary>
    public partial struct TypeForwarder
    {
        internal MetadataReader _reader;
        internal TypeForwarderHandle _handle;
        public TypeForwarderHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public ScopeReferenceHandle Scope
        {
            get
            {
                return _scope;
            }
        } // Scope

        internal ScopeReferenceHandle _scope;
        public ConstantStringValueHandle Name
        {
            get
            {
                return _name;
            }
        } // Name

        internal ConstantStringValueHandle _name;
        public IEnumerable<TypeForwarderHandle> NestedTypes
        {
            get
            {
                return (IEnumerable<TypeForwarderHandle>)_nestedTypes;
            }
        } // NestedTypes

        internal TypeForwarderHandle[] _nestedTypes;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // TypeForwarder

    /// <summary>
    /// TypeForwarderHandle
    /// </summary>
    public partial struct TypeForwarderHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeForwarderHandle)
                return _value == ((TypeForwarderHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeForwarderHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeForwarderHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeForwarderHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeForwarder || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeForwarder) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeForwarderHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeForwarder GetTypeForwarder(MetadataReader reader)
        {
            return reader.GetTypeForwarder(this);
        } // GetTypeForwarder

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeForwarder)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeForwarderHandle

    /// <summary>
    /// TypeInstantiationSignature
    /// </summary>
    public partial struct TypeInstantiationSignature
    {
        internal MetadataReader _reader;
        internal TypeInstantiationSignatureHandle _handle;
        public TypeInstantiationSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public Handle GenericType
        {
            get
            {
                return _genericType;
            }
        } // GenericType

        internal Handle _genericType;
        
        /// One of: TypeDefinition, TypeReference, TypeSpecification
        public IEnumerable<Handle> GenericTypeArguments
        {
            get
            {
                return (IEnumerable<Handle>)_genericTypeArguments;
            }
        } // GenericTypeArguments

        internal Handle[] _genericTypeArguments;
    } // TypeInstantiationSignature

    /// <summary>
    /// TypeInstantiationSignatureHandle
    /// </summary>
    public partial struct TypeInstantiationSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeInstantiationSignatureHandle)
                return _value == ((TypeInstantiationSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeInstantiationSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeInstantiationSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeInstantiationSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeInstantiationSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeInstantiationSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeInstantiationSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeInstantiationSignature GetTypeInstantiationSignature(MetadataReader reader)
        {
            return reader.GetTypeInstantiationSignature(this);
        } // GetTypeInstantiationSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeInstantiationSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeInstantiationSignatureHandle

    /// <summary>
    /// TypeReference
    /// </summary>
    public partial struct TypeReference
    {
        internal MetadataReader _reader;
        internal TypeReferenceHandle _handle;
        public TypeReferenceHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: NamespaceReference, TypeReference
        public Handle ParentNamespaceOrType
        {
            get
            {
                return _parentNamespaceOrType;
            }
        } // ParentNamespaceOrType

        internal Handle _parentNamespaceOrType;
        public ConstantStringValueHandle TypeName
        {
            get
            {
                return _typeName;
            }
        } // TypeName

        internal ConstantStringValueHandle _typeName;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // TypeReference

    /// <summary>
    /// TypeReferenceHandle
    /// </summary>
    public partial struct TypeReferenceHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeReferenceHandle)
                return _value == ((TypeReferenceHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeReferenceHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeReferenceHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeReferenceHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeReference || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeReference) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeReferenceHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeReference GetTypeReference(MetadataReader reader)
        {
            return reader.GetTypeReference(this);
        } // GetTypeReference

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeReference)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeReferenceHandle

    /// <summary>
    /// TypeSpecification
    /// </summary>
    public partial struct TypeSpecification
    {
        internal MetadataReader _reader;
        internal TypeSpecificationHandle _handle;
        public TypeSpecificationHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        
        /// One of: TypeDefinition, TypeReference, TypeInstantiationSignature, SZArraySignature, ArraySignature, PointerSignature, ByReferenceSignature, TypeVariableSignature, MethodTypeVariableSignature
        public Handle Signature
        {
            get
            {
                return _signature;
            }
        } // Signature

        internal Handle _signature;
        public IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get
            {
                return (IEnumerable<CustomAttributeHandle>)_customAttributes;
            }
        } // CustomAttributes

        internal CustomAttributeHandle[] _customAttributes;
    } // TypeSpecification

    /// <summary>
    /// TypeSpecificationHandle
    /// </summary>
    public partial struct TypeSpecificationHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeSpecificationHandle)
                return _value == ((TypeSpecificationHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeSpecificationHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeSpecificationHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeSpecificationHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeSpecification || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeSpecification) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeSpecificationHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeSpecification GetTypeSpecification(MetadataReader reader)
        {
            return reader.GetTypeSpecification(this);
        } // GetTypeSpecification

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeSpecification)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeSpecificationHandle

    /// <summary>
    /// TypeVariableSignature
    /// </summary>
    public partial struct TypeVariableSignature
    {
        internal MetadataReader _reader;
        internal TypeVariableSignatureHandle _handle;
        public TypeVariableSignatureHandle Handle
        {
            get
            {
                return _handle;
            }
        } // Handle

        public int Number
        {
            get
            {
                return _number;
            }
        } // Number

        internal int _number;
    } // TypeVariableSignature

    /// <summary>
    /// TypeVariableSignatureHandle
    /// </summary>
    public partial struct TypeVariableSignatureHandle
    {
        public override bool Equals(object obj)
        {
            if (obj is TypeVariableSignatureHandle)
                return _value == ((TypeVariableSignatureHandle)obj)._value;
            else if (obj is Handle)
                return _value == ((Handle)obj)._value;
            else
                return false;
        } // Equals

        public bool Equals(TypeVariableSignatureHandle handle)
        {
            return _value == handle._value;
        } // Equals

        public bool Equals(Handle handle)
        {
            return _value == handle._value;
        } // Equals

        public override int GetHashCode()
        {
            return (int)_value;
        } // GetHashCode

        internal int _value;
        internal TypeVariableSignatureHandle(Handle handle) : this(handle._value)
        {

        }

        internal TypeVariableSignatureHandle(int value)
        {
            HandleType hType = (HandleType)(value >> 24);
            if (!(hType == 0 || hType == HandleType.TypeVariableSignature || hType == HandleType.Null))
                throw new ArgumentException();
            _value = (value & 0x00FFFFFF) | (((int)HandleType.TypeVariableSignature) << 24);
            _Validate();
        }

        public static implicit operator  Handle(TypeVariableSignatureHandle handle)
        {
            return new Handle(handle._value);
        } // Handle

        internal int Offset
        {
            get
            {
                return (this._value & 0x00FFFFFF);
            }
        } // Offset

        public TypeVariableSignature GetTypeVariableSignature(MetadataReader reader)
        {
            return reader.GetTypeVariableSignature(this);
        } // GetTypeVariableSignature

        public bool IsNull(MetadataReader reader)
        {
            return reader.IsNull(this);
        } // IsNull

        public Handle ToHandle(MetadataReader reader)
        {
            return reader.ToHandle(this);
        } // ToHandle

        [System.Diagnostics.Conditional("DEBUG")]
        internal void _Validate()
        {
            if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.TypeVariableSignature)
                throw new ArgumentException();
        } // _Validate

        public override String ToString()
        {
            return String.Format("{0:X8}", _value);
        } // ToString
    } // TypeVariableSignatureHandle
} // Internal.Metadata.NativeFormat
