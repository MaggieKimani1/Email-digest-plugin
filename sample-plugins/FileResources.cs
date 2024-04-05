using Humanizer.Localisation;
using System.Reflection;

namespace sample_plugins
{
    internal static class FileResources
    {
        /// <summary>
        /// Get the file stream.
        /// </summary>
        /// <param name="fileName">The file name with relative path.</param>
        /// <returns>The file stream.</returns>
        public static string GetStream(string fileName)
        {
            var content = File.ReadAllText(fileName);
            var assembly = typeof(FileResources).GetTypeInfo().Assembly;

            var stream = assembly.GetManifestResourceStream(fileName);

            if (stream == null)
            {
                throw new FileNotFoundException($"The embedded resource '{fileName}' was not found.", fileName);
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
