// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.CommandLine
{
    class CommandLineException : Exception
    {
        public CommandLineException(string message)
            : base(message)
        {
        }
    }
}
