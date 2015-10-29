// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents a field
    /// </summary>
    public abstract partial class FieldDesc
    {
        /// <summary>
        /// Cached empty array to avoid allocations
        /// </summary>
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

        /// <summary>
        /// Derived types may return a field name, but will not necessarily
        /// </summary>
        public virtual string Name
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// The context to use with this FieldDesc
        /// </summary>
        public abstract TypeSystemContext Context
        {
            get;
        }

        /// <summary>
        /// The type that defined this field
        /// </summary>
        public abstract MetadataType OwningType
        {
            get;
        }

        /// <summary>
        /// The type of this field
        /// </summary>
        public abstract TypeDesc FieldType
        {
            get;
        }

        /// <summary>
        /// True if the field is static
        /// </summary>
        public abstract bool IsStatic
        {
            get;
        }

        /// <summary>
        /// True if the field is marked initonly (readonly)
        /// </summary>
        public abstract bool IsInitOnly
        {
            get;
        }

        /// <summary>
        /// True if the field is a thread static field
        /// </summary>
        public abstract bool IsThreadStatic
        {
            get;
        }

        /// <summary>
        /// True if the field is an RVA static field
        /// </summary>
        public abstract bool HasRva
        {
            get;
        }

        /// <summary>
        /// Gets the definition of the field from the uninstantiated generic owning type
        /// </summary>
        public virtual FieldDesc GetTypicalFieldDefinition()
        {
            return this;
        }

        /// <summary>
        /// Gets the version of this field on owning type instantiated over typeInstantiation and methodInstantiation. 
        /// The field returned may have a different identity than this.
        /// </summary>
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
