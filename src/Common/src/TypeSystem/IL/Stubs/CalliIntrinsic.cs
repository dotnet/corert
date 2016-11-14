// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using FatFunctionPointerConstants = Internal.Runtime.FatFunctionPointerConstants;

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

            bool useTransformedCalli = true;

            if ((signature.Flags & MethodSignatureFlags.UnmanagedCallingConventionMask) != 0)
            {
                // Fat function pointer only ever exist for managed targets.
                useTransformedCalli = false;
            }

            if (((MetadataType)target.OwningType).Name == "RawCalliHelper")
            {
                // RawCalliHelper doesn't need the transform.
                useTransformedCalli = false;
            }

            if (useTransformedCalli)
                EmitTransformedCalli(emitter, codeStream, signature);
            else
                codeStream.Emit(ILOpcode.calli, emitter.NewToken(signature));
            
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }

        /// <summary>
        /// Generates a calli sequence that is aware of fat function pointers and can unwrap them into
        /// a function pointer + instantiation argument if necessary.
        /// </summary>
        public static void EmitTransformedCalli(ILEmitter emitter, ILCodeStream codestream, MethodSignature targetSignature)
        {
            TypeSystemContext context = targetSignature.ReturnType.Context;

            int thisPointerParamDelta = 0;
            if (!targetSignature.IsStatic)
                thisPointerParamDelta = 1;

            // Start by saving the pointer to call and all the args into locals

            ILLocalVariable vPointerToCall = emitter.NewLocal(context.GetWellKnownType(WellKnownType.IntPtr));
            codestream.EmitStLoc(vPointerToCall);

            ILLocalVariable[] vParameters = new ILLocalVariable[targetSignature.Length + thisPointerParamDelta];
            for (int i = thisPointerParamDelta; i < vParameters.Length; i++)
            {
                vParameters[vParameters.Length - i - 1 + thisPointerParamDelta] = emitter.NewLocal(targetSignature[targetSignature.Length - (i - thisPointerParamDelta) - 1]);
                codestream.EmitStLoc(vParameters[vParameters.Length - i - 1 + thisPointerParamDelta]);
            }
            if (!targetSignature.IsStatic)
            {
                vParameters[0] = emitter.NewLocal(context.GetWellKnownType(WellKnownType.Object));
                codestream.EmitStLoc(vParameters[0]);
            }

            // Is this a fat pointer?
            codestream.EmitLdLoc(vPointerToCall);
            Debug.Assert(((FatFunctionPointerConstants.Offset - 1) & FatFunctionPointerConstants.Offset) == 0);
            codestream.EmitLdc(FatFunctionPointerConstants.Offset);
            codestream.Emit(ILOpcode.and);

            ILCodeLabel notAFatPointer = emitter.NewCodeLabel();
            codestream.Emit(ILOpcode.brfalse, notAFatPointer);

            //
            // Fat pointer case
            //
            codestream.EmitLdLoc(vPointerToCall);
            codestream.EmitLdc(FatFunctionPointerConstants.Offset);
            codestream.Emit(ILOpcode.sub);

            // Get the pointer to call from the fat pointer
            codestream.Emit(ILOpcode.dup);
            codestream.Emit(ILOpcode.ldind_i);
            codestream.EmitStLoc(vPointerToCall);

            // Get the instantiation argument
            codestream.EmitLdc(context.Target.PointerSize);
            codestream.Emit(ILOpcode.add);
            codestream.Emit(ILOpcode.ldind_i);
            codestream.Emit(ILOpcode.ldind_i);
            ILLocalVariable instArg = emitter.NewLocal(context.GetWellKnownType(WellKnownType.IntPtr));
            codestream.EmitStLoc(instArg);

            // Load this
            int firstRealParameter = 0;
            if (!targetSignature.IsStatic)
            {
                codestream.EmitLdLoc(vParameters[0]);
                firstRealParameter = 1;
            }

            // Load hidden arg
            codestream.EmitLdLoc(instArg);

            // Load rest of args
            for (int i = firstRealParameter; i < vParameters.Length; i++)
            {
                codestream.EmitLdLoc(vParameters[i]);
            }
            codestream.EmitLdLoc(vPointerToCall);

            // The signature has a hidden argument
            TypeDesc[] newParameters = new TypeDesc[targetSignature.Length + 1];
            for (int i = 0; i < targetSignature.Length; i++)
                newParameters[i + 1] = targetSignature[i];
            newParameters[0] = context.GetWellKnownType(WellKnownType.IntPtr);
            MethodSignature newMethodSignature = new MethodSignature(targetSignature.Flags,
                targetSignature.GenericParameterCount, targetSignature.ReturnType, newParameters);

            codestream.Emit(ILOpcode.calli, emitter.NewToken(newMethodSignature));

            ILCodeLabel done = emitter.NewCodeLabel();
            codestream.Emit(ILOpcode.br, done);

            //
            // Not a fat pointer case
            //
            codestream.EmitLabel(notAFatPointer);

            for (int i = 0; i < vParameters.Length; i++)
            {
                codestream.EmitLdLoc(vParameters[i]);
            }
            codestream.EmitLdLoc(vPointerToCall);
            codestream.Emit(ILOpcode.calli, emitter.NewToken(targetSignature));

            codestream.EmitLabel(done);
        }
    }
}
