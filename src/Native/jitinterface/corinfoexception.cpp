//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include "corinfoexception.h"

extern "C" CorInfoException* AllocException(const WCHAR* message, int messageLength)
{
    return new CorInfoException(message, messageLength);
}

extern "C" void FreeException(CorInfoException* pException)
{
    delete pException;
}

extern "C" const WCHAR* GetExceptionMessage(const CorInfoException* pException)
{
    return pException->GetMessage();
}
