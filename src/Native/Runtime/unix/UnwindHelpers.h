// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "ICodeManager.h"

// This class is used to encapsulate the internals of our unwinding implementation
// and any custom versions of libunwind structures that we use for performance 
// reasons.
class UnwindHelpers
{
public:
    static bool StepFrame(MethodInfo* pMethodInfo, REGDISPLAY *regs);

#ifdef TARGET_ARM64
    static bool StepFrame(REGDISPLAY* regs, PTR_VOID unwindInfo);
    static bool StepFrameCompact(REGDISPLAY* regs, PTR_VOID unwindInfo);
#endif
};
