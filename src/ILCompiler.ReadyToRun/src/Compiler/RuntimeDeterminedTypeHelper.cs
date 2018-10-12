// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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

            InstantiatedType instantiatedType1 = type1 as InstantiatedType;
            InstantiatedType instantiatedType2 = type2 as InstantiatedType;
            if (instantiatedType1 != null || instantiatedType2 != null)
            {
                if (instantiatedType1 == null || instantiatedType2 == null)
                {
                    return false;
                }
                if (instantiatedType1.GetTypeDefinition() != instantiatedType2.GetTypeDefinition())
                {
                    return false;
                }
                if (instantiatedType1.Instantiation.Length != instantiatedType2.Instantiation.Length)
                {
                    return false;
                }
                for (int typeArgIndex = 0; typeArgIndex < instantiatedType1.Instantiation.Length; typeArgIndex++)
                {
                    if (!Equals(instantiatedType1.Instantiation[typeArgIndex], instantiatedType2.Instantiation[typeArgIndex]))
                    {
                        return false;
                    }
                }
                return true;
            }

            throw new NotImplementedException();
        }

        public static int GetHashCode(TypeDesc type)
        {
            if (type is RuntimeDeterminedType runtimeDeterminedType)
            {
                return runtimeDeterminedType.RuntimeDeterminedDetailsType.Index ^
                    ((int)runtimeDeterminedType.RuntimeDeterminedDetailsType.Kind << 30);
            }
            if (type is InstantiatedType instantiatedType)
            {
                int hashcode = instantiatedType.GetTypeDefinition().GetHashCode() ^ 
                    unchecked(instantiatedType.Instantiation.Length << 24);
                for (int typeArgIndex = 0; typeArgIndex < instantiatedType.Instantiation.Length; typeArgIndex++)
                {
                    hashcode = unchecked(hashcode * 73 + GetHashCode(instantiatedType.Instantiation[typeArgIndex]));
                }
                return hashcode;
            }
            throw new NotImplementedException();
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
                sb.Append("<");
                for (int typeArgIndex = 0; typeArgIndex < instantiatedType.Instantiation.Length; typeArgIndex++)
                {
                    if (typeArgIndex != 0)
                    {
                        sb.Append(", ");
                    }
                    WriteTo(instantiatedType.Instantiation[typeArgIndex], sb);
                }
                sb.Append(">");
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
