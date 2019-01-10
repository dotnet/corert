// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Internal.Runtime;
using Internal.Runtime.CompilerServices;

namespace System
{
    // CONTRACT with Runtime
    // The Object type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type EEType*
    // VTable Contract: The first vtable slot should be the finalizer for object => The first virtual method in the object class should be the Finalizer

    public unsafe class Object
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        private EEType* m_pEEType;
#pragma warning restore

        // Creates a new instance of an Object.
        public Object()
        {
        }

        // Allow an object to free resources before the object is reclaimed by the GC.
        // CONTRACT with runtime: This method's virtual slot number is hardcoded in the binder. It is an
        // implementation detail where it winds up at runtime.
        // **** Do not add any virtual methods in this class ahead of this ****

        ~Object()
        {
        }

        public virtual bool Equals(object o)
        {
            return false;
        }

        public virtual int GetHashCode()
        {
            return 0;
        }

        internal EEType* EEType
        {
            get
            {
                // NOTE:  if managed code can be run when the GC has objects marked, then this method is 
                //        unsafe.  But, generically, we don't expect managed code such as this to be allowed
                //        to run while the GC is running.
                return m_pEEType;
            }
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
        internal uint GetRawDataSize()
        {
            return EEType->BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(EEType*);
        }
    }
}
