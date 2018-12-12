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

.class public auto ansi beforefieldinit Valid_ValidType_Valid
	extends [System.Runtime]System.Object
	implements [InterfaceDefinition]Interface
{
	.method public final hidebysig newslot virtual 
		instance void M1 () cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance void M2 () cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance void M3 (
			int32 i
		) cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance int32 M4 (
			int32 i
		) cil managed 
	{
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: ret
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

.class public auto ansi beforefieldinit InterfaceImplHasDuplicate_InvalidType_InterfaceImplHasDuplicate
	extends [System.Runtime]System.Object
	implements [InterfaceDefinition]Interface, [InterfaceDefinition]Interface
{	
	.method public final hidebysig newslot virtual 
		instance void M1 () cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance void M2 () cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance void M3 (
			int32 i
		) cil managed 
	{
		.maxstack 8

		IL_0000: ret
	}

	.method public final hidebysig newslot virtual 
		instance int32 M4 (
			int32 i
		) cil managed 
	{
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: ret
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

.class public auto ansi beforefieldinit MissingMethod_InvalidType_InterfaceMethodNotImplemented
	extends [System.Runtime]System.Object
	implements [InterfaceDefinition]Interface
{
	.method public final hidebysig newslot virtual 
		instance void M2 () cil managed 
	{
		.maxstack 8
 		IL_0000: ret
	}
 	.method public final hidebysig newslot virtual 
		instance void M3 (
			int32 i
		) cil managed 
	{
		.maxstack 8
 		IL_0000: ret
	}
 	.method public final hidebysig newslot virtual 
		instance int32 M4 (
			int32 i
		) cil managed 
	{
		.maxstack 8
 		IL_0000: ldc.i4.0
		IL_0001: ret
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

.class public auto ansi abstract beforefieldinit MissingMethod_ValidType_AbstractClass
	extends [System.Runtime]System.Object
	implements [InterfaceDefinition]Interface
{
	.method public final hidebysig newslot virtual 
		instance void M2 () cil managed 
	{
		.maxstack 8
 		IL_0000: ret
	}
 	.method public final hidebysig newslot virtual 
		instance void M3 (
			int32 i
		) cil managed 
	{
		.maxstack 8
 		IL_0000: ret
	}
 	.method public final hidebysig newslot virtual 
		instance int32 M4 (
			int32 i
		) cil managed 
	{
		.maxstack 8
 		IL_0000: ldc.i4.0
		IL_0001: ret
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
