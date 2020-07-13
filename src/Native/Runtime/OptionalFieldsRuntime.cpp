// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Implementations of methods of OptionalFields which are used only at runtime (i.e. reading field values).
//
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "daccess.h"
#include "PalRedhawkCommon.h"
#include "PalRedhawk.h"
#include "rhassert.h"
#include "rhbinder.h"
#include "eetype.h"
#include "ObjectLayout.h"
#include "varint.h"

// Reads the field type from the current byte of the stream and indicates whether this represents the last
// field.
/*static*/ OptionalFieldTag OptionalFields::DecodeFieldTag(PTR_UInt8 * ppFields, bool *pfLastField)
{
    UInt8 tagByte;
    tagByte = **ppFields;

    // The last field has the most significant bit of the byte set.
    *pfLastField = (tagByte & 0x80) != 0;

    // The remaining 7 bits encode the field type.
    OptionalFieldTag eTag = (OptionalFieldTag)(tagByte & 0x7f);

    // Advance the pointer past the header.
    (*ppFields)++;

    return eTag;
}

// Reads a field value (or the basis for an out-of-line record delta) starting from the first byte after the
// field header. Advances the field location to the start of the next field.
UInt32 OptionalFields::DecodeFieldValue(PTR_UInt8 * ppFields)
{
    // VarInt is used to encode the field value (and updates the field pointer in doing so).
    return VarInt::ReadUnsigned(*ppFields);
}

/*static*/ UInt32 OptionalFields::GetInlineField(OptionalFieldTag eTag, UInt32 uiDefaultValue)
{       
    // Point at start of encoding stream.
    PTR_UInt8 pFields = dac_cast<PTR_UInt8>(this);

    for (;;)
    {
        // Read field tag, an indication of whether this is the last field and the field value (we always read
        // the value, even if the tag is not a match because decoding the value advances the field pointer to
        // the next field).
        bool fLastField;
        OptionalFieldTag eCurrentTag = DecodeFieldTag(&pFields, &fLastField);
        UInt32 uiCurrentValue = DecodeFieldValue(&pFields);

        // If we found a tag match return the current value.
        if (eCurrentTag == eTag)
            return uiCurrentValue;

        // If this was the last field we're done as well.
        if (fLastField)
            break;
    }

    // Reached end of stream without getting a match. Field is not present so return default value.
    return uiDefaultValue;
}
