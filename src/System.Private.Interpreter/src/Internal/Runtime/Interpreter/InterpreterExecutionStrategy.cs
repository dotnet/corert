// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    public class InterpreterExecutionStrategy : MethodExecutionStrategy
    {
        public override IntPtr OnEntryPoint(MethodEntrypointPtr methodEntrypointPtr, IntPtr callerArgumentsInfo)
        {
            var context = TypeSystemContextFactory.Create();
            MethodDesc method = methodEntrypointPtr.MethodIdentifier.ToMethodDesc(context);
            InterpreterCallInterceptor callInterceptor = new InterpreterCallInterceptor(context, method);
            return callInterceptor.GetThunkAddress();
        }
    }
}
