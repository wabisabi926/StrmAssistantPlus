using System;
using System.IO;

namespace StrmAssistant.Common
{
    internal static class AtomicFileReplacer
    {
        public static void Replace(string sourcePath, string destinationPath)
        {
            Replace(sourcePath, destinationPath, (source, destination) =>
                File.Replace(source, destination, null));
        }

        public static void Replace(string sourcePath, string destinationPath, Action<string, string> replaceFile)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Source path is required.", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Destination path is required.", nameof(destinationPath));
            if (replaceFile == null) throw new ArgumentNullException(nameof(replaceFile));
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Replacement source does not exist.", sourcePath);

            if (File.Exists(destinationPath))
            {
                replaceFile(sourcePath, destinationPath);
                return;
            }

            File.Move(sourcePath, destinationPath);
        }
    }
}
