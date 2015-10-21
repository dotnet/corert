// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;

using Internal.TypeSystem;

namespace Internal.IL
{
    //
    // This duplicates types from System.Reflection.Metadata to avoid layering issues, and 
    // because of the System.Reflection.Metadata constructors are not public anyway.
    //

    public enum ILExceptionRegionKind
    {
        Catch = 0,
        Filter = 1,
        Finally = 2,
        Fault = 4,
    }

    public struct ILExceptionRegion
    {
        private readonly ILExceptionRegionKind _kind;
        private readonly int _tryOffset;
        private readonly int _tryLength;
        private readonly int _handlerOffset;
        private readonly int _handlerLength;
        private readonly int _classToken;
        private readonly int _filterOffset;

        public ILExceptionRegion(
            ILExceptionRegionKind kind,
            int tryOffset,
            int tryLength,
            int handlerOffset,
            int handlerLength,
            int classToken,
            int filterOffset)
        {
            _kind = kind;
            _tryOffset = tryOffset;
            _tryLength = tryLength;
            _handlerOffset = handlerOffset;
            _handlerLength = handlerLength;
            _classToken = classToken;
            _filterOffset = filterOffset;
        }

        public ILExceptionRegionKind Kind
        {
            get { return _kind; }
        }

        public int TryOffset
        {
            get { return _tryOffset; }
        }

        public int TryLength
        {
            get { return _tryLength; }
        }

        public int HandlerOffset
        {
            get { return _handlerOffset; }
        }

        public int HandlerLength
        {
            get { return _handlerLength; }
        }

        public int ClassToken
        {
            get { return _classToken; }
        }

        public int FilterOffset
        {
            get { return _filterOffset; }
        }
    }

    [System.Diagnostics.DebuggerTypeProxy(typeof(MethodILDebugView))]
    public abstract class MethodIL
    {
        public abstract byte[] GetILBytes();
        public abstract int GetMaxStack();
        public abstract bool GetInitLocals();
        public abstract TypeDesc[] GetLocals();
        public abstract Object GetObject(int token);
        public abstract ILExceptionRegion[] GetExceptionRegions();
    }
}
