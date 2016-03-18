// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace Internal.JitInterface
{
    internal unsafe partial class CorInfoImpl
    {
        private struct IntrinsicKey
        {
            public string MethodName;
            public string TypeNamespace;
            public string TypeName;

            public bool Equals(IntrinsicKey other)
            {
                return (MethodName == other.MethodName) && 
                    (TypeNamespace == other.TypeNamespace) && 
                    (TypeName == other.TypeName);
            }

            public override int GetHashCode()
            {
                return MethodName.GetHashCode() +
                    ((TypeNamespace != null) ? TypeNamespace.GetHashCode() : 0) +
                    ((TypeName != null) ? TypeName.GetHashCode() : 0);
            }
        }

        private class IntrinsicEntry
        {
            public IntrinsicKey Key;
            public CorInfoIntrinsics Id;
        }

        private class IntrinsicHashtable : LockFreeReaderHashtable<IntrinsicKey, IntrinsicEntry>
        {
            protected override bool CompareKeyToValue(IntrinsicKey key, IntrinsicEntry value)
            {
                return key.Equals(value.Key);
            }
            protected override bool CompareValueToValue(IntrinsicEntry value1, IntrinsicEntry value2)
            {
                return value1.Key.Equals(value2.Key);
            }
            protected override IntrinsicEntry CreateValueFromKey(IntrinsicKey key)
            {
                Debug.Assert(false, "CreateValueFromKey not supported");
                return null;
            }
            protected override int GetKeyHashCode(IntrinsicKey key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(IntrinsicEntry value)
            {
                return value.Key.GetHashCode();
            }

            public void Add(CorInfoIntrinsics id, string methodName, string typeNamespace, string typeName)
            {
                var entry = new IntrinsicEntry();
                entry.Id = id;
                entry.Key.MethodName = methodName;
                entry.Key.TypeNamespace = typeNamespace;
                entry.Key.TypeName = typeName;
                AddOrGetExisting(entry);
            }
        }

        static IntrinsicHashtable InitializeIntrinsicHashtable()
        {
            IntrinsicHashtable table = new IntrinsicHashtable();

            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Sin, "Sin", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Cos, "Cos", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Sqrt, "Sqrt", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Abs, "Abs", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Round, "Round", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Cosh, "Cosh", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Sinh, "Sinh", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Tan, "Tan", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Tanh, "Tanh", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Asin, "Asin", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Acos, "Acos", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Atan, "Atan", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Atan2, "Atan2", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Log10, "Log10", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Pow, "Pow", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Exp, "Exp", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Ceiling, "Ceiling", "System", "Math");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Floor, "Floor", "System", "Math");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_GetChar, null, null, null); // unused
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Array_GetDimLength, "GetLength", "System", "Array"); // not handled
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Get, "Get", null, null);
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Address, "Address", null, null);
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Set, "Set", null, null);
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_StringGetChar, "get_Chars", "System", "String");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_StringLength, "get_Length", "System", "String");
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InitializeArray, "InitializeArray", "System.Runtime.CompilerServices", "RuntimeHelpers");
            //table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_GetTypeFromHandle, "GetTypeFromHandle", "System", "Type"); // RuntimeTypeHandle has to be RuntimeType
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_RTH_GetValueInternal, "GetValueInternal", "System", "RuntimeTypeHandle");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_TypeEQ, "op_Equality", "System", "Type"); // not in .NET Core
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_TypeNEQ, "op_Inequality", "System", "Type"); // not in .NET Core
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_Object_GetType, "GetType", "System", "Object");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_StubHelpers_GetStubContext, "GetStubContext", "System.StubHelpers", "StubHelpers"); // interop-specific
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_StubHelpers_GetStubContextAddr, "GetStubContextAddr", "System.StubHelpers", "StubHelpers"); // interop-specific
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_StubHelpers_GetNDirectTarget, "GetNDirectTarget", "System.StubHelpers", "StubHelpers"); // interop-specific
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedAdd32, "Add", System.Threading", "Interlocked"); // unused
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedAdd64, "Add", System.Threading", "Interlocked"); // unused
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXAdd32, "ExchangeAdd", "System.Threading", "Interlocked");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXAdd64, "ExchangeAdd", "System.Threading", "Interlocked"); // ambiguous match
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXchg32, "Exchange", "System.Threading", "Interlocked");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXchg64, "Exchange", "System.Threading", "Interlocked"); // ambiguous match
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedCmpXchg32, "CompareExchange", "System.Threading", "Interlocked");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedCmpXchg64, "CompareExchange", "System.Threading", "Interlocked"); // ambiguous match
            table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_MemoryBarrier, "MemoryBarrier", "System.Threading", "Interlocked");
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_GetCurrentManagedThread, "GetCurrentThreadNative", "System", "Thread"); // not in .NET Core
            // table.Add(CorInfoIntrinsics.CORINFO_INTRINSIC_GetManagedThreadId, "get_ManagedThreadId", "System", "Thread"); // not in .NET Core

            // If this assert fails, make sure to add the new intrinsics to the table above and update the expected count below.
            Debug.Assert((int)CorInfoIntrinsics.CORINFO_INTRINSIC_Count == 45);

            return table;
        }

        static IntrinsicHashtable s_IntrinsicHashtable = InitializeIntrinsicHashtable();

        private CorInfoIntrinsics getIntrinsicID(CORINFO_METHOD_STRUCT_* ftn, ref bool pMustExpand)
        {
            pMustExpand = false;

            var method = HandleToObject(ftn);

            Debug.Assert(method.IsIntrinsic);

            IntrinsicKey key = new IntrinsicKey();
            key.MethodName = method.Name;

            var metadataType = method.OwningType as MetadataType;
            if (metadataType != null)
            {
                key.TypeNamespace = metadataType.Namespace;
                key.TypeName = metadataType.Name;
            }

            IntrinsicEntry entry;
            if (!s_IntrinsicHashtable.TryGetValue(key, out entry))
                return CorInfoIntrinsics.CORINFO_INTRINSIC_Illegal;

            // Some intrinsics need further disambiguation
            CorInfoIntrinsics id = entry.Id;
            switch (id)
            {
                case CorInfoIntrinsics.CORINFO_INTRINSIC_Abs:
                    {
                        // RyuJIT handles floating point overloads only
                        var returnTypeCategory = method.Signature.ReturnType.Category;
                        if (returnTypeCategory != TypeFlags.Double && returnTypeCategory != TypeFlags.Single)
                            return CorInfoIntrinsics.CORINFO_INTRINSIC_Illegal;
                    }
                    break;
                case CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Get:
                case CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Address:
                case CorInfoIntrinsics.CORINFO_INTRINSIC_Array_Set:
                    if (!method.OwningType.IsArray)
                        return CorInfoIntrinsics.CORINFO_INTRINSIC_Illegal;
                    break;

                case CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXAdd32:
                case CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXchg32:
                case CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedCmpXchg32:
                    {
                        // RyuJIT handles int32 and int64 overloads only
                        var returnTypeCategory = method.Signature.ReturnType.Category;
                        if (returnTypeCategory != TypeFlags.Int32 && returnTypeCategory != TypeFlags.Int64 && returnTypeCategory != TypeFlags.IntPtr)
                            return CorInfoIntrinsics.CORINFO_INTRINSIC_Illegal;

                        // int64 overloads have different ids
                        if (returnTypeCategory == TypeFlags.Int64)
                        {
                            Debug.Assert((int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXAdd32 + 1 == (int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXAdd64);
                            Debug.Assert((int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXchg32 + 1 == (int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedXchg64);
                            Debug.Assert((int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedCmpXchg32 + 1 == (int)CorInfoIntrinsics.CORINFO_INTRINSIC_InterlockedCmpXchg64);
                            id = (CorInfoIntrinsics)((int)id + 1);
                        }
                    }
                    break;

                case CorInfoIntrinsics.CORINFO_INTRINSIC_RTH_GetValueInternal:
                case CorInfoIntrinsics.CORINFO_INTRINSIC_InitializeArray:
                    pMustExpand = true;
                    break;

                default:
                    break;
            }

            return id;
        }
    }
}
