using System.IO;

namespace IronRebuilder.RewriteInfo
{
    /// <summary>
    /// Provide an input and output stream for rebuilding assemblies in a file
    /// </summary>
    /// <remarks>
    /// This class will overwrite the assembly that was originally created and will delete it on errors.
    /// </remarks>
    public class FileRewriteInfo : IRewriteInfo
    {
        private readonly bool deleteOnOtherFailures;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileRewriteInfo"/> class.
        /// </summary>
        /// <param name="path">The path of the assembly.</param>
        /// <param name="deleteOnOtherFailures">if set to <c>true</c> <paramref name="path"/> is deleted when other rewrites fail</param>
        public FileRewriteInfo(string path, bool deleteOnOtherFailures)
        {
            Path = path;
            this.deleteOnOtherFailures = deleteOnOtherFailures;
        }

        /// <summary>
        /// Gets the path that input and output streams are made for.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// An action to take when a rebuild was not successful.
        /// <paramref name="wasThis" /> indicates if this rewrite caused the rebuild issue
        /// </summary>
        /// <param name="wasThis">If this rewrite caused the rebuild issue</param>
        public void FailureAction(bool wasThis)
        {
            if (File.Exists(Path) && (wasThis || deleteOnOtherFailures)) File.Delete(Path);
        }

        /// <summary>
        /// Makes an input stream that reads the original assembly.
        /// </summary>
        /// <returns>
        /// An input stream that reads the original assembly.
        /// </returns>
        public Stream MakeInputStream() => new FileStream(Path, FileMode.Open);

        /// <summary>
        /// Makes an output stream that the resulting assembly is written to.
        /// </summary>
        /// <returns>
        /// An output stream that the resulting assembly is written to.
        /// </returns>
        public Stream MakeOutputStream() => new FileStream(Path, FileMode.Create);
    }
}
