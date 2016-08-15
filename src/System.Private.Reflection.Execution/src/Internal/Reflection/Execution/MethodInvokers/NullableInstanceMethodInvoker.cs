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

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // Special invoker for Nullable<T> instance methods. This is disgusting beyond the pale but what do you expect when it comes to a hack-farm like Nullable<>.
    //
    // We cannot lean on Delegate.DynamicInvoke() here as you cannot take or invoke a delegate to a Nullable<T> method even on the desktop.
    //
    // The desktop *does* allow MethodInfo.Invoke() on a Nullable<T>, however. 
    //
    // We go one step further and allow invoking on a Nullable<T> method where the "this" is null (i.e. a boxed version of a Nullable<T> where
    // HasValue is false.) 
    //
    // We could go and generate IL helper methods (for each possible T) just for the Nullable<T> case but given the small number of 
    // methods and their trivial semantics, it's just easier for us to emulate the Nullable behavior ourselves.
    //
    internal sealed class NullableInstanceMethodInvoker : MethodInvoker
    {
        public NullableInstanceMethodInvoker(MetadataReader reader, MethodHandle methodHandle, RuntimeTypeHandle nullableTypeHandle, MethodInvokeInfo methodInvokeInfo)
        {
            _id = NullableMethodId.None;
            s_nullableTypeHandle = nullableTypeHandle;
            Method method = methodHandle.GetMethod(reader);
            if (MethodAttributes.Public == (method.Flags & MethodAttributes.MemberAccessMask))
            {
                // Note: Since we control the definition of Nullable<>, we're not checking signatures here.
                String name = method.Name.GetConstantStringValue(reader).Value;
                switch (name)
                {
                    case "GetType":
                        _id = NullableMethodId.GetType;
                        break;

                    case "ToString":
                        _id = NullableMethodId.ToString;
                        break;

                    case "Equals":
                        _id = NullableMethodId.Equals;
                        break;

                    case "GetHashCode":
                        _id = NullableMethodId.GetHashCode;
                        break;

                    case ".ctor":
                        _id = NullableMethodId.Ctor;
                        break;

                    case "get_HasValue":
                        _id = NullableMethodId.get_HasValue;
                        break;

                    case "get_Value":
                        _id = NullableMethodId.get_Value;
                        break;

                    case "GetValueOrDefault":
                        IEnumerator<Handle> parameters = method.Signature.GetMethodSignature(reader).Parameters.GetEnumerator();
                        if (parameters.MoveNext())
                            _id = NullableMethodId.GetValueOrDefault_1;
                        else
                            _id = NullableMethodId.GetValueOrDefault_0;
                        break;

                    default:
                        break;
                }
            }
        }

        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            Object value = thisObject;
            bool hasValue = (thisObject != null);
            switch (_id)
            {
                case NullableMethodId.GetType:
                    CheckArgumentCount(arguments, 0);
                    return value.GetType(); // Note: this throws a NullReferenceException if hasValue is false. Well so does the desktop.

                case NullableMethodId.ToString:
                    CheckArgumentCount(arguments, 0);
                    return hasValue ? value.ToString() : "";

                case NullableMethodId.Equals:
                    {
                        CheckArgumentCount(arguments, 1);
                        Object other = arguments[0];
                        if (!hasValue)
                            return other == null;
                        if (other == null)
                            return false;
                        return value.Equals(other);
                    }

                case NullableMethodId.GetHashCode:
                    CheckArgumentCount(arguments, 0);
                    return hasValue ? value.GetHashCode() : 0;

                case NullableMethodId.Ctor:
                    {
                        // Constructor case is tricky. Our implementation of NewObject() does not accept Nullable<T>'s so this is one of those cases
                        // where the constructor is responsible for both the allocation and initialization. Fortunately, we only have to return the boxed
                        // version of Nullable<T> which conveniently happens to be equal to the value we were passed in.
                        CheckArgumentCount(arguments, 1);
                        RuntimeTypeHandle theT = RuntimeAugments.GetNullableType(s_nullableTypeHandle);
                        Object argument = RuntimeAugments.CheckArgument(arguments[0], theT);
                        return argument;
                    }

                case NullableMethodId.get_HasValue:
                    CheckArgumentCount(arguments, 0);
                    return hasValue;

                case NullableMethodId.get_Value:
                    CheckArgumentCount(arguments, 0);
                    if (!hasValue)
                        throw new InvalidOperationException(SR.InvalidOperation_NoValue);
                    return value;

                case NullableMethodId.GetValueOrDefault_0:
                    {
                        CheckArgumentCount(arguments, 0);
                        if (hasValue)
                            return value;
                        RuntimeTypeHandle theT = RuntimeAugments.GetNullableType(s_nullableTypeHandle);
                        return RuntimeAugments.NewObject(theT);
                    }

                case NullableMethodId.GetValueOrDefault_1:
                    {
                        CheckArgumentCount(arguments, 1);
                        RuntimeTypeHandle theT = RuntimeAugments.GetNullableType(s_nullableTypeHandle);
                        Object defaultValue = RuntimeAugments.CheckArgument(arguments[0], theT);
                        return hasValue ? value : defaultValue;
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            // Desktop compat: MethodInfos to Nullable<T> methods cannot be turned into delegates.
            throw new ArgumentException(SR.Arg_DlgtTargMeth);
        }

        private void CheckArgumentCount(Object[] arguments, int expected)
        {
            if (arguments.Length != expected)
                throw new TargetParameterCountException();
        }

        private enum NullableMethodId
        {
            GetType = 0,
            ToString = 1,
            Equals = 2,
            GetHashCode = 3,
            Ctor = 4,
            get_HasValue = 5,
            get_Value = 6,
            GetValueOrDefault_0 = 7,
            GetValueOrDefault_1 = 8,

            None = -1,
        }

        private NullableMethodId _id;
        private static RuntimeTypeHandle s_nullableTypeHandle;
    }
}

