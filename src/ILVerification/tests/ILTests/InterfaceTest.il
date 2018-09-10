// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.class interface public auto ansi abstract I
{
	// Methods
	.method public hidebysig newslot abstract virtual 
		instance void M () cil managed 
	{
	} // end of method I::M

} // end of class I


.class public auto ansi beforefieldinit C
	extends [System.Runtime]System.Object
	implements I
{

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x2057
		// Code size 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [System.Runtime]System.Object::.ctor()
		IL_0006: ret
	} // end of method C::.ctor

} // end of class C
