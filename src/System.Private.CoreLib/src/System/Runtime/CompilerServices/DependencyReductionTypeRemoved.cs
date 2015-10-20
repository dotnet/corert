// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            Debug.Assert(false, "A type that was removed by dependency reduction has been instantiated.");
            throw new Exception("A type that was removed by dependency reduction has been instantiated.");
        }
    }
}
