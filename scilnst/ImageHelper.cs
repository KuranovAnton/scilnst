using System.IO;
using System.Windows.Media.Imaging;

namespace scilnst
{
    public static class ImageHelper
    {
        private static readonly string _imagesDirectory =
            Path.Combine(Directory.GetCurrentDirectory(), "Images");

        private static readonly string _stubPath =
            Path.Combine(_imagesDirectory, "stub.jpg");

        static ImageHelper()
        {
            EnsureImagesDirectoryExists();
        }

        private static void EnsureImagesDirectoryExists()
        {
            if (!Directory.Exists(_imagesDirectory))
            {
                Directory.CreateDirectory(_imagesDirectory);
            }
        }

        public static BitmapImage LoadImage(string photoPath)
        {
            try
            {
                var fullPath = GetFullPath(photoPath);

                if (ShouldUseStubImage(photoPath, fullPath))
                {
                    return LoadStubImage();
                }

                return CreateBitmapFromFile(fullPath);
            }
            catch
            {
                return LoadStubImage();
            }
        }

        private static bool ShouldUseStubImage(string photoPath, string fullPath)
        {
            return string.IsNullOrEmpty(photoPath) || !File.Exists(fullPath);
        }

        private static BitmapImage CreateBitmapFromFile(string filePath)
        {
            var imageData = File.ReadAllBytes(filePath);
            return CreateBitmapFromBytes(imageData);
        }

        private static BitmapImage CreateBitmapFromBytes(byte[] imageData)
        {
            var bitmap = new BitmapImage();

            using (var stream = new MemoryStream(imageData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            return bitmap;
        }

        public static BitmapImage LoadStubImage()
        {
            try
            {
                if (File.Exists(_stubPath))
                {
                    return CreateBitmapFromFile(_stubPath);
                }
            }
            catch
            {
            }

            return null;
        }

        public static string SaveImage(string sourceFilePath)
        {
            if (IsInvalidSourceFile(sourceFilePath))
            {
                return null;
            }

            try
            {
                var fileName = GenerateUniqueFileName();
                var destinationPath = GetFullPath(fileName);

                File.Copy(sourceFilePath, destinationPath, overwrite: true);

                return fileName;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsInvalidSourceFile(string sourceFilePath)
        {
            return string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath);
        }

        private static string GenerateUniqueFileName()
        {
            return $"{Guid.NewGuid():N}.jpg";
        }

        public static void DeleteImage(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return;
            }

            try
            {
                var fullPath = GetFullPath(photoPath);

                if (CanDeleteFile(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool CanDeleteFile(string fullPath)
        {
            return File.Exists(fullPath) && !IsStubImage(fullPath);
        }

        private static bool IsStubImage(string fullPath)
        {
            return fullPath == _stubPath;
        }

        private static string GetFullPath(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return _stubPath;
            }

            return Path.Combine(_imagesDirectory, photoPath);
        }
    }
}