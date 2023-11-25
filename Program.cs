using System.Security.Cryptography;

namespace Siler
{
    internal class Program
    {
        private static readonly int MaxConcurrentTasks = 10;
        private static readonly int SecureDeletePasses = 3;
        private static readonly int OverwriteFilePasses = 3;

        private static async Task Main(string[] args)
        {
            string path = args.Length > 0 ? args[0] : PromptForPath();

            if (File.Exists(path))
            {
                await SecureDeleteAsync(path);
            }
            else if (Directory.Exists(path))
            {
                var filePaths = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
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
            }
            else
            {
                Console.WriteLine("Path does not exist.");
            }
        }

        private static string PromptForPath()
        {
            Console.WriteLine("Enter the path (file or directory):");
            return Console.ReadLine();
        }

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