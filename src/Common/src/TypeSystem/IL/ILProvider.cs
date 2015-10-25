// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

namespace Internal.IL
{
    class ILProvider
    {
        // TODO: Caching

        public ILProvider()
        {
        }

        public MethodIL GetMethodIL(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                // TODO: Workaround: we should special case methods with Intrinsic attribute, but since
                //       CoreLib source is still not in the repo, we have to work with what we have, which is
                //       an MCG attribute on the type itself...
                if (((EcmaType)method.OwningType).HasCustomAttribute("System.Runtime.InteropServices", "McgIntrinsicsAttribute"))
                {
                    if (method.Name == "Call")
                    {
                        return CalliIntrinsic.EmitIL(method);
                    }
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
