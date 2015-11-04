//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

// Return the number of bytes necessary to encode the given integer.
/*static*/ UInt32 OptionalFields::EncodingSize(UInt32 uiValue)
{
    // One byte for the field header plus whatever VarInt takes to encode the value.
    return (UInt32)(1 + VarInt::WriteUnsigned(NULL, uiValue));
}

#ifndef DACCESS_COMPILE
// Encode the given field type and integer into the buffer provided (which is guaranteed to have enough
// space). Update the pointer into the buffer to point just past the newly encoded bytes. Note that any
// processing of the value for use with out-of-line records has already been performed; we're given the raw
// value to encode.
/*static*/ void OptionalFields::EncodeField(UInt8 ** ppFields, OptionalFieldTag eTag, bool fLastField, UInt32 uiValue)
{
    // Encode the header byte: most significant bit indicates whether this is the last field, remaining bits
    // the field type.
    **ppFields = (UInt8)((fLastField ? 0x80 : 0x00) | eTag);
    (*ppFields)++;

    // Have VarInt encode the value.
    *ppFields += VarInt::WriteUnsigned(*ppFields, uiValue);
}


#ifndef BINDER
void OptionalFieldsRuntimeBuilder::Decode(OptionalFields * pOptionalFields)
{
    ZeroMemory(m_rgFields, sizeof(m_rgFields));

    if (pOptionalFields == NULL)
        return;

    // Point at start of encoding stream.
    UInt8 * pFields = (UInt8*)pOptionalFields;

    for (;;)
    {
        // Read field tag, an indication of whether this is the last field and the field value (we always read
        // the value, even if the tag is not a match because decoding the value advances the field pointer to
        // the next field).
        bool fLastField;
        OptionalFieldTag eCurrentTag = OptionalFields::DecodeFieldTag(&pFields, &fLastField);
        UInt32 uiCurrentValue = OptionalFields::DecodeFieldValue(&pFields);

        // If we found a tag match return the current value.
        m_rgFields[eCurrentTag].m_fPresent = true;
        m_rgFields[eCurrentTag].m_uiValue = uiCurrentValue;

        // If this was the last field we're done as well.
        if (fLastField)
            break;
    }
}

UInt32 OptionalFieldsRuntimeBuilder::EncodingSize()
{
    UInt32 size = 0;
    for (int eTag = 0; eTag < OFT_Count; eTag++)
    {
        if (!m_rgFields[eTag].m_fPresent)
            continue;
        
        size += OptionalFields::EncodingSize(m_rgFields[eTag].m_uiValue);
    }
    return size;
}


UInt32 OptionalFieldsRuntimeBuilder::Encode(OptionalFields * pOptionalFields)
{
    int eLastTag;
    for (eLastTag = OFT_Count - 1; eLastTag >= 0; eLastTag--)
    {
        if (m_rgFields[eLastTag].m_fPresent)
            break;
    }

    if (eLastTag < 0)
        return 0;

    UInt8 * pFields = (UInt8*)pOptionalFields;

    for (int eTag = 0; eTag <= eLastTag; eTag++)
    {
        if (!m_rgFields[eTag].m_fPresent)
            continue;

        OptionalFields::EncodeField(&pFields, (OptionalFieldTag)eTag, eTag == eLastTag, m_rgFields[eTag].m_uiValue);
    }

    return (UInt32)(pFields - (UInt8*)pOptionalFields);
}
#endif // !BINDER
#endif // !DACCESS_COMPILE
