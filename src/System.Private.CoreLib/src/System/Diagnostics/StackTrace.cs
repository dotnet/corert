// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Text;
using System.Reflection;

using Internal.Diagnostics;

namespace System.Diagnostics
{
    /// <summary>
    /// Class which represents a description of a stack trace
    /// There is no good reason for the methods of this class to be virtual.
    /// In order to ensure trusted code can trust the data it gets from a
    /// StackTrace, we use an InheritanceDemand to prevent partially-trusted
    /// subclasses.
    /// </summary>
    public class StackTrace
    {
        public const int METHODS_TO_SKIP = 0;

        /// <summary>
        /// Stack frames comprising this stack trace.
        /// </summary>
        private StackFrame[] _stackFrames;

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace()
        {
            InitializeForThreadFrameIndex(METHODS_TO_SKIP, needFileInfo: false);
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(bool needFileInfo)
        {
            InitializeForThreadFrameIndex(METHODS_TO_SKIP, needFileInfo);
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(int skipFrames)
        {
            InitializeForThreadFrameIndex(skipFrames, needFileInfo: false);
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(int skipFrames, bool needFileInfo)
        {
            InitializeForThreadFrameIndex(skipFrames, needFileInfo);
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(Exception e)
        {
            InitializeForExceptionFrameIndex(e, METHODS_TO_SKIP, needFileInfo: false);
        }

        /// <summary>
        /// Constructs a stack trace from the current location.
        /// </summary>
        public StackTrace(Exception e, bool needFileInfo)
        {
            InitializeForExceptionFrameIndex(e, METHODS_TO_SKIP, needFileInfo);
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(Exception e, int skipFrames)
        {
            InitializeForExceptionFrameIndex(e, skipFrames, needFileInfo: false);
        }

        /// <summary>
        /// Constructs a stack trace from the current location, in a caller's
        /// frame
        /// </summary>
        public StackTrace(Exception e, int skipFrames, bool needFileInfo)
        {
            InitializeForExceptionFrameIndex(e, skipFrames, needFileInfo);
        }

        /// <summary>
        /// Constructs a "fake" stack trace, just containing a single frame.
        /// Does not have the overhead of a full stack trace.
        /// </summary>
        public StackTrace(StackFrame frame)
        {
            _stackFrames = new StackFrame[] { frame };
        }

        /// <summary>
        /// Construct a stack trace based on a subset of a precomputed array of IP addresses.
        /// </summary>
        /// <param name="ipAddresses">Array of IP addresses to use as the stack trace</param>
        /// <param name="startIndex">Starting index in the array to use</param>
        /// <param name="endIndex">Ending index in the array (one plus the last element)</param>
        /// <param name="needFileInfo">True when source file / line information is requested</param>
        internal StackTrace(IntPtr[] ipAddresses, int startIndex, int endIndex, bool needFileInfo)
        {
            InitializeForIpAddressArray(ipAddresses, startIndex, endIndex, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on current thread and given initial frame index.
        /// </summary>
        private void InitializeForThreadFrameIndex(int skipFrames, bool needFileInfo)
        {
            int frameCount = -RuntimeImports.RhGetCurrentThreadStackTrace(Array.Empty<IntPtr>());
            Debug.Assert(frameCount >= 0);
            IntPtr[] stackTrace = new IntPtr[frameCount];
            int trueFrameCount = RuntimeImports.RhGetCurrentThreadStackTrace(stackTrace);
            Debug.Assert(trueFrameCount == frameCount);
            InitializeForIpAddressArray(stackTrace, skipFrames, frameCount, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on a given exception and initial frame index.
        /// </summary>
        private void InitializeForExceptionFrameIndex(Exception exception, int skipFrames, bool needFileInfo)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }
            IntPtr[] stackIPs = exception.GetStackIPs();
            InitializeForIpAddressArray(stackIPs, skipFrames, stackIPs.Length, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on a given array of IP addresses.
        /// </summary>
        private void InitializeForIpAddressArray(IntPtr[] ipAddresses, int skipFrames, int endFrameIndex, bool needFileInfo)
        {
            int frameCount = (skipFrames < endFrameIndex ? endFrameIndex - skipFrames : 0);

            // Calculate true frame count upfront - we need to skip EdiSeparators which get
            // collapsed onto boolean flags on the preceding stack frame
            int outputFrameCount = 0;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (ipAddresses[frameIndex + skipFrames] != StackTraceHelper.SpecialIP.EdiSeparator)
                {
                    outputFrameCount++;
                }
            }

            if (outputFrameCount > 0)
            {
                _stackFrames = new StackFrame[outputFrameCount];
                int outputFrameIndex = 0;
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    IntPtr ipAddress = ipAddresses[frameIndex + skipFrames];
                    if (ipAddress != StackTraceHelper.SpecialIP.EdiSeparator)
                    {
                        _stackFrames[outputFrameIndex++] = new StackFrame(ipAddress, needFileInfo);
                    }
                    else if (outputFrameIndex > 0)
                    {
                        _stackFrames[outputFrameIndex - 1].SetIsLastFrameFromForeignExceptionStackTrace(true);
                    }
                }
                Debug.Assert(outputFrameIndex == outputFrameCount);
            }
        }

        /// <summary>
        /// Property to get the number of frames in the stack trace
        /// </summary>
        public virtual int FrameCount
        {
            get { return _stackFrames != null ? _stackFrames.Length : 0; }
        }

        /// <summary>
        /// Returns a given stack frame.  Stack frames are numbered starting at
        /// zero, which is the last stack frame pushed.
        /// </summary>
        public virtual StackFrame GetFrame(int index)
        {
            if (_stackFrames != null && index >= 0 && index < _stackFrames.Length)
            {
                return _stackFrames[index];
            }
            return null;
        }

        /// <summary>
        /// Returns an array of all stack frames for this stacktrace.
        /// The array is ordered and sized such that GetFrames()[i] == GetFrame(i)
        /// The nth element of this array is the same as GetFrame(n).
        /// The length of the array is the same as FrameCount.
        /// </summary>
        public virtual StackFrame[] GetFrames()
        {
            if (_stackFrames == null)
            {
                return null;
            }

            StackFrame[] array = new StackFrame[_stackFrames.Length];
            Array.Copy(_stackFrames, array, _stackFrames.Length);
            return array;
        }

        /// <summary>
        /// Builds a readable representation of the stack trace
        /// </summary>
        public override string ToString()
        {
            return ToString(TraceFormat.Normal);    // default behavior in RT did not have trailing newline
        }

        // TraceFormat is Used to specify options for how the 
        // string-representation of a StackTrace should be generated.
        internal enum TraceFormat
        {
            Normal,
            TrailingNewLine,        // include a trailing new line character
        }

        internal string ToString(TraceFormat traceFormat)
        {
            if (_stackFrames == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            foreach (StackFrame frame in _stackFrames)
            {
                frame.AppendToStackTrace(builder);
            }
            
            if (traceFormat == TraceFormat.TrailingNewLine)
            {
                builder.Append(Environment.NewLine);
            }

            return builder.ToString();
        }

    }
}
