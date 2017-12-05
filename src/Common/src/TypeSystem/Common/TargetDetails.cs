// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Specifies the target CPU architecture.
    /// </summary>
    public enum TargetArchitecture
    {
        Unknown,
        ARM,
        ARMEL,
        ARM64,
        X64,
        X86,
        Wasm32
    }

    /// <summary>
    /// Specifies the target ABI.
    /// </summary>
    public enum TargetOS
    {
        Unknown,
        Windows,
        Linux,
        OSX,
        FreeBSD,
        NetBSD,
        WebAssembly,
    }

    public enum TargetAbi
    {
        Unknown,
        /// <summary>
        /// Cross-platform console model
        /// </summary>
        CoreRT,
        /// <summary>
        /// Windows-specific UWP model
        /// </summary>
        ProjectN,
        /// <summary>
        /// Jit runtime ABI
        /// </summary>
        Jit
    }

    /// <summary>
    /// Represents various details about the compilation target that affect
    /// layout, padding, allocations, or ABI.
    /// </summary>
    public partial class TargetDetails
    {
        /// <summary>
        /// Gets the target CPU architecture.
        /// </summary>
        public TargetArchitecture Architecture
        {
            get;
        }

        /// <summary>
        /// Gets the target ABI.
        /// </summary>
        public TargetOS OperatingSystem
        {
            get;
        }

        public TargetAbi Abi
        {
            get;
        }

        /// <summary>
        /// Gets the size of a pointer for the target of the compilation.
        /// </summary>
        public int PointerSize
        {
            get
            {
                switch (Architecture)
                {
                    case TargetArchitecture.ARM64:
                    case TargetArchitecture.X64:
                        return 8;
                    case TargetArchitecture.ARM:
                    case TargetArchitecture.ARMEL:
                    case TargetArchitecture.X86:
                    case TargetArchitecture.Wasm32:
                        return 4;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        /// <summary>
        /// Gets the maximum alignment to which something can be aligned
        /// </summary>
        public static int MaximumAlignment
        {
            get
            {
                return 8;
            }
        }

        public LayoutInt LayoutPointerSize => new LayoutInt(PointerSize);

        /// <summary>
        /// Gets the default field packing size.
        /// </summary>
        public int DefaultPackingSize
        {
            get
            {
                // We use default packing size of 8 irrespective of the platform.
                return 8;
            }
        }

        /// <summary>
        /// Gets the minimum required method alignment.
        /// </summary>
        public int MinimumFunctionAlignment
        {
            get
            {
                // We use a minimum alignment of 4 irrespective of the platform.
                return 4;
            }
        }

        public TargetDetails(TargetArchitecture architecture, TargetOS targetOS, TargetAbi abi)
        {
            Architecture = architecture;
            OperatingSystem = targetOS;
            Abi = abi;
        }

        /// <summary>
        /// Retrieves the size of a well known type.
        /// </summary>
        public LayoutInt GetWellKnownTypeSize(DefType type)
        {
            switch (type.Category)
            {
                case TypeFlags.Void:
                    return new LayoutInt(PointerSize);
                case TypeFlags.Boolean:
                    return new LayoutInt(1);
                case TypeFlags.Char:
                    return new LayoutInt(2);
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                    return new LayoutInt(1);
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    return new LayoutInt(2);
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    return new LayoutInt(4);
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    return new LayoutInt(8);
                case TypeFlags.Single:
                    return new LayoutInt(4);
                case TypeFlags.Double:
                    return new LayoutInt(8);
                case TypeFlags.UIntPtr:
                case TypeFlags.IntPtr:
                    return new LayoutInt(PointerSize);
            }

            // Add new well known types if necessary

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Retrieves the alignment required by a well known type.
        /// </summary>
        public LayoutInt GetWellKnownTypeAlignment(DefType type)
        {
            // Size == Alignment for all platforms.
            return GetWellKnownTypeSize(type);
        }

        /// <summary>
        /// Given an alignment of the fields of a type, determine the alignment that is necessary for allocating the object on the GC heap
        /// </summary>
        /// <returns></returns>
        public LayoutInt GetObjectAlignment(LayoutInt fieldAlignment)
        {
            switch (Architecture)
            {
                case TargetArchitecture.ARM:
                case TargetArchitecture.ARMEL:
                case TargetArchitecture.Wasm32:
                    // ARM supports two alignments for objects on the GC heap (4 byte and 8 byte)
                    if (fieldAlignment.IsIndeterminate)
                        return LayoutInt.Indeterminate;

                    if (fieldAlignment.AsInt <= 4)
                        return new LayoutInt(4);
                    else
                        return new LayoutInt(8);
                case TargetArchitecture.X64:
                case TargetArchitecture.ARM64:
                    return new LayoutInt(8);
                case TargetArchitecture.X86:
                    return new LayoutInt(4);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns True if compiling for Windows
        /// </summary>
        public bool IsWindows
        {
            get
            {
                return OperatingSystem == TargetOS.Windows;
            }
        }

        /// <summary>
        /// Maximum number of elements in a HFA type.
        /// </summary>
        public int MaximumHfaElementCount
        {
            get
            {
                // There is a hard limit of 4 elements on an HFA type, see
                // http://blogs.msdn.com/b/vcblog/archive/2013/07/12/introducing-vector-calling-convention.aspx
                Debug.Assert(Architecture == TargetArchitecture.ARM ||
                    Architecture == TargetArchitecture.ARMEL ||
                    Architecture == TargetArchitecture.ARM64 ||
                    Architecture == TargetArchitecture.X64 ||
                    Architecture == TargetArchitecture.X86);

                return 4;
            }
        }
    }
}
