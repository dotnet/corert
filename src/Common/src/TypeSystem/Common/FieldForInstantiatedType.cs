// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Descibes a field on an instantiated type
    /// </summary>
    public sealed class FieldForInstantiatedType : FieldDesc
    {
        /// <summary>
        /// The field on the uninstantiated type
        /// </summary>
        FieldDesc _fieldDef;
        InstantiatedType _instantiatedType;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fieldDef">FieldDesc for the field on an uninstantiated type</param>
        /// <param name="instantiatedType">Instantiated type this field is a member of</param>
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

        /// <summary>
        /// Returns the instantiated type of this field
        /// </summary>
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

        public override bool IsThreadStatic
        {
            get
            {
                return _fieldDef.IsThreadStatic;
            }
        }

        public override bool HasRva
        {
            get
            {
                return _fieldDef.HasRva;
            }
        }

        public override FieldDesc GetTypicalFieldDefinition()
        {
            return _fieldDef;
        }
    }
}
