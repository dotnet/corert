// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public abstract partial class MetadataTypeSystemContext : TypeSystemContext
    {
        private struct WellKnownTypeDef
        {
            internal string Name { get; }
            internal bool IsOptional { get; }

            internal WellKnownTypeDef(string name, bool optional = false)
            {
                Name = name;
                IsOptional = optional;
            }

            public override string ToString() => $"{Name}, {IsOptional}";
            public static implicit operator WellKnownTypeDef(string name) =>  new WellKnownTypeDef(name);
            public static WellKnownTypeDef Optional(string name) => new WellKnownTypeDef(name, true);
        }

        private static readonly WellKnownTypeDef[] s_wellKnownTypes = new WellKnownTypeDef[] {
            "Void",
            "Boolean",
            "Char",
            "SByte",
            "Byte",
            "Int16",
            "UInt16",
            "Int32",
            "UInt32",
            "Int64",
            "UInt64",
            "IntPtr",
            "UIntPtr",
            "Single",
            "Double",

            "ValueType",
            "Enum",
            "Nullable`1",

            "Object",
            "String",
            "Array",
            "MulticastDelegate",

            "RuntimeTypeHandle",
            "RuntimeMethodHandle",
            "RuntimeFieldHandle",

            "Exception",

            "TypedReference",
            WellKnownTypeDef.Optional("ByReference`1"),
        };

        private MetadataType[] _wellKnownTypes;

        public MetadataTypeSystemContext()
        {
        }

        public MetadataTypeSystemContext(TargetDetails details)
            : base(details)
        {
        }

        public virtual void SetSystemModule(ModuleDesc systemModule)
        {
            InitializeSystemModule(systemModule);

            // Sanity check the name table
            Debug.Assert(s_wellKnownTypes[(int)WellKnownType.MulticastDelegate - 1].Name == "MulticastDelegate");

            _wellKnownTypes = new MetadataType[s_wellKnownTypes.Length];

            // Initialize all well known types - it will save us from checking the name for each loaded type
            for (int typeIndex = 0; typeIndex < _wellKnownTypes.Length; typeIndex++)
            {
                WellKnownTypeDef wellKnownType = s_wellKnownTypes[typeIndex];
                MetadataType type = systemModule.GetType("System", wellKnownType.Name, !wellKnownType.IsOptional);
                if (type != null)
                {
                    type.SetWellKnownType((WellKnownType)(typeIndex + 1));
                    _wellKnownTypes[typeIndex] = type;
                }
            }
        }

        public override DefType GetWellKnownType(TypeSystem.WellKnownType wellKnownType)
        {
            Debug.Assert(_wellKnownTypes != null, "Forgot to call SetSystemModule?");
            return _wellKnownTypes[(int)wellKnownType - 1];
        }

        protected sealed internal override bool ComputeHasStaticConstructor(TypeDesc type)
        {
            if (type is MetadataType)
            {
                return ((MetadataType)type).GetStaticConstructor() != null;
            }
            return false;
        }
    }
}
