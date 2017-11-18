// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly DelegateTests
{
}

.class private auto ansi beforefieldinit C
       extends [System.Runtime]System.Object
{
    // assignment from Func<int, string> to Func<int, object> is valid
    .method private hidebysig instance class [System.Runtime]System.Func`2<int32,object>
          DelegateAssignmentReturn_Valid(class [System.Runtime]System.Func`2<int32,string> input) cil managed
    {
        ldarg.1
        ret
    }

    // assignment from Func<object, int> to Func<string, int> is valid
    .method private hidebysig instance class [System.Runtime]System.Func`2<string,int32>
          DelegateAssignmentParameter_Valid(class [System.Runtime]System.Func`2<object,int32> input) cil managed
    {
        ldarg.1
        ret
    }

    // assignment from Func<string, int> to Func<object, int> is invalid
    .method private hidebysig instance class [System.Runtime]System.Func`2<object,int32>
          DelegateAssignmentParameter_Invalid_StackUnexpected(class [System.Runtime]System.Func`2<string,int32> input) cil managed
    {
        ldarg.1
        ret
    }

    // assignment from Func<int, object> to Func<int, string> is invalid
    .method private hidebysig instance class [System.Runtime]System.Func`2<int32,string>
          DelegateAssignment_Invalid_StackUnexpected(class [System.Runtime]System.Func`2<int32,object> input) cil managed
    {
        ldarg.1
        ret
    }

    .method public hidebysig specialname rtspecialname
          instance void  .ctor() cil managed
    {
        ldarg.0
        call       instance void [System.Runtime]System.Object::.ctor()
        ret
    }
}
