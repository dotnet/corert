// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    public struct MethodImplRecord
    {
        public MethodDesc Decl;
        public MethodDesc Body;
    }

    // MethodImpl api surface for types.
    public partial class MetadataType
    {
        /// <summary>
        /// Compute an array of all MethodImpls that pertain to overriding virtual (non-interface methods) on this type.
        /// May be expensive.
        /// </summary>
        protected abstract MethodImplRecord[] ComputeVirtualMethodImplsForType();

        private MethodImplRecord[] _allVirtualMethodImplsForType;
        /// <summary>
        /// Get an array of all MethodImpls that pertain to overriding virtual (non-interface methods) on this type. 
        /// Expected to cache results so this api can be used repeatedly.
        /// </summary>
        public MethodImplRecord[] VirtualMethodImplsForType
        {
            get
            {
                if (_allVirtualMethodImplsForType == null)
                {
                    _allVirtualMethodImplsForType = ComputeVirtualMethodImplsForType();
                }

                return _allVirtualMethodImplsForType;
            }
        }

        /// <summary>
        /// Get an array of MethodImpls where the Decl method matches by name with the specified name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string name);
    }

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
                instMethodImpls[i].Decl = _typeDef.Context.GetMethodForInstantiatedType(uninstMethodImpls[i].Decl, this);
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
