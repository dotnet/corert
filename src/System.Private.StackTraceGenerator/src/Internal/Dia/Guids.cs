// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.StackGenerator.Dia
{
    internal static class Guids
    {
        public static readonly IEnumerable<Guid> DiaSource_CLSIDs =
            new Guid[]
            {
                new Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D"),  // msdia120.dll
                new Guid("761D3BCD-1304-41D5-94E8-EAC54E4AC172"),  // msdia110.dll
            };

        public static readonly Guid IID_IDiaDataSource = new Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D");
    }
}

