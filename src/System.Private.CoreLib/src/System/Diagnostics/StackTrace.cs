// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Diagnostics
{
    /// <summary>
    /// Class which represents a description of a stack trace
    /// There is no good reason for the methods of this class to be virtual.
    /// In order to ensure trusted code can trust the data it gets from a
    /// StackTrace, we use an InheritanceDemand to prevent partially-trusted
    /// subclasses.
    /// </summary>
    [Serializable]
    public class StackTrace
    {
        public const int METHODS_TO_SKIP = 0;

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(int skipFrames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(int skipFrames, bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(Exception e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(Exception e, bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(Exception e, int skipFrames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(Exception e, int skipFrames, bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a "fake" stack trace, just containing a single frame.
        /// Does not have the overhead of a full stack trace.
        /// </summary>
        public StackTrace(StackFrame frame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Property to get the number of frames in the stack trace
        /// </summary>
        public virtual int FrameCount
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Returns a given stack frame.  Stack frames are numbered starting at
        /// zero, which is the last stack frame pushed.
        /// </summary>
        public virtual StackFrame GetFrame(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns an array of all stack frames for this stacktrace.
        /// The array is ordered and sized such that GetFrames()[i] == GetFrame(i)
        /// The nth element of this array is the same as GetFrame(n).
        /// The length of the array is the same as FrameCount.
        /// </summary>
        public virtual StackFrame[] GetFrames()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds a readable representation of the stack trace
        /// </summary>
        public override String ToString()
        {
            throw new NotImplementedException();
        }
    }
}
