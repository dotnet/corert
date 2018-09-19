// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly InterfaceDefinition
{
}

.assembly extern System.Runtime
{
}

.class interface public auto ansi abstract Interface
{	
	.method public hidebysig newslot abstract virtual 
		instance void M1 () cil managed 
	{
	}

	.method public hidebysig newslot abstract virtual 
		instance void M2 () cil managed 
	{
	}

}

.class public auto ansi beforefieldinit Class
	extends [System.Runtime]System.Object
	implements Interface
{	
	.method public final hidebysig newslot virtual 
		instance void M1 () cil managed 
	{		
		.maxstack 8

		IL_0000: ret
	}

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{		
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [System.Runtime]System.Object::.ctor()
		IL_0006: ret
	}
} 
