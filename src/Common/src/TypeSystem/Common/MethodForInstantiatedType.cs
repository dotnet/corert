// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public sealed partial class MethodForInstantiatedType : MethodDesc
    {
        MethodDesc _typicalMethodDef;
        InstantiatedType _instantiatedType;

        MethodSignature _signature;

        internal MethodForInstantiatedType(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
        {
            _typicalMethodDef = typicalMethodDef;
            _instantiatedType = instantiatedType;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _typicalMethodDef.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _instantiatedType;
            }
        }

        TypeDesc Instantiate(TypeDesc type)
        {
            return type.InstantiateSignature(_instantiatedType.Instantiation, new Instantiation());
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    MethodSignature template = _typicalMethodDef.Signature;
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
                return _typicalMethodDef.Instantiation;
            }
        }

        public override bool IsVirtual
        {
            get
            {
                return _typicalMethodDef.IsVirtual;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return _typicalMethodDef.IsNewSlot;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return _typicalMethodDef.IsAbstract;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return _typicalMethodDef.HasCustomAttribute(attributeNamespace, attributeName);
        }

        public override MethodDesc GetTypicalMethodDefinition()
        {
            return _typicalMethodDef;
        }

        public override string Name
        {
            get
            {
                return _typicalMethodDef.Name;
            }
        }

        public override string ToString()
        {
            return OwningType.ToString() + "." + Name;
        }

    }
}
