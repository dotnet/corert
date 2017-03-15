// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract partial class MethodBase : MemberInfo
    {
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle) => ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle);
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle, RuntimeTypeHandle declaringType) => ReflectionAugments.ReflectionCoreCallbacks.GetMethodFromHandle(handle, declaringType);

        // This is actually an ILC intrinsic.
        public static MethodBase GetCurrentMethod() { throw new NotImplementedException(); }
    }
}
