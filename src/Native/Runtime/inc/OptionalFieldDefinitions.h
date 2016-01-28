// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file is designed to be included multiple times with different definitions of the
// DEFINE_INLINE_OPTIONAL_FIELD and DEFINE_OUTLINE_OPTIONAL_FIELD macros in order to build data structures
// related to each type of EEType optional field we support (see OptionalFields.h for details).
//

// The order of definition of the fields is somewhat important: for types that require multiple optional
// fields the fields are laid out in the order of definition. Thus access to the fields defined first will be
// slightly faster than the later fields.

#ifndef DEFINE_INLINE_OPTIONAL_FIELD
#error Must define DEFINE_INLINE_OPTIONAL_FIELD before including this file
#endif

#ifndef DEFINE_OUTLINE_OPTIONAL_FIELD
#error Must define DEFINE_OUTLINE_OPTIONAL_FIELD before including this file
#endif

//                               Field name                Field type
DEFINE_INLINE_OPTIONAL_FIELD    (RareFlags,                UInt32)
DEFINE_INLINE_OPTIONAL_FIELD    (ICastableIsInstSlot,      UInt16)
DEFINE_INLINE_OPTIONAL_FIELD    (DispatchMap,              UInt32)
DEFINE_INLINE_OPTIONAL_FIELD    (ValueTypeFieldPadding,    UInt32)
DEFINE_INLINE_OPTIONAL_FIELD    (ICastableGetImplTypeSlot, UInt16)
DEFINE_INLINE_OPTIONAL_FIELD    (NullableValueOffset,      UInt8)

#undef DEFINE_INLINE_OPTIONAL_FIELD
#undef DEFINE_OUTLINE_OPTIONAL_FIELD
