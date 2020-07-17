// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Reflection;
using System.Runtime;
using System.Text;

using Internal.DeveloperExperience;
using Internal.Diagnostics;

namespace System.Diagnostics
{
    /// <summary>
    /// Stack frame represents a single frame in a stack trace; frames
    /// corresponding to methods with available symbolic information
    /// provide source file / line information. Some frames may provide IL
    /// offset information and / or MethodBase reflection information.
    /// There is no good reason for the methods of this class to be virtual.
    /// </summary>
    public partial class StackFrame
    {
        /// <summary>
        /// IP address representing this stack frame.
        /// </summary>
        private IntPtr _ipAddress;

        /// <summary>
        /// File info flag to use for stack trace-style formatting.
        /// </summary>
        private bool _needFileInfo;

        /// <summary>
        /// Constructs a StackFrame corresponding to a given IP address.
        /// </summary>
        internal StackFrame(IntPtr ipAddress, bool needFileInfo)
        {
            InitializeForIpAddress(ipAddress, needFileInfo);
        }

        /// <summary>
        /// Internal stack frame initialization based on IP address.
        /// </summary>
        private void InitializeForIpAddress(IntPtr ipAddress, bool needFileInfo)
        {
            _ipAddress = ipAddress;
            _needFileInfo = needFileInfo;
            
            if (_ipAddress == StackTraceHelper.SpecialIP.EdiSeparator)
            {
                _isLastFrameFromForeignExceptionStackTrace = true;
            }
            else if (_ipAddress != IntPtr.Zero)
            {
                IntPtr methodStartAddress = RuntimeImports.RhFindMethodStartAddress(ipAddress);

                _nativeOffset = (int)(_ipAddress.ToInt64() - methodStartAddress.ToInt64());

                DeveloperExperience.Default.TryGetILOffsetWithinMethod(_ipAddress, out _ilOffset);
                DeveloperExperience.Default.TryGetMethodBase(methodStartAddress, out _method);

                if (needFileInfo)
                {
                    DeveloperExperience.Default.TryGetSourceLineInfo(
                        _ipAddress,
                        out _fileName,
                        out _lineNumber,
                        out _columnNumber);
                }
            }
        }

        /// <summary>
        /// Internal stack frame initialization based on frame index within the stack of the current thread.
        /// </summary>
        private void BuildStackFrame(int frameIndex, bool needFileInfo)
        {
            IntPtr ipAddress = LocateIpAddressForStackFrame(frameIndex);
            InitializeForIpAddress(ipAddress, needFileInfo);
        }
        
        /// <summary>
        /// Locate IP address corresponding to a given frame. Ignore .NET Native-specific rethrow markers.
        /// </summary>
        private IntPtr LocateIpAddressForStackFrame(int frameIndex)
        {
            IntPtr[] frameArray = new IntPtr[frameIndex + 1];
            int returnedFrameCount = RuntimeImports.RhGetCurrentThreadStackTrace(frameArray);
            int realFrameCount = (returnedFrameCount >= 0 ? returnedFrameCount : frameArray.Length);
            if (frameIndex < realFrameCount)
            {
                return frameArray[frameIndex];
            }

            // No more frames are available
            return IntPtr.Zero;
        }

        /// <summary>
        /// Return native IP address for this stack frame.
        /// </summary>
        internal IntPtr GetNativeIPAddress()
        {
            return _ipAddress;
        }

        /// <summary>
        /// Check whether method info is available.
        /// </summary>
        internal bool HasMethod()
        {
            return _method != null;
        }

        /// <summary>
        /// Format stack frame without MethodBase info. Return true if the stack info
        /// is valid and line information should be appended if available.
        /// </summary>
        private bool AppendStackFrameWithoutMethodBase(StringBuilder builder)
        {
            builder.Append(DeveloperExperience.Default.CreateStackTraceString(_ipAddress, includeFileInfo: false));
            return true;
        }

        /// <summary>
        /// Set rethrow marker.
        /// </summary>
        internal void SetIsLastFrameFromForeignExceptionStackTrace()
        {
            _isLastFrameFromForeignExceptionStackTrace = true;
        }

        /// <summary>
        /// Builds a representation of the stack frame for use in the stack trace.
        /// </summary>
        internal void AppendToStackTrace(StringBuilder builder)
        {
            if (_ipAddress != StackTraceHelper.SpecialIP.EdiSeparator)
            {
                builder.Append(SR.StackTrace_AtWord);
                builder.AppendLine(DeveloperExperience.Default.CreateStackTraceString(_ipAddress, _needFileInfo));
            }
            if (_isLastFrameFromForeignExceptionStackTrace)
            {
                builder.AppendLine(SR.StackTrace_EndStackTraceFromPreviousThrow);
            }
        }
    }
}
