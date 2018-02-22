// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Base class for all value classes.
**
**
===========================================================*/

using System.Runtime;

using Internal.Runtime.CompilerServices;
using Internal.Runtime.Augments;

using Debug = System.Diagnostics.Debug;

namespace System
{
    // CONTRACT with Runtime
    // Place holder type for type hierarchy, Compiler/Runtime requires this class
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract class ValueType
    {
        public override String ToString()
        {
            return this.GetType().ToString();
        }

#if PROJECTN
        public override bool Equals(object obj)
        {
            return RuntimeAugments.Callbacks.ValueTypeEqualsUsingReflection(this, obj);
        }

        public override int GetHashCode()
        {
            return RuntimeAugments.Callbacks.ValueTypeGetHashCodeUsingReflection(this);
        }
    }
#else
        private const int UseFastHelper = -1;
        private const int GetNumFields = -1;

        // An override of this method will be injected by the compiler into all valuetypes that cannot be compared
        // using a simple memory comparison.
        // This API is a bit awkward because we want to avoid burning more than one vtable slot on this.
        // When index == GetNumFields, this method is expected to return the number of fields of this
        // valuetype. Otherwise, it returns the offset and type handle of the index-th field on this type.
        internal virtual int __GetFieldHelper(int index, out EETypePtr eeType)
        {
            // Value types that don't override this method will use the fast path that looks at bytes, not fields.
            Debug.Assert(index == GetNumFields);
            eeType = default;
            return UseFastHelper;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.EETypePtr != this.EETypePtr)
                return false;

            int numFields = __GetFieldHelper(GetNumFields, out _);

            ref byte thisRawData = ref this.GetRawData();
            ref byte thatRawData = ref obj.GetRawData();

            if (numFields == UseFastHelper)
            {
                // Sanity check - if there are GC references, we should not be comparing bytes
                Debug.Assert(RuntimeImports.RhGetGCDescSize(this.EETypePtr) == 0);

                // Compare the memory
                int valueTypeSize = (int)this.EETypePtr.ValueTypeSize;
                for (int i = 0; i < valueTypeSize; i++)
                {
                    if (Unsafe.Add(ref thisRawData, i) != Unsafe.Add(ref thatRawData, i))
                        return false;
                }
            }
            else
            {
                // Foreach field, box and call the Equals method.
                for (int i = 0; i < numFields; i++)
                {
                    int fieldOffset = __GetFieldHelper(i, out EETypePtr fieldType);

                    // Fetch the value of the field on both types
                    object thisField = RuntimeImports.RhBoxAny(ref Unsafe.Add(ref thisRawData, fieldOffset), fieldType);
                    object thatField = RuntimeImports.RhBoxAny(ref Unsafe.Add(ref thatRawData, fieldOffset), fieldType);

                    // Compare the fields
                    if (thisField == null)
                    {
                        if (thatField != null)
                            return false;
                    }
                    else if (!thisField.Equals(thatField))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
#endif
    }
}
