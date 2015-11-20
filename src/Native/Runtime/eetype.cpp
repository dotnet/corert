//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "assert.h"
#include "rhbinder.h"
#include "eetype.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"

#include "CommonMacros.inl"

#pragma warning(disable:4127) // C4127: conditional expression is constant

// Validate an EEType extracted from an object.
bool EEType::Validate(bool assertOnFail /* default: true */)
{
#define REPORT_FAILURE() do { if (assertOnFail) { ASSERT_UNCONDITIONALLY("EEType::Validate check failed"); } return false; } while (false)

    // Deal with the most common case of a bad pointer without an exception.
    if (this == NULL)
        REPORT_FAILURE();

    // EEType structures should be at least pointer aligned.
    if (dac_cast<TADDR>(this) & (sizeof(TADDR)-1))
        REPORT_FAILURE();

    // Verify object size is bigger than min_obj_size
    size_t minObjSize = get_BaseSize();
    if (get_ComponentSize() != 0)
    {
        // If it is an array, we will align the size to the nearest pointer alignment, even if there are 
        // zero elements.  Our strings take advantage of this.
        minObjSize = (size_t)ALIGN_UP(minObjSize, sizeof(TADDR));
    }
    if (minObjSize < (3 * sizeof(TADDR)))
        REPORT_FAILURE();

    switch (get_Kind())
    {
    case CanonicalEEType:
    {
        // If the parent type is NULL this had better look like Object.
        if (m_RelatedType.m_pBaseType == NULL)
        {
            if (IsRelatedTypeViaIAT() ||
                get_IsValueType() ||
                HasFinalizer() ||
                HasReferenceFields() ||
                IsRuntimeAllocated() ||
                HasGenericVariance())
            {
                REPORT_FAILURE();
            }
        }
        break;
    }

    case ClonedEEType:
    {
        // Cloned types must have a related type.
        if (m_RelatedType.m_ppCanonicalTypeViaIAT == NULL)
            REPORT_FAILURE();

        // Either we're dealing with a clone of String or a generic type. We can tell the difference based
        // on the component size.
        switch (get_ComponentSize())
        {
        case 0:
        {
            // Cloned generic type.
            if (!IsRelatedTypeViaIAT() ||
                IsRuntimeAllocated())
            {
                REPORT_FAILURE();
            }
            break;
        }

        case 2:
        {
            // Cloned string.
            if (!IsRelatedTypeViaIAT() ||
                get_IsValueType() ||
                HasFinalizer() ||
                HasReferenceFields() ||
                IsRuntimeAllocated() ||
                HasGenericVariance())
            {
                REPORT_FAILURE();
            }

            break;
        }

        default:
            // Apart from cloned strings we don't expected cloned types to have a component size.
            REPORT_FAILURE();
        }
        break;
    }

    case ParameterizedEEType:
    {
        // The only parameter EETypes that can exist on the heap are arrays

        // Array types must have a related type.
        if (m_RelatedType.m_pRelatedParameterType == NULL)
            REPORT_FAILURE();

        // Component size cannot be zero in this case.
        if (get_ComponentSize() == 0)
            REPORT_FAILURE();

        if (get_IsValueType() ||
            HasFinalizer() ||
            IsRuntimeAllocated() ||
            HasGenericVariance())
        {
            REPORT_FAILURE();
        }

        break;
    }

    case GenericTypeDefEEType:
    {
        // We should never see uninstantiated generic type definitions here
        // since we should never construct an object instance around them.
        REPORT_FAILURE();
    }

    default:
        // Should be unreachable.
        REPORT_FAILURE();
    }

#undef REPORT_FAILURE

    return true;
}

//-----------------------------------------------------------------------------------------------------------
EEType::Kinds EEType::get_Kind()
{
	return (Kinds)(m_usFlags & (UInt16)EETypeKindMask);
}

//-----------------------------------------------------------------------------------------------------------
EEType * EEType::get_CanonicalEEType()
{
	// cloned EETypes must always refer to types in other modules
	ASSERT(IsCloned());
	ASSERT(IsRelatedTypeViaIAT());

	return *PTR_PTR_EEType(reinterpret_cast<TADDR>(m_RelatedType.m_ppCanonicalTypeViaIAT));
}

//-----------------------------------------------------------------------------------------------------------
EEType * EEType::get_RelatedParameterType()
{
	ASSERT(IsParameterizedType());

	if (IsRelatedTypeViaIAT())
		return *PTR_PTR_EEType(reinterpret_cast<TADDR>(m_RelatedType.m_ppRelatedParameterTypeViaIAT));
	else
		return PTR_EEType(reinterpret_cast<TADDR>(m_RelatedType.m_pRelatedParameterType));
}