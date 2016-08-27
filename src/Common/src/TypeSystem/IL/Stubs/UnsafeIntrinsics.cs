// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for generic System.Runtime.CompilerServices.Unsafe intrinsics.
    /// </summary>
    public static class UnsafeIntrinsics
    {
        public static MethodIL EmitIL(MethodDesc method)
        {
            Debug.Assert(((MetadataType)method.OwningType).Name == "Unsafe");

            switch (method.Name)
            {
                case "SizeOf":
                    return EmitSizeOf(method);
                case "As":
                    return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldarg_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
                case "AddRaw":
                    return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldarg_0, (byte)ILOpcode.ldarg_1, (byte)ILOpcode.add, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }

            return null;
        }

        private static MethodIL EmitSizeOf(MethodDesc method)
        {
            Debug.Assert(method.Signature.IsStatic && method.Signature.Length == 0);

            TypeSystemContext context = method.Context;

            ILEmitter emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();
            codeStream.Emit(ILOpcode.sizeof_, emit.NewToken(context.GetSignatureVariable(0, method: true)));
            codeStream.Emit(ILOpcode.ret);
            return emit.Link(method);
        }
    }
}
