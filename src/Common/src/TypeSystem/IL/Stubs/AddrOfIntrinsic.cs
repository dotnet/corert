// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            var fptrField = target.Context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType.GetField("m_extraFunctionPointerOrData");
            if (fptrField == null)
            {
                // TODO: Better exception type. Should be: "CoreLib doesn't have a required thing in it".
                throw new NotImplementedException();
            }

            codeStream.Emit(ILOpcode.ldfld, emitter.NewToken(fptrField));

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link();
        }
    }
}
