// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Reflection.Core.NonPortable;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to implement ldtoken instruction.
    /// </summary>
    internal static class LdTokenHelpers
    {
        private static RuntimeTypeHandle GetRuntimeTypeHandle(IntPtr pEEType)
        {
#if CLR_RUNTIMETYPEHANDLE
            return new RuntimeTypeHandle(ReflectionCoreNonPortable.GetRuntimeTypeForEEType(new EETypePtr(pEEType)));
#else
            return new RuntimeTypeHandle(new EETypePtr(pEEType));
#endif
        }
    }
}
