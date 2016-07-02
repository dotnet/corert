// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;

using global::Internal.Runtime.TypeLoader;
using global::Internal.Runtime.Augments;
using global::Internal.Runtime.CompilerServices;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Extensions.NonPortable
{
    public static class DelegateMethodInfoRetriever
    {
        public static MethodInfo GetDelegateMethodInfo(Delegate del)
        {
            if (del == null)
                throw new ArgumentException();
            Delegate[] invokeList = del.GetInvocationList();
            del = invokeList[invokeList.Length - 1];
            RuntimeTypeHandle typeOfFirstParameterIfInstanceDelegate;
            bool isOpenResolver;
            IntPtr originalLdFtnResult = RuntimeAugments.GetDelegateLdFtnResult(del, out typeOfFirstParameterIfInstanceDelegate, out isOpenResolver);
            if (originalLdFtnResult == (IntPtr)0)
                return null;

            MethodHandle methodHandle = default(MethodHandle);
            RuntimeTypeHandle[] genericMethodTypeArgumentHandles = null;

            bool callTryGetMethod = true;

            unsafe
            {
                if (isOpenResolver)
                {
                    OpenMethodResolver* resolver = (OpenMethodResolver*)originalLdFtnResult;
                    if (resolver->ResolverType == OpenMethodResolver.OpenNonVirtualResolve)
                    {
                        originalLdFtnResult = resolver->CodePointer;
                        // And go on to do normal ldftn processing.
                    }
                    else if (resolver->ResolverType == OpenMethodResolver.DispatchResolve)
                    {
                        callTryGetMethod = false;
                        methodHandle = resolver->Handle.AsMethodHandle();
                        genericMethodTypeArgumentHandles = null;
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(resolver->ResolverType == OpenMethodResolver.GVMResolve);

                        callTryGetMethod = false;
                        methodHandle = resolver->Handle.AsMethodHandle();

                        RuntimeTypeHandle declaringTypeHandleIgnored;
                        MethodNameAndSignature nameAndSignatureIgnored;
                        if (!TypeLoaderEnvironment.Instance.TryGetRuntimeMethodHandleComponents(resolver->GVMMethodHandle, out declaringTypeHandleIgnored, out nameAndSignatureIgnored, out genericMethodTypeArgumentHandles))
                            return null;
                    }
                }
            }

            if (callTryGetMethod)
            {
                if (!ReflectionExecution.ExecutionEnvironment.TryGetMethodForOriginalLdFtnResult(originalLdFtnResult, ref typeOfFirstParameterIfInstanceDelegate, out methodHandle, out genericMethodTypeArgumentHandles))
                    return null;
            }
            MethodBase methodBase = ReflectionCoreExecution.ExecutionDomain.GetMethod(typeOfFirstParameterIfInstanceDelegate, methodHandle, genericMethodTypeArgumentHandles);
            MethodInfo methodInfo = methodBase as MethodInfo;
            if (methodInfo != null)
                return methodInfo;
            return null; // GetMethod() returned a ConstructorInfo.
        }
    }
}
