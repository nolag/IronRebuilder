using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using IronRebuilder.CodeReplacers;
using IronRebuilder.RewriteInfo;

namespace IronRebuilder
{
    /// <summary>
    /// The main running program
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main method
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The exit code</returns>
        public static int Main(string[] args)
        {
            var parsedArgs = Parser.Default.ParseArguments<CmdLineArgs>(args);
            if (parsedArgs.Errors.Count() != 0)
            {
                Console.WriteLine(string.Join(Environment.NewLine, parsedArgs.Errors.Select(e => e.ToString())));
                return 1;
            }

            var replacements = new List<ICodeReplacer>();
            replacements.Add(new GenericEnum(Console.WriteLine));
            var files = parsedArgs.Value.Files.Split(';');

            var rewrites = files.Select(f => new FileRewriteInfo(f.Trim(), true)).ToArray();
            return Core.Rebuild(rewrites, replacements, parsedArgs.Value.StrongName) ? 0 : 1;
        }

        private class CmdLineArgs
        {
            [Option('f', "files", Required = true, HelpText = "Input files to be rebuilt, semicolon seperated.")]
            public string Files { get; set; }

            [Option('s', "strongName", Required = false, HelpText = "The file used to sign the assembly/assemblies")]
            public string StrongName { get; set; }
        }
    }
}