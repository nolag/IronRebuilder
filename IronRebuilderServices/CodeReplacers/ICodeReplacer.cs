using Mono.Cecil;

namespace IronRebuilder.CodeReplacers
{
    /// <summary>
    /// An interface for classes that replace attributes in an assembly
    /// </summary>
    public interface ICodeReplacer
    {
        /// <summary>
        /// Replaces an attribute, returning true for success and false otherwise
        /// </summary>
        /// <param name="assembly">The assembly</param>
        /// <returns>/// Returns if the replacement was successful</returns>
        bool Replace(AssemblyDefinition assembly);

        /// <summary>
        /// Validate an assembly after all replacements have been done.
        /// </summary>
        /// <param name="assembly">The assembly</param>
        /// <returns>/// Returns if the replacement was successful</returns>
        bool Validate(AssemblyDefinition assembly);
    }
}