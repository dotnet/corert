// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    // Indicates whether or not a given ManuallyManaged method will rendez-vous 
    // with GC if a suspension is being requested.  Most ManuallyManaged methods
    // will be PollPolicy.Never.  The only distinction made by the code generator
    // is between Always and everything else.  The distinction between Sometimes
    // and Never is merely for documentation purposes.
    internal enum GcPollPolicy
    {
        Always = 1,
        Sometimes = 2,
        Never = 3,
    }

    internal class ManuallyManagedAttribute : Attribute
    {
        public ManuallyManagedAttribute(GcPollPolicy poll)
        {
        }
    }
}
