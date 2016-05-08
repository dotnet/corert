// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    public sealed class InstantiatedMethodIL : MethodIL
    {
        private MethodIL _methodIL;
        private Instantiation _typeInstantiation;
        private Instantiation _methodInstantiation;

        public InstantiatedMethodIL(MethodIL methodIL, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            Debug.Assert(!(methodIL is InstantiatedMethodIL));
            _methodIL = methodIL;

            _typeInstantiation = typeInstantiation;
            _methodInstantiation = methodInstantiation;
        }

        public override MethodIL GetMethodILDefinition()
        {
            return _methodIL;
        }
        
        public override byte[] GetILBytes()
        {
            return _methodIL.GetILBytes();
        }

        public override int GetMaxStack()
        {
            return _methodIL.GetMaxStack();
        }

        public override ILExceptionRegion[] GetExceptionRegions()
        {
            return _methodIL.GetExceptionRegions();
        }

        public override bool GetInitLocals()
        {
            return _methodIL.GetInitLocals();
        }

        public override LocalVariableDefinition[] GetLocals()
        {
            LocalVariableDefinition[] locals = _methodIL.GetLocals();
            LocalVariableDefinition[] clone = null;

            for (int i = 0; i < locals.Length; i++)
            {
                TypeDesc uninst = locals[i].Type;
                TypeDesc inst = uninst.InstantiateSignature(_typeInstantiation, _methodInstantiation);
                if (uninst != inst)
                {
                    if (clone == null)
                    {
                        clone = new LocalVariableDefinition[locals.Length];
                        for (int j = 0; j < clone.Length; j++)
                        {
                            clone[j] = locals[j];
                        }
                    }
                    clone[i] = new LocalVariableDefinition(inst, locals[i].IsPinned);
                }
            }

            return (clone == null) ? locals : clone;
        }

        public override Object GetObject(int token)
        {
            Object o = _methodIL.GetObject(token);

            if (o is MethodDesc)
            {
                o = ((MethodDesc)o).InstantiateSignature(_typeInstantiation, _methodInstantiation);
            }
            else if (o is TypeDesc)
            {
                o = ((TypeDesc)o).InstantiateSignature(_typeInstantiation, _methodInstantiation);
            }
            else if (o is FieldDesc)
            {
                o = ((FieldDesc)o).InstantiateSignature(_typeInstantiation, _methodInstantiation);
            }
            else if (o is MethodSignature)
            {
                MethodSignature template = (MethodSignature)o;
                MethodSignatureBuilder builder = new MethodSignatureBuilder(template);

                builder.ReturnType = template.ReturnType.InstantiateSignature(_typeInstantiation, _methodInstantiation);
                for (int i = 0; i < template.Length; i++)
                    builder[i] = template[i].InstantiateSignature(_typeInstantiation, _methodInstantiation);

                o = builder.ToSignature();
            }


            return o;
        }
    }
}
