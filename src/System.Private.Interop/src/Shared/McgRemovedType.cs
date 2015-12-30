// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace System.Runtime.InteropServices
{
    // This type should never be directly used. The RemoveDeadReferences transform
    // can replace Types that should not be used with this type.
    public class McgRemovedType
    {
        public McgRemovedType()
        {
            Debug.Assert(false, "A type that was removed by MCG dependency reduction has been instantiated.");
            throw new Exception(SR.Arg_RemovedTypeInstantiated);
        }
    }
}
