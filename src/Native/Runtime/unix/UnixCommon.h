// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Common functions and constants shared by parts of the Unix PAL
//

static const int tccSecondsToMilliSeconds = 1000;
static const int tccSecondsToMicroSeconds = 1000000;
static const int tccSecondsToNanoSeconds = 1000000000;
static const int tccMilliSecondsToMicroSeconds = 1000;
static const int tccMilliSecondsToNanoSeconds = 1000000;
static const int tccMicroSecondsToNanoSeconds = 1000;

void TimeSpecAdd(timespec* time, uint32_t milliseconds);
