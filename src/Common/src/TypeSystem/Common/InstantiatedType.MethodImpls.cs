// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    // Implementation of MethodImpl api surface implemented without metadata access.
    public partial class InstantiatedType
    {
        /// <summary>
        /// Instantiate a MethodImplRecord from uninstantiated form to instantiated form
        /// </summary>
        /// <param name="uninstMethodImpls"></param>
        /// <returns></returns>
        private MethodImplRecord[] InstantiateMethodImpls(MethodImplRecord[] uninstMethodImpls)
        {
            if (uninstMethodImpls == null || uninstMethodImpls.Length == 0)
                return uninstMethodImpls;

            MethodImplRecord[] instMethodImpls = new MethodImplRecord[uninstMethodImpls.Length];

            for (int i = 0; i < uninstMethodImpls.Length; i++)
            {
                var implTypeInstantiated = uninstMethodImpls[i].Decl.OwningType.InstantiateSignature(this.Instantiation, new Instantiation());
                if (implTypeInstantiated is InstantiatedType)
                {
                    instMethodImpls[i].Decl = _typeDef.Context.GetMethodForInstantiatedType(uninstMethodImpls[i].Decl.GetTypicalMethodDefinition(), (InstantiatedType)implTypeInstantiated);
                }
                else
                {
                    instMethodImpls[i].Decl = uninstMethodImpls[i].Decl;
                }

                instMethodImpls[i].Body = _typeDef.Context.GetMethodForInstantiatedType(uninstMethodImpls[i].Body, this);
            }

            return instMethodImpls;
        }

        protected override MethodImplRecord[] ComputeVirtualMethodImplsForType()
        {
            MethodImplRecord[] uninstMethodImpls = _typeDef.VirtualMethodImplsForType;
            return InstantiateMethodImpls(uninstMethodImpls);
        }

        public override MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string name)
        {
            MethodImplRecord[] uninstMethodImpls = _typeDef.FindMethodsImplWithMatchingDeclName(name);
            return InstantiateMethodImpls(uninstMethodImpls);
        }
    }
}
