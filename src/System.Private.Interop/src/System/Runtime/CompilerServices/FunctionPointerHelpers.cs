// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
