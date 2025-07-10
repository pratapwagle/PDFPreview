using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Diagnostics;

namespace PDFPreviewUWP
{
    /// <summary>
    /// Helper class for handling file access in UWP applications
    /// Demonstrates proper file access patterns that avoid "Access Denied" errors
    /// </summary>
    public static class UWPFileAccessHelper
    {
        /// <summary>
        /// Safely pick a PDF file using file picker
        /// This is the recommended approach for user-selected files
        /// </summary>
        public static async Task<StorageFile> PickPDFFileAsync()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".pdf");

            return await picker.PickSingleFileAsync();
        }

        /// <summary>
        /// Check if a file path is accessible to UWP apps
        /// </summary>
        public static bool IsPathAccessible(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // App package files (ms-appx://)
            if (filePath.StartsWith("ms-appx://"))
                return true;

            // App data locations
            var appData = ApplicationData.Current;
            if (filePath.StartsWith(appData.LocalFolder.Path) ||
                filePath.StartsWith(appData.TemporaryFolder.Path) ||
                filePath.StartsWith(appData.RoamingFolder.Path))
                return true;

            // Generally, other paths are not accessible without file picker
            return false;
        }

        /// <summary>
        /// Get a StorageFile from an accessible path
        /// </summary>
        public static async Task<StorageFile> GetStorageFileFromAccessiblePathAsync(string filePath)
        {
            if (!IsPathAccessible(filePath))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{filePath}' is not accessible to UWP apps. " +
                    "Use file picker for user files or ensure the file is in app data folders.");
            }

            try
            {
                if (filePath.StartsWith("ms-appx://"))
                {
                    var uri = new Uri(filePath);
                    return await StorageFile.GetFileFromApplicationUriAsync(uri);
                }
                else
                {
                    return await StorageFile.GetFileFromPathAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing file: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Copy a file to app local data folder
        /// </summary>
        public static async Task<StorageFile> CopyToLocalDataAsync(StorageFile sourceFile, string newFileName = null)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var fileName = newFileName ?? $"copy_{DateTime.Now:yyyyMMdd_HHmmss}_{sourceFile.Name}";
            
            return await sourceFile.CopyAsync(localFolder, fileName, NameCollisionOption.ReplaceExisting);
        }

        /// <summary>
        /// Generate ms-appdata URL for a file in local data folder
        /// </summary>
        public static string GetAppDataUrl(string fileName)
        {
            return $"ms-appdata:///local/{fileName}";
        }
    }

    /// <summary>
    /// Examples of UWP file access patterns
    /// </summary>
    public static class UWPFileAccessExamples
    {
        /// <summary>
        /// Example: Correct way to load user-selected PDF
        /// </summary>
        public static async Task<string> LoadUserSelectedPDFExample()
        {
            try
            {
                // ✅ Use file picker for user files
                var file = await UWPFileAccessHelper.PickPDFFileAsync();
                if (file == null) return null;

                // ✅ Copy to app data for web access
                var copiedFile = await UWPFileAccessHelper.CopyToLocalDataAsync(file);
                
                // ✅ Generate ms-appdata URL
                return UWPFileAccessHelper.GetAppDataUrl(copiedFile.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in user PDF example: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Example: Loading PDF from app package
        /// </summary>
        public static async Task<string> LoadBundledPDFExample(string pdfFileName)
        {
            try
            {
                // ✅ App package files are accessible
                var uri = new Uri($"ms-appx:///Assets/{pdfFileName}");
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                
                // Copy to local data for web access
                var copiedFile = await UWPFileAccessHelper.CopyToLocalDataAsync(file);
                
                return UWPFileAccessHelper.GetAppDataUrl(copiedFile.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in bundled PDF example: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Example: What NOT to do
        /// </summary>
        public static async Task<string> WrongWayExample()
        {
            try
            {
                // ❌ This will throw "Access Denied" for most paths
                var file = await StorageFile.GetFileFromPathAsync(@"C:\Users\Documents\MyFile.pdf");
                return file.Path;
            }
            catch (UnauthorizedAccessException)
            {
                // This is expected - UWP apps can't access arbitrary file system paths
                Debug.WriteLine("Access denied - this is expected behavior");
                throw;
            }
        }
    }
}
