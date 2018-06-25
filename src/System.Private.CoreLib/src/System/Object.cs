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

using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Internal.Reflection.Core.NonPortable;

using Internal.Runtime;
using Internal.Runtime.CompilerServices;

namespace System
{
    // CONTRACT with Runtime
    // The Object type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type EEType_ptr (or void * till a tool bug can be fixed)
    // VTable Contract: The first vtable slot should be the finalizer for object => The first virtual method in the object class should be the Finalizer

    // The Object is the root class for all object in the CLR System. Object 
    // is the super class for all other CLR objects and provide a set of methods and low level
    // services to subclasses. 

    // PREFER: public class Object
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public unsafe class Object
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // Marked as internal for now so that some classes (System.Buffer, System.Enum) can use C#'s fixed
        // statement on partially typed objects. Wouldn't have to do this if we could directly declared pinned
        // locals.
        // @TODO: Consider making this EETypePtr instead of void *.
        [NonSerialized]
        internal IntPtr m_pEEType;
#pragma warning restore

        // Creates a new instance of an Object.
        [NonVersionable]
        public Object()
        {
        }

        // Allow an object to free resources before the object is reclaimed by the GC.
        // CONTRACT with runtime: This method's virtual slot number is hardcoded in the binder and the runtime.
        // **** Do not add any virtual methods in this class ahead of this ****
        [NonVersionable]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1821:RemoveEmptyFinalizers")]
        ~Object()
        {
        }

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

        public virtual string ToString()
        {
            return GetType().ToString();
        }

        // Returns a boolean indicating if the passed in object obj is equal to this.
        // Equality is defined as object equality for reference types.
        // For Value Types, the toolchain (will) generate a ValueType.Equals override method,
        // and will not be using this routine.

        public virtual bool Equals(object obj)
        {
            if (this == obj)
                return true;

            // If a value type comes through here, the desktop CLR will memcmps all the
            // value type's contents (including pad bytes between fields). In practice, we are unlikely to reach
            // this (System.ValueType overrides Equals()) edge case.s

            return false;
        }

        public static bool Equals(object objA, object objB)
        {
            if (objA == objB)
            {
                return true;
            }
            if (objA == null || objB == null)
            {
                return false;
            }
            return objA.Equals(objB);
        }

        //[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static bool ReferenceEquals(object objA, object objB)
        {
            return objA == objB;
        }

        // GetHashCode is intended to serve as a hash function for this object.
        // Based on the contents of the object, the hash function will return a suitable
        // value with a relatively random distribution over the various inputs.
        // 
        public virtual int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        internal EETypePtr EETypePtr
        {
            get
            {
                // PREFER: return m_pEEType;
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
        internal uint GetRawDataSize()
        {
            return EETypePtr.BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(EEType*);
        }
    }
}

