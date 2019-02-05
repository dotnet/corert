// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.Text;

namespace ILCompiler
{
    /// <summary>
    /// Helper class used to collapse runtime determined types
    /// based on their kind and index as we otherwise don't need
    /// to distinguish among them for the purpose of emitting
    /// signatures and generic lookups.
    /// </summary>
    public static class RuntimeDeterminedTypeHelper
    {
        public static bool Equals(Instantiation instantiation1, Instantiation instantiation2)
        {
            if (instantiation1.Length != instantiation2.Length)
            {
                return false;
            }
            for (int argIndex = 0; argIndex < instantiation1.Length; argIndex++)
            {
                if (!Equals(instantiation1[argIndex], instantiation2[argIndex]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Equals(TypeDesc type1, TypeDesc type2)
        {
            if (type1 == type2)
            {
                return true;
            }

            RuntimeDeterminedType runtimeDeterminedType1 = type1 as RuntimeDeterminedType;
            RuntimeDeterminedType runtimeDeterminedType2 = type2 as RuntimeDeterminedType;
            if (runtimeDeterminedType1 != null || runtimeDeterminedType2 != null)
            {
                if (runtimeDeterminedType1 == null || runtimeDeterminedType2 == null)
                {
                    return false;
                }
                return runtimeDeterminedType1.RuntimeDeterminedDetailsType.Index == runtimeDeterminedType2.RuntimeDeterminedDetailsType.Index &&
                    runtimeDeterminedType1.RuntimeDeterminedDetailsType.Kind == runtimeDeterminedType2.RuntimeDeterminedDetailsType.Kind;
            }

            ArrayType arrayType1 = type1 as ArrayType;
            ArrayType arrayType2 = type2 as ArrayType;
            if (arrayType1 != null || arrayType2 != null)
            {
                if (arrayType1 == null || arrayType2 == null)
                {
                    return false;
                }
                return arrayType1.Rank == arrayType2.Rank &&
                    arrayType1.IsSzArray == arrayType2.IsSzArray &&
                    Equals(arrayType1.ElementType, arrayType2.ElementType);
            }

            ByRefType byRefType1 = type1 as ByRefType;
            ByRefType byRefType2 = type2 as ByRefType;
            if (byRefType1 != null || byRefType2 != null)
            {
                if (byRefType1 == null || byRefType2 == null)
                {
                    return false;
                }
                return Equals(byRefType1.ParameterType, byRefType2.ParameterType);
            }

            if (type1.GetTypeDefinition() != type2.GetTypeDefinition() ||
                !Equals(type1.Instantiation, type2.Instantiation))
            {
                return false;
            }

            return true;
        }

        public static bool Equals(MethodDesc method1, MethodDesc method2)
        {
            if (method1 == method2)
            {
                return true;
            }
            if (!Equals(method1.OwningType, method2.OwningType) ||
                method1.Signature.Length != method2.Signature.Length ||
                !Equals(method1.Instantiation, method2.Instantiation) ||
                !Equals(method1.Signature.ReturnType, method2.Signature.ReturnType))
            {
                return false;
            }
            for (int argIndex = 0; argIndex < method1.Signature.Length; argIndex++)
            {
                if (!Equals(method1.Signature[argIndex], method2.Signature[argIndex]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Equals(FieldDesc field1, FieldDesc field2)
        {
            if (field1 == null || field2 == null)
            {
                return field1 == null && field2 == null;
            }
            return field1.Name == field2.Name &&
                RuntimeDeterminedTypeHelper.Equals(field1.OwningType, field2.OwningType) &&
                RuntimeDeterminedTypeHelper.Equals(field1.FieldType, field2.FieldType);
        }

        public static int GetHashCode(Instantiation instantiation)
        {
            int hashcode = unchecked(instantiation.Length << 24);
            for (int typeArgIndex = 0; typeArgIndex < instantiation.Length; typeArgIndex++)
            {
                hashcode = unchecked(hashcode * 73 + GetHashCode(instantiation[typeArgIndex]));
            }
            return hashcode;

        }

        public static int GetHashCode(TypeDesc type)
        {
            if (type is RuntimeDeterminedType runtimeDeterminedType)
            {
                return runtimeDeterminedType.RuntimeDeterminedDetailsType.Index ^
                    ((int)runtimeDeterminedType.RuntimeDeterminedDetailsType.Kind << 30);
            }
            return type.GetTypeDefinition().GetHashCode() ^ GetHashCode(type.Instantiation);
        }

        public static int GetHashCode(MethodDesc method)
        {
            return unchecked(GetHashCode(method.OwningType) + 97 * (
                method.GetTypicalMethodDefinition().GetHashCode() + 31 * GetHashCode(method.Instantiation)));
        }

        public static int GetHashCode(FieldDesc field)
        {
            return unchecked(GetHashCode(field.OwningType) + 97 * GetHashCode(field.FieldType) + 31 * field.Name.GetHashCode());
        }

        public static void WriteTo(Instantiation instantiation, Utf8StringBuilder sb)
        {
            sb.Append("<");
            for (int typeArgIndex = 0; typeArgIndex < instantiation.Length; typeArgIndex++)
            {
                if (typeArgIndex != 0)
                {
                    sb.Append(", ");
                }
                WriteTo(instantiation[typeArgIndex], sb);
            }
            sb.Append(">");
        }

        public static void WriteTo(TypeDesc type, Utf8StringBuilder sb)
        {
            if (type is RuntimeDeterminedType runtimeDeterminedType)
            {
                switch (runtimeDeterminedType.RuntimeDeterminedDetailsType.Kind)
                {
                    case GenericParameterKind.Type:
                        sb.Append("T");
                        break;

                    case GenericParameterKind.Method:
                        sb.Append("M");
                        break;

                    default:
                        throw new NotImplementedException();
                }
                sb.Append(runtimeDeterminedType.RuntimeDeterminedDetailsType.Index.ToString());
            }
            else if (type is InstantiatedType instantiatedType)
            {
                sb.Append(instantiatedType.GetTypeDefinition().ToString());
                WriteTo(instantiatedType.Instantiation, sb);
            }
            else if (type is ArrayType arrayType)
            {
                WriteTo(arrayType.ElementType, sb);
                sb.Append("[");
                switch (arrayType.Rank)
                {
                    case 0:
                        break;
                    case 1:
                        sb.Append("*");
                        break;
                    default:
                        sb.Append(new String(',', arrayType.Rank - 1));
                        break;
                }
                sb.Append("]");
            }
            else if (type is ByRefType byRefType)
            {
                WriteTo(byRefType.ParameterType, sb);
                sb.Append("&");
            }
            else
            {
                Debug.Assert(type is DefType);
                sb.Append(type.ToString());
            }
        }

        public static void WriteTo(MethodDesc method, Utf8StringBuilder sb)
        {
            WriteTo(method.Signature.ReturnType, sb);
            sb.Append(" ");
            WriteTo(method.OwningType, sb);
            sb.Append(".");
            sb.Append(method.Name);
            if (method.HasInstantiation)
            {
                WriteTo(method.Instantiation, sb);
            }
            sb.Append("(");
            for (int argIndex = 0; argIndex < method.Signature.Length; argIndex++)
            {
                if (argIndex != 0)
                {
                    sb.Append(", ");
                }
                WriteTo(method.Signature[argIndex], sb);
            }
            sb.Append(")");
        }

        public static void WriteTo(FieldDesc field, Utf8StringBuilder sb)
        {
            WriteTo(field.FieldType, sb);
            sb.Append(" ");
            WriteTo(field.OwningType, sb);
            sb.Append(".");
            sb.Append(field.Name);
        }
    }
}
