// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Reflection
{
    public sealed class Missing
    {
        private Missing()
        {
        }

        public static readonly Missing Value = new Missing();
    }
}
