// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Runtime.InteropServices;
using global::Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================
    // These ExecutionEnvironment entrypoints provide access to the Interop\MCG information that
    // enables Reflection invoke
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        public sealed override bool IsCOMObject(Type type)
        {
            return McgMarshal.IsCOMObject(type);
        }
    }
}

