// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    public sealed class InstantiatedMethod : MethodDesc
    {
        MethodDesc _methodDef;
        Instantiation _instantiation;

        MethodSignature _signature;

        internal InstantiatedMethod(MethodDesc methodDef, Instantiation instantiation)
        {
            Debug.Assert(!(methodDef is InstantiatedMethod));
            _methodDef = methodDef;

            Debug.Assert(instantiation.Length > 0);
            _instantiation = instantiation;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _methodDef.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _methodDef.OwningType;
            }
        }

        TypeDesc Instantiate(TypeDesc type)
        {
            return type.InstantiateSignature(new Instantiation(), _instantiation);
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    MethodSignature template = _methodDef.Signature;
                    MethodSignatureBuilder builder = new MethodSignatureBuilder(template);

                    builder.ReturnType = Instantiate(template.ReturnType);
                    for (int i = 0; i < template.Length; i++)
                        builder[i] = Instantiate(template[i]);

                    _signature = builder.ToSignature();
                }

                return _signature;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                return _instantiation;
            }
        }

        public override MethodDesc GetMethodDefinition()
        {
            return _methodDef;
        }

        public override MethodDesc GetTypicalMethodDefinition()
        {
            return _methodDef.GetTypicalMethodDefinition();
        }

        public override string Name
        {
            get
            {
                return _methodDef.Name;
            }
        }

        public override string ToString()
        {
            // TODO: Append instantiation
            return _methodDef.ToString();
        }
    }
}
