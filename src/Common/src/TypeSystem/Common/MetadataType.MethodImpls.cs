// Copyright (c) Microsoft. All rights reserved.
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
}
