// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ---------------------------------------------------------------------------
// TypeHashingAlgorithms.cs
//
// Generic functions to compute the hashcode value of types
// ---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Internal.NativeFormat
{
    static class TypeHashingAlgorithms
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _rotl(int value, int shift)
        {
            return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
        }

        //
        // Returns the hashcode value of the 'src' string
        //
        public static int ComputeNameHashCode(string src)
        {
            int hash1 = 0x6DA3B944;
            int hash2 = 0;

            for (int i = 0; i < src.Length; i += 2)
            {
                hash1 = (hash1 + _rotl(hash1, 5)) ^ src[i];
                if ((i + 1) < src.Length)
                    hash2 = (hash2 + _rotl(hash2, 5)) ^ src[i + 1];
            }

            hash1 += _rotl(hash1, 8);
            hash2 += _rotl(hash2, 8);

            return hash1 ^ hash2;
        }

        // This function may be needed in a portion of the codebase which is too low level to use 
        // globalization, ergo, we cannot call ToString on the integer.
        private static string IntToString(int arg)
        {
            // This IntToString function is only expected to be used for MDArrayRanks, and therefore is only for positive numbers
            Debug.Assert(arg > 0);
            StringBuilder sb = new StringBuilder(1);

            while (arg != 0)
            {
                sb.Append((char)('0' + (arg % 10)));
                arg = arg / 10;
            }

            // Reverse the string
            int sbLen = sb.Length;
            int pivot = sbLen / 2;
            for (int i = 0; i < pivot; i++)
            {
                int iToSwapWith = sbLen - i - 1;
                char temp = sb[i];
                sb[i] = sb[iToSwapWith];
                sb[iToSwapWith] = temp;
            }

            return sb.ToString();
        }

        public static int ComputeArrayTypeHashCode(int elementTypeHashcode, int rank)
        {
            // Arrays are treated as generic types in some parts of our system. The array hashcodes are 
            // carefully crafted to be the same as the hashcodes of their implementation generic types.

            int hashCode;
            if (rank == -1)
            {
                hashCode = unchecked((int)0xd5313557u);
                Debug.Assert(hashCode == ComputeNameHashCode("System.Array`1"));
            }
            else
            {
                hashCode = ComputeNameHashCode("System.MDArrayRank" + IntToString(rank) + "`1");
            }

            hashCode = (hashCode + _rotl(hashCode, 13)) ^ elementTypeHashcode;
            return (hashCode + _rotl(hashCode, 15));
        }

        public static int ComputeArrayTypeHashCode<T>(T elementType, int rank)
        {
            return ComputeArrayTypeHashCode(elementType.GetHashCode(), rank);
        }


        public static int ComputePointerTypeHashCode(int pointeeTypeHashcode)
        {
            return (pointeeTypeHashcode + _rotl(pointeeTypeHashcode, 5)) ^ 0x12D0;
        }

        public static int ComputePointerTypeHashCode<T>(T pointeeType)
        {
            return ComputePointerTypeHashCode(pointeeType.GetHashCode());
        }


        public static int ComputeByrefTypeHashCode(int parameterTypeHashcode)
        {
            return (parameterTypeHashcode + _rotl(parameterTypeHashcode, 7)) ^ 0x4C85;
        }

        public static int ComputeByrefTypeHashCode<T>(T parameterType)
        {
            return ComputeByrefTypeHashCode(parameterType.GetHashCode());
        }


        public static int ComputeNestedTypeHashCode(int enclosingTypeHashcode, int nestedTypeNameHash)
        {
            return (enclosingTypeHashcode + _rotl(enclosingTypeHashcode, 11)) ^ nestedTypeNameHash;
        }


        public static int ComputeGenericInstanceHashCode<ARG>(int genericDefinitionHashCode, ARG[] genericTypeArguments)
        {
            int hashcode = genericDefinitionHashCode;
            for (int i = 0; i < genericTypeArguments.Length; i++)
            {
                int argumentHashCode = genericTypeArguments[i].GetHashCode();
                hashcode = (hashcode + _rotl(hashcode, 13)) ^ argumentHashCode;
            }
            return (hashcode + _rotl(hashcode, 15));
        }
    }
}
