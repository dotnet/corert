// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Internal.Runtime.Augments;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerServices
{
    internal static class GenericVirtualMethodSupport
    {
        private static unsafe IntPtr GVMLookupForSlotWorker(RuntimeTypeHandle type, RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, MethodNameAndSignature methodNameAndSignature)
        {
            bool slotChanged = false;

            IntPtr resolution = IntPtr.Zero;

            // Otherwise, walk parent hierarchy attempting to resolve
            EETypePtr eetype = type.ToEETypePtr();

            IntPtr functionPointer = IntPtr.Zero;
            IntPtr genericDictionary = IntPtr.Zero;

            while (!eetype.IsNull)
            {
                RuntimeTypeHandle handle = new RuntimeTypeHandle(eetype);
                string methodName = methodNameAndSignature.Name;
                RuntimeSignature methodSignature = methodNameAndSignature.Signature;
                if (RuntimeAugments.TypeLoaderCallbacks.TryGetGenericVirtualTargetForTypeAndSlot(handle, ref declaringType, genericArguments, ref methodName, ref methodSignature, out functionPointer, out genericDictionary, out slotChanged))
                {
                    methodNameAndSignature = new MethodNameAndSignature(methodName, methodSignature);

                    if (!slotChanged)
                        resolution = FunctionPointerOps.GetGenericMethodFunctionPointer(functionPointer, genericDictionary);
                    break;
                }

                eetype = eetype.BaseType;
            }

            // If the current slot to examine has changed, restart the lookup.
            // This happens when there is an interface call.
            if (slotChanged)
            {
                return GVMLookupForSlotWorker(type, declaringType, genericArguments, methodNameAndSignature);
            }

            if (resolution == IntPtr.Zero)
            {
                Environment.FailFast("GVM resolution failure");
            }

            return resolution;
        }

        internal static unsafe IntPtr GVMLookupForSlot(RuntimeTypeHandle type, RuntimeMethodHandle slot)
        {
            RuntimeTypeHandle declaringTypeHandle;
            MethodNameAndSignature nameAndSignature;
            RuntimeTypeHandle[] genericMethodArgs;
            if (!RuntimeAugments.TypeLoaderCallbacks.GetRuntimeMethodHandleComponents(slot, out declaringTypeHandle, out nameAndSignature, out genericMethodArgs))
            {
                System.Diagnostics.Debug.Assert(false);
                return IntPtr.Zero;
            }

            return GVMLookupForSlotWorker(type, declaringTypeHandle, genericMethodArgs, nameAndSignature);
        }
    }
}
