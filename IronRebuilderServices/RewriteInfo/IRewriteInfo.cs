using System.IO;

namespace IronRebuilder.RewriteInfo
{
    /// <summary>
    /// The interface is used to provide an input and output stream for rebuilding assemblies
    /// </summary>
    public interface IRewriteInfo
    {
        /// <summary>
        /// An action to take when a rebuild was not successful.
        /// <paramref name="wasThis"/> indicates if this rewrite caused the rebuild issue
        /// </summary>
        /// <param name="wasThis">If this rewrite caused the rebuild issue</param>
        void FailureAction(bool wasThis);

        /// <summary>
        /// Makes an input stream that reads the original assembly.
        /// </summary>
        /// <returns>An input stream that reads the original assembly.</returns>
        Stream MakeInputStream();

        /// <summary>
        /// Makes an output stream that the resulting assembly is written to.
        /// </summary>
        /// <returns>An output stream that the resulting assembly is written to.</returns>
        Stream MakeOutputStream();
    }
}
