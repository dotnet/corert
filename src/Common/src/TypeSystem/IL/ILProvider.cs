// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    class ILProvider
    {
        // TODO: Caching

        public ILProvider()
        {
        }

        private MethodIL TryGetIntrinsicMethodIL(MethodDesc method)
        {
            // Provides method bodies for intrinsics recognized by the compiler.
            // It can return null if it's not an intrinsic recognized by the compiler,
            // but an intrinsic e.g. recognized by codegen.

            Debug.Assert(method.IsIntrinsic);

            if (method.Name == "UncheckedCast" && method.OwningType.Name == "System.Runtime.CompilerServices.RuntimeHelpers")
            {
                return new ILStubMethodIL(new byte[] { (byte)ILOpcode.ldarg_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }

            return null;
        }

        public MethodIL GetMethodIL(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                // TODO: Workaround: we should special case methods with Intrinsic attribute, but since
                //       CoreLib source is still not in the repo, we have to work with what we have, which is
                //       an MCG attribute on the type itself...
                if (((MetadataType)method.OwningType).HasCustomAttribute("System.Runtime.InteropServices", "McgIntrinsicsAttribute"))
                {
                    if (method.Name == "Call")
                    {
                        return CalliIntrinsic.EmitIL(method);
                    }
                }

                if (method.IsIntrinsic)
                {
                    MethodIL result = TryGetIntrinsicMethodIL(method);
                    if (result != null)
                        return result;
                }

                if (method.IsPInvokeImpl && PInvokeMarshallingThunkEmitter.RequiresMarshalling(method))
                {
                    return new PInvokeMarshallingThunkEmitter(method).EmitIL();
                }

                return EcmaMethodIL.Create((EcmaMethod)method);
            }
            else
            if (method is MethodForInstantiatedType)
            {
                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(methodDefinitionIL, method.OwningType.Instantiation, new Instantiation());
            }
            else
            if (method is InstantiatedMethod)
            {
                return new InstantiatedMethodIL(GetMethodIL(method.GetMethodDefinition()), new Instantiation(), method.Instantiation);
            }
            else
            if (method is ILStubMethod)
            {
                return ((ILStubMethod)method).EmitIL();
            }
            else
            if (method is ArrayMethod)
            {
                return new ArrayMethodILEmitter((ArrayMethod)method).EmitIL();
            }
            else
            {
                return null;
            }
        }
    }
}
