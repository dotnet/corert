// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "corinfoexception.h"
#include "dllexport.h"

DLL_EXPORT CorInfoException* __stdcall AllocException(const WCHAR* message, int messageLength)
{
    return new CorInfoException(message, messageLength);
}

DLL_EXPORT void __stdcall FreeException(CorInfoException* pException)
{
    delete pException;
}

DLL_EXPORT const WCHAR* __stdcall GetExceptionMessage(const CorInfoException* pException)
{
    return pException->GetMessage();
}
