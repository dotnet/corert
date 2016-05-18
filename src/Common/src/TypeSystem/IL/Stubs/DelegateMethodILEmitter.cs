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
                TypeSystemContext context = method.Context;

                ILEmitter emit = new ILEmitter();

                TypeDesc delegateType = context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;
                MethodDesc objectCtorMethod = context.GetWellKnownType(WellKnownType.Object).GetDefaultConstructor();
                FieldDesc functionPointerField = delegateType.GetKnownField("m_functionPointer");
                FieldDesc firstParameterField = delegateType.GetKnownField("m_firstParameter");

                ILCodeStream codeStream = emit.NewCodeStream();
                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.call, emit.NewToken(objectCtorMethod));
                codeStream.EmitLdArg(0);
                codeStream.EmitLdArg(1);
                codeStream.Emit(ILOpcode.stfld, emit.NewToken(firstParameterField));
                codeStream.EmitLdArg(0);
                codeStream.EmitLdArg(2);
                codeStream.Emit(ILOpcode.stfld, emit.NewToken(functionPointerField));
                codeStream.Emit(ILOpcode.ret);

                return emit.Link(method);
            }

            return null;
        }
    }
}
