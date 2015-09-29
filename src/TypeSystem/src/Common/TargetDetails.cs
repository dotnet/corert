// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public enum TargetArchitecture
    {
        Unknown,
        X64,
    }

    public class TargetDetails
    {
        public TargetArchitecture Architecture
        {
            get; private set;
        }

        public int PointerSize
        {
            get
            {
                switch (Architecture)
                {
                    case TargetArchitecture.X64:
                        return 8;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public int DefaultPackingSize
        {
            get
            {
                // We use default packing size of 8 irrespective of the platform.
                return 8;
            }
        }

        public TargetDetails(TargetArchitecture architecture)
        {
            Architecture = architecture;
        }

        public int GetWellKnownTypeSize(MetadataType type)
        {
            switch (type.Category)
            {
                case TypeFlags.Void:
                    return PointerSize;
                case TypeFlags.Boolean:
                    return 1;
                case TypeFlags.Char:
                    return 2;
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                    return 1;
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    return 2;
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    return 4;
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    return 8;
                case TypeFlags.Single:
                    return 4;
                case TypeFlags.Double:
                    return 8;
                case TypeFlags.UIntPtr:
                case TypeFlags.IntPtr:
                    return PointerSize;
            }
            
            // Add new well known types if necessary

            throw new InvalidOperationException();
        }

        public int GetWellKnownTypeAlignment(MetadataType type)
        {
            // Size == Alignment for all platforms.
            return GetWellKnownTypeSize(type);
        }
    }
}
