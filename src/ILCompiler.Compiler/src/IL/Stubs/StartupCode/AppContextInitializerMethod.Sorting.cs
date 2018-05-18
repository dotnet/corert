// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs.StartupCode
{
    partial class AppContextInitializerMethod
    {
        protected override int ClassCode => 15749517;

        protected override int CompareToImpl(MethodDesc other, TypeSystemComparer comparer)
        {
            // Should be a singleton
            Debug.Assert(this == other);
            return 0;
        }
    }
}
