using System;

namespace IronRebuilder.Attributes
{
    /// <summary>
    /// Attributing a generic parameter with this attribute will cause IronRebuilder to change the parameter to require that it extend Enum.
    /// </summary>
    /// <remarks>
    /// An example (in C#) of how this attribute works is below.  Note that although the example is in C# Iron rebuilder will work on any .net assembly.
    /// public class MyClass{[GenericEnum]T}
    /// would be recompiled to the equivalent (if it compiled) of
    /// public class MyClass{T}
    ///     where T : Enum
    /// </remarks>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.GenericParameter, Inherited = true)]
    public class GenericEnumAttribute : Attribute
    {
    }
}
