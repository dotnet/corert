// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// 
//
//
// ===========================================================================
// File: sha1.cpp
// 
// ===========================================================================
/*++

Abstract:

    SHA-1 implementation

Revision History:

--*/

/*
       File sha1.cpp    <STRIP>Version 03 August 2000.</STRIP>
                   
                     
      This implements the SHA-1 hash function.  
      For algorithmic background see (for example)


           Alfred J. Menezes et al
           Handbook of Applied Cryptography
           The CRC Press Series on Discrete Mathematics
                   and its Applications
           CRC Press LLC, 1997
           ISBN 0-8495-8523-7
           QA76.9A25M643

       Also see FIPS 180-1 - Secure Hash Standard, 
       1993 May 11 and 1995 April 17, by the U.S.
       National Institute of Standards and Technology (NIST).

*/


#include "common.h"
#include "common.h"
#include "CommonTypes.h"
#include "CommonMacros.h"
#include "PalRedhawkCommon.h"

#include "sha1.h"


#define ROTATE32L(x,n) rotate32l(x,n)
#define SHAVE32(x)     (UInt32)(x)

static UInt32 rotate32l(UInt32 val, int shift)
{
    shift &= 0x1f;
    val = (val >> (0x20 - shift)) | (val << shift);
    return val;
}

static void SHA1_block(SHA1_CTX *ctx)
/*
     Update the SHA-1 hash from a fresh 64 bytes of data.
*/
{ 
    static const UInt32 sha1_round1 = 0x5A827999u; 
    static const UInt32 sha1_round2 = 0x6ED9EBA1u;
    static const UInt32 sha1_round3 = 0x8F1BBCDCu;
    static const UInt32 sha1_round4 = 0xCA62C1D6u;
    
    UInt32 a = ctx->partial_hash[0], b = ctx->partial_hash[1]; 
    UInt32 c = ctx->partial_hash[2], d = ctx->partial_hash[3];
    UInt32 e = ctx->partial_hash[4];
    UInt32 msg80[80]; 
    int i;

    // OACR note:
    // Loop conditions are using (i <= limit - increment) instead of (i < limit) to satisfy OACR. When the increment is greater
    // than 1, OACR incorrectly thinks that the max value of 'i' is (limit - 1).

    for (i = 0; i < 16; i++) {   // Copy to local array, zero original
                                  // Extend length to 80
        const UInt32 datval = ctx->awaiting_data[i];
        ctx->awaiting_data[i] = 0;
        msg80[i] = datval;
    }

    for (i = 16; i <= 80 - 2; i += 2) {
        const UInt32 temp1 =    msg80[i-3] ^ msg80[i-8] 
                        ^ msg80[i-14] ^ msg80[i-16];
        const UInt32 temp2 =    msg80[i-2] ^ msg80[i-7] 
                        ^ msg80[i-13] ^ msg80[i-15];
        msg80[i  ] = ROTATE32L(temp1, 1);
        msg80[i+1] = ROTATE32L(temp2, 1);
    }
  
#define ROUND1(B, C, D) ((D ^ (B & (C ^ D))) + sha1_round1)
                        //  Equivalent to (B & C) | (~B & D).
                        //  (check cases B = 0 and B = 1)
#define ROUND2(B, C, D) ((B ^ C ^ D) + sha1_round2)

#define ROUND3(B, C, D) ((C & (B | D) | (B & D)) + sha1_round3)

#define ROUND4(B, C, D) ((B ^ C ^ D) + sha1_round4)

// Round 1
    for (i = 0; i <= 20 - 5; i += 5) { 
        e += ROTATE32L(a, 5) + ROUND1(b, c, d) + msg80[i];
        b = ROTATE32L(b, 30);

        d += ROTATE32L(e, 5) + ROUND1(a, b, c) + msg80[i+1];
        a = ROTATE32L(a, 30);

        c += ROTATE32L(d, 5) + ROUND1(e, a, b) + msg80[i+2];
        e = ROTATE32L(e, 30);

        b += ROTATE32L(c, 5) + ROUND1(d, e, a) + msg80[i+3];
        d = ROTATE32L(d, 30);

        a += ROTATE32L(b, 5) + ROUND1(c, d, e) + msg80[i+4];
        c = ROTATE32L(c, 30);
#if 0
        printf("i = %ld %08lx %08lx %08lx %08lx %08lx\n", 
            i, a, b, c, d, e);
#endif
    } // for i

// Round 2
    for (i = 20; i <= 40 - 5; i += 5) { 
        e += ROTATE32L(a, 5) + ROUND2(b, c, d) + msg80[i];
        b = ROTATE32L(b, 30);

        d += ROTATE32L(e, 5) + ROUND2(a, b, c) + msg80[i+1];
        a = ROTATE32L(a, 30);

        c += ROTATE32L(d, 5) + ROUND2(e, a, b) + msg80[i+2];
        e = ROTATE32L(e, 30);

        b += ROTATE32L(c, 5) + ROUND2(d, e, a) + msg80[i+3];
        d = ROTATE32L(d, 30);

        a += ROTATE32L(b, 5) + ROUND2(c, d, e) + msg80[i+4];
        c = ROTATE32L(c, 30);
    } // for i

// Round 3
    for (i = 40; i <= 60 - 5; i += 5) { 
        e += ROTATE32L(a, 5) + ROUND3(b, c, d) + msg80[i];
        b = ROTATE32L(b, 30);

        d += ROTATE32L(e, 5) + ROUND3(a, b, c) + msg80[i+1];
        a = ROTATE32L(a, 30);

        c += ROTATE32L(d, 5) + ROUND3(e, a, b) + msg80[i+2];
        e = ROTATE32L(e, 30);

        b += ROTATE32L(c, 5) + ROUND3(d, e, a) + msg80[i+3];
        d = ROTATE32L(d, 30);

        a += ROTATE32L(b, 5) + ROUND3(c, d, e) + msg80[i+4];
        c = ROTATE32L(c, 30);
    } // for i

// Round 4
    for (i = 60; i <= 80 - 5; i += 5) { 
        e += ROTATE32L(a, 5) + ROUND4(b, c, d) + msg80[i];
        b = ROTATE32L(b, 30);

        d += ROTATE32L(e, 5) + ROUND4(a, b, c) + msg80[i+1];
        a = ROTATE32L(a, 30);

        c += ROTATE32L(d, 5) + ROUND4(e, a, b) + msg80[i+2];
        e = ROTATE32L(e, 30);

        b += ROTATE32L(c, 5) + ROUND4(d, e, a) + msg80[i+3];
        d = ROTATE32L(d, 30);

        a += ROTATE32L(b, 5) + ROUND4(c, d, e) + msg80[i+4];
        c = ROTATE32L(c, 30);
    } // for i

#undef ROUND1
#undef ROUND2
#undef ROUND3
#undef ROUND4

    ctx->partial_hash[0] += a;
    ctx->partial_hash[1] += b;
    ctx->partial_hash[2] += c;
    ctx->partial_hash[3] += d;
    ctx->partial_hash[4] += e;
#if 0
    for (i = 0; i < 16; i++) {
        printf("%8lx ", msg16[i]);
        if ((i & 7) == 7) printf("\n");
    }
    printf("a, b, c, d, e = %08lx %08lx %08lx %08lx %08lx\n", 
        a, b, c, d, e);
    printf("Partial hash = %08lx %08lx %08lx %08lx %08lx\n",
        (long)ctx->partial_hash[0], (long)ctx->partial_hash[1],
        (long)ctx->partial_hash[2], (long)ctx->partial_hash[3],
        (long)ctx->partial_hash[4]);
#endif 
} // end SHA1_block


void SHA1Hash::SHA1Init(SHA1_CTX *ctx)
{
    ctx->nbit_total[0] = ctx->nbit_total[1] = 0;
    
    for (UInt32 i = 0; i != 16; i++) {
        ctx->awaiting_data[i] = 0;
    }
   
     /* 
         Initialize hash variables.
         
     */

    ctx->partial_hash[0] = 0x67452301u;
    ctx->partial_hash[1] = 0xefcdab89u;
    ctx->partial_hash[2] = ~ctx->partial_hash[0];
    ctx->partial_hash[3] = ~ctx->partial_hash[1];
    ctx->partial_hash[4] = 0xc3d2e1f0u;

} 

void SHA1Hash::SHA1Update(
        SHA1_CTX *  ctx,        // IN/OUT
        const UInt8 *    msg,    // IN
        UInt32           nbyte)  // IN
/*
    Append data to a partially hashed SHA-1 message.
*/
{
    const UInt8 *fresh_data = msg;
    UInt32 nbyte_left = nbyte;
    UInt32 nbit_occupied = ctx->nbit_total[0] & 511;
    UInt32 *awaiting_data;
    const UInt32 nbitnew_low = SHAVE32(8*nbyte);


    ASSERT((nbit_occupied & 7) == 0);   // Partial bytes not implemented
    
    ctx->nbit_total[0] += nbitnew_low;
    ctx->nbit_total[1] += (nbyte >> 29) 
           + (SHAVE32(ctx->nbit_total[0]) < nbitnew_low);

        /* Advance to word boundary in waiting_data */
    
    if ((nbit_occupied & 31) != 0) {
        awaiting_data = ctx->awaiting_data + nbit_occupied/32;

        while ((nbit_occupied & 31) != 0 && nbyte_left != 0) {
            nbit_occupied += 8;
            *awaiting_data |= (UInt32)*fresh_data++ 
                     << ((-(int)nbit_occupied) & 31);
            nbyte_left--;            // Start at most significant byte
        }
    } // if nbit_occupied

             /* Transfer 4 bytes at a time */

    do {
        const UInt32 nword_occupied = nbit_occupied/32;
        UInt32 nwcopy = min(nbyte_left/4, 16 - nword_occupied);
        ASSERT (nbit_occupied <= 512);
        ASSERT ((nbit_occupied & 31) == 0 || nbyte_left == 0);
        awaiting_data = ctx->awaiting_data + nword_occupied;
        nbyte_left -= 4*nwcopy;
        nbit_occupied += 32*nwcopy;

        while (nwcopy != 0) {
            const UInt32 byte0 = (UInt32)fresh_data[0];
            const UInt32 byte1 = (UInt32)fresh_data[1];
            const UInt32 byte2 = (UInt32)fresh_data[2];
            const UInt32 byte3 = (UInt32)fresh_data[3];
            *awaiting_data++ = byte3 | (byte2 << 8)
                        | (byte1 << 16) | (byte0 << 24);
                             /* Big endian */
            fresh_data += 4;
            nwcopy--;
        } 

        if (nbit_occupied == 512) {
            SHA1_block(ctx);
            nbit_occupied = 0;
            awaiting_data -= 16;
            ASSERT(awaiting_data == ctx->awaiting_data);
        }
    } while (nbyte_left >= 4); 

    ASSERT (ctx->awaiting_data + nbit_occupied/32
                       == awaiting_data);

    while (nbyte_left != 0) {
        const UInt32 new_byte = (UInt32)*fresh_data++;

        ASSERT((nbit_occupied & 31) <= 16);
        nbit_occupied += 8;
        *awaiting_data |= new_byte << ((-(int)nbit_occupied) & 31);
        nbyte_left--;
    }
    
    ASSERT (nbit_occupied == (ctx->nbit_total[0] & 511));
} // end SHA1Update



void SHA1Hash::SHA1Final(
        SHA1_CTX *  ctx,            // IN/OUT
        UInt8 *          digest)     // OUT
/*
        Finish a SHA-1 hash.
*/
{
    const UInt32 nbit0 = ctx->nbit_total[0];
    const UInt32 nbit1 = ctx->nbit_total[1];
    UInt32 nbit_occupied = nbit0 & 511;
    UInt32 i;

    ASSERT((nbit_occupied & 7) == 0);

    ctx->awaiting_data[nbit_occupied/32] 
         |= (UInt32)0x80 << ((-8-nbit_occupied) & 31);
                          // Append a 1 bit
    nbit_occupied += 8;


    // Append zero bits until length (in bits) is 448 mod 512.
    // Then append the length, in bits.
    // Here we assume the buffer was zeroed earlier.

    if (nbit_occupied > 448) {   // If fewer than 64 bits left
        SHA1_block(ctx);
        nbit_occupied = 0;
    }
    ctx->awaiting_data[14] = nbit1;
    ctx->awaiting_data[15] = nbit0;
    SHA1_block(ctx);

         /* Copy final digest to user-supplied byte array */

    for (i = 0; i != 5; i++) {
        const UInt32 dwi = ctx->partial_hash[i];
        digest[4*i + 0] = (UInt8)((dwi >> 24) & 255);
        digest[4*i + 1] = (UInt8)((dwi >> 16) & 255);
        digest[4*i + 2] = (UInt8)((dwi >>  8) & 255);
        digest[4*i + 3] = (UInt8)(dwi         & 255);  // Big-endian
    }
} // end SHA1Final

SHA1Hash::SHA1Hash()
{
    m_fFinalized = false;
    SHA1Init(&m_Context);
}
    
void SHA1Hash::AddData(const UInt8 *pbData, UInt32 cbData)
{
    if (m_fFinalized)
        return;
        
    SHA1Update(&m_Context, pbData, cbData);
}

// Retrieve a pointer to the final hash.
UInt8 *SHA1Hash::GetHash()
{
    if (m_fFinalized)
        return m_Value;

    SHA1Final(&m_Context, m_Value);
     
    m_fFinalized = true;

    return m_Value;
}

