namespace Siler
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Enter the path (file or directory):");
            string path = Console.ReadLine();
            int passes = 3;

            if (File.Exists(path))
            {
                SecureDelete(path, passes);
            }
            else if (Directory.Exists(path))
            {
                var filePaths = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (string filePath in filePaths)
                {
                    SecureDelete(filePath, passes);
                }
            }
            else
            {
                Console.WriteLine("Path does not exist.");
            }
        }

        /// <summary>
        /// Securely deletes a file or all files in a directory with multiple passes.
        /// </summary>
        /// <param name="filePath">Path of the file or directory to be securely deleted.</param>
        /// <param name="passes">Number of overwrite passes to perform.</param>
        private static void SecureDelete(string filePath, int passes)
        {
            try
            {
                string currentPath = filePath;

                for (int i = 0; i < passes; i++)
                {
                    currentPath = OverwriteFile(currentPath);
                }

                File.Delete(currentPath);
                Console.WriteLine($"File securely deleted: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during secure deletion: {ex.Message}");
            }
        }

        /// <summary>
        /// Overwrites a file with various patterns, resizes it, renames it, and returns the new file path.
        /// </summary>
        /// <param name="filePath">Path of the file to be overwritten.</param>
        /// <returns>New path of the overwritten and renamed file.</returns>
        private static string OverwriteFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                long length = fileInfo.Length;

                int bufferSize = CalculateBufferSize(length);

                using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[][] patterns = GeneratePatterns(bufferSize);

                    foreach (var pattern in patterns)
                    {
                        for (long i = 0; i < length; i += pattern.Length)
                        {
                            stream.Write(pattern, 0, pattern.Length);
                            stream.Flush();
                        }
                    }
                }

                File.WriteAllBytes(filePath, new byte[128]);

                string newFilePath = Path.Combine(fileInfo.DirectoryName, Path.GetRandomFileName());
                File.Move(filePath, newFilePath);

                return newFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during file overwriting: {ex.Message}");
                return filePath;
            }
        }

        /// <summary>
        /// Calculates an appropriate buffer size based on the file length.
        /// </summary>
        /// <param name="length">Length of the file.</param>
        /// <returns>Calculated buffer size.</returns>
        private static int CalculateBufferSize(long length)
        {
            const int minBufferSize = 1024;
            const int maxBufferSize = 16 * 1024; ;
            return (int)Math.Min(Math.Max(length / 100, minBufferSize), maxBufferSize);
        }

        /// <summary>
        /// Generates an array of byte patterns to be used for overwriting files.
        /// </summary>
        /// <param name="bufferSize">Size of each byte pattern buffer.</param>
        /// <returns>An array of byte patterns.</returns>
        private static byte[][] GeneratePatterns(int bufferSize)
        {
            return new byte[][]
            {
                new byte[bufferSize],
                Enumerable.Repeat((byte)0xAA, bufferSize).ToArray(),
                Enumerable.Repeat((byte)0x55, bufferSize).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)(i % 256)).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)('~')).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)(i % 2 == 0 ? 0x00 : 0xFF)).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)(i)).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)(bufferSize - i % 256)).ToArray(),
                Enumerable.Range(0, bufferSize).Select(i => (byte)(i % 2 == 0 ? 'A' : 'Z')).ToArray(),
                Enumerable.Repeat((byte)'*', bufferSize).ToArray(),
                new byte[bufferSize]
            };
        }
    }
}