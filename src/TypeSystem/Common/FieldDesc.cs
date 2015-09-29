// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public abstract partial class FieldDesc
    {
        public readonly static FieldDesc[] EmptyFields = new FieldDesc[0];

        public override int GetHashCode()
        {
            // Inherited types are expected to override
            return RuntimeHelpers.GetHashCode(this);
        }

        public override bool Equals(Object o)
        {
            return Object.ReferenceEquals(this, o);
        }

        public virtual string Name
        {
            get
            {
                return null;
            }
        }

        public abstract TypeSystemContext Context
        {
            get;
        }

        public abstract MetadataType OwningType
        {
            get;
        }

        public abstract TypeDesc FieldType
        {
            get;
        }

        public abstract bool IsStatic
        {
            get;
        }

        public abstract bool IsInitOnly
        {
            get;
        }

        public virtual FieldDesc GetTypicalFieldDefinition()
        {
            return this;
        }

        public virtual FieldDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            FieldDesc field = this;

            TypeDesc owningType = field.OwningType;
            TypeDesc instantiatedOwningType = owningType.InstantiateSignature(typeInstantiation, methodInstantiation);
            if (owningType != instantiatedOwningType)
                field = instantiatedOwningType.Context.GetFieldForInstantiatedType(field.GetTypicalFieldDefinition(), (InstantiatedType)instantiatedOwningType);

            return field;
        }
    }
}
