// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public abstract class SignatureVariable : TypeDesc
    {
        TypeSystemContext _context;
        int _index;

        internal SignatureVariable(TypeSystemContext context, int index)
        {
            _context = context;
            _index = index;
        }

        public int Index
        {
            get
            {
                return _index;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        public abstract bool IsMethodSignatureVariable
        {
            get;
        }
    }

    public sealed class SignatureTypeVariable : SignatureVariable
    {
        internal SignatureTypeVariable(TypeSystemContext context, int index) : base(context, index)
        {
        }

        public override bool IsMethodSignatureVariable
        {
            get
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Index * 0x5498341 + 0x832424;
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            throw new NotImplementedException();
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return typeInstantiation.IsNull ? this : typeInstantiation[Index];
        }
    }

    public sealed class SignatureMethodVariable : SignatureVariable
    {
        internal SignatureMethodVariable(TypeSystemContext context, int index) : base(context, index)
        {
        }

        public override bool IsMethodSignatureVariable
        {
            get
            {
                return true;
            }
        }

        public override int GetHashCode()
        {
            return Index * 0x7822381 + 0x54872645;
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            throw new NotImplementedException();
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return methodInstantiation.IsNull ? this : methodInstantiation[Index];
        }
    }
}
