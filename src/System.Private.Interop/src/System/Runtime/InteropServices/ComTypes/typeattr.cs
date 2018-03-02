// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices.ComTypes
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TYPEATTR
    {
        // Constant used with the memid fields.
        public const int MEMBER_ID_NIL = unchecked((int)0xFFFFFFFF);

        // Actual fields of the TypeAttr struct.
        public Guid guid;
        public Int32 lcid;
        public Int32 dwReserved;
        public Int32 memidConstructor;
        public Int32 memidDestructor;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible",  Justification="Backwards compatibility")]
        public IntPtr lpstrSchema;
        public Int32 cbSizeInstance;
        public TYPEKIND typekind;
        public Int16 cFuncs;
        public Int16 cVars;
        public Int16 cImplTypes;
        public Int16 cbSizeVft;
        public Int16 cbAlignment;
        public TYPEFLAGS wTypeFlags;
        public Int16 wMajorVerNum;
        public Int16 wMinorVerNum;
        public TYPEDESC tdescAlias;
        public IDLDESC idldescType;
    }
}
