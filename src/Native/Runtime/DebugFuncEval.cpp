// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "common.h"
#include "CommonTypes.h"
#include "DebugFuncEval.h"

GVAL_IMPL_INIT(UInt64, g_FuncEvalTarget, 0);

#ifndef DACCESS_COMPILE

void* DebugFuncEval::GetFuncEvalTarget()
{
    return (void*)g_FuncEvalTarget;
}

#endif //!DACCESS_COMPILE
