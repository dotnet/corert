// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
class VarInt
{
public:
    static UInt32 ReadUnsigned(PTR_UInt8 & pbEncoding)
    {
        UIntNative lengthBits = *pbEncoding & 0x0F;
        size_t  negLength = s_negLengthTab[lengthBits];
        UIntNative shift = s_shiftTab[lengthBits];
        UInt32 result = *(PTR_UInt32)(pbEncoding - negLength - 4);

        result >>= shift;
        pbEncoding -= negLength;

        return result;
    }

    //
    // WARNING: This method returns the negative of the length of the value that it just skipped!
    //
    // This was helpful in the GC info scan loop because it allowed us to always skip past unsigned values in 
    // the  body of the loop.  At the end of loop, we use this negative sign to distinguish between two cases
    // and that allows us to decode the unsigned value that we need outside of the loop.  Note that we encode
    // the negatives in the s_negLengthTable to avoid any additional operations in the body of the GC scan 
    // loop.
    // 
    static IntNative SkipUnsigned(PTR_UInt8 & pbEncoding)
    {
        UIntNative lengthBits = *pbEncoding & 0x0F;
        size_t negLength = s_negLengthTab[lengthBits];
        pbEncoding -= negLength;
        return negLength;
    }

    static UIntNative WriteUnsigned(PTR_UInt8 pbDest, UInt32 value)
    {
        if (pbDest == NULL)
        {
            if (value < 128)
                return 1;

            if (value < 128*128)
                return 2;

            if (value < 128*128*128)
                return 3;

            if (value < 128*128*128*128)
                return 4;

            return 5;
        }

        if (value < 128)
        {
            *pbDest++ = (UInt8)(value*2 + 0);
            return 1;
        }

        if (value < 128*128)
        {
            *pbDest++ = (UInt8)(value*4 + 1);
            *pbDest++ = (UInt8)(value >> 6);
            return 2;
        }

        if (value < 128*128*128)
        {
            *pbDest++ = (UInt8)(value*8 + 3);
            *pbDest++ = (UInt8)(value >> 5);
            *pbDest++ = (UInt8)(value >> 13);
            return 3;
        }

        if (value < 128*128*128*128)
        {
            *pbDest++ = (UInt8)(value*16 + 7);
            *pbDest++ = (UInt8)(value >> 4);
            *pbDest++ = (UInt8)(value >> 12);
            *pbDest++ = (UInt8)(value >> 20);
            return 4;
        }

        *pbDest++ = 15;
        *pbDest++ = (UInt8)value;
        *pbDest++ = (UInt8)(value >> 8);
        *pbDest++ = (UInt8)(value >> 16);
        *pbDest++ = (UInt8)(value >> 24);
        return 5;
    }

private:
    static Int8 s_negLengthTab[16];
    static UInt8 s_shiftTab[16];
};

__declspec(selectany)
Int8 VarInt::s_negLengthTab[16] = 
{
    -1,    // 0
    -2,    // 1
    -1,    // 2
    -3,    // 3

    -1,    // 4
    -2,    // 5
    -1,    // 6
    -4,    // 7

    -1,    // 8
    -2,    // 9
    -1,    // 10
    -3,    // 11

    -1,    // 12
    -2,    // 13
    -1,    // 14
    -5,    // 15
};

__declspec(selectany)
UInt8 VarInt::s_shiftTab[16] =
{
    32-7*1,    // 0
    32-7*2,    // 1
    32-7*1,    // 2
    32-7*3,    // 3

    32-7*1,    // 4
    32-7*2,    // 5
    32-7*1,    // 6
    32-7*4,    // 7

    32-7*1,    // 8
    32-7*2,    // 9
    32-7*1,    // 10
    32-7*3,    // 11

    32-7*1,    // 12
    32-7*2,    // 13
    32-7*1,    // 14
    0,         // 15
};
