// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    public sealed class ReadyToRunILProvider : ILProvider
    {
        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler.
        /// It can return null if it's not an intrinsic recognized by the compiler,
        /// but an intrinsic e.g. recognized by codegen.
        /// </summary>
        private MethodIL TryGetIntrinsicMethodIL(MethodDesc method)
        {
            return null;
        }

        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler that
        /// are specialized per instantiation. It can return null if the intrinsic
        /// is not recognized.
        /// </summary>
        private MethodIL TryGetPerInstantiationIntrinsicMethodIL(MethodDesc method)
        {
            return null;
        }

        public override MethodIL GetMethodIL(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                if (method.IsIntrinsic)
                {
                    MethodIL result = TryGetIntrinsicMethodIL(method);
                    if (result != null)
                        return result;
                }

                MethodIL methodIL = EcmaMethodIL.Create((EcmaMethod)method);
                if (methodIL != null)
                    return methodIL;

                return null;
            }
            else if (method is MethodForInstantiatedType || method is InstantiatedMethod)
            {
                // Intrinsics specialized per instantiation
                if (method.IsIntrinsic)
                {
                    MethodIL methodIL = TryGetPerInstantiationIntrinsicMethodIL(method);
                    if (methodIL != null)
                        return methodIL;
                }

                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(method, methodDefinitionIL);
            }
            else
            {
                return null;
            }
        }
    }
}
