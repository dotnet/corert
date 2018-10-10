// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly InterfaceImplementation
{
}

.assembly extern System.Runtime
{
}

.assembly extern InterfaceDefinition
{
}

.class public auto ansi beforefieldinit Class
	extends [System.Runtime]System.Object
	implements [InterfaceDefinition]Interface
{	
	.method public final hidebysig newslot 
		instance void M1 () cil managed 
	{		
		.maxstack 8

		IL_0000: newobj instance void [System.Runtime]System.NotImplementedException::.ctor()
		IL_0005: throw
	}

	.method public final hidebysig newslot virtual 
		instance void M3 () cil managed 
	{		
		.maxstack 8

		IL_0000: newobj instance void [System.Runtime]System.NotImplementedException::.ctor()
		IL_0005: throw
	}

	.method public final hidebysig newslot virtual 
		instance void M4 (
			int32 i
		) cil managed 
	{		
		.maxstack 8

		IL_0000: newobj instance void [System.Runtime]System.NotImplementedException::.ctor()
		IL_0005: throw
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

