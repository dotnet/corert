// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                new Guid("E6756135-1E65-4D17-8576-610761398C3C"),  // msdia140.dll
                new Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D"),  // msdia120.dll
            };

        public static readonly Guid IID_IDiaDataSource = new Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D");
    }
}

