using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IronRebuilder.Cecil;
using IronRebuilder.CodeReplacers;
using IronRebuilder.RewriteInfo;
using Mono.Cecil;

namespace IronRebuilder
{
    /// <summary>
    /// The core of IronRebuilder, provides the Rebuild method
    /// </summary>
    public static class Core
    {
        /// <summary>
        /// Rebuilds the assembles in <paramref name="rewrites"/> using <paramref name="replacements"/>
        /// and strong naming the assembly with the resulting assembly <paramref name="strongNameFile"/> if it's not null
        /// </summary>
        /// <param name="rewrites">A list of <see cref="IRewriteInfo"/> to rewrite</param>
        /// <param name="replacements">T he code replacements to use</param>
        /// <param name="strongNameFile">The file to use for strong naming the assembly, if null the assembly will not be strong named</param>
        /// <returns>If the recompile was successful</returns>
        public static bool Rebuild(IList<IRewriteInfo> rewrites, IList<ICodeReplacer> replacements, string strongNameFile)
        {
            var success = true;

            var assembliesByName = new Dictionary<string, AssemblyDefinition>();
            var assemblyResolver = new DictionaryAssemblyResolver(assembliesByName);

            var assemblies = new AssemblyDefinition[rewrites.Count];
            for (int i = 0; i < rewrites.Count; i++)
            {
                var rewrite = rewrites[i];
                AssemblyDefinition assembly;
                using (var assemblyStream = rewrite.MakeInputStream())
                {
                    var readParams = new ReaderParameters(ReadingMode.Deferred)
                    {
                        AssemblyResolver = assemblyResolver,
                    };

                    assembly = AssemblyDefinition.ReadAssembly(assemblyStream, readParams);
                    assemblies[i] = assembly;
                }
            }

            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var rebuilt = Recompile(assembly, replacements);
                if (rebuilt)
                {
                    assembliesByName[assembly.FullName] = assembly;
                    assemblies[i] = assembly;
                }
                else
                {
                    rewrites[i].FailureAction(true);
                    success = false;
                }
            }

            if (success)
            {
                for (int i = 0; i < rewrites.Count; i++)
                {
                    var assembly = assemblies[i];

                    if (!Validate(assembly, replacements))
                    {
                        success = false;
                        rewrites[i].FailureAction(true);
                        continue;
                    }

                    foreach (var module in assembly.Modules)
                    {
                        var refToRemove = module.AssemblyReferences.SingleOrDefault(a => a.Name == "IronRebuilderAttributes");
                        if (refToRemove != null)
                        {
                            module.AssemblyReferences.Remove(refToRemove);
                        }
                    }

                    var writeParams = new WriterParameters();
                    if (strongNameFile != null)
                    {
                        writeParams.StrongNameKeyPair = new StrongNameKeyPair(File.ReadAllBytes(strongNameFile));
                    }

                    using (var outStream = rewrites[i].MakeOutputStream())
                    {
                        assembly.Write(outStream, writeParams);
                    }
                }
            }

            if (!success)
            {
                foreach (var rewrite in rewrites)
                {
                    rewrite.FailureAction(false);
                }
            }

            return success;
        }

        private static bool Recompile(AssemblyDefinition assembly, IList<ICodeReplacer> replacements)
        {
            foreach (var replacement in replacements)
            {
                if (!replacement.Replace(assembly))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Validate(AssemblyDefinition assembly, IList<ICodeReplacer> replacements)
        {
            if (replacements == null)
            {
                throw new ArgumentNullException(nameof(replacements));
            }

            if (assembly == null)
            {
                return false;
            }

            foreach (var replacement in replacements)
            {
                if (!replacement.Validate(assembly))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
