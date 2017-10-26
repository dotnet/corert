// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Runtime.CompilerServices
{
    // This type should never be directly used. Dependency reduction
    // can replace TypeHandles that should not be used with the TypeHandle
    // for this type.

    // Rooted so we don't replace types with a different missing type
    [DependencyReductionRoot]
    public class DependencyReductionTypeRemoved
    {
        public DependencyReductionTypeRemoved()
        {
            Debug.Fail("A type that was removed by dependency reduction has been instantiated.");
            throw new Exception("A type that was removed by dependency reduction has been instantiated.");
        }
    }
}
