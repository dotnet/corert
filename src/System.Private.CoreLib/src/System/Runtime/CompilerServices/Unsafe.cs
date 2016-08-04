// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.CompilerServices
{
    //
    // Subsetted clone of System.Runtime.CompilerServices.Unsafe for internal runtime use.
    // Keep in sync with https://github.com/dotnet/corefx/tree/master/src/System.Runtime.CompilerServices.Unsafe.
    // 

    /// <summary>
    /// Contains generic, low-level functionality for manipulating pointers.
    /// </summary>
    public static class Unsafe
    {
#if CORERT
        /// <summary>
        /// Reinterprets the given reference as a reference to a value of type <typeparamref name="TTo"/>.
        /// </summary>
        [Intrinsic]
        public static extern ref TTo As<TFrom, TTo>(ref TFrom source);
        // ldarg.0
        // ret
#endif
    }
}
