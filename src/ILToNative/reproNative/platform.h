// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

int UTF8ToWideCharLen(char* bytes, int len);

int UTF8ToWideChar(char* bytes, int len, uint16_t* buffer, int bufLen);

