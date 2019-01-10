// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Reflection.Core.NonPortable;

using Internal.Runtime;
using Internal.Runtime.CompilerServices;

namespace System
{
    // CONTRACT with Runtime
    // The Object type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type EEType_ptr (or void * till a tool bug can be fixed)
    // VTable Contract: The first vtable slot should be the finalizer for object => The first virtual method in the object class should be the Finalizer

    public partial class Object
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // Marked as internal for now so that some classes (System.Buffer, System.Enum) can use C#'s fixed
        // statement on partially typed objects. Wouldn't have to do this if we could directly declared pinned
        // locals.
        // TODO: Consider making this EEType* instead of IntPtr.
        [NonSerialized]
        internal IntPtr m_pEEType;
#pragma warning restore

#if INPLACE_RUNTIME
        internal unsafe EEType* EEType
        {
            get
            {
                return (EEType*)m_pEEType;
            }
        }
#endif

        [Intrinsic]
        public Type GetType()
        {
            return RuntimeTypeUnifier.GetRuntimeTypeForEEType(EETypePtr);
        }

        internal EETypePtr EETypePtr
        {
            get
            {
                return new EETypePtr(m_pEEType);
            }
        }

        protected object MemberwiseClone()
        {
            return RuntimeImports.RhMemberwiseClone(this);
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RawData
        {
            public byte Data;
        }

        internal ref byte GetRawData()
        {
            return ref Unsafe.As<RawData>(this).Data;
        }

        /// <summary>
        /// Return size of all data (excluding ObjHeader and EEType*).
        /// Note that for strings/arrays this would include the Length as well. 
        /// </summary>
        internal unsafe uint GetRawDataSize()
        {
            return EETypePtr.BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(EEType*);
        }
    }
}
