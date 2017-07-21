// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly ExceptionRegion
{
}

.class public sequential ansi sealed beforefieldinit ExceptionRegionTests
       extends [System.Runtime]System.Object
{
    .method public instance void ExceptionRegion.NestedTryFinally_Valid() cil managed
    {
        .try  
        { 
            nop
            leave EndTryCatch
        } 
        finally
        {
            .try 
            { 
                nop
                leave EndFinally
            } 
            catch [System.Runtime]System.Exception
            {
                nop
                leave EndFinally
            }
EndFinally:
            endfinally
        }
EndTryCatch:
        ret
    }
}

