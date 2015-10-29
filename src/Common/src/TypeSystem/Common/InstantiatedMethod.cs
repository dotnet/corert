// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents an instantiation of a generic method
    /// </summary>
    public sealed class InstantiatedMethod : MethodDesc
    {
        MethodDesc _methodDef;
        Instantiation _instantiation;

        MethodSignature _signature;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="methodDef">Uninstantiated method</param>
        /// <param name="instantiation">Generic arguments for the method</param>
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

        /// <summary>
        /// Instantiates type over the generic arguments of this method
        /// </summary>
        TypeDesc Instantiate(TypeDesc type)
        {
            return type.InstantiateSignature(new Instantiation(), _instantiation);
        }

        /// <summary>
        /// Returns a signature with parameters specialized to this instantiation
        /// </summary>
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

        public override bool IsVirtual
        {
            get
            {
                return _methodDef.IsVirtual;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return _methodDef.IsNewSlot;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return _methodDef.IsAbstract;
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
            var sb = new StringBuilder(_methodDef.ToString());
            sb.Append('<');
            for (int i = 0; i < _instantiation.Length; i++)
                sb.Append(_instantiation[i].ToString());
            sb.Append('>');
            return sb.ToString();
        }
    }
}
