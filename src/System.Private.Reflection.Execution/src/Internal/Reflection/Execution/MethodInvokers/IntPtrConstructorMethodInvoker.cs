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
using global::Internal.LowLevelLinq;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // IntPtr and UIntPtr constructors are intrinsics and require special casing to invoke.
    //
    internal sealed class IntPtrConstructorMethodInvoker : MethodInvoker
    {
        public IntPtrConstructorMethodInvoker(MetadataReader reader, MethodHandle methodHandle)
        {
            // Since we control the definition of System.IntPtr, we only do enough analysis of the signature to disambiguate the constructors we support.
            _id = IntPtrConstructorId.None;
            Method method = methodHandle.GetMethod(reader);
            ParameterTypeSignatureHandle[] parameterTypeSignatureHandles = method.Signature.GetMethodSignature(reader).Parameters.ToArray();
            if (parameterTypeSignatureHandles.Length == 1)
            {
                ParameterTypeSignature parameterTypeSignature = parameterTypeSignatureHandles[0].GetParameterTypeSignature(reader);

                // If any parameter is a pointer type, bail as we don't support Invokes on pointers.
                if (parameterTypeSignature.Type.HandleType != HandleType.TypeDefinition)
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_PointerArguments);

                TypeDefinition typeDefinition = parameterTypeSignature.Type.ToTypeDefinitionHandle(reader).GetTypeDefinition(reader);
                String name = typeDefinition.Name.GetString(reader);
                switch (name)
                {
                    case "Int32":
                        _id = IntPtrConstructorId.Int32;
                        break;

                    case "Int64":
                        _id = IntPtrConstructorId.Int64;
                        break;

                    case "UInt32":
                        _id = IntPtrConstructorId.UInt32;
                        break;

                    case "UInt64":
                        _id = IntPtrConstructorId.UInt64;
                        break;

                    default:
                        break;
                }
            }
        }

        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            switch (_id)
            {
                case IntPtrConstructorId.Int32:
                    {
                        CheckArgumentCount(arguments, 1);
                        Int32 value = (Int32)(RuntimeAugments.CheckArgument(arguments[0], typeof(Int32).TypeHandle));
                        try
                        {
                            return new IntPtr(value);
                        }
                        catch (Exception inner)
                        {
                            throw new TargetInvocationException(inner);
                        }
                    }

                case IntPtrConstructorId.Int64:
                    {
                        CheckArgumentCount(arguments, 1);
                        Int64 value = (Int64)(RuntimeAugments.CheckArgument(arguments[0], typeof(Int64).TypeHandle));
                        try
                        {
                            return new IntPtr(value);
                        }
                        catch (Exception inner)
                        {
                            throw new TargetInvocationException(inner);
                        }
                    }

                case IntPtrConstructorId.UInt32:
                    {
                        CheckArgumentCount(arguments, 1);
                        UInt32 value = (UInt32)(RuntimeAugments.CheckArgument(arguments[0], typeof(UInt32).TypeHandle));
                        try
                        {
                            return new UIntPtr(value);
                        }
                        catch (Exception inner)
                        {
                            throw new TargetInvocationException(inner);
                        }
                    }

                case IntPtrConstructorId.UInt64:
                    {
                        CheckArgumentCount(arguments, 1);
                        UInt64 value = (UInt64)(RuntimeAugments.CheckArgument(arguments[0], typeof(UInt64).TypeHandle));
                        try
                        {
                            return new UIntPtr(value);
                        }
                        catch (Exception inner)
                        {
                            throw new TargetInvocationException(inner);
                        }
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            Debug.Assert(false, "This code should be unreachable. ConstructorInfos do not expose a CreateDelegate().");
            throw NotImplemented.ByDesign;
        }

        private void CheckArgumentCount(Object[] arguments, int expected)
        {
            if (arguments.Length != expected)
                throw new TargetParameterCountException();
        }

        private enum IntPtrConstructorId
        {
            Int32 = 0,
            UInt32 = 1,
            Int64 = 2,
            UInt64 = 3,
            None = -1,
        }

        private IntPtrConstructorId _id;
    }
}

