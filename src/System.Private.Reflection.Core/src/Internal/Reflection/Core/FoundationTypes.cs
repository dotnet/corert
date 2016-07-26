// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace Internal.Reflection.Core
{
    // This class serves up a small set of foundation types that have special meaning to Reflection.
    public abstract class FoundationTypes
    {
        public abstract Type SystemObject { get; }
        public abstract Type SystemValueType { get; }
        public abstract Type SystemEnum { get; }
        public abstract Type SystemVoid { get; }
        public abstract Type SystemArray { get; }
        public abstract Type SystemString { get; }
        public abstract Type SystemType { get; }
        public abstract Type SystemBoolean { get; }
        public abstract Type SystemChar { get; }
        public abstract Type SystemSByte { get; }
        public abstract Type SystemByte { get; }
        public abstract Type SystemInt16 { get; }
        public abstract Type SystemUInt16 { get; }
        public abstract Type SystemInt32 { get; }
        public abstract Type SystemUInt32 { get; }
        public abstract Type SystemInt64 { get; }
        public abstract Type SystemUInt64 { get; }
        public abstract Type SystemIntPtr { get; }
        public abstract Type SystemUIntPtr { get; }
        public abstract Type SystemSingle { get; }
        public abstract Type SystemDouble { get; }
    }
}
