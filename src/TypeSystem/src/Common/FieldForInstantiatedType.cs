// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public sealed class FieldForInstantiatedType : FieldDesc
    {
        FieldDesc _fieldDef;
        InstantiatedType _instantiatedType;

        internal FieldForInstantiatedType(FieldDesc fieldDef, InstantiatedType instantiatedType)
        {
            _fieldDef = fieldDef;
            _instantiatedType = instantiatedType;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _fieldDef.Context;
            }
        }

        public override MetadataType OwningType
        {
            get
            {
                return _instantiatedType;
            }
        }

        public override string Name
        {
            get
            {
                return _fieldDef.Name;
            }
        }

        public override TypeDesc FieldType
        {
            get
            {
                return _fieldDef.FieldType.InstantiateSignature(_instantiatedType.Instantiation, new Instantiation());
            }
        }

        public override bool IsStatic
        {
            get
            {
                return _fieldDef.IsStatic;
            }
        }

        public override bool IsInitOnly
        {
            get
            {
                return _fieldDef.IsInitOnly;
            }
        }

        public override FieldDesc GetTypicalFieldDefinition()
        {
            return _fieldDef;
        }
    }
}
