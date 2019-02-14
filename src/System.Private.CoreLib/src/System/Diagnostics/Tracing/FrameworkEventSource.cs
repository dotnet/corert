// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics.Tracing
{
    sealed internal class FrameworkEventSource
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

        public static bool IsInitialized { get => true; }

        public bool IsEnabled(EventLevel level, EventKeywords keywords)
        {
            return false;
        }

        public void ThreadPoolEnqueueWorkObject(object workID)
        {
        }

        public void ThreadPoolDequeueWorkObject(object workID)
        {
        }

        public void ThreadTransferSendObj(object id, int kind, string info, bool multiDequeues, int intInfo1, int intInfo2)
        {
        }

        public void ThreadTransferReceiveObj(object id, int kind, string info)
        {
        }
    }
}

