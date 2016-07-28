// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System
{
    //
    // This is not actually an instance of the System.DBNull type exposed out of System.Data.Common.
    // But this is also true on CoreCLR, so it's still compatible!
    //
    // This will of course, confound anyone who actually tries to compare the real System.DBNull to what we return.
    // This is, however, the least worst of many bad options. Current apps out in the wild do not have access to a
    // contract exposing System.DBNull so this hack of checking the type name via reflection is what they actually do.
    //
    // Official guidance is: Call ParameterInfo.HasDefaultValue.
    //

    //
    // @todo: B#1190393 - this type should be "internal" but has to be made "public" in order for it to be reflectable under
    // our reflection-block mechanism. The ReflectionBlock mechanism is on the block itself, so once that's done, we can make this type
    // "internal" too.
    //
    public sealed class DBNull
    {
        private DBNull()
        {
        }

        internal static readonly Object Value = new DBNull();
    }
}

