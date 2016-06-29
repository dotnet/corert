// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    partial class DefType
    {
        public override bool IsRuntimeDeterminedSubtype
        {
            get
            {
                foreach (TypeDesc type in Instantiation)
                {
                    if (type.IsRuntimeDeterminedSubtype)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Converts the type to the shared runtime determined form where the types this type is instantiatied
        /// over become bound to the generic parameters of this type.
        /// </summary>
        public DefType ConvertToSharedRuntimeDeterminedForm()
        {
            Instantiation instantiation = Instantiation;
            if (instantiation.Length > 0)
            {
                MetadataType typeDefinition = (MetadataType)GetTypeDefinition();

                bool changed;
                Instantiation sharedInstantiation = RuntimeDeterminedTypeUtilities.ConvertInstantiationToSharedRuntimeForm(
                    instantiation, typeDefinition.Instantiation, out changed);
                if (changed)
                {
                    return Context.GetInstantiatedType(typeDefinition, sharedInstantiation);
                }
            }

            return this;
        }
    }
}