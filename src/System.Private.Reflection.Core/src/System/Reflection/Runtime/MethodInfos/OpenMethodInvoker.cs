// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.ParameterInfos;

using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.MethodInfos
{
    internal sealed class OpenMethodInvoker : MethodInvoker
    {
        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            throw new InvalidOperationException(SR.Arg_UnboundGenParam);
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            throw new InvalidOperationException(SR.Arg_UnboundGenParam);
        }
    }
}
