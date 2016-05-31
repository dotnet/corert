// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for Activator.CreateInstanceIntrinsic. This intrinsic provides
    /// implementation of Activator.CreateInstance<T> that does not dependent on reflection.
    /// </summary>
    public static class CreateInstanceIntrinsic
    {
        public static MethodIL EmitIL(MethodDesc target)
        {
            Debug.Assert(target.Name == "CreateInstanceIntrinsic");
            Debug.Assert(target.Instantiation.Length == 1);

            ILEmitter emitter = new ILEmitter();
            var codeStream = emitter.NewCodeStream();

            // TODO: This won't work for shared generics
            // https://github.com/dotnet/corert/issues/368

            TypeDesc type = target.Instantiation[0];
            MethodDesc ctorMethod = type.GetDefaultConstructor();

            if (ctorMethod == null)
            {
                if (type.IsValueType)
                {
                    var loc = emitter.NewLocal(type);
                    codeStream.EmitLdLoca(loc);
                    codeStream.Emit(ILOpcode.initobj, emitter.NewToken(type));
                    codeStream.EmitLdLoc(loc);
                }
                else
                {
                    var missingCtor = type.Context.SystemModule.GetKnownType("System", "Activator").
                        GetNestedType("ClassWithMissingConstructor").GetDefaultConstructor();

                    codeStream.Emit(ILOpcode.newobj, emitter.NewToken(missingCtor));
                }
            }
            else
            {
                codeStream.Emit(ILOpcode.newobj, emitter.NewToken(ctorMethod));
            }

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(target);
        }
    }
}
