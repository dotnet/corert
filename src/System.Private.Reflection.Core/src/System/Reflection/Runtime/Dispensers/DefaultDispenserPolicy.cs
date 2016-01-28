// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // For now, this is the dispenser policy used inside S.R.R.
    //
    internal sealed class DefaultDispenserPolicy : DispenserPolicy
    {
        public sealed override DispenserAlgorithm GetAlgorithm(DispenserScenario scenario)
        {
#if TEST_CODEGEN_OPTIMIZATION
            return DispenserAlgorithm.CreateAlways;
#else
            switch (scenario)
            {
                // Type.GetTypeInfo() for Runtime types.
                case DispenserScenario.Type_TypeInfo:
                    return DispenserAlgorithm.LatchesTypeInfoInsideType;

                // Metadata typedef handle to RuntimeTypeInfo
                case DispenserScenario.TypeDef_TypeInfo:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // TypeInfo + Name to EventInfo
                case DispenserScenario.TypeInfoAndName_EventInfo:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // TypeInfo + Name to FieldInfo
                case DispenserScenario.TypeInfoAndName_FieldInfo:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // TypeInfo + Name to MethodInfo
                case DispenserScenario.TypeInfoAndName_MethodInfo:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // TypeInfo + Name to PropertyInfo
                case DispenserScenario.TypeInfoAndName_PropertyInfo:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // Assembly + NamespaceTypeName to Type
                case DispenserScenario.AssemblyAndNamespaceTypeName_Type:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // Assembly refName to Assembly
                case DispenserScenario.AssemblyRefName_Assembly:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                // RuntimeAssembly to CaseInsensitiveTypeDictionary
                case DispenserScenario.RuntimeAssembly_CaseInsensitiveTypeDictionary:
                    return DispenserAlgorithm.ReuseAlways;

                // Scope definition handle to RuntimeAssembly
                case DispenserScenario.Scope_Assembly:
                    return DispenserAlgorithm.ReuseAsLongAsValueIsAlive;

                default:
                    return DispenserAlgorithm.CreateAlways;
            }
#endif //!TEST_CODEGEN_OPTIMIZATION

        }
    }
}


