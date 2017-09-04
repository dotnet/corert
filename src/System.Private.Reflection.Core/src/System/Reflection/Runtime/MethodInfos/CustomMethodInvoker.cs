// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Runtime.Augments;
using Internal.Reflection.Core.Execution;

namespace System.Reflection.Runtime.MethodInfos
{
    //
    // Custom invoker for edge case scenarios not handled by the toolchain. Examples: Strings and Nullables.
    //
    internal sealed class CustomMethodInvoker : MethodInvoker
    {
        public CustomMethodInvoker(Type thisType, Type[] parameterTypes, InvokerOptions options, CustomMethodInvokerAction action)
        {
            _action = action;
            _options = options;
            _thisType = thisType;
            _parameterTypes = parameterTypes;
        }

        protected sealed override object Invoke(object thisObject, object[] arguments, BinderBundle binderBundle, bool wrapInTargetInvocationException)
        {
            Debug.Assert(arguments != null);

            // This does not handle optional parameters. None of the methods we use custom invocation for have them.
            if (!(thisObject == null && 0 != (_options & InvokerOptions.AllowNullThis)))
                ValidateThis(thisObject, _thisType.TypeHandle);

            if (arguments.Length != _parameterTypes.Length)
                throw new TargetParameterCountException();

            object[] convertedArguments = new object[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                convertedArguments[i] = RuntimeAugments.CheckArgument(arguments[i], _parameterTypes[i].TypeHandle, binderBundle);
            }
            object result;
            try
            {
                result = _action(thisObject, convertedArguments, _thisType);
            }
            catch (Exception e) when (wrapInTargetInvocationException && ((_options & InvokerOptions.DontWrapException) == 0))
            {
                throw new TargetInvocationException(e);
            }
            return result;
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            if (_thisType.IsConstructedGenericType && _thisType.GetGenericTypeDefinition() == CommonRuntimeTypes.Nullable)
            {
                // Desktop compat: MethodInfos to Nullable<T> methods cannot be turned into delegates.
                throw new ArgumentException(SR.Arg_DlgtTargMeth);
            }

            throw new PlatformNotSupportedException();
        }

        public sealed override IntPtr LdFtnResult => throw new PlatformNotSupportedException();

        private readonly InvokerOptions _options;
        private readonly CustomMethodInvokerAction _action;
        private readonly Type _thisType;
        private readonly Type[] _parameterTypes;
    }
}

