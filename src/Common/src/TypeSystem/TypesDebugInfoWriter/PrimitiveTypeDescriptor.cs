// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Internal.TypeSystem.TypesDebugInfo
{
    public class PrimitiveTypeDescriptor
    {
        internal enum TYPE_ENUM
        {
            //  Special Types

            T_NOTYPE = 0x0000,   // uncharacterized type (no type)
            T_ABS = 0x0001,   // absolute symbol
            T_SEGMENT = 0x0002,   // segment type
            T_VOID = 0x0003,   // void
            T_HRESULT = 0x0008,   // OLE/COM HRESULT
            T_32PHRESULT = 0x0408,   // OLE/COM HRESULT __ptr32//
            T_64PHRESULT = 0x0608,   // OLE/COM HRESULT __ptr64//
            T_PVOID = 0x0103,   // near pointer to void
            T_PFVOID = 0x0203,   // far pointer to void
            T_PHVOID = 0x0303,   // huge pointer to void
            T_32PVOID = 0x0403,   // 32 bit pointer to void
            T_64PVOID = 0x0603,   // 64 bit pointer to void
            T_CURRENCY = 0x0004,   // BASIC 8 byte currency value
            T_NOTTRANS = 0x0007,   // type not translated by cvpack
            T_BIT = 0x0060,   // bit
            T_PASCHAR = 0x0061,   // Pascal CHAR

            //  Character types

            T_CHAR = 0x0010,   // 8 bit signed
            T_32PCHAR = 0x0410,   // 32 bit pointer to 8 bit signed
            T_64PCHAR = 0x0610,   // 64 bit pointer to 8 bit signed

            T_UCHAR = 0x0020,   // 8 bit unsigned
            T_32PUCHAR = 0x0420,   // 32 bit pointer to 8 bit unsigned
            T_64PUCHAR = 0x0620,   // 64 bit pointer to 8 bit unsigned

            //  really a character types

            T_RCHAR = 0x0070,   // really a char
            T_32PRCHAR = 0x0470,   // 32 bit pointer to a real char
            T_64PRCHAR = 0x0670,   // 64 bit pointer to a real char

            //  really a wide character types

            T_WCHAR = 0x0071,   // wide char
            T_32PWCHAR = 0x0471,   // 32 bit pointer to a wide char
            T_64PWCHAR = 0x0671,   // 64 bit pointer to a wide char

            //  8 bit int types

            T_INT1 = 0x0068,   // 8 bit signed int
            T_32PINT1 = 0x0468,   // 32 bit pointer to 8 bit signed int
            T_64PINT1 = 0x0668,   // 64 bit pointer to 8 bit signed int

            T_UINT1 = 0x0069,   // 8 bit unsigned int
            T_32PUINT1 = 0x0469,   // 32 bit pointer to 8 bit unsigned int
            T_64PUINT1 = 0x0669,   // 64 bit pointer to 8 bit unsigned int

            //  16 bit short types

            T_SHORT = 0x0011,   // 16 bit signed
            T_32PSHORT = 0x0411,   // 32 bit pointer to 16 bit signed
            T_64PSHORT = 0x0611,   // 64 bit pointer to 16 bit signed

            T_USHORT = 0x0021,   // 16 bit unsigned
            T_32PUSHORT = 0x0421,   // 32 bit pointer to 16 bit unsigned
            T_64PUSHORT = 0x0621,   // 64 bit pointer to 16 bit unsigned

            //  16 bit int types

            T_INT2 = 0x0072,   // 16 bit signed int
            T_32PINT2 = 0x0472,   // 32 bit pointer to 16 bit signed int
            T_64PINT2 = 0x0672,   // 64 bit pointer to 16 bit signed int

            T_UINT2 = 0x0073,   // 16 bit unsigned int
            T_32PUINT2 = 0x0473,   // 32 bit pointer to 16 bit unsigned int
            T_64PUINT2 = 0x0673,   // 64 bit pointer to 16 bit unsigned int

            //  32 bit long types

            T_LONG = 0x0012,   // 32 bit signed
            T_ULONG = 0x0022,   // 32 bit unsigned
            T_32PLONG = 0x0412,   // 32 bit pointer to 32 bit signed
            T_32PULONG = 0x0422,   // 32 bit pointer to 32 bit unsigned
            T_64PLONG = 0x0612,   // 64 bit pointer to 32 bit signed
            T_64PULONG = 0x0622,   // 64 bit pointer to 32 bit unsigned

            //  32 bit int types

            T_INT4 = 0x0074,   // 32 bit signed int
            T_32PINT4 = 0x0474,   // 32 bit pointer to 32 bit signed int
            T_64PINT4 = 0x0674,   // 64 bit pointer to 32 bit signed int

            T_UINT4 = 0x0075,   // 32 bit unsigned int
            T_32PUINT4 = 0x0475,   // 32 bit pointer to 32 bit unsigned int
            T_64PUINT4 = 0x0675,   // 64 bit pointer to 32 bit unsigned int

            //  64 bit quad types

            T_QUAD = 0x0013,   // 64 bit signed
            T_32PQUAD = 0x0413,   // 32 bit pointer to 64 bit signed
            T_64PQUAD = 0x0613,   // 64 bit pointer to 64 bit signed

            T_UQUAD = 0x0023,   // 64 bit unsigned
            T_32PUQUAD = 0x0423,   // 32 bit pointer to 64 bit unsigned
            T_64PUQUAD = 0x0623,   // 64 bit pointer to 64 bit unsigned

            //  64 bit int types

            T_INT8 = 0x0076,   // 64 bit signed int
            T_32PINT8 = 0x0476,   // 32 bit pointer to 64 bit signed int
            T_64PINT8 = 0x0676,   // 64 bit pointer to 64 bit signed int

            T_UINT8 = 0x0077,   // 64 bit unsigned int
            T_32PUINT8 = 0x0477,   // 32 bit pointer to 64 bit unsigned int
            T_64PUINT8 = 0x0677,   // 64 bit pointer to 64 bit unsigned int

            //  128 bit octet types

            T_OCT = 0x0014,   // 128 bit signed
            T_32POCT = 0x0414,   // 32 bit pointer to 128 bit signed
            T_64POCT = 0x0614,   // 64 bit pointer to 128 bit signed

            T_UOCT = 0x0024,   // 128 bit unsigned
            T_32PUOCT = 0x0424,   // 32 bit pointer to 128 bit unsigned
            T_64PUOCT = 0x0624,   // 64 bit pointer to 128 bit unsigned

            //  128 bit int types

            T_INT16 = 0x0078,   // 128 bit signed int
            T_32PINT16 = 0x0478,   // 32 bit pointer to 128 bit signed int
            T_64PINT16 = 0x0678,   // 64 bit pointer to 128 bit signed int

            T_UINT16 = 0x0079,   // 128 bit unsigned int
            T_32PUINT16 = 0x0479,   // 32 bit pointer to 128 bit unsigned int
            T_64PUINT16 = 0x0679,   // 64 bit pointer to 128 bit unsigned int

            //  32 bit real types

            T_REAL32 = 0x0040,   // 32 bit real
            T_32PREAL32 = 0x0440,   // 32 bit pointer to 32 bit real
            T_64PREAL32 = 0x0640,   // 64 bit pointer to 32 bit real

            //  64 bit real types

            T_REAL64 = 0x0041,   // 64 bit real
            T_32PREAL64 = 0x0441,   // 32 bit pointer to 64 bit real
            T_64PREAL64 = 0x0641,   // 64 bit pointer to 64 bit real

            //  80 bit real types

            T_REAL80 = 0x0042,   // 80 bit real
            T_32PREAL80 = 0x0442,   // 32 bit pointer to 80 bit real
            T_64PREAL80 = 0x0642,   // 64 bit pointer to 80 bit real

            //  128 bit real types

            T_REAL128 = 0x0043,   // 128 bit real
            T_32PREAL128 = 0x0443,   // 32 bit pointer to 128 bit real
            T_64PREAL128 = 0x0643,   // 64 bit pointer to 128 bit real

            //  32 bit complex types

            T_CPLX32 = 0x0050,   // 32 bit complex
            T_32PCPLX32 = 0x0450,   // 32 bit pointer to 32 bit complex
            T_64PCPLX32 = 0x0650,   // 64 bit pointer to 32 bit complex

            //  64 bit complex types

            T_CPLX64 = 0x0051,   // 64 bit complex
            T_32PCPLX64 = 0x0451,   // 32 bit pointer to 64 bit complex
            T_64PCPLX64 = 0x0651,   // 64 bit pointer to 64 bit complex

            //  80 bit complex types

            T_CPLX80 = 0x0052,   // 80 bit complex
            T_32PCPLX80 = 0x0452,   // 32 bit pointer to 80 bit complex
            T_64PCPLX80 = 0x0652,   // 64 bit pointer to 80 bit complex

            //  128 bit complex types

            T_CPLX128 = 0x0053,   // 128 bit complex
            T_32PCPLX128 = 0x0453,   // 32 bit pointer to 128 bit complex
            T_64PCPLX128 = 0x0653,   // 64 bit pointer to 128 bit complex

            //  boolean types

            T_BOOL08 = 0x0030,   // 8 bit boolean
            T_32PBOOL08 = 0x0430,   // 32 bit pointer to 8 bit boolean
            T_64PBOOL08 = 0x0630,   // 64 bit pointer to 8 bit boolean

            T_BOOL16 = 0x0031,   // 16 bit boolean
            T_32PBOOL16 = 0x0431,   // 32 bit pointer to 18 bit boolean
            T_64PBOOL16 = 0x0631,   // 64 bit pointer to 18 bit boolean

            T_BOOL32 = 0x0032,   // 32 bit boolean
            T_32PBOOL32 = 0x0432,   // 32 bit pointer to 32 bit boolean
            T_64PBOOL32 = 0x0632,   // 64 bit pointer to 32 bit boolean

            T_BOOL64 = 0x0033,   // 64 bit boolean
            T_32PBOOL64 = 0x0433,   // 32 bit pointer to 64 bit boolean
            T_64PBOOL64 = 0x0633,   // 64 bit pointer to 64 bit boolean
        };

        public static uint GetPrimitiveTypeIndex(TypeDesc type)
        {
            Debug.Assert(type.IsPrimitive, "it is not a primitive type");
            switch (type.Category)
            {
                case TypeFlags.Void:
                    return (uint)TYPE_ENUM.T_VOID;
                case TypeFlags.Boolean:
                    return (uint)TYPE_ENUM.T_BOOL08;
                case TypeFlags.Char:
                    return (uint)TYPE_ENUM.T_WCHAR;
                case TypeFlags.SByte:
                    return (uint)TYPE_ENUM.T_INT1;
                case TypeFlags.Byte:
                    return (uint)TYPE_ENUM.T_UINT1;
                case TypeFlags.Int16:
                    return (uint)TYPE_ENUM.T_INT2;
                case TypeFlags.UInt16:
                    return (uint)TYPE_ENUM.T_UINT2;
                case TypeFlags.Int32:
                    return (uint)TYPE_ENUM.T_INT4;
                case TypeFlags.UInt32:
                    return (uint)TYPE_ENUM.T_UINT4;
                case TypeFlags.Int64:
                    return (uint)TYPE_ENUM.T_INT8;
                case TypeFlags.UInt64:
                    return (uint)TYPE_ENUM.T_UINT8;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    if (type.Context.Target.PointerSize == 8)
                    {
                        return (uint)TYPE_ENUM.T_64PVOID;
                    }
                    else
                    {
                        return (uint)TYPE_ENUM.T_32PVOID;
                    }
                case TypeFlags.Single:
                    return (uint)TYPE_ENUM.T_REAL32;
                case TypeFlags.Double:
                    return (uint)TYPE_ENUM.T_REAL64;
                default:
                    return 0;
            }
        }
    }
}
