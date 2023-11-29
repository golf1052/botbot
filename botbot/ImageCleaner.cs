using System;
using System.IO;
using System.Threading.Tasks;

namespace botbot
{
    public class ImageCleaner
    {
        private const string ImagesDirectory = "../../images/";
        private readonly TimeSpan interval;

        public ImageCleaner()
        {
            interval = TimeSpan.FromHours(1);
        }

        public async Task Run()
        {
            while (true)
            {
                foreach (var file in Directory.EnumerateFiles(ImagesDirectory))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc <= DateTime.UtcNow - TimeSpan.FromDays(7))
                    {
                        fileInfo.Delete();
                    }
                }
                await Task.Delay(interval);
            }
        }
    }
}
