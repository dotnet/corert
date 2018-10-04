﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Helper class used to calculate hash codes compatible with the CoreCLR
    /// GetVersionResilientMethod/TypeHashCode.
    /// </summary>
    public static class ReadyToRunHashCode
    {
        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L15">ComputeNameHashCode</a>
        /// </summary>
        /// <param name="name">Name string to hash</param>
        public static int NameHashCode(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }

            int hash1 = 0x6DA3B944;
            int hash2 = 0;

            // DIFFERENT FROM CORERT: We hash UTF-8 bytes here, while CoreRT hashes UTF-16 characters.
            byte[] src = Encoding.UTF8.GetBytes(name);
            for (int i = 0; i < src.Length; i += 2)
            {
                hash1 = unchecked(hash1 + RotateLeft(hash1, 5)) ^ src[i];
                if (i + 1 < src.Length)
                {
                    hash2 = unchecked(hash2 + RotateLeft(hash2, 5)) ^ src[i + 1];
                }
                else
                {
                    break;
                }
            }

            hash1 = unchecked(hash1 + RotateLeft(hash1, 8));
            hash2 = unchecked(hash2 + RotateLeft(hash2, 8));

            return unchecked((int)(hash1 ^ hash2));
        }

        /// <summary>
        /// Calculate hash code for a namespace - name combination.
        /// CoreCLR 2-parameter <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L42">ComputeNameHashCode</a>
        /// DIFFERENT FROM CORERT: CoreRT hashes the full name as one string ("namespace.name"),
        /// as the full name is already available. In CoreCLR we normally only have separate
        /// strings for namespace and name, thus we hash them separately.
        /// </summary>
        /// <param name="namespacePart">Namespace name</param>
        /// <param name="namePart">Type name within the namespace</param>
        static int NameHashCode(string namespacePart, string namePart)
        {
            return NameHashCode(namespacePart) ^ NameHashCode(namePart);
        }

        /// <summary>
        /// CoreCLR 1-parameter <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/versionresilienthashcode.cpp#L76">GetVersionResilientTypeHashCode</a>
        /// </summary>
        /// <param name="type">Type to hash</param>
        public static int TypeHashCode(TypeDesc type)
        {
            if (type.GetTypeDefinition() is DefType defType)
            {
                int hashcode = NameHashCode(defType.Namespace, defType.Name);
                DefType containingType = defType.ContainingType;
                if (containingType != null)
                {
                    hashcode = NestedTypeHashCode(TypeHashCode(containingType), hashcode);
                }
                if (type.HasInstantiation)
                {
                    return GenericInstanceHashCode(hashcode, type.Instantiation);
                }
                else
                {
                    return hashcode;
                }
            }

            if (type is ArrayType arrayType)
            {
                return ArrayTypeHashCode(TypeHashCode(arrayType.ElementType), arrayType.Rank);
            }

            if (type is PointerType pointerType)
            {
                return PointerTypeHashCode(TypeHashCode(pointerType.ParameterType));
            }

            if (type is ByRefType byRefType)
            {
                return ByrefTypeHashCode(TypeHashCode(byRefType.ParameterType));
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L80">ComputeNestedTypeHashCode</a>
        /// </summary>
        /// <param name="enclosingTypeHashcode">Hash code of the enclosing type</param>
        /// <param name="nestedTypeNameHash">Hash code of the nested type name</param>
        private static int NestedTypeHashCode(int enclosingTypeHashcode, int nestedTypeNameHash)
        {
            return unchecked(enclosingTypeHashcode + RotateLeft(enclosingTypeHashcode, 11)) ^ nestedTypeNameHash;
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L52">ComputeArrayTypeHashCode</a>
        /// </summary>
        /// <param name="elementTypeHashcode">Hash code representing the array element type</param>
        /// <param name="rank">Array rank</param>
        private static int ArrayTypeHashCode(int elementTypeHashcode, int rank)
        {
            // DIFFERENT FROM CORERT: This is much simplified compared to CoreRT, to avoid converting rank to string.
            // For single-dimensinal array, the result is identical to CoreRT.
            int hashCode = unchecked((int)0xd5313556 + rank);
            if (rank == 1)
            {
                Debug.Assert(hashCode == NameHashCode("System.Array`1"));
            }
            hashCode = unchecked(hashCode + RotateLeft(hashCode, 13)) ^ elementTypeHashcode;
            return unchecked(hashCode + RotateLeft(hashCode, 15));
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L66">ComputePointerTypeHashCode</a>
        /// </summary>
        /// <param name="pointeeTypeHashcode">Hash code of the pointee type</param>
        private static int PointerTypeHashCode(int pointeeTypeHashcode)
        {
            return unchecked(pointeeTypeHashcode + RotateLeft(pointeeTypeHashcode, 5)) ^ 0x12D0;
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L73">ComputeByrefTypeHashCode</a>
        /// </summary>
        /// <param name="parameterTypeHashCode">Hash code representing the parameter type</param>
        private static int ByrefTypeHashCode(int parameterTypeHashcode)
        {
            return unchecked(parameterTypeHashcode + RotateLeft(parameterTypeHashcode, 7)) ^ 0x4C85;
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/typehashingalgorithms.h#L88">ComputeGenericInstanceHashCode</a>
        /// </summary>
        /// <param name="hashcode">Base hash code</param>
        /// <param name="instantiation">Instantiation to include in the hash</param>
        private static int GenericInstanceHashCode(int hashcode, Instantiation instantiation)
        {
            for (int i = 0; i < instantiation.Length; i++)
            {
                int argumentHashCode = TypeHashCode(instantiation[i]);
                hashcode = unchecked(hashcode + RotateLeft(hashcode, 13)) ^ argumentHashCode;
            }
            return unchecked(hashcode + RotateLeft(hashcode, 15));
        }

        /// <summary>
        /// CoreCLR <a href="https://github.com/dotnet/coreclr/blob/030e0af89bb897554acef575075c69aaf5176268/src/vm/versionresilienthashcode.cpp#L129">GetVersionResilientMethodHashCode</a>
        /// </summary>
        /// <param name="method">Method to hash</param>
        public static int MethodHashCode(MethodDesc method)
        {
            int hashCode = TypeHashCode(method.OwningType);
            int methodNameHashCode = NameHashCode(method.Name);

            // Todo: Add signature to hash.
            if (method.HasInstantiation)
            {
                hashCode ^= GenericInstanceHashCode(methodNameHashCode, method.Instantiation);
            }
            else
            {
                hashCode ^= methodNameHashCode;
            }

            return hashCode;
        }

        /// <summary>
        /// Bitwise left 32-bit rotation with wraparound.
        /// </summary>
        /// <param name="value">Value to rotate</param>
        /// <param name="bitCount">Number of bits</param>
        private static int RotateLeft(int value, int bitCount)
        {
            return unchecked((int)(((uint)value << bitCount) | ((uint)value >> (32 - bitCount))));
        }
    }
}
