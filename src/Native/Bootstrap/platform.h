// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

int UTF8ToWideCharLen(char* bytes, int len);

int UTF8ToWideChar(char* bytes, int len, uint16_t* buffer, int bufLen);

