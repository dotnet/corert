// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for AddrOf intrinsics. The intrinsics work around the inability to express
    /// ldftn instruction in C#. The intrinsic method returns the target address of delegate pointing to 
    /// static method. The assumption is that the delegate was just initialized and thus the optimizing
    /// code generator will be able to eliminate the unnecessary delegate allocation.
    /// </summary>
    public static class AddrOfIntrinsic
    {
        public static MethodIL EmitIL(MethodDesc target)
        {
            Debug.Assert(target.Name == "AddrOf");
            Debug.Assert(target.Signature.Length == 1
                && target.Signature.ReturnType == target.Context.GetWellKnownType(WellKnownType.IntPtr));

            ILEmitter emitter = new ILEmitter();
            var codeStream = emitter.NewCodeStream();

            codeStream.EmitLdArg(0);

            var fptrField = target.Context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType.GetKnownField("m_extraFunctionPointerOrData");
            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(fptrField));

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }
    }
}
