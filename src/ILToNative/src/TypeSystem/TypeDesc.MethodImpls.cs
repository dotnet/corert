﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        protected abstract MethodImplRecord[] ComputeGetAllVirtualMethodImplsForType();

        private MethodImplRecord[] _allVirtualMethodImplsForType;
        /// <summary>
        /// Get an array of all MethodImpls that pertain to overriding virtual (non-interface methods) on this type. 
        /// Expected to cache results so this api can be used repeatedly.
        /// </summary>
        public MethodImplRecord[] GetAllVirtualMethodImplsForType()
        {
            if (_allVirtualMethodImplsForType == null)
            {
                _allVirtualMethodImplsForType = ComputeGetAllVirtualMethodImplsForType();
            }

            return _allVirtualMethodImplsForType;
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
            if (uninstMethodImpls.Length == 0)
                return uninstMethodImpls;

            MethodImplRecord[] instMethodImpls = new MethodImplRecord[uninstMethodImpls.Length];

            for (int i = 0; i < uninstMethodImpls.Length; i++)
            {
                instMethodImpls[i].Decl = _typeDef.Context.GetMethodForInstantiatedType(uninstMethodImpls[i].Decl, this);
                instMethodImpls[i].Body = _typeDef.Context.GetMethodForInstantiatedType(uninstMethodImpls[i].Body, this);
            }

            return instMethodImpls;
        }

        protected override MethodImplRecord[] ComputeGetAllVirtualMethodImplsForType()
        {
            MethodImplRecord[] uninstMethodImpls = _typeDef.GetAllVirtualMethodImplsForType();
            return InstantiateMethodImpls(uninstMethodImpls);
        }

        public override MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string name)
        {
            MethodImplRecord[] uninstMethodImpls = _typeDef.FindMethodsImplWithMatchingDeclName(name);
            return InstantiateMethodImpls(uninstMethodImpls);
        }
    }
}
