// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public sealed class SignatureTypeVariable : TypeDesc
    {
        TypeSystemContext _context;
        int _index;

        internal SignatureTypeVariable(TypeSystemContext context, int index)
        {
            _context = context;
            _index = index;
        }

        public override int GetHashCode()
        {
            return _index * 0x5498341 + 0x832424;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            throw new NotImplementedException();
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return typeInstantiation.IsNull ? this : typeInstantiation[_index];
        }
    }

    public sealed class SignatureMethodVariable : TypeDesc
    {
        TypeSystemContext _context;
        int _index;

        internal SignatureMethodVariable(TypeSystemContext context, int index)
        {
            _context = context;
            _index = index;
        }

        public override int GetHashCode()
        {
            return _index * 0x7822381 + 0x54872645;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            throw new NotImplementedException();
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return methodInstantiation.IsNull ? this : methodInstantiation[_index];
        }
    }
}
