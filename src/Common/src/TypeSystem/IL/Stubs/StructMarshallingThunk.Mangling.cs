// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace Internal.IL.Stubs
{
    public partial class StructMarshallingThunk : IPrefixMangledType
    {
        TypeDesc IPrefixMangledType.BaseType
        {
            get
            {
                return ManagedType;
            }
        }

        string IPrefixMangledType.Prefix
        {
            get
            {
                switch (ThunkType)
                {
                    case StructMarshallingThunkType.ManagedToNative:
                        return "ManagedToNative";
                    case StructMarshallingThunkType.NativeToManage:
                        return "NativeToManaged";
                    case StructMarshallingThunkType.Cleanup:
                        return "Cleanup";
                    default:
                        System.Diagnostics.Debug.Assert(false, "Unexpected Struct marshalling thunk type");
                        return string.Empty;
                }
            }
        }
    }
}
