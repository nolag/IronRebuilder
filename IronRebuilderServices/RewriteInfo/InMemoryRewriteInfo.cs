using System;
using System.IO;

namespace IronRebuilder.RewriteInfo
{
    /// <summary>
    /// Provide an input and output stream for rebuilding assemblies in memory
    /// </summary>
    public class InMemoryRewriteInfo : IRewriteInfo
    {
        private readonly byte[] orignalBytes;
        private readonly bool deleteOnOtherFailures;

        private MemoryStream latestRewrite;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryRewriteInfo"/> class.
        /// </summary>
        /// <param name="orignalBytes">The bytes that will populate the stream when calling MakeInputStream</param>
        /// <param name="deleteOnOtherFailures">If the memory output stream should be cleared if another rewriteInfo was the cause of an error</param>
        public InMemoryRewriteInfo(byte[] orignalBytes, bool deleteOnOtherFailures = true)
        {
            this.orignalBytes = new byte[orignalBytes.Length];
            this.deleteOnOtherFailures = deleteOnOtherFailures;
            Array.Copy(orignalBytes, this.orignalBytes, orignalBytes.Length);
        }

        /// <summary>
        /// An action to take when a rebuild was not successful.
        /// <paramref name="wasThis" /> indicates if this rewrite caused the rebuild issue
        /// </summary>
        /// <param name="wasThis">If this rewrite caused the rebuild issue</param>
        public void FailureAction(bool wasThis)
        {
            if (wasThis || deleteOnOtherFailures)
            {
                latestRewrite = null;
            }
        }

        /// <summary>
        /// Makes an input stream that reads the original assembly.
        /// </summary>
        /// <returns>
        /// An input stream that reads the original assembly.
        /// </returns>
        public Stream MakeInputStream() => new MemoryStream(orignalBytes, false);

        /// <summary>
        /// Makes an output stream that the resulting assembly is written to.
        /// </summary>
        /// <returns>
        /// An output stream that the resulting assembly is written to.
        /// </returns>
        public Stream MakeOutputStream() => latestRewrite = new MemoryStream();

        /// <summary>
        /// Gets the bytes that have been written to the last stream returned by MakeOutputStream
        /// </summary>
        /// <returns>The bytes that have been written to the last stream returned by MakeOutputStream</returns>
        public byte[] GetWrittenValue()
        {
            if (latestRewrite == null)
            {
                return new byte[0];
            }

            return latestRewrite.ToArray();
        }
    }
}
