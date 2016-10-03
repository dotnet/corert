// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    partial class MethodDesc
    {
        /// <summary>
        /// Gets the shared runtime determined form of the method. This is a canonical form of the method
        /// where generic arguments of the method and the owning type have been converted to runtime determined types.
        /// </summary>
        public MethodDesc GetSharedRuntimeFormMethodTarget()
        {
            MethodDesc result = this;

            DefType owningType = OwningType as DefType;
            if (owningType != null)
            {
                // First find the method on the shared runtime form of the owning type
                DefType sharedRuntimeOwningType = owningType.ConvertToSharedRuntimeDeterminedForm();
                if (sharedRuntimeOwningType != owningType)
                {
                    result = Context.GetMethodForInstantiatedType(
                        GetTypicalMethodDefinition(), (InstantiatedType)sharedRuntimeOwningType);
                }

                // Now convert the method instantiation to the shared runtime form
                if (result.HasInstantiation)
                {
                    MethodDesc uninstantiatedMethod = result.GetMethodDefinition();

                    bool changed;
                    Instantiation sharedInstantiation = RuntimeDeterminedTypeUtilities.ConvertInstantiationToSharedRuntimeForm(
                        Instantiation, uninstantiatedMethod.Instantiation, out changed);

                    // If either the instantiation changed, or we switched the owning type, we need to find the matching
                    // instantiated method.
                    if (changed || result != this)
                    {
                        result = Context.GetInstantiatedMethod(uninstantiatedMethod, sharedInstantiation);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the type which holds the implementation of this method. This is typically the owning method,
        /// unless this method models a target of a constrained method call.
        /// </summary>
        public TypeDesc ImplementationType
        {
            get
            {
                // TODO: IsConstrainedMethod
                return OwningType;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a shared method body.
        /// </summary>
        public bool IsSharedByGenericInstantiations
        {
            get
            {
                return IsCanonicalMethod(CanonicalFormKind.Any);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is a canonical method that will only become concrete
        /// at runtime (after supplying the generic context).
        /// </summary>
        public bool IsRuntimeDeterminedExactMethod
        {
            get
            {
                TypeDesc containingType = ImplementationType;
                if (containingType.IsRuntimeDeterminedSubtype)
                    return true;

                foreach (TypeDesc typeArg in Instantiation)
                {
                    if (typeArg.IsRuntimeDeterminedSubtype)
                        return true;
                }

                return false;
            }
        }
    }
}
