// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    public static class DelegateMethodILEmitter
    {
        public static MethodIL EmitIL(MethodDesc method)
        {
            Debug.Assert(method.OwningType.IsDelegate);
            Debug.Assert(method.IsRuntimeImplemented);

            if (method.Name == "BeginInvoke" || method.Name == "EndInvoke")
            {
                // BeginInvoke and EndInvoke are not supported on .NET Core
                ILEmitter emit = new ILEmitter();
                ILCodeStream codeStream = emit.NewCodeStream();
                MethodDesc notSupportedExceptionHelper = method.Context.GetHelperEntryPoint("ThrowHelpers", "ThrowPlatformNotSupportedException");
                codeStream.EmitCallThrowHelper(emit, notSupportedExceptionHelper);
                return emit.Link(method);
            }

            if (method.Name == ".ctor")
            {
                // TODO: this should be an assert that codegen never asks for this.
                // This is just a workaround for https://github.com/dotnet/corert/issues/2102
                // The code below is making a wild guess that we're creating a closed
                // instance delegate. Without shared generics, this should only happen
                // for virtual method (so we're fine there). With shared generics, this can
                // happen for anything and might be pretty wrong.
                TypeSystemContext context = method.Context;

                ILEmitter emit = new ILEmitter();
                TypeDesc delegateType = context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;
                MethodDesc initializeMethod = delegateType.GetKnownMethod("InitializeClosedInstanceSlow", null);
                ILCodeStream codeStream = emit.NewCodeStream();

                codeStream.EmitLdArg(0);
                codeStream.EmitLdArg(1);
                codeStream.EmitLdArg(2);
                codeStream.Emit(ILOpcode.call, emit.NewToken(initializeMethod));
                codeStream.Emit(ILOpcode.ret);

                return emit.Link(method);
            }

            return null;
        }
    }
}
