// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Runtime.CompilerServices;
using Internal.Reflection.Core.NonPortable;

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
    public unsafe class Object
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // Marked as internal for now so that some classes (System.Buffer, System.Enum) can use C#'s fixed
        // statement on partially typed objects. Wouldn't have to do this if we could directly declared pinned
        // locals.
        // @TODO: Consider making this EETypePtr instead of void *.
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

        ~Object()
        {
        }

        public Type GetType()
        {
            return ReflectionCoreNonPortable.GetRuntimeTypeForEEType(EETypePtr);
        }

        public virtual String ToString()
        {
            return GetType().ToString();
        }

        // Returns a boolean indicating if the passed in object obj is equal to this.
        // Equality is defined as object equality for reference types.
        // For Value Types, the toolchain (will) generate a ValueType.Equals override method,
        // and will not be using this routine.

        public virtual bool Equals(Object obj)
        {
            if (this == obj)
                return true;

            // If a value type comes through here, the desktop CLR will memcmps all the
            // value type's contents (including pad bytes between fields). In practice, we are unlikely to reach
            // this (System.ValueType overrides Equals()) edge case.s

            return false;
        }

        public static bool Equals(Object objA, Object objB)
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
        public static bool ReferenceEquals(Object objA, Object objB)
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

        // If you use C#'s 'fixed' statement to get the address of m_pEEType, you want to pass it into this
        // function to get the address of the first field.  NOTE: If you use GetAddrOfPinnedObject instead,
        // C# may optimize away the pinned local, producing incorrect results.
        static internal unsafe IntPtr GetAddrOfPinnedObjectFromEETypeField(IntPtr* ppEEType)
        {
            return (IntPtr)((byte*)ppEEType + sizeof(void*));
        }

        protected object MemberwiseClone()
        {
            return RuntimeImports.RhMemberwiseClone(this);
        }
    }
}

