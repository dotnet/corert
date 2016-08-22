// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    public struct ModuleHandle
    {
        // Public in full framework but not in .NetCore contract
        internal static readonly ModuleHandle EmptyHandle = default(ModuleHandle);
#pragma warning disable 169
        private IntPtr _dummy; //Needed for apireview diff noise elimination
#pragma warning restore
    }
}
