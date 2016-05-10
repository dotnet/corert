// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    public static class MissingMethodBodyILEmitter
    {
        public static MethodIL EmitIL(MethodDesc method)
        {
            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();
            MethodDesc typeLoadExceptionHelper = method.Context.GetHelperEntryPoint("ThrowHelpers", "ThrowTypeLoadException");
            codeStream.EmitCallThrowHelper(emit, typeLoadExceptionHelper);
            return emit.Link(method);
        }
    }
}
