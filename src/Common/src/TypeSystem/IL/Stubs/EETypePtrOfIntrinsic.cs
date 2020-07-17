// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for EETypePtrOf intrinsic. The intrinsic works around the inability to express the raw
    /// "ldtoken type" instruction in C#. The intrinsic method returns the pointer to the EEType for the type passed
    /// as the generic argument.
    /// </summary>
    public static class EETypePtrOfIntrinsic
    {
        public static MethodIL EmitIL(MethodDesc target)
        {
            Debug.Assert(target.Name == "EETypePtrOf");
            Debug.Assert(target.Signature.Length == 0
                && target.Signature.ReturnType == target.OwningType);
            Debug.Assert(target.Instantiation.Length == 1);

            ILEmitter emitter = new ILEmitter();
            var codeStream = emitter.NewCodeStream();

            TypeSystemContext context = target.Context;
            TypeDesc runtimeTypeHandleType = context.GetWellKnownType(WellKnownType.RuntimeTypeHandle);
            MethodDesc getValueInternalMethod = runtimeTypeHandleType.GetKnownMethod("GetValueInternal", null);
            MethodDesc eetypePtrCtorMethod = context.SystemModule
                .GetKnownType("System", "EETypePtr")
                .GetKnownMethod(".ctor", new MethodSignature(0, 0, context.GetWellKnownType(WellKnownType.Void),
                new TypeDesc[] { context.GetWellKnownType(WellKnownType.IntPtr) }));

            // The sequence of these instructions is important. JIT is able to optimize out
            // the LDTOKEN+GetValueInternal call into "load EEType pointer onto the evaluation stack".
            codeStream.Emit(ILOpcode.ldtoken, emitter.NewToken(context.GetSignatureVariable(0, true)));
            codeStream.Emit(ILOpcode.call, emitter.NewToken(getValueInternalMethod));
            codeStream.Emit(ILOpcode.newobj, emitter.NewToken(eetypePtrCtorMethod));
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }
    }
}
