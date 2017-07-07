// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//------------------------------------------------------------------------------------------------------------
// @TODO: this type is related to throwing exceptions out of Rtm. If we did not have to throw
// out of Rtm, then we would note have to have the code below to get a classlib exception object given
// an exception id, or the special functions to back up the MDIL THROW_* instructions, or the allocation
// failure helper. If we could move to a world where we never throw out of Rtm, perhaps to other generated code, 
// then we could remove all of this.
//------------------------------------------------------------------------------------------------------------

namespace System.Runtime
{
    public enum ExceptionIDs
    {
        OutOfMemory = 1,
        Arithmetic = 2,
        ArrayTypeMismatch = 3,
        DivideByZero = 4,
        IndexOutOfRange = 5,
        InvalidCast = 6,
        Overflow = 7,
        NullReference = 8,
        AccessViolation = 9,
        DataMisaligned = 10,
        ThreadAbort = 11,
    }
}
