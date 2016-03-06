// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public abstract partial class MetadataTypeSystemContext : TypeSystemContext
    {
        private static readonly string[] s_wellKnownTypeNames = new string[] {
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
        };

        private MetadataType[] _wellKnownTypes = new MetadataType[s_wellKnownTypeNames.Length];

        public MetadataTypeSystemContext()
        {
        }

        public MetadataTypeSystemContext(TargetDetails details)
            : base(details)
        {
        }

        public void SetSystemModule(ModuleDesc systemModule)
        {
            InitializeSystemModule(systemModule);

            // Sanity check the name table
            Debug.Assert(s_wellKnownTypeNames[(int)WellKnownType.MulticastDelegate - 1] == "MulticastDelegate");

            // Initialize all well known types - it will save us from checking the name for each loaded type
            for (int typeIndex = 0; typeIndex < _wellKnownTypes.Length; typeIndex++)
            {
                MetadataType type = systemModule.GetType("System", s_wellKnownTypeNames[typeIndex]);
                type.SetWellKnownType((WellKnownType)(typeIndex + 1));
                _wellKnownTypes[typeIndex] = type;
            }
        }

        public override DefType GetWellKnownType(WellKnownType wellKnownType)
        {
            return _wellKnownTypes[(int)wellKnownType - 1];
        }
    }
}
