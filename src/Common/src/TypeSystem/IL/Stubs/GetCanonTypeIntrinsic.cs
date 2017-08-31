// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Runtime;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method body for the GetCanonType intrinsic.
    /// </summary>
    public static class GetCanonTypeIntrinsic
    {
        public static MethodIL EmitIL(MethodDesc target)
        {
            Debug.Assert(target.Signature.Length == 1);

            ILEmitter emitter = new ILEmitter();
            var codeStream = emitter.NewCodeStream();

            TypeSystemContext context = target.Context;
            TypeDesc runtimeTypeHandleType = context.GetWellKnownType(WellKnownType.RuntimeTypeHandle);
            Debug.Assert(target.Signature.ReturnType == runtimeTypeHandleType);

            ILCodeLabel lNotCanon = emitter.NewCodeLabel();
            codeStream.Emit(ILOpcode.ldarg_0);
            codeStream.EmitLdc((int)CanonTypeKind.NormalCanon);
            codeStream.Emit(ILOpcode.bne_un, lNotCanon);
            codeStream.Emit(ILOpcode.ldtoken, emitter.NewToken(context.CanonType));
            codeStream.Emit(ILOpcode.ret);
            codeStream.EmitLabel(lNotCanon);

            ILCodeLabel lNotUniversalCanon = emitter.NewCodeLabel();
            codeStream.Emit(ILOpcode.ldarg_0);
            codeStream.EmitLdc((int)CanonTypeKind.UniversalCanon);
            codeStream.Emit(ILOpcode.bne_un, lNotUniversalCanon);
            codeStream.Emit(ILOpcode.ldtoken, emitter.NewToken(context.UniversalCanonType));
            codeStream.Emit(ILOpcode.ret);
            codeStream.EmitLabel(lNotUniversalCanon);

            ILLocalVariable vNullTypeHandle = emitter.NewLocal(runtimeTypeHandleType);
            codeStream.EmitLdLoca(vNullTypeHandle);
            codeStream.Emit(ILOpcode.initobj, emitter.NewToken(runtimeTypeHandleType));
            codeStream.EmitLdLoc(vNullTypeHandle);

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }
    }
}
