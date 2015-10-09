// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//Internal.Runtime.Augments
//-------------------------------------------------
//  Why does this exist?:
//    Reflection.Execution cannot physically live in System.Private.CoreLib.dll
//    as it has a dependency on System.Reflection.Metadata. Its inherently
//    low-level nature means, however, it is closely tied to System.Private.CoreLib.dll.
//    This contract provides the two-communication between those two .dll's.
//
//
//  Implemented by:
//    System.Private.CoreLib.dll
//
//  Consumed by:
//    Reflection.Execution.dll

using System;

namespace Internal.Runtime.Augments
{
    public static class DynamicDelegateAugments
    {
        //
        // Helper to create a interpreted delegate for LINQ and DLR expression trees
        //
        public static Delegate CreateObjectArrayDelegate(Type delegateType, Func<object[], object> invoker)
        {
            return Delegate.CreateObjectArrayDelegate(delegateType, invoker);
        }

        //         
        // Returns a new delegate which can only be dynamically invoked (dlg.DynamicInvoke)
        //
        public static Delegate CreateDynamicDelegate(Func<object[], object> handler)
        {
            // implementation is provied by ILTransform
            throw NotImplemented.ByDesign;
        }
    }
}
