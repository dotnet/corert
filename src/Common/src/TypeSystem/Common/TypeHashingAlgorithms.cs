// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ---------------------------------------------------------------------------
// TypeHashingAlgorithms.cs
//
// Generic functions to compute the hashcode value of types
// ---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Internal.NativeFormat
{
    static public class TypeHashingAlgorithms
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

        public static unsafe int ComputeASCIINameHashCode(byte* data, int length, out bool isAscii)
        {
            int hash1 = 0x6DA3B944;
            int hash2 = 0;
            int asciiMask = 0;

            for (int i = 0; i < length; i += 2)
            {
                int b1 = data[i];
                asciiMask |= b1;
                hash1 = (hash1 + _rotl(hash1, 5)) ^ b1;
                if ((i + 1) < length)
                {
                    int b2 = data[i];
                    asciiMask |= b2;
                    hash2 = (hash2 + _rotl(hash2, 5)) ^ b2;
                }
            }

            hash1 += _rotl(hash1, 8);
            hash2 += _rotl(hash2, 8);

            isAscii = (asciiMask & 0x80) == 0;

            return hash1 ^ hash2;
        }

        public static int ComputeArrayTypeHashCode(int elementTypeHashcode, int rank)
        {
            // Arrays are treated as generic types in some parts of our system. The array hashcodes are 
            // carefully crafted to be the same as the hashcodes of their implementation generic types.

            int hashCode;
            if (rank == 1)
            {
                hashCode = unchecked((int)0xd5313557u);
                Debug.Assert(hashCode == ComputeNameHashCode("System.Array`1"));
            }
            else
            {
                hashCode = ComputeNameHashCode("System.MDArrayRank" + rank.ToString() + "`1");
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

        public static int ComputeGenericInstanceHashCode(int genericDefinitionHashCode, Internal.TypeSystem.Instantiation genericTypeArguments)
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
