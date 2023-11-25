using System.Security.Cryptography;

namespace Siler
{
    internal class Program
    {
        /// <summary>
        /// The maximum number of concurrent tasks allowed when performing secure deletion operations.
        /// This limits the number of files being securely deleted at the same time.
        /// </summary>
        private static readonly int MaxConcurrentTasks = 10;

        /// <summary>
        /// The number of passes to use when securely deleting a file.
        /// Each pass involves overwriting the file with random data to ensure its contents are irrecoverable.
        /// </summary>
        private static readonly int SecureDeletePasses = 3;

        /// <summary>
        /// The number of passes to perform when overwriting a file during the secure deletion process.
        /// Each pass writes a new pattern of random data over the file's contents.
        /// </summary>
        private static readonly int OverwriteFilePasses = 3;

        /// <summary>
        /// The main entry point for the application.
        /// It determines if the provided path is a file or a directory and performs secure deletion accordingly.
        /// </summary>
        /// <param name="args">Command line arguments where the first argument is expected to be the path to be securely deleted.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private static async Task Main(string[] args)
        {
            string path = args.Length > 0 ? args[0] : PromptForPath();

            if (File.Exists(path))
            {
                await SecureDeleteAsync(path);
            }
            else if (Directory.Exists(path))
            {
                await SecureDeleteDirectoryAsync(path);
            }
            else
            {
                Console.WriteLine("Path does not exist.");
            }
        }

        /// <summary>
        /// Asynchronously performs a secure deletion of a specified directory and all its contents.
        /// This method first securely deletes all files within the directory and its subdirectories.
        /// It then securely deletes each directory by renaming it to a random name before deletion,
        /// to reduce the likelihood of recovering the original directory names.
        /// </summary>
        /// <param name="directoryPath">The path of the directory to be securely deleted.</param>
        /// <returns>A Task representing the asynchronous operation of securely deleting the directory and its contents.</returns>
        private static async Task SecureDeleteDirectoryAsync(string directoryPath)
        {
            var filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            using (var semaphore = new SemaphoreSlim(MaxConcurrentTasks))
            {
                var tasks = new List<Task>();

                foreach (var filePath in filePaths)
                {
                    await semaphore.WaitAsync();

                    var task = SecureDeleteAsync(filePath).ContinueWith(t => semaphore.Release());
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }

            var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);

            foreach (var directory in directories)
            {
                var directoryInfo = new DirectoryInfo(directory);
                string newDirectoryPath;
                do
                {
                    newDirectoryPath = Path.Combine(directoryInfo.Parent.FullName, Path.GetRandomFileName());
                } while (Directory.Exists(newDirectoryPath));

                Directory.Move(directory, newDirectoryPath);
                Directory.Delete(newDirectoryPath, false);
                Console.WriteLine($"Directory securely deleted: {newDirectoryPath}");
            }

            Directory.Delete(directoryPath, false);
            Console.WriteLine($"Directory securely deleted: {directoryPath}");
        }

        /// <summary>
        /// Prompts the user to enter a path for a file or directory.
        /// </summary>
        /// <returns>The path entered by the user.</returns>
        private static string PromptForPath()
        {
            Console.WriteLine("Enter the path (file or directory):");
            return Console.ReadLine();
        }

        /// <summary>
        /// Asynchronously performs a secure deletion of the specified file.
        /// This involves overwriting the file multiple times before deleting it.
        /// </summary>
        /// <param name="filePath">The path of the file to be securely deleted.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private static async Task SecureDeleteAsync(string filePath)
        {
            try
            {
                string currentPath = filePath;

                for (int i = 0; i < SecureDeletePasses; i++)
                {
                    currentPath = await OverwriteFileAsync(currentPath);
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
        /// Asynchronously overwrites a file with random data multiple times to securely delete it.
        /// After overwriting, the file is resized, renamed, and the new path is returned.
        /// </summary>
        /// <param name="filePath">The path of the file to be overwritten.</param>
        /// <returns>A Task resulting in the new file path after secure overwriting.</returns>
        private static async Task<string> OverwriteFileAsync(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                long length = fileInfo.Length;

                int bufferSize = CalculateBufferSize(length);

                using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    for (int pass = 0; pass < OverwriteFilePasses; pass++)
                    {
                        byte[] randomPattern = GenerateRandomPattern(bufferSize);
                        for (long i = 0; i < length; i += bufferSize)
                        {
                            int bytesToWrite = (int)Math.Min(bufferSize, length - i);
                            await stream.WriteAsync(randomPattern, 0, bytesToWrite);
                            await stream.FlushAsync();
                        }
                        stream.Seek(0, SeekOrigin.Begin);
                    }
                }

                using (var rng = new RNGCryptoServiceProvider())
                {
                    byte[] sizeBytes = new byte[4];
                    rng.GetBytes(sizeBytes);
                    int randomSize = BitConverter.ToInt32(sizeBytes, 0) & 0x3FF;

                    byte[] randomBytes = new byte[randomSize];
                    rng.GetBytes(randomBytes);
                    await File.WriteAllBytesAsync(filePath, randomBytes);
                }

                string newFilePath;
                do
                {
                    newFilePath = Path.Combine(fileInfo.DirectoryName, Path.GetRandomFileName());
                } while (File.Exists(newFilePath));

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
        /// Calculates the buffer size for overwriting based on the file length.
        /// Ensures that the buffer size is within the specified minimum and maximum limits.
        /// </summary>
        /// <param name="length">The length of the file.</param>
        /// <returns>The calculated buffer size.</returns>
        private static int CalculateBufferSize(long length)
        {
            const int minBufferSize = 1024 * 1024;
            const int maxBufferSize = 16 * 1024 * 1024;

            if (length <= minBufferSize)
            {
                return (int)length;
            }
            else
            {
                long dynamicBufferSize = length / 100;
                return (int)Math.Min(Math.Max(dynamicBufferSize, minBufferSize), maxBufferSize);
            }
        }

        /// <summary>
        /// Generates a random byte pattern of the specified size using a cryptographic random number generator.
        /// This pattern is used for securely overwriting files.
        /// </summary>
        /// <param name="bufferSize">The size of the byte pattern to generate.</param>
        /// <returns>A byte array containing the random pattern.</returns>
        private static byte[] GenerateRandomPattern(int bufferSize)
        {
            var randomBytes = new byte[bufferSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}