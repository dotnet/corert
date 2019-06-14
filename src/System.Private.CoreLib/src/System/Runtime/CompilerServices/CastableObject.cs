// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// !!!! DO NOT USE THIS INTERFACE DIRECTLY EXCEPT IN THE IMPLEMENTATION OF CASTABLEOBJECT!!!!
    /// </summary>
    internal interface ICastableObject
        // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
        : ICastable
    // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
    {
        object CastToInterface(EETypePtr interfaceType, bool produceCastErrorException, out Exception castError);
    }

    public abstract class CastableObject : ICastableObject
    {
        // THIS FIELD IS USED BY THE RUNTIME DIRECTLY! IT MUST NOT BE REMOVED BY THE REDUCER
        [System.Diagnostics.DebuggerBrowsable(Diagnostics.DebuggerBrowsableState.Never)]
        private object _hiddenCacheField;

        object ICastableObject.CastToInterface(EETypePtr interfaceType, bool produceCastErrorException, out Exception castError)
        {
            return CastToInterface(new RuntimeTypeHandle(interfaceType), produceCastErrorException, out castError);
        }

        // This is called if casting this object to the given interface type would otherwise fail. Casting
        // here means the IL isinst and castclass instructions in the case where they are given an interface
        // type as the target type. This function may also be called during interface dispatch.
        //
        // A return value of non-null indicates the cast is valid.
        // The return value (if non-null) must be an object instance that implements the specified interface.
        //
        // If null is returned when this is called as part of a castclass then the usual InvalidCastException
        // will be thrown unless an alternate exception is assigned to the castError output parameter. This
        // parameter is ignored on successful casts or during the evaluation of an isinst (which returns null
        // rather than throwing on error).
        //
        // The results of this call are cached
        //
        // The results of this call should be semantically  invariant for the same object, interface type pair. 
        // That is because this is the only guard placed before an interface invocation at runtime. It is possible
        // that this call may occur more than once for a given pair, and it is possible that the results of multiple calls
        // may remain in use over time.
        protected abstract object CastToInterface(RuntimeTypeHandle interfaceType, bool produceCastErrorException, out Exception castError);

        // TODO!! BEGIN REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
        RuntimeTypeHandle ICastable.GetImplType(RuntimeTypeHandle interfaceType)
        {
            // TODO! Remove this hack that forces the il2il pipeline to not remove the ICastableObject interface or the _hiddenCacheField
            Exception junk;
            _hiddenCacheField = ((ICastableObject)this).CastToInterface(default(EETypePtr), false, out junk);
            // ENDTODO!
            return default(RuntimeTypeHandle);
        }

        bool ICastable.IsInstanceOfInterface(RuntimeTypeHandle interfaceType, out Exception castError)
        {
            castError = null;
            return false;
        }
        // TODO!! END REMOVE THIS CODE WHEN WE REMOVE ICASTABLE
    }
}
