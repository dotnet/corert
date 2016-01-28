// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;

using global::Internal.Reflection.Core;

namespace Internal.Reflection.Execution
{
    internal sealed class FoundationTypesImplementation : FoundationTypes
    {
        public sealed override Type SystemObject { get { return typeof(Object); } }
        public sealed override Type SystemValueType { get { return typeof(ValueType); } }
        public sealed override Type SystemEnum { get { return typeof(Enum); } }
        public sealed override Type SystemVoid { get { return typeof(void); } }
        public sealed override Type SystemArray { get { return typeof(Array); } }
        public sealed override Type SystemString { get { return typeof(String); } }
        public sealed override Type SystemType { get { return typeof(Type); } }
        public sealed override Type SystemBoolean { get { return typeof(Boolean); } }
        public sealed override Type SystemChar { get { return typeof(Char); } }
        public sealed override Type SystemSByte { get { return typeof(SByte); } }
        public sealed override Type SystemByte { get { return typeof(Byte); } }
        public sealed override Type SystemInt16 { get { return typeof(Int16); } }
        public sealed override Type SystemUInt16 { get { return typeof(UInt16); } }
        public sealed override Type SystemInt32 { get { return typeof(Int32); } }
        public sealed override Type SystemUInt32 { get { return typeof(UInt32); } }
        public sealed override Type SystemInt64 { get { return typeof(Int64); } }
        public sealed override Type SystemUInt64 { get { return typeof(UInt64); } }
        public sealed override Type SystemIntPtr { get { return typeof(IntPtr); } }
        public sealed override Type SystemUIntPtr { get { return typeof(UIntPtr); } }
        public sealed override Type SystemSingle { get { return typeof(Single); } }
        public sealed override Type SystemDouble { get { return typeof(Double); } }
    }
}
