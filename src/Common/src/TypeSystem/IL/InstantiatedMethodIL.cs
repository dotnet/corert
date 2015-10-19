// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;

namespace Internal.IL
{
    public sealed class InstantiatedMethodIL : MethodIL
    {
        MethodIL _methodIL;
        Instantiation _typeInstantiation;
        Instantiation _methodInstantiation;

        public InstantiatedMethodIL(MethodIL methodIL, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            _methodIL = methodIL;

            _typeInstantiation = typeInstantiation;
            _methodInstantiation = methodInstantiation;
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

        public override TypeDesc[] GetLocals()
        {
            TypeDesc[] locals = _methodIL.GetLocals();
            TypeDesc[] clone = null;

            for (int i = 0; i < locals.Length; i++)
            {
                TypeDesc uninst = locals[i];
                TypeDesc inst  = uninst.InstantiateSignature(_typeInstantiation, _methodInstantiation);
                if (uninst != inst)
                {
                    if (clone == null)
                    {
                        clone = new TypeDesc[locals.Length];
                        for (int j = 0; j < clone.Length; j++)
                        {
                            clone[j] = locals[j];
                        }
                    }
                    clone[i] = inst;
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
