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
using System.Diagnostics;

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

        private EEType* _pEEType;
        // TODO: Consider making this EETypePtr instead of EEType*.

#pragma warning restore

        // Creates a new instance of an Object.
        internal Object()
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
                return _pEEType;
                //PREFER _pEEType.ToPointer();
            }
        }

        private unsafe int GetArrayLength()
        {
            Debug.Assert(_pEEType->IsArray, "this is only supported on arrays");

            // m_numComponents is an int field that is directly after _pEEType
            fixed (EEType** ptr = &_pEEType)
                return *(int*)(ptr + 1);
        }

        internal object MemberwiseClone()
        {
            object objClone;
#if FEATURE_64BIT_ALIGNMENT
            if (_pEEType->RequiresAlign8)
            {
                if (_pEEType->IsArray)
                    objClone = InternalCalls.RhpNewArrayAlign8(_pEEType, GetArrayLength());
                else if (_pEEType->IsFinalizable)
                    objClone = InternalCalls.RhpNewFinalizableAlign8(_pEEType);
                else
                    objClone = InternalCalls.RhpNewFastAlign8(_pEEType);
            }
            else
#endif // FEATURE_64BIT_ALIGNMENT
            {
                if (_pEEType->IsArray)
                    objClone = InternalCalls.RhpNewArray(_pEEType, GetArrayLength());
                else if (_pEEType->IsFinalizable)
                    objClone = InternalCalls.RhpNewFinalizable(_pEEType);
                else
                    objClone = InternalCalls.RhpNewFast(_pEEType);
            }

            InternalCalls.RhpCopyObjectContents(objClone, this);

            return objClone;
        }
    }
}
