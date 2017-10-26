// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System
{
    // Implements the Decimal data type. The Decimal data type can
    // represent values ranging from -79,228,162,514,264,337,593,543,950,335 to
    // 79,228,162,514,264,337,593,543,950,335 with 28 significant digits. The
    // Decimal data type is ideally suited to financial calculations that
    // require a large number of significant digits and no round-off errors.
    //
    // The finite set of values of type Decimal are of the form m
    // / 10e, where m is an integer such that
    // -296 <; m <; 296, and e is an integer
    // between 0 and 28 inclusive.
    //
    // Contrary to the float and double data types, decimal
    // fractional numbers such as 0.1 can be represented exactly in the
    // Decimal representation. In the float and double
    // representations, such numbers are often infinite fractions, making those
    // representations more prone to round-off errors.
    //
    // The Decimal class implements widening conversions from the
    // ubyte, char, short, int, and long types
    // to Decimal. These widening conversions never loose any information
    // and never throw exceptions. The Decimal class also implements
    // narrowing conversions from Decimal to ubyte, char,
    // short, int, and long. These narrowing conversions round
    // the Decimal value towards zero to the nearest integer, and then
    // converts that integer to the destination type. An OverflowException
    // is thrown if the result is not within the range of the destination type.
    //
    // The Decimal class provides a widening conversion from
    // Currency to Decimal. This widening conversion never loses any
    // information and never throws exceptions. The Currency class provides
    // a narrowing conversion from Decimal to Currency. This
    // narrowing conversion rounds the Decimal to four decimals and then
    // converts that number to a Currency. An OverflowException
    // is thrown if the result is not within the range of the Currency type.
    //
    // The Decimal class provides narrowing conversions to and from the
    // float and double types. A conversion from Decimal to
    // float or double may loose precision, but will not loose
    // information about the overall magnitude of the numeric value, and will never
    // throw an exception. A conversion from float or double to
    // Decimal throws an OverflowException if the value is not within
    // the range of the Decimal type.
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public partial struct Decimal : IFormattable, IComparable, IConvertible, IComparable<Decimal>, IEquatable<Decimal>, IDeserializationCallback
    {
        // Sign mask for the flags field. A value of zero in this bit indicates a
        // positive Decimal value, and a value of one in this bit indicates a
        // negative Decimal value.
        // 
        // Look at OleAut's DECIMAL_NEG constant to check for negative values
        // in native code.
        private const uint SignMask = 0x80000000;

        // Scale mask for the flags field. This byte in the flags field contains
        // the power of 10 to divide the Decimal value by. The scale byte must
        // contain a value between 0 and 28 inclusive.
        private const uint ScaleMask = 0x00FF0000;

        // Number of bits scale is shifted by.
        private const int ScaleShift = 16;

        // Constant representing the Decimal value 0.
        public const Decimal Zero = 0m;

        // Constant representing the Decimal value 1.
        public const Decimal One = 1m;

        // Constant representing the Decimal value -1.
        public const Decimal MinusOne = -1m;

        // Constant representing the largest possible Decimal value. The value of
        // this constant is 79,228,162,514,264,337,593,543,950,335.
        public const Decimal MaxValue = 79228162514264337593543950335m;

        // Constant representing the smallest possible Decimal value. The value of
        // this constant is -79,228,162,514,264,337,593,543,950,335.
        public const Decimal MinValue = -79228162514264337593543950335m;

        private const int CurrencyScale = 4; // Divide the "Int64" representation by 1E4 to get the "true" value of the Currency.

        // The lo, mid, hi, and flags fields contain the representation of the
        // Decimal value. The lo, mid, and hi fields contain the 96-bit integer
        // part of the Decimal. Bits 0-15 (the lower word) of the flags field are
        // unused and must be zero; bits 16-23 contain must contain a value between
        // 0 and 28, indicating the power of 10 to divide the 96-bit integer part
        // by to produce the Decimal value; bits 24-30 are unused and must be zero;
        // and finally bit 31 indicates the sign of the Decimal value, 0 meaning
        // positive and 1 meaning negative.
        //
        // NOTE: Do not change the offsets of these fields. This structure maps to the OleAut DECIMAL structure
        // and can be passed as such in P/Invokes.
        [FieldOffset(0)]
        private int flags; // Do not rename (binary serialization)
        [FieldOffset(4)]
        private int hi; // Do not rename (binary serialization)
        [FieldOffset(8)]
        private int lo; // Do not rename (binary serialization)
        [FieldOffset(12)]
        private int mid; // Do not rename (binary serialization)

        // NOTE: This set of fields overlay the ones exposed to serialization (which have to be signed ints for serialization compat.)
        // The code inside Decimal was ported from C++ and expect unsigned values.
        [FieldOffset(0), NonSerialized]
        private uint uflags;
        [FieldOffset(4), NonSerialized]
        private uint uhi;
        [FieldOffset(8), NonSerialized]
        private uint ulo;
        [FieldOffset(12), NonSerialized]
        private uint umid;

        /// <summary>
        /// The low and mid fields combined in little-endian order
        /// </summary>
        [FieldOffset(8), NonSerialized]
        private ulong ulomidLE;

        // Constructs a Decimal from an integer value.
        //
        public Decimal(int value)
        {
            //  JIT today can't inline methods that contains "starg" opcode.
            //  For more details, see DevDiv Bugs 81184: x86 JIT CQ: Removing the inline striction of "starg".
            int value_copy = value;
            if (value_copy >= 0)
            {
                uflags = 0;
            }
            else
            {
                uflags = SignMask;
                value_copy = -value_copy;
            }
            lo = value_copy;
            mid = 0;
            hi = 0;
        }

        // Constructs a Decimal from an unsigned integer value.
        //
        [CLSCompliant(false)]
        public Decimal(uint value)
        {
            uflags = 0;
            ulo = value;
            umid = 0;
            uhi = 0;
        }

        // Constructs a Decimal from a long value.
        //
        public Decimal(long value)
        {
            //  JIT today can't inline methods that contains "starg" opcode.
            //  For more details, see DevDiv Bugs 81184: x86 JIT CQ: Removing the inline striction of "starg".
            long value_copy = value;
            if (value_copy >= 0)
            {
                uflags = 0;
            }
            else
            {
                uflags = SignMask;
                value_copy = -value_copy;
            }
            Low64 = (ulong)value_copy;
            uhi = 0;
        }

        // Constructs a Decimal from an unsigned long value.
        //
        [CLSCompliant(false)]
        public Decimal(ulong value)
        {
            uflags = 0;
            Low64 = value;
            uhi = 0;
        }

        // Constructs a Decimal from a float value.
        //
        public Decimal(float value)
        {
            DecCalc.VarDecFromR4(value, out this);
        }

        // Constructs a Decimal from a double value.
        //
        public Decimal(double value)
        {
            DecCalc.VarDecFromR8(value, out this);
        }

        //
        // Decimal <==> Currency conversion.
        //
        // A Currency represents a positive or negative decimal value with 4 digits past the decimal point. The actual Int64 representation used by these methods
        // is the currency value multiplied by 10,000. For example, a currency value of $12.99 would be represented by the Int64 value 129,900.
        //

        public static Decimal FromOACurrency(long cy)
        {
            Decimal d = default(Decimal);

            ulong absoluteCy; // has to be ulong to accommodate the case where cy == long.MinValue.
            if (cy < 0)
            {
                d.IsNegative = true;
                absoluteCy = (ulong)(-cy);
            }
            else
            {
                absoluteCy = (ulong)cy;
            }

            // In most cases, FromOACurrency() produces a Decimal with Scale set to 4. Unless, that is, some of the trailing digits past the decimal point are zero,
            // in which case, for compatibility with .Net, we reduce the Scale by the number of zeros. While the result is still numerically equivalent, the scale does
            // affect the ToString() value. In particular, it prevents a converted currency value of $12.95 from printing uglily as "12.9500".
            int scale = CurrencyScale;
            if (absoluteCy != 0)  // For compatibility, a currency of 0 emits the Decimal "0.0000" (scale set to 4).
            {
                while (scale != 0 && ((absoluteCy % 10) == 0))
                {
                    scale--;
                    absoluteCy /= 10;
                }
            }

            // No need to set d.Hi32 - a currency will never go high enough for it to be anything other than zero.
            d.Low64 = absoluteCy;
            d.Scale = scale;
            return d;
        }

        public static long ToOACurrency(Decimal value)
        {
            long cy;
            DecCalc.VarCyFromDec(ref value, out cy);
            return cy;
        }

        private static bool IsValid(uint flags) => (flags & ~(SignMask | ScaleMask)) == 0 && ((flags & ScaleMask) <= (28 << 16));

        // Constructs a Decimal from an integer array containing a binary
        // representation. The bits argument must be a non-null integer
        // array with four elements. bits[0], bits[1], and
        // bits[2] contain the low, middle, and high 32 bits of the 96-bit
        // integer part of the Decimal. bits[3] contains the scale factor
        // and sign of the Decimal: bits 0-15 (the lower word) are unused and must
        // be zero; bits 16-23 must contain a value between 0 and 28, indicating
        // the power of 10 to divide the 96-bit integer part by to produce the
        // Decimal value; bits 24-30 are unused and must be zero; and finally bit
        // 31 indicates the sign of the Decimal value, 0 meaning positive and 1
        // meaning negative.
        //
        // Note that there are several possible binary representations for the
        // same numeric value. For example, the value 1 can be represented as {1,
        // 0, 0, 0} (integer value 1 with a scale factor of 0) and equally well as
        // {1000, 0, 0, 0x30000} (integer value 1000 with a scale factor of 3).
        // The possible binary representations of a particular value are all
        // equally valid, and all are numerically equivalent.
        //
        public Decimal(int[] bits)
        {
            SetBits(bits);
        }

        private void SetBits(int[] bits)
        {
            if (bits == null)
                throw new ArgumentNullException(nameof(bits));
            if (bits.Length == 4)
            {
                uint f = (uint)bits[3];
                if (IsValid(f))
                {
                    lo = bits[0];
                    mid = bits[1];
                    hi = bits[2];
                    uflags = f;
                    return;
                }
            }
            throw new ArgumentException(SR.Arg_DecBitCtor);
        }

        // Constructs a Decimal from its constituent parts.
        // 
        public Decimal(int lo, int mid, int hi, bool isNegative, byte scale)
        {
            if (scale > 28)
                throw new ArgumentOutOfRangeException(nameof(scale), SR.ArgumentOutOfRange_DecimalScale);
            this.lo = lo;
            this.mid = mid;
            this.hi = hi;
            uflags = ((uint)scale) << 16;
            if (isNegative)
                uflags |= SignMask;
        }

        void IDeserializationCallback.OnDeserialization(Object sender)
        {
            // OnDeserialization is called after each instance of this class is deserialized.
            // This callback method performs decimal validation after being deserialized.
            try
            {
                SetBits(GetBits(this));
            }
            catch (ArgumentException e)
            {
                throw new SerializationException(SR.Overflow_Decimal, e);
            }
        }

        // Constructs a Decimal from its constituent parts.
        private Decimal(int lo, int mid, int hi, int flags)
        {
            if ((flags & ~(SignMask | ScaleMask)) == 0 && (flags & ScaleMask) <= (28 << 16))
            {
                this.lo = lo;
                this.mid = mid;
                this.hi = hi;
                this.flags = flags;
                return;
            }
            throw new ArgumentException(SR.Arg_DecBitCtor);
        }

        // Returns the absolute value of the given Decimal. If d is
        // positive, the result is d. If d is negative, the result
        // is -d.
        //
        internal static Decimal Abs(Decimal d)
        {
            return new Decimal(d.lo, d.mid, d.hi, (int)(d.uflags & ~SignMask));
        }


        // Adds two Decimal values.
        //
        public static Decimal Add(Decimal d1, Decimal d2)
        {
            DecCalc.VarDecAdd(ref d1, ref d2);
            return d1;
        }


        // Rounds a Decimal to an integer value. The Decimal argument is rounded
        // towards positive infinity.
        public static Decimal Ceiling(Decimal d)
        {
            return (-(Decimal.Floor(-d)));
        }

        // Compares two Decimal values, returning an integer that indicates their
        // relationship.
        //
        public static int Compare(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2);
        }

        // Compares this object to another object, returning an integer that
        // indicates the relationship. 
        // Returns a value less than zero if this  object
        // null is considered to be less than any instance.
        // If object is not of type Decimal, this method throws an ArgumentException.
        // 
        public int CompareTo(Object value)
        {
            if (value == null)
                return 1;
            if (!(value is Decimal))
                throw new ArgumentException(SR.Arg_MustBeDecimal);

            Decimal other = (Decimal)value;
            return DecCalc.VarDecCmp(ref this, ref other);
        }

        public int CompareTo(Decimal value)
        {
            return DecCalc.VarDecCmp(ref this, ref value);
        }

        // Divides two Decimal values.
        //
        public static Decimal Divide(Decimal d1, Decimal d2)
        {
            DecCalc.VarDecDiv(ref d1, ref d2);
            return d1;
        }

        // Checks if this Decimal is equal to a given object. Returns true
        // if the given object is a boxed Decimal and its value is equal to the
        // value of this Decimal. Returns false otherwise.
        //
        public override bool Equals(Object value)
        {
            if (value is Decimal)
            {
                Decimal other = (Decimal)value;
                return DecCalc.VarDecCmp(ref this, ref other) == 0;
            }
            return false;
        }

        public bool Equals(Decimal value)
        {
            return DecCalc.VarDecCmp(ref this, ref value) == 0;
        }

        // Returns the hash code for this Decimal.
        //
        public unsafe override int GetHashCode()
        {
            double dbl = DecCalc.VarR8FromDec(ref this);
            if (dbl == 0.0)
                // Ensure 0 and -0 have the same hash code
                return 0;

            // conversion to double is lossy and produces rounding errors so we mask off the lowest 4 bits
            // 
            // For example these two numerically equal decimals with different internal representations produce
            // slightly different results when converted to double:
            //
            // decimal a = new decimal(new int[] { 0x76969696, 0x2fdd49fa, 0x409783ff, 0x00160000 });
            //                     => (decimal)1999021.176470588235294117647000000000 => (double)1999021.176470588
            // decimal b = new decimal(new int[] { 0x3f0f0f0f, 0x1e62edcc, 0x06758d33, 0x00150000 }); 
            //                     => (decimal)1999021.176470588235294117647000000000 => (double)1999021.1764705882
            //
            return (int)(((((uint*)&dbl)[0]) & 0xFFFFFFF0) ^ ((uint*)&dbl)[1]);
        }

        // Compares two Decimal values for equality. Returns true if the two
        // Decimal values are equal, or false if they are not equal.
        //
        public static bool Equals(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) == 0;
        }

        // Rounds a Decimal to an integer value. The Decimal argument is rounded
        // towards negative infinity.
        //
        public static Decimal Floor(Decimal d)
        {
            DecCalc.VarDecInt(ref d);
            return d;
        }

        // Converts this Decimal to a string. The resulting string consists of an
        // optional minus sign ("-") followed to a sequence of digits ("0" - "9"),
        // optionally followed by a decimal point (".") and another sequence of
        // digits.
        //
        public override String ToString()
        {
            return Number.FormatDecimal(this, null, null);
        }

        public String ToString(String format)
        {
            return Number.FormatDecimal(this, format, null);
        }

        public String ToString(IFormatProvider provider)
        {
            return Number.FormatDecimal(this, null, provider);
        }

        public String ToString(String format, IFormatProvider provider)
        {
            return Number.FormatDecimal(this, format, provider);
        }


        // Converts a string to a Decimal. The string must consist of an optional
        // minus sign ("-") followed by a sequence of digits ("0" - "9"). The
        // sequence of digits may optionally contain a single decimal point (".")
        // character. Leading and trailing whitespace characters are allowed.
        // Parse also allows a currency symbol, a trailing negative sign, and
        // parentheses in the number.
        //
        public static Decimal Parse(String s)
        {
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s.AsReadOnlySpan(), NumberStyles.Number, null);
        }

        internal const NumberStyles InvalidNumberStyles = ~(NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                                                           | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign
                                                           | NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint
                                                           | NumberStyles.AllowThousands | NumberStyles.AllowExponent
                                                           | NumberStyles.AllowCurrencySymbol | NumberStyles.AllowHexSpecifier);

        internal static void ValidateParseStyleFloatingPoint(NumberStyles style)
        {
            // Check for undefined flags
            if ((style & InvalidNumberStyles) != 0)
            {
                throw new ArgumentException(SR.Argument_InvalidNumberStyles, nameof(style));
            }
            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            { // Check for hex number
                throw new ArgumentException(SR.Arg_HexStyleNotSupported);
            }
        }

        public static Decimal Parse(String s, NumberStyles style)
        {
            ValidateParseStyleFloatingPoint(style);
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s.AsReadOnlySpan(), style, null);
        }

        public static Decimal Parse(String s, IFormatProvider provider)
        {
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s.AsReadOnlySpan(), NumberStyles.Number, provider);
        }

        public static Decimal Parse(String s, NumberStyles style, IFormatProvider provider)
        {
            ValidateParseStyleFloatingPoint(style);
            if (s == null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.s);
            return Number.ParseDecimal(s.AsReadOnlySpan(), style, provider);
        }

        public static Decimal Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider)
        {
            ValidateParseStyleFloatingPoint(style);
            return Number.ParseDecimal(s, style, provider);
        }

        public static Boolean TryParse(String s, out Decimal result)
        {
            if (s == null)
            {
                result = 0;
                return false;
            }

            return Number.TryParseDecimal(s.AsReadOnlySpan(), NumberStyles.Number, null, out result);
        }

        public static Boolean TryParse(String s, NumberStyles style, IFormatProvider provider, out Decimal result)
        {
            ValidateParseStyleFloatingPoint(style);
            if (s == null)
            {
                result = 0;
                return false;
            }
            return Number.TryParseDecimal(s.AsReadOnlySpan(), style, provider, out result);
        }

        public static Boolean TryParse(ReadOnlySpan<char> s, out Decimal result, NumberStyles style = NumberStyles.Integer, IFormatProvider provider = null)
        {
            ValidateParseStyleFloatingPoint(style);
            return Number.TryParseDecimal(s, style, provider, out result);
        }

        // Returns a binary representation of a Decimal. The return value is an
        // integer array with four elements. Elements 0, 1, and 2 contain the low,
        // middle, and high 32 bits of the 96-bit integer part of the Decimal.
        // Element 3 contains the scale factor and sign of the Decimal: bits 0-15
        // (the lower word) are unused; bits 16-23 contain a value between 0 and
        // 28, indicating the power of 10 to divide the 96-bit integer part by to
        // produce the Decimal value; bits 24-30 are unused; and finally bit 31
        // indicates the sign of the Decimal value, 0 meaning positive and 1
        // meaning negative.
        //
        public static int[] GetBits(Decimal d)
        {
            return new int[] { d.lo, d.mid, d.hi, d.flags };
        }

        internal static void GetBytes(Decimal d, byte[] buffer)
        {
            Debug.Assert((buffer != null && buffer.Length >= 16), "[GetBytes]buffer != null && buffer.Length >= 16");
            buffer[0] = (byte)d.lo;
            buffer[1] = (byte)(d.lo >> 8);
            buffer[2] = (byte)(d.lo >> 16);
            buffer[3] = (byte)(d.lo >> 24);

            buffer[4] = (byte)d.mid;
            buffer[5] = (byte)(d.mid >> 8);
            buffer[6] = (byte)(d.mid >> 16);
            buffer[7] = (byte)(d.mid >> 24);

            buffer[8] = (byte)d.hi;
            buffer[9] = (byte)(d.hi >> 8);
            buffer[10] = (byte)(d.hi >> 16);
            buffer[11] = (byte)(d.hi >> 24);

            buffer[12] = (byte)d.flags;
            buffer[13] = (byte)(d.flags >> 8);
            buffer[14] = (byte)(d.flags >> 16);
            buffer[15] = (byte)(d.flags >> 24);
        }

        // Returns the larger of two Decimal values.
        //
        internal static Decimal Max(Decimal d1, Decimal d2)
        {
            return Compare(d1, d2) >= 0 ? d1 : d2;
        }

        // Returns the smaller of two Decimal values.
        //
        internal static Decimal Min(Decimal d1, Decimal d2)
        {
            return Compare(d1, d2) < 0 ? d1 : d2;
        }


        public static Decimal Remainder(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecMod(ref d1, ref d2);
        }

        // Multiplies two Decimal values.
        //
        public static Decimal Multiply(Decimal d1, Decimal d2)
        {
            DecCalc.VarDecMul(ref d1, ref d2);
            return d1;
        }

        // Returns the negated value of the given Decimal. If d is non-zero,
        // the result is -d. If d is zero, the result is zero.
        //
        public static Decimal Negate(Decimal d)
        {
            return new Decimal(d.lo, d.mid, d.hi, (int)(d.uflags ^ SignMask));
        }

        // Rounds a Decimal value to a given number of decimal places. The value
        // given by d is rounded to the number of decimal places given by
        // decimals. The decimals argument must be an integer between
        // 0 and 28 inclusive.
        //
        // By default a mid-point value is rounded to the nearest even number. If the mode is
        // passed in, it can also round away from zero.

        public static Decimal Round(Decimal d)
        {
            return Round(d, 0);
        }

        public static Decimal Round(Decimal d, int decimals)
        {
            Decimal result = new Decimal();

            if (decimals < 0 || decimals > 28)
                throw new ArgumentOutOfRangeException(nameof(decimals), SR.ArgumentOutOfRange_DecimalRound);

            DecCalc.VarDecRound(ref d, decimals, ref result);

            d = result;
            return d;
        }

        public static Decimal Round(Decimal d, MidpointRounding mode)
        {
            return Round(d, 0, mode);
        }

        public static Decimal Round(Decimal d, int decimals, MidpointRounding mode)
        {
            if (decimals < 0 || decimals > 28)
                throw new ArgumentOutOfRangeException(nameof(decimals), SR.ArgumentOutOfRange_DecimalRound);
            if (mode < MidpointRounding.ToEven || mode > MidpointRounding.AwayFromZero)
                throw new ArgumentException(SR.Format(SR.Argument_InvalidEnumValue, mode, "MidpointRounding"), nameof(mode));

            if (mode == MidpointRounding.ToEven)
            {
                Decimal result = new Decimal();
                DecCalc.VarDecRound(ref d, decimals, ref result);
                d = result;
            }
            else
            {
                DecCalc.InternalRoundFromZero(ref d, decimals);
            }
            return d;
        }

        internal static int Sign(ref decimal d) => (d.lo | d.mid | d.hi) == 0 ? 0 : (d.flags >> 31) | 1;

        // Subtracts two Decimal values.
        //
        public static Decimal Subtract(Decimal d1, Decimal d2)
        {
            DecCalc.VarDecSub(ref d1, ref d2);
            return d1;
        }

        // Converts a Decimal to an unsigned byte. The Decimal value is rounded
        // towards zero to the nearest integer value, and the result of this
        // operation is returned as a byte.
        //
        public static byte ToByte(Decimal value)
        {
            uint temp;
            try
            {
                temp = ToUInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Byte, e);
            }
            if (temp < Byte.MinValue || temp > Byte.MaxValue) throw new OverflowException(SR.Overflow_Byte);
            return (byte)temp;
        }

        // Converts a Decimal to a signed byte. The Decimal value is rounded
        // towards zero to the nearest integer value, and the result of this
        // operation is returned as a byte.
        //
        [CLSCompliant(false)]
        public static sbyte ToSByte(Decimal value)
        {
            int temp;
            try
            {
                temp = ToInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_SByte, e);
            }
            if (temp < SByte.MinValue || temp > SByte.MaxValue) throw new OverflowException(SR.Overflow_SByte);
            return (sbyte)temp;
        }

        // Converts a Decimal to a short. The Decimal value is
        // rounded towards zero to the nearest integer value, and the result of
        // this operation is returned as a short.
        //
        public static short ToInt16(Decimal value)
        {
            int temp;
            try
            {
                temp = ToInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Int16, e);
            }
            if (temp < Int16.MinValue || temp > Int16.MaxValue) throw new OverflowException(SR.Overflow_Int16);
            return (short)temp;
        }

        // Converts a Decimal to a double. Since a double has fewer significant
        // digits than a Decimal, this operation may produce round-off errors.
        //
        public static double ToDouble(Decimal d)
        {
            return DecCalc.VarR8FromDec(ref d);
        }

        // Converts a Decimal to an integer. The Decimal value is rounded towards
        // zero to the nearest integer value, and the result of this operation is
        // returned as an integer.
        //
        public static int ToInt32(Decimal d)
        {
            if (d.Scale != 0) DecCalc.VarDecFix(ref d);
            if (d.hi == 0 && d.mid == 0)
            {
                int i = d.lo;
                if (!d.IsNegative)
                {
                    if (i >= 0) return i;
                }
                else
                {
                    i = -i;
                    if (i <= 0) return i;
                }
            }
            throw new OverflowException(SR.Overflow_Int32);
        }

        // Converts a Decimal to a long. The Decimal value is rounded towards zero
        // to the nearest integer value, and the result of this operation is
        // returned as a long.
        //
        public static long ToInt64(Decimal d)
        {
            if (d.Scale != 0) DecCalc.VarDecFix(ref d);
            if (d.uhi == 0)
            {
                long l = d.ulo | (long)(int)d.umid << 32;
                if (!d.IsNegative)
                {
                    if (l >= 0) return l;
                }
                else
                {
                    l = -l;
                    if (l <= 0) return l;
                }
            }
            throw new OverflowException(SR.Overflow_Int64);
        }

        // Converts a Decimal to an ushort. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as an ushort.
        //
        [CLSCompliant(false)]
        public static ushort ToUInt16(Decimal value)
        {
            uint temp;
            try
            {
                temp = ToUInt32(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_UInt16, e);
            }
            if (temp < UInt16.MinValue || temp > UInt16.MaxValue) throw new OverflowException(SR.Overflow_UInt16);
            return (ushort)temp;
        }

        // Converts a Decimal to an unsigned integer. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as an unsigned integer.
        //
        [CLSCompliant(false)]
        public static uint ToUInt32(Decimal d)
        {
            if (d.Scale != 0) DecCalc.VarDecFix(ref d);
            if (d.uhi == 0 && d.umid == 0)
            {
                if (!d.IsNegative || d.ulo == 0)
                    return d.ulo;
            }
            throw new OverflowException(SR.Overflow_UInt32);
        }

        // Converts a Decimal to an unsigned long. The Decimal 
        // value is rounded towards zero to the nearest integer value, and the 
        // result of this operation is returned as a long.
        //
        [CLSCompliant(false)]
        public static ulong ToUInt64(Decimal d)
        {
            if (d.Scale != 0) DecCalc.VarDecFix(ref d);
            if (d.uhi == 0)
            {
                ulong l = (ulong)d.ulo | ((ulong)d.umid << 32);
                if (!d.IsNegative || l == 0)
                    return l;
            }
            throw new OverflowException(SR.Overflow_UInt64);
        }

        // Converts a Decimal to a float. Since a float has fewer significant
        // digits than a Decimal, this operation may produce round-off errors.
        //
        public static float ToSingle(Decimal d)
        {
            return DecCalc.VarR4FromDec(ref d);
        }

        // Truncates a Decimal to an integer value. The Decimal argument is rounded
        // towards zero to the nearest integer value, corresponding to removing all
        // digits after the decimal point.
        //
        public static Decimal Truncate(Decimal d)
        {
            DecCalc.VarDecFix(ref d);
            return d;
        }

        public static implicit operator Decimal(byte value)
        {
            return new Decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator Decimal(sbyte value)
        {
            return new Decimal(value);
        }

        public static implicit operator Decimal(short value)
        {
            return new Decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator Decimal(ushort value)
        {
            return new Decimal(value);
        }

        public static implicit operator Decimal(char value)
        {
            return new Decimal(value);
        }

        public static implicit operator Decimal(int value)
        {
            return new Decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator Decimal(uint value)
        {
            return new Decimal(value);
        }

        public static implicit operator Decimal(long value)
        {
            return new Decimal(value);
        }

        [CLSCompliant(false)]
        public static implicit operator Decimal(ulong value)
        {
            return new Decimal(value);
        }


        public static explicit operator Decimal(float value)
        {
            return new Decimal(value);
        }

        public static explicit operator Decimal(double value)
        {
            return new Decimal(value);
        }

        public static explicit operator byte(Decimal value)
        {
            return ToByte(value);
        }

        [CLSCompliant(false)]
        public static explicit operator sbyte(Decimal value)
        {
            return ToSByte(value);
        }

        public static explicit operator char(Decimal value)
        {
            UInt16 temp;
            try
            {
                temp = ToUInt16(value);
            }
            catch (OverflowException e)
            {
                throw new OverflowException(SR.Overflow_Char, e);
            }
            return (char)temp;
        }

        public static explicit operator short(Decimal value)
        {
            return ToInt16(value);
        }

        [CLSCompliant(false)]
        public static explicit operator ushort(Decimal value)
        {
            return ToUInt16(value);
        }

        public static explicit operator int(Decimal value)
        {
            return ToInt32(value);
        }

        [CLSCompliant(false)]
        public static explicit operator uint(Decimal value)
        {
            return ToUInt32(value);
        }

        public static explicit operator long(Decimal value)
        {
            return ToInt64(value);
        }

        [CLSCompliant(false)]
        public static explicit operator ulong(Decimal value)
        {
            return ToUInt64(value);
        }

        public static explicit operator float(Decimal value)
        {
            return ToSingle(value);
        }

        public static explicit operator double(Decimal value)
        {
            return ToDouble(value);
        }

        public static Decimal operator +(Decimal d)
        {
            return d;
        }

        public static Decimal operator -(Decimal d)
        {
            return Negate(d);
        }

        public static Decimal operator ++(Decimal d)
        {
            return Add(d, One);
        }

        public static Decimal operator --(Decimal d)
        {
            return Subtract(d, One);
        }

        public static Decimal operator +(Decimal d1, Decimal d2)
        {
            return Add(d1, d2);
        }

        public static Decimal operator -(Decimal d1, Decimal d2)
        {
            return Subtract(d1, d2);
        }

        public static Decimal operator *(Decimal d1, Decimal d2)
        {
            return Multiply(d1, d2);
        }

        public static Decimal operator /(Decimal d1, Decimal d2)
        {
            return Divide(d1, d2);
        }

        public static Decimal operator %(Decimal d1, Decimal d2)
        {
            return Remainder(d1, d2);
        }

        public static bool operator ==(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) == 0;
        }

        public static bool operator !=(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) != 0;
        }

        public static bool operator <(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) < 0;
        }

        public static bool operator <=(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) <= 0;
        }

        public static bool operator >(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) > 0;
        }

        public static bool operator >=(Decimal d1, Decimal d2)
        {
            return DecCalc.VarDecCmp(ref d1, ref d2) >= 0;
        }

        //
        // IConvertible implementation
        //

        public TypeCode GetTypeCode()
        {
            return TypeCode.Decimal;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this);
        }


        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Decimal", "Char"));
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this);
        }

        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return this;
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException(String.Format(SR.InvalidCast_FromTo, "Decimal", "DateTime"));
        }

        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }
    }
}
