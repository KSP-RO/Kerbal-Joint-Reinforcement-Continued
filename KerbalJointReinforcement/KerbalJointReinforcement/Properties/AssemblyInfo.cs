﻿#define CIBUILD_disabled
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("KerbalJointReinforcement")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("ferram4, KSP-RO")]
[assembly: AssemblyProduct("KerbalJointReinforcement")]
[assembly: AssemblyCopyright("Copyright © ferram4, KSP-RO 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("f66c6126-fa48-4bb8-9eaf-71e817cc2aa0")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
#if CIBUILD
[assembly: AssemblyVersion("@MAJOR@.@MINOR@.@PATCH@.@BUILD@")]
[assembly: AssemblyFileVersion("@MAJOR@.@MINOR@.@PATCH@.@BUILD@")]
[assembly: KSPAssembly("KerbalJointReinforcement", @MAJOR@, @MINOR@, @PATCH@)]
#else
[assembly: AssemblyVersion("3.99.0.0")]
[assembly: AssemblyFileVersion("3.99.0.0")]
[assembly: KSPAssembly("KerbalJointReinforcement", 3, 99, 0)]
#endif
