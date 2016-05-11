using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace IronRebuilder.Cecil
{
    /// <summary>
    /// A class that attempts to resolve to a dictionary, then defaults to another
    /// <see cref="IAssemblyResolver"/>
    /// </summary>
    /// <seealso cref="IAssemblyResolver" />
    internal class DictionaryAssemblyResolver : IAssemblyResolver
    {
        private readonly IReadOnlyDictionary<string, AssemblyDefinition> definitions;
        private readonly IAssemblyResolver defaultTo;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryAssemblyResolver"/> class.
        /// </summary>
        /// <param name="definitions">The definitions.</param>
        /// <param name="defaultTo">The default to.</param>
        internal DictionaryAssemblyResolver(
            IReadOnlyDictionary<string, AssemblyDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            this.definitions = definitions;
            defaultTo = new DefaultAssemblyResolver();
        }

        public AssemblyDefinition Resolve(string fullName)
        {
            return Resolve(fullName, null);
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return Resolve(name.FullName, null);
        }

        public AssemblyDefinition Resolve(string fullName, ReaderParameters parameters)
        {
            AssemblyDefinition def;

            if (parameters == null)
            {
                parameters = new ReaderParameters();
            }

            if (definitions.TryGetValue(fullName, out def))
            {
                return def;
            }

            if (parameters.AssemblyResolver == null) parameters.AssemblyResolver = this;
            var resolved = defaultTo.Resolve(fullName, parameters);
            return resolved;
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            return Resolve(name.FullName, parameters);
        }
    }
}
