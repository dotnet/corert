// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
        public readonly ILExceptionRegionKind Kind;
        public readonly int TryOffset;
        public readonly int TryLength;
        public readonly int HandlerOffset;
        public readonly int HandlerLength;
        public readonly int ClassToken;
        public readonly int FilterOffset;

        public ILExceptionRegion(
            ILExceptionRegionKind kind,
            int tryOffset,
            int tryLength,
            int handlerOffset,
            int handlerLength,
            int classToken,
            int filterOffset)
        {
            Kind = kind;
            TryOffset = tryOffset;
            TryLength = tryLength;
            HandlerOffset = handlerOffset;
            HandlerLength = handlerLength;
            ClassToken = classToken;
            FilterOffset = filterOffset;
        }
    }

    [System.Diagnostics.DebuggerTypeProxy(typeof(MethodILDebugView))]
    public abstract partial class MethodIL
    {
        public abstract MethodDesc GetOwningMethod();
        public abstract byte[] GetILBytes();
        public abstract int GetMaxStack();
        public abstract bool GetInitLocals();
        public abstract LocalVariableDefinition[] GetLocals();
        public abstract Object GetObject(int token);
        public abstract ILExceptionRegion[] GetExceptionRegions();
    }
}
