// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================
    // This file has all the GVM resolution related logic
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        public sealed override unsafe bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref IntPtr methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated)
        {
            throw new NotImplementedException();
        }
    }
}

