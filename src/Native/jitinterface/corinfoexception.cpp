//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include "corinfoexception.h"
#include "dllexport.h"

DLL_EXPORT CorInfoException* AllocException(const WCHAR* message, int messageLength)
{
    return new CorInfoException(message, messageLength);
}

DLL_EXPORT void FreeException(CorInfoException* pException)
{
    delete pException;
}

DLL_EXPORT const WCHAR* GetExceptionMessage(const CorInfoException* pException)
{
    return pException->GetMessage();
}
