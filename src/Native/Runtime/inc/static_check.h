//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// ---------------------------------------------------------------------------
// static_check.h
//
// Static assertion checking infrastructure
// ---------------------------------------------------------------------------

#ifndef __STATIC_CHECK_H__
#define __STATIC_CHECK_H__

//
// Static assert. Now uses the built-in compiler "static_assert" function.
//

#define STATIC_ASSERT_MSG( cond, msg ) static_assert( cond, #msg )
#define STATIC_ASSERT( cond ) STATIC_ASSERT_MSG(cond, static_assert_failure)

#endif // __STATIC_CHECK_H__
