//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include <string.h>

#ifdef PLATFORM_UNIX
typedef char16_t WCHAR;
#else
typedef wchar_t WCHAR;
#endif

class CorInfoException
{
public:
    CorInfoException(const WCHAR* message, int messageLength)
    {
        this->message = new WCHAR[messageLength + 1];
        memcpy(this->message, message, messageLength * sizeof(WCHAR));
        this->message[messageLength] = L'\0';
    }

    ~CorInfoException()
    {
        if (message != nullptr)
        {
            delete[] message;
            message = nullptr;
        }
    }

    const WCHAR* GetMessage() const
    {
        return message;
    }

private:
    WCHAR* message;
};
