// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

using TargetException = System.ArgumentException;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // Implements Invoke() for Get/Set methods on array.
    //
    internal sealed class SyntheticMethodInvoker : MethodInvoker
    {
        public SyntheticMethodInvoker(RuntimeTypeHandle thisType, RuntimeTypeHandle[] parameterTypes, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            _invoker = invoker;
            _options = options;
            _thisType = thisType;
            _parameterTypes = parameterTypes;
        }

        public override Object Invoke(Object thisObject, Object[] arguments, BinderBundle binderBundle)
        {
            //@todo: This does not handle optional parameters (nor does it need to as today we're only using it for three synthetic array methods.)
            if (!(thisObject == null && 0 != (_options & InvokerOptions.AllowNullThis)))
                MethodInvokerUtils.ValidateThis(thisObject, _thisType);
            if (arguments == null)
                arguments = Array.Empty<Object>();
            if (arguments.Length != _parameterTypes.Length)
                throw new TargetParameterCountException();
            Object[] convertedArguments = new Object[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                convertedArguments[i] = RuntimeAugments.CheckArgument(arguments[i], _parameterTypes[i], binderBundle);
            }
            Object result;
            try
            {
                result = _invoker(thisObject, convertedArguments);
            }
            catch (Exception e)
            {
                if (0 != (_options & InvokerOptions.DontWrapException))
                    throw;
                else
                    throw new TargetInvocationException(e);
            }
            return result;
        }

        public override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            throw new PlatformNotSupportedException();
        }

        public sealed override IntPtr LdFtnResult
        {
            get
            {
                throw new PlatformNotSupportedException();
            }
        }
        
        private InvokerOptions _options;
        private Func<Object, Object[], Object> _invoker;
        private RuntimeTypeHandle _thisType;
        private RuntimeTypeHandle[] _parameterTypes;
    }
}

