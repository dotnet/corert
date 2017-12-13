// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Object is the root class for all CLR objects.  This class
** defines only the basics.
**
** 
===========================================================*/

using System.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime.CompilerServices;

// TODO: remove when m_pEEType becomes EETypePtr
using EEType = Internal.Runtime.EEType;
using ObjHeader = Internal.Runtime.ObjHeader;

namespace System
{
    // CONTRACT with Runtime
    // The Object type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type EEType*
    // VTable Contract: The first vtable slot should be the finalizer for object => The first virtual method in the object class should be the Finalizer

    // The Object is the root class for all object in the CLR System. Object 
    // is the super class for all other CLR objects and provide a set of methods and low level
    // services to subclasses. 

    public unsafe class Object
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649

        // Marked as internal for now so that some classes can use C#'s fixed statement on objects. 
        // Wouldn't have to do this if we could directly declared pinned locals.
        // TODO: Consider making this EETypePtr instead of EEType*.
        internal EEType* m_pEEType;

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

        internal unsafe EEType* EEType
        {
            get
            {
                // NOTE:  if managed code can be run when the GC has objects marked, then this method is 
                //        unsafe.  But, generically, we don't expect managed code such as this to be allowed
                //        to run while the GC is running.
                return m_pEEType;
                //PREFER m_pEEType.ToPointer();
            }
        }

        internal EETypePtr EETypePtr
        {
            get
            {
                return new EETypePtr(new IntPtr(m_pEEType));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private class RawData
        {
            public byte Data;
        }

        /// <summary>
        /// Return beginning of all data (excluding ObjHeader and EEType*) within this object.
        /// Note that for strings/arrays this would include the Length as well. 
        /// </summary>
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
            return EETypePtr.BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(EEType*);
        }
    }
}
