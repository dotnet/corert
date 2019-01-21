// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics.Tracing
{
    sealed internal class FrameworkEventSource : EventSource
    {
        // Defines the singleton instance for the Resources ETW provider
        public static readonly FrameworkEventSource Log = new FrameworkEventSource();

        // Keyword definitions.  These represent logical groups of events that can be turned on and off independently
        // Often each task has a keyword, but where tasks are determined by subsystem, keywords are determined by
        // usefulness to end users to filter.  Generally users don't mind extra events if they are not high volume
        // so grouping low volume events together in a single keywords is OK (users can post-filter by task if desired)
        public static class Keywords
        {
            public const EventKeywords ThreadPool = (EventKeywords)0x0002;
            public const EventKeywords ThreadTransfer = (EventKeywords)0x0010;
        }

        // The FrameworkEventSource GUID is {8E9F5090-2D75-4d03-8A81-E5AFBF85DAF1}
        private FrameworkEventSource()
            : base(new Guid(0x8e9f5090, 0x2d75, 0x4d03, 0x8a, 0x81, 0xe5, 0xaf, 0xbf, 0x85, 0xda, 0xf1), "System.Diagnostics.Eventing.FrameworkEventSource")
        {
        }

        public void ThreadPoolEnqueueWorkObject(object workID)
        {
        }

        public void ThreadPoolDequeueWorkObject(object workID)
        {
        }
    }
}

