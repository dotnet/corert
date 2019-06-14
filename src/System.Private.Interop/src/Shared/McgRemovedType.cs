// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Runtime.InteropServices
{
    // This type should never be directly used. The RemoveDeadReferences transform
    // can replace Types that should not be used with this type.
    public class McgRemovedType
    {
        public McgRemovedType()
        {
            Debug.Fail("A type that was removed by MCG dependency reduction has been instantiated.");
            throw new Exception(SR.Arg_RemovedTypeInstantiated);
        }
    }
}
