// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;

namespace System
{
    public abstract partial class Delegate
    {
        // Default parameter support
        private const int DefaultParamTypeNone = 0;
        private const int DefaultParamTypeBool = 1;
        private const int DefaultParamTypeChar = 2;
        private const int DefaultParamTypeI1 = 3;
        private const int DefaultParamTypeI2 = 4;
        private const int DefaultParamTypeI4 = 5;
        private const int DefaultParamTypeI8 = 6;
        private const int DefaultParamTypeR4 = 7;
        private const int DefaultParamTypeR8 = 8;
        private const int DefaultParamTypeString = 9;
        private const int DefaultParamTypeDefault = 10;
        private const int DefaultParamTypeDecimal = 11;
        private const int DefaultParamTypeDateTime = 12;
        private const int DefaultParamTypeNoneButOptional = 13;
        private const int DefaultParamTypeUI1 = 14;
        private const int DefaultParamTypeUI2 = 15;
        private const int DefaultParamTypeUI4 = 16;
        private const int DefaultParamTypeUI8 = 17;

        private struct StringDataParser
        {
            private string _str;
            private int _offset;
            public StringDataParser(string str)
            {
                _str = str;
                _offset = 0;
            }

            public void SetOffset(int offset)
            {
                _offset = offset;
            }

            public long GetLong()
            {
                long returnValue;

                char curVal = _str[_offset++];

                // Special encoding for MinInt is 0x0001 (which would normally mean -0).
                if (curVal == 0x0001)
                {
                    return long.MinValue;
                }

                // High bit is used to indicate an extended value
                // Low bit is sign bit
                // The middle 14 bits are used to hold 14 bits of the actual long value.
                // A sign bit approach is used so that a negative number can be represented with 1 char value.
                returnValue = (long)(curVal & (char)0x7FFE);
                returnValue = returnValue >> 1;
                bool isNegative = ((curVal & (char)1)) == 1;
                int additionalCharCount = 0;
                int bitsAcquired = 14;
                // For additional characters, the first 3 additional characters hold 15 bits of data
                // and the last character may hold 5 bits of data.
                while ((curVal & (char)0x8000) != 0)
                {
                    additionalCharCount++;
                    curVal = _str[_offset++];
                    long grabValue = (long)(curVal & (char)0x7FFF);
                    grabValue <<= bitsAcquired;
                    bitsAcquired += 15;
                    returnValue |= grabValue;
                }

                if (isNegative)
                    returnValue = -returnValue;

                return returnValue;
            }

            public int GetInt()
            {
                return checked((int)GetLong());
            }

            public unsafe float GetFloat()
            {
                int inputLocation = checked((int)GetLong());
                float result = 0;
                byte* inputPtr = (byte*)&inputLocation;
                byte* outputPtr = (byte*)&result;
                for (int i = 0; i < 4; i++)
                {
                    outputPtr[i] = inputPtr[i];
                }

                return result;
            }

            public unsafe double GetDouble()
            {
                long inputLocation = GetLong();
                double result = 0;
                byte* inputPtr = (byte*)&inputLocation;
                byte* outputPtr = (byte*)&result;
                for (int i = 0; i < 8; i++)
                {
                    outputPtr[i] = inputPtr[i];
                }

                return result;
            }

            public unsafe string GetString()
            {
                fixed (char* strData = _str)
                {
                    int length = (int)GetLong();
                    char c = _str[_offset]; // Check for index out of range concerns.
                    string strRet = new string(strData, _offset, length);
                    _offset += length;

                    return strRet;
                }
            }

            public void Skip(int count)
            {
                _offset += count;
            }
        }

        /// <summary>
        /// Retrieves the default value for a parameter of the delegate.
        /// </summary>
        /// <param name="thType">The type of the parameter to retrieve.</param>
        /// <param name="argIndex">The index of the parameter on the method to retrieve.</param>
        /// <param name="defaultValue">The default value of the parameter if available.</param>
        /// <returns>true if the default parameter value is available, otherwise false.</returns>
        internal bool TryGetDefaultParameterValue(RuntimeTypeHandle thType, int argIndex, out object defaultValue)
        {
            defaultValue = null;

            // The LoadDefaultValueString() has the following contract
            // If it returns false, the delegate invoke does not have default values.
            // If it returns true, then the s_DefaultValueString variable is set to 
            // describe the default values for this invoke.
            string defaultValueString = null;
            if (LoadDefaultValueString())
            {
                defaultValueString = s_DefaultValueString;
            }

            // Group index of 0 indicates there are no default parameters
            if (defaultValueString == null)
            {
                return false;
            }
            StringDataParser dataParser = new StringDataParser(defaultValueString);

            // Skip to current argument
            int curArgIndex = 0;
            while (curArgIndex != argIndex)
            {
                int skip = dataParser.GetInt();
                dataParser.Skip(skip);
                curArgIndex++;
            }

            // Discard size of current argument
            int sizeOfCurrentArg = dataParser.GetInt();

            int defaultValueType = dataParser.GetInt();

            switch (defaultValueType)
            {
                case DefaultParamTypeNone:
                default:
                    return false;

                case DefaultParamTypeString:
                    defaultValue = dataParser.GetString();
                    return true;

                case DefaultParamTypeDefault:
                    if (thType.ToEETypePtr().IsValueType)
                    {
                        if (thType.ToEETypePtr().IsNullable)
                        {
                            defaultValue = null;
                            return true;
                        }
                        else
                        {
                            defaultValue = RuntimeImports.RhNewObject(thType.ToEETypePtr());
                            return true;
                        }
                    }
                    else
                    {
                        defaultValue = null;
                        return true;
                    }

                case DefaultParamTypeBool:
                    defaultValue = (dataParser.GetInt() == 1);
                    return true;
                case DefaultParamTypeChar:
                    defaultValue = (char)dataParser.GetInt();
                    return true;
                case DefaultParamTypeI1:
                    defaultValue = (sbyte)dataParser.GetInt();
                    return true;
                case DefaultParamTypeUI1:
                    defaultValue = (byte)dataParser.GetInt();
                    return true;
                case DefaultParamTypeI2:
                    defaultValue = (short)dataParser.GetInt();
                    return true;
                case DefaultParamTypeUI2:
                    defaultValue = (ushort)dataParser.GetInt();
                    return true;
                case DefaultParamTypeI4:
                    defaultValue = dataParser.GetInt();
                    return true;
                case DefaultParamTypeUI4:
                    defaultValue = checked((uint)dataParser.GetLong());
                    return true;
                case DefaultParamTypeI8:
                    defaultValue = dataParser.GetLong();
                    return true;
                case DefaultParamTypeUI8:
                    defaultValue = (ulong)dataParser.GetLong();
                    return true;
                case DefaultParamTypeR4:
                    defaultValue = dataParser.GetFloat();
                    return true;
                case DefaultParamTypeR8:
                    defaultValue = dataParser.GetDouble();
                    return true;
                case DefaultParamTypeDecimal:
                    int[] decimalBits = new int[4];
                    decimalBits[0] = dataParser.GetInt();
                    decimalBits[1] = dataParser.GetInt();
                    decimalBits[2] = dataParser.GetInt();
                    decimalBits[3] = dataParser.GetInt();
                    defaultValue = new decimal(decimalBits);
                    return true;
                case DefaultParamTypeDateTime:
                    defaultValue = new DateTime(dataParser.GetLong());
                    return true;
                case DefaultParamTypeNoneButOptional:
                    defaultValue = System.Reflection.Missing.Value;
                    return true;
            }
        }
    }
}
