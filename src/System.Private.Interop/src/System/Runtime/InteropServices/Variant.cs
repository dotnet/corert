// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Variant is the basic COM type for late-binding. It can contain any other COM data type.
    /// This type definition precisely matches the unmanaged data layout so that the struct can be passed
    /// to and from COM calls.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Variant
    {
        // Most of the data types in the Variant are carried in _typeUnion
        [FieldOffset(0)]
        private TypeUnion _typeUnion;

        // Decimal is the largest data type and it needs to use the space that is normally unused in TypeUnion._wReserved1, etc.
        // Hence, it is declared to completely overlap with TypeUnion. A Decimal does not use the first two bytes, and so
        // TypeUnion._vt can still be used to encode the type.
        [FieldOffset(0)]
        private Decimal _decimal;

        [StructLayout(LayoutKind.Explicit)]
        private struct TypeUnion
        {
            [FieldOffset(0)]
            internal ushort _vt;
            [FieldOffset(2)]
            internal ushort _wReserved1;
            [FieldOffset(4)]
            internal ushort _wReserved2;
            [FieldOffset(6)]
            internal ushort _wReserved3;
            [FieldOffset(8)]
            internal UnionTypes _unionTypes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Record
        {
            private IntPtr _record;
            private IntPtr _recordInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UnionTypes
        {
            [FieldOffset(0)]
            internal SByte _i1;
            [FieldOffset(0)]
            internal Int16 _i2;
            [FieldOffset(0)]
            internal Int32 _i4;
            [FieldOffset(0)]
            internal Int64 _i8;
            [FieldOffset(0)]
            internal Byte _ui1;
            [FieldOffset(0)]
            internal UInt16 _ui2;
            [FieldOffset(0)]
            internal UInt32 _ui4;
            [FieldOffset(0)]
            internal UInt64 _ui8;
            [FieldOffset(0)]
            internal Int32 _int;
            [FieldOffset(0)]
            internal UInt32 _uint;
            [FieldOffset(0)]
            internal Int16 _bool;
            [FieldOffset(0)]
            internal Int32 _error;
            [FieldOffset(0)]
            internal Single _r4;
            [FieldOffset(0)]
            internal Double _r8;
            [FieldOffset(0)]
            internal Int64 _cy;
            [FieldOffset(0)]
            internal double _date;
            [FieldOffset(0)]
            internal IntPtr _bstr;
            [FieldOffset(0)]
            internal IntPtr _unknown;
            [FieldOffset(0)]
            internal IntPtr _dispatch;
            [FieldOffset(0)]
            internal IntPtr _pvarVal;
            [FieldOffset(0)]
            internal IntPtr _byref;
            [FieldOffset(0)]
            internal Record _record;
        }
#pragma warning disable 618 // error CS0618: 'VarEnum' is obsolete:
        public Variant(object value)
        {
            this = new Variant();

            if (value == null) this.VariantType = VarEnum.VT_EMPTY;
            else if (value is sbyte) this.AsI1 = (sbyte)value;
            else if (value is byte) this.AsUi1 = (byte)value;
            else if (value is short) this.AsI2 = (short)value;
            else if (value is ushort) this.AsUi2 = (ushort)value;
            else if (value is char)
            {
                char unboxedChar = (char)value;
                this.AsUi2 = (ushort)unboxedChar;
            }
            else if (value is int) this.AsI4 = (int)value;
            else if (value is uint) this.AsUi4 = (uint)value;
            else if (value is long) this.AsI8 = (long)value;
            else if (value is ulong) this.AsUi8 = (ulong)value;
            else if (value is bool) this.AsBool = (bool)value;
            else if (value is decimal) this.AsDecimal = (decimal)value;
            else if (value is double) this.AsR8 = (double)value;
            else if (value is float) this.AsR4 = (float)value;
            else if (value is string) this.AsBstr = (string)value;
            else if (value is Array || value is SafeHandle || value is CriticalHandle || value is VariantWrapper)
            {
                throw new ArgumentException(SR.Format(SR.Arg_VariantTypeNotSupported, value.GetTypeHandle().GetDisplayName()));
            }
            else
            {
                this.AsUnknown = value;
            }
        }

        /// <summary>
        /// Get the managed object representing the Variant.
        /// </summary>
        /// <returns></returns>
        public object ToObject()
        {
            // Check the simple case upfront
            if (IsEmpty)
            {
                return null;
            }

            switch (VariantType)
            {
                case VarEnum.VT_I1: return AsI1;
                case VarEnum.VT_I2: return AsI2;
                case VarEnum.VT_I4: return AsI4;
                case VarEnum.VT_I8: return AsI8;
                case VarEnum.VT_UI1: return AsUi1;
                case VarEnum.VT_UI2: return AsUi2;
                case VarEnum.VT_UI4: return AsUi4;
                case VarEnum.VT_UI8: return AsUi8;
                case VarEnum.VT_INT: return AsInt;
                case VarEnum.VT_UINT: return AsUint;
                case VarEnum.VT_BOOL: return AsBool;
                case VarEnum.VT_R4: return AsR4;
                case VarEnum.VT_R8: return AsR8;
                case VarEnum.VT_DECIMAL: return AsDecimal;
                case VarEnum.VT_BSTR: return AsBstr;
                case VarEnum.VT_UNKNOWN: return AsUnknown;
                default:
                    throw new ArgumentException(SR.Format(SR.Arg_VariantTypeNotSupported, VariantType));
            }
        }

        /// <summary>
        /// Release any unmanaged memory associated with the Variant
        /// </summary>
        /// <returns></returns>
        public void Clear()
        {
            // We do not need to call OLE32's VariantClear for primitive types or ByRefs
            // to safe ourselves the cost of interop transition.
            // ByRef indicates the memory is not owned by the VARIANT itself while
            // primitive types do not have any resources to free up.
            // Hence, only safearrays, BSTRs, interfaces and user types are
            // handled differently.
            VarEnum vt = VariantType;
            if ((vt & VarEnum.VT_BYREF) != 0)
            {
                VariantType = VarEnum.VT_EMPTY;
            }
            else if (
              ((vt & VarEnum.VT_ARRAY) != 0) ||
              ((vt) == VarEnum.VT_BSTR) ||
              ((vt) == VarEnum.VT_UNKNOWN) ||
              ((vt) == VarEnum.VT_DISPATCH) ||
              ((vt) == VarEnum.VT_VARIANT) ||
              ((vt) == VarEnum.VT_RECORD) ||
              ((vt) == VarEnum.VT_VARIANT)
              )
            {
                unsafe
                {
                    fixed (void* pThis = &this)
                    {
                        ExternalInterop.VariantClear((IntPtr)pThis);
                    }
                }
                Debug.Assert(IsEmpty, "variant");
            }
            else
            {
                VariantType = VarEnum.VT_EMPTY;
            }
        }


        public VarEnum VariantType
        {
            get
            {
                return (VarEnum)_typeUnion._vt;
            }
            set
            {
                _typeUnion._vt = (ushort)value;
            }
        }

        internal bool IsEmpty
        {
            get
            {
                return _typeUnion._vt == (ushort)VarEnum.VT_EMPTY;
            }
        }


        // VT_I1
        internal SByte AsI1
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_I1, "variant");
                return _typeUnion._unionTypes._i1;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_I1;
                _typeUnion._unionTypes._i1 = value;
            }
        }

        // VT_I2
        internal Int16 AsI2
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_I2, "variant");
                return _typeUnion._unionTypes._i2;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_I2;
                _typeUnion._unionTypes._i2 = value;
            }
        }

        // VT_I4
        internal Int32 AsI4
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_I4, "variant");
                return _typeUnion._unionTypes._i4;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_I4;
                _typeUnion._unionTypes._i4 = value;
            }
        }

        // VT_I8
        internal Int64 AsI8
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_I8, "variant");
                return _typeUnion._unionTypes._i8;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_I8;
                _typeUnion._unionTypes._i8 = value;
            }
        }

        // VT_UI1
        internal Byte AsUi1
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UI1, "variant");
                return _typeUnion._unionTypes._ui1;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UI1;
                _typeUnion._unionTypes._ui1 = value;
            }
        }

        // VT_UI2
        internal UInt16 AsUi2
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UI2, "variant");
                return _typeUnion._unionTypes._ui2;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UI2;
                _typeUnion._unionTypes._ui2 = value;
            }
        }

        // VT_UI4
        internal UInt32 AsUi4
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UI4, "variant");
                return _typeUnion._unionTypes._ui4;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UI4;
                _typeUnion._unionTypes._ui4 = value;
            }
        }

        // VT_UI8
        internal UInt64 AsUi8
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UI8, "variant");
                return _typeUnion._unionTypes._ui8;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UI8;
                _typeUnion._unionTypes._ui8 = value;
            }
        }

        // VT_INT
        internal Int32 AsInt
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_INT, "variant");
                return _typeUnion._unionTypes._int;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_INT;
                _typeUnion._unionTypes._int = value;
            }
        }

        // VT_UINT
        internal UInt32 AsUint
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UINT, "variant");
                return _typeUnion._unionTypes._uint;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UINT;
                _typeUnion._unionTypes._uint = value;
            }
        }

        // VT_BOOL
        internal bool AsBool
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_BOOL, "variant");
                return _typeUnion._unionTypes._bool != 0;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_BOOL;
                _typeUnion._unionTypes._bool = value ? (short)1 : (short)0;
            }
        }

        // VT_R4
        internal Single AsR4
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_R4, "variant");
                return _typeUnion._unionTypes._r4;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_R4;
                _typeUnion._unionTypes._r4 = value;
            }
        }

        // VT_R8
        internal Double AsR8
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_R8, "variant");
                return _typeUnion._unionTypes._r8;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_R8;
                _typeUnion._unionTypes._r8 = value;
            }
        }

        // VT_DECIMAL
        internal Decimal AsDecimal
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_DECIMAL, "variant");
                // The first byte of Decimal is unused, but usually set to 0
                Variant v = this;
                v._typeUnion._vt = 0;
                return v._decimal;
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_DECIMAL;
                _decimal = value;
                // _vt overlaps with _decimal, and should be set after setting _decimal
                _typeUnion._vt = (ushort)VarEnum.VT_DECIMAL;
            }
        }

        // VT_BSTR
        internal String AsBstr
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_BSTR, "variant");
                return (string)Marshal.PtrToStringBSTR(this._typeUnion._unionTypes._bstr);
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_BSTR;
                if (value == null)
                    _typeUnion._unionTypes._bstr = IntPtr.Zero;
                else
                    _typeUnion._unionTypes._bstr = Marshal.StringToBSTR(value);
            }
        }

        // VT_UNKNOWN
        internal Object AsUnknown
        {
            get
            {
                Debug.Assert(VariantType == VarEnum.VT_UNKNOWN, "variant");
                if (_typeUnion._unionTypes._unknown == IntPtr.Zero)
                    return null;
                return Marshal.GetObjectForIUnknown(_typeUnion._unionTypes._unknown);
            }
            set
            {
                Debug.Assert(IsEmpty, "variant"); // The setter can only be called once as VariantClear might be needed otherwise
                VariantType = VarEnum.VT_UNKNOWN;
                if (value == null)
                    _typeUnion._unionTypes._unknown = IntPtr.Zero;
                else
                    _typeUnion._unionTypes._unknown = Marshal.GetIUnknownForObject(value);
            }
        }
    }
#pragma warning restore 618
}
