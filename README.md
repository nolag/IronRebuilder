# IronRebuilder

Iron Rebuilder is a set of tools for .net with the goal of using attributes to reduce the amount of boiler plate code and to allow safer, less buggy code.  
Overview 

## IronRebuilder.exe

A tool that runs post-build to replace references to the IronRebuilderAttributes’s attributes with their desired effect.  See Current Attributes and Planned Attributes for more information on individual attributes.

The exe is run IronRebuilder –f “<file to rebuild>”

Or

The exe is run IronRebuilder –f “<file1 to rebuild>,<file2 to rebuild>”

A string name can be added with -s <file.snk>

Note that multiple files can be specified at once, allowing you to use the GenericEnum in one assembly that calls into another and relies on GenericEnum.  For example

If assembly 1 has
public void DoSomething<[GenericEnum] T>…
And you wish to call it from assembly 2’s method
public void DoSometingMore<[GenericEnum] T>
{
	DoSomething<T>
…
}
You will need to rebuild them together, as the complier won’t know that T is an Enum in DoSomethingMore otherwise. 

## IronRebuilderAttributes.dll

This DLL contains all the iron attributes that the IronRebuilder uses.  This assembly does not need to be shipped (and can have copy-local set to false) if the IronRebuilder is run after complication, or if the IronRebuilderService is used with all of the replacements. 

## IronRebuilderService.dll
This DLL contains the guts of the IronRebuilder.  It provides code replacers for all of the attributes in IronAttributes.dll

## Current Attributes
### GenericEnum

Allows generic parameters to extend Enum.  To work, the generic parameter must either not extend anything or must extend struct (recommended because of the note below)

Example:

Class1<[GenericEnum]TEnum> where TEnum : struct
{
	…
}

Produces a class equivalent to the code below, if it was compliable
Class1<TEnum> where TEnum : Enum
{
	…
}

Note: Since the replacements are run after the code is compiled, the compiler will allow misuse of the generic parameter within the produced artifact.  IronRebuilderService.dll will detect and report theses errors, and IronRebuilder.exe will fail upon this detection outputting the error to the console. 

## Planned Attributes

### ProtectedAndInternal

Allows internal methods to also be protected.  Unlike protected internal in C#, this would require the caller/user to be both able to access protected and internal mthods.


### NotNull

Adds a null check and a throw for null argurments

Example:
public void DoSomething([NotNull] value)
{
…
}
Becomes
public void DoSomething(value)
{
	if (value == null)
{
	throw new ArgumentNullException(“value);
}
	…
}

## FAQ

### How do I use IronRebuilder’s attributes with my project?
1) Include the IronRebuilderAttributes nuget in your project (you can change copy local to false for the dll)
2) Include the IronRebuilder nuget in your project
3) Add a post-build to call iron rebuilder in your project’s .csproj.  Note that this must run before any nuget packing or the packed nuget will not be correct
<PropertyGroup>
    <PostBuildEvent>"$(SolutionDir)\packages\IronRebuilder.1.0.0\tools\IronRebuilder.exe" -f "$(TargetPath)"</PostBuildEvent>
  </PropertyGroup>

### Do I need to ship any of the IronRebuilder components if I rebuild with IronRebuilder.exe?

No, since IronRebuilder.exe (and the IronRebuilderService.dll if all replacements are used) removes references to all of the attributes in IronRebuilderAttributes.dll, it is not necessary to ship IronRebuilderAttributes.dll with your code.  For IronRebuilder.exe and IronRebuilderService.dll they do not need to be shipped, for the same reason you don’t need to ship your compiler with your code.

### Do the attributes work with project references or just DLL references?
Both, although some of the errors, will only appear in the console and not in the errors list.

### I don’t see any compilation errors, but my build failed.  Why did my build fail?
Look in the Console tab.  Either Iron rebuilder, or Visual Studio detected an error, that Visual Studio is not used to seeing.

### Can I add my own attributes without contributing back to the main project?
Yes, using the IronAttributesService assembly you can add your own replacements

### What’s the best way to strong name my assembly?
Use the -s option with IronRebuilder.exe and specify the file

###When can I sign my assembly?
After the rebuild

### Can a project (Project1) have a project reference on an project (Project2) that uses IronRebuilder without using IronRebuilder in Project 1###
Yes, but there can be some odd behaviour.  When first building you may see the error in the next question or similar.  Attempting to build again will have the correct error in the output of the build console
### I have a warning when I have a project reference, but no errors and I can’t compile my project ###
Look in the console tab for the error.  There is one known warning, below [1] that is known to sound scary but be ok once the errors are fixed.  The error is caused by misuse of an item in the project that was rebuilt.  One example of this is if you use a [GenericEnum] and have a generic parameter that is not an enum.  

Warning IDE0006 Error encountered while loading the project. Some project features, such as full solution analysis for the failed project and projects that depend on it, have been disabled.
