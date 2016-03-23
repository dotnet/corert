// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "rhassert.h"
#include "rhbinder.h"
#include "eetype.h"
#include "GenericInstance.h"

bool UnifiedGenericInstance::Equals(GenericInstanceDesc * pLocalGid)
{
    GenericInstanceDesc * pCanonicalGid = GetGid();
    UInt32 cTypeVars = pCanonicalGid->GetArity();

    // If the number of type arguments is different, we can never have a match.
    if (cTypeVars != pLocalGid->GetArity())
        return false;

    // Compare the generic type itself.
    if (pCanonicalGid->GetGenericTypeDef().GetValue() != pLocalGid->GetGenericTypeDef().GetValue())
        return false;

    // Compare the type arguments of the instantiation.
    for (UInt32 i = 0; i < cTypeVars; i++)
    {
        EEType * pUnifiedType = pCanonicalGid->GetParameterType(i).GetValue();
        EEType * pLocalType = pLocalGid->GetParameterType(i).GetValue();
        if (pUnifiedType != pLocalType)
        {
            // Direct pointer comparison failed, but there are a couple of cases where converting the local
            // generic instantiation to the unified version had to update the type variable EEType to avoid
            // including a pointer to an arbitrary module (one not related to the generic instantiation via a
            // direct type dependence).
            //  * Cloned types were converted to their underlying canonical types.
            //  * Some array types were re-written to use a module-neutral definition.
            if (pLocalType->IsCanonical())
                return false;
            if (pLocalType->IsCloned())
            {
                if (pUnifiedType != pLocalType->get_CanonicalEEType())
                    return false;
                else
                    continue;   // type parameter matches
            }
            ASSERT(pLocalType->IsParameterizedType());
            if (!pUnifiedType->IsParameterizedType())
                return false;
            if (pUnifiedType->get_RelatedParameterType() != pLocalType->get_RelatedParameterType())
                return false;
            if (pUnifiedType->get_ParameterizedTypeShape() != pLocalType->get_ParameterizedTypeShape())
                return false;
        }
    }

    return true;
}
