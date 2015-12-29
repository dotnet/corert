// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    using Internal.Runtime.Augments;

    public static class FunctionPointerHelpers
    {
        public static Delegate UnsafeDelegateFromStaticMethodFunctionPointer(System.Type delegateType, IntPtr pfnStaticManagedMethod)
        {
            return RuntimeAugments.CreateDelegate(
                            delegateType.TypeHandle,
                            pfnStaticManagedMethod,
                            thisObject: null, isStatic: true, isOpen: true);
        }
    }
}
