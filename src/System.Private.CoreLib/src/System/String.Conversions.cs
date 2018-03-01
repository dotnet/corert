// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;

namespace System
{
    public partial class String : IEnumerable, IEnumerable<char>, IConvertible, ICloneable
    {
        public object Clone()
        {
            return this;
        }

        public static unsafe String Copy(String str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            int length = str.Length;

            String result = FastAllocateString(length);

            fixed (char* dest = &result._firstChar)
            fixed (char* src = &str._firstChar)
            {
                wstrcpy(dest, src, length);
            }
            return result;
        }

        // Converts a substring of this string to an array of characters.  Copies the
        // characters of this string beginning at position sourceIndex and ending at
        // sourceIndex + count - 1 to the character array buffer, beginning
        // at destinationIndex.
        //
        unsafe public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), SR.ArgumentOutOfRange_NegativeCount);
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), SR.ArgumentOutOfRange_Index);
            if (count > Length - sourceIndex)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), SR.ArgumentOutOfRange_IndexCount);
            if (destinationIndex > destination.Length - count || destinationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex), SR.ArgumentOutOfRange_IndexCount);

            // Note: fixed does not like empty arrays
            if (count > 0)
            {
                fixed (char* src = &_firstChar)
                fixed (char* dest = destination)
                    wstrcpy(dest + destinationIndex, src + sourceIndex, count);
            }
        }

        // Returns the entire string as an array of characters.
        unsafe public char[] ToCharArray()
        {
            // Huge performance improvement for short strings by doing this.
            int length = Length;
            if (length > 0)
            {
                char[] chars = new char[length];
                fixed (char* src = &_firstChar)
                fixed (char* dest = &chars[0])
                {
                    wstrcpy(dest, src, length);
                }
                return chars;
            }
            return Array.Empty<char>();
        }

        // Returns a substring of this string as an array of characters.
        //
        unsafe public char[] ToCharArray(int startIndex, int length)
        {
            // Range check everything.
            if (startIndex < 0 || startIndex > Length || startIndex > Length - length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.ArgumentOutOfRange_Index);
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_Index);

            if (length > 0)
            {
                char[] chars = new char[length];
                fixed (char* src = &_firstChar)
                fixed (char* dest = &chars[0])
                {
                    wstrcpy(dest, src + startIndex, length);
                }
                return chars;
            }
            return Array.Empty<char>();
        }

        public static bool IsNullOrEmpty(String value)
        {
            return (value == null || value.Length == 0);
        }

        public static bool IsNullOrWhiteSpace(String value)
        {
            if (value == null) return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!Char.IsWhiteSpace(value[i])) return false;
            }

            return true;
        }

        // Gets the length of this string
        //
        /// This is a EE implemented function so that the JIT can recognise is specially
        /// and eliminate checks on character fetchs in a loop like:
        ///        for(int i = 0; i < str.Length; i++) str[i]  
        /// The actually code generated for this will be one instruction and will be inlined.
        //
        // Spec#: Add postcondition in a contract assembly.  Potential perf problem.
        public int Length
        {
            get { return _stringLength; }
        }

        internal ref char GetRawStringData()
        {
            return ref _firstChar;
        }

        // Returns this string.
        public override String ToString()
        {
            return this;
        }

        // Returns this string.
        public String ToString(IFormatProvider provider)
        {
            return this;
        }

        public CharEnumerator GetEnumerator()
        {
            return new CharEnumerator(this);
        }

        IEnumerator<char> IEnumerable<char>.GetEnumerator()
        {
            return new CharEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CharEnumerator(this);
        }

        //
        // IConvertible implementation
        // 

        public TypeCode GetTypeCode()
        {
            return TypeCode.String;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return Convert.ToBoolean(this, provider);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return Convert.ToChar(this, provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return Convert.ToSByte(this, provider);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return Convert.ToByte(this, provider);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return Convert.ToInt16(this, provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return Convert.ToUInt16(this, provider);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return Convert.ToInt32(this, provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return Convert.ToUInt32(this, provider);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return Convert.ToInt64(this, provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return Convert.ToUInt64(this, provider);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return Convert.ToSingle(this, provider);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return Convert.ToDouble(this, provider);
        }

        Decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal(this, provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return Convert.ToDateTime(this, provider);
        }

        Object IConvertible.ToType(Type type, IFormatProvider provider)
        {
            return Convert.DefaultToType((IConvertible)this, type, provider);
        }

        // Normalization Methods
        // These just wrap calls to Normalization class
        public bool IsNormalized()
        {
            return IsNormalized(NormalizationForm.FormC);
        }

        public bool IsNormalized(NormalizationForm normalizationForm)
        {
            return Normalization.IsNormalized(this, normalizationForm);
        }

        public String Normalize()
        {
            return Normalize(NormalizationForm.FormC);
        }

        public String Normalize(NormalizationForm normalizationForm)
        {
            return Normalization.Normalize(this, normalizationForm);
        }
    }
}
