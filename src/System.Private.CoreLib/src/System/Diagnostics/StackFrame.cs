// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace System.Diagnostics
{
    /// <summary>
    /// Stack frame represents a single frame in a stack trace; frames
    /// corresponding to methods with available symbolic information
    /// provide source file / line information. Some frames may provide IL
    /// offset information and / or MethodBase reflection information.
    /// There is no good reason for the methods of this class to be virtual.
    /// </summary>
    [Serializable]
    public class StackFrame
    {
        /// <summary>
        /// Constant returned when the native or IL offset is unknown
        /// </summary>
        public const int OFFSET_UNKNOWN = -1;

        /// <summary>
        /// Constructs a StackFrame corresponding to the active stack frame.
        /// </summary>
        public StackFrame()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a StackFrame corresponding to the active stack frame.
        /// </summary>
        public StackFrame(bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a StackFrame corresponding to a calling stack frame.
        /// </summary>
        public StackFrame(int skipFrames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a StackFrame corresponding to a calling stack frame.
        /// </summary>
        public StackFrame(int skipFrames, bool fNeedFileInfo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a "fake" stack frame, just containing the given file
        /// name and line number.  Use when you don't want to use the
        /// debugger's line mapping logic.
        /// </summary>
        public StackFrame(String fileName, int lineNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Constructs a "fake" stack frame, just containing the given file
        /// name, line number and column number.  Use when you don't want to
        /// use the debugger's line mapping logic.
        /// </summary>
        public StackFrame(String fileName, int lineNumber, int colNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the method the frame is executing
        /// </summary>
        public virtual MethodBase GetMethod()
        {
            Contract.Ensures(Contract.Result<MethodBase>() != null);

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the offset from the start of the native (jitted) code for the
        /// method being executed
        /// </summary>
        public virtual int GetNativeOffset()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the offset from the start of the IL code for the
        /// method being executed.  This offset may be approximate depending
        /// on whether the jitter is generating debuggable code or not.
        /// </summary>
        public virtual int GetILOffset()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the file name containing the code being executed.  This
        /// information is normally extracted from the debugging symbols
        /// for the executable.
        /// </summary>
        public virtual String GetFileName()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the line number in the file containing the code being executed.
        /// This information is normally extracted from the debugging symbols
        /// for the executable.
        /// </summary>
        public virtual int GetFileLineNumber()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the column number in the line containing the code being executed.
        /// This information is normally extracted from the debugging symbols
        /// for the executable.
        /// </summary>
        public virtual int GetFileColumnNumber()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds a readable representation of the stack frame
        /// </summary>
        public override String ToString()
        {
            throw new NotImplementedException();
        }
    }
}
