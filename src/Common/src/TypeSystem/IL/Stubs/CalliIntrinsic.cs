// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for Calli intrinsics. The intrinsics work around the inability to express
    /// calli instruction in C#. The intrinsic method has a shape of
    /// "T Call&lt;T&gt;(IntPtr address, X arg0,... Y argN)", for which a body will be provided by
    /// this generator that loads the arguments and performs a calli to the address specified as the first argument.
    /// </summary>
    public static class CalliIntrinsic
    {
        public static MethodIL EmitIL(MethodDesc target)
        {
            Debug.Assert(target.Name == "Call");
            Debug.Assert(target.Signature.Length > 0
                && target.Signature[0] == target.Context.GetWellKnownType(WellKnownType.IntPtr));

            ILEmitter emitter = new ILEmitter();
            var codeStream = emitter.NewCodeStream();

            // Load all the arguments except the first one (IntPtr address)
            for (int i = 1; i < target.Signature.Length; i++)
            {
                codeStream.EmitLdArg(i);
            }

            // now load IntPtr address
            codeStream.EmitLdArg(0);

            // Create a signature for the calli by copying the signature of the containing method
            // while skipping the first argument
            MethodSignature template = target.Signature;
            TypeDesc returnType = template.ReturnType;
            TypeDesc[] parameters = new TypeDesc[template.Length - 1];
            for (int i = 1; i < template.Length; i++)
            {
                parameters[i - 1] = template[i];
            }

            var signature = new MethodSignature(template.Flags, 0, returnType, parameters);
            codeStream.Emit(ILOpcode.calli, emitter.NewToken(signature));
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }
    }
}
