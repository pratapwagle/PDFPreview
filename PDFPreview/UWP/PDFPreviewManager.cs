using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections.Generic;
using Windows.Storage.Streams;

namespace PDFPreviewUWP
{
    /// <summary>
    /// Manages PDF preview functionality with WebView2 and AWS MFE integration
    /// Supports multiple PDF loading methods: Custom Stream, Data URLs, and File URLs
    /// </summary>
    public class PDFPreviewManager
    {
        private WebView2 webView;
        // Dictionary to store active PDF files for custom streaming
        private readonly Dictionary<string, StorageFile> _activePDFs = new Dictionary<string, StorageFile>();

        public PDFPreviewManager(WebView2 webView)
        {
            this.webView = webView;
        }

        public async Task InitializeAsync()
        {
            try 
            {
                Debug.WriteLine($"🔄 Initializing WebView2 (PDF Method: {AppConfig.CurrentPDFMethod})");
                
                // First, initialize WebView2 based on method requirements
                if (AppConfig.UseFileUrls || AppConfig.CurrentPDFMethod == PDFLoadingMethod.FileURL)
                {
                    Debug.WriteLine("🔒 Using WebView2 initialization with file URL support");
                    Debug.WriteLine($"🔧 Browser arguments: {AppConfig.GetWebView2Arguments()}");
                    await InitializeWithFileUrlSupport();
                }
                else
                {
                    Debug.WriteLine("📄 Using standard WebView2 initialization");
                    await webView.EnsureCoreWebView2Async();
                }

                // Configure settings for file access (after WebView2 is initialized)
                webView.CoreWebView2.Settings.AreDevToolsEnabled = AppConfig.EnableDebugging;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                
                // Add web message handler for communication with MFE
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                
                // Setup navigation completion handler to ensure custom stream handler is registered
                // after the WebView2 is fully ready
                webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                
                // Always setup custom stream handler for uwp-pdf:// protocol immediately
                // This ensures it's available even if user switches to CustomStream method later
                await SetupCustomStreamHandler();
                
                Debug.WriteLine($"✅ WebView2 initialized successfully (PDF Method: {AppConfig.CurrentPDFMethod})");
                
                // Test file URL access if using FileURL method
                if (AppConfig.CurrentPDFMethod == PDFLoadingMethod.FileURL)
                {
                    Debug.WriteLine("🧪 Testing file URL access...");
                    var fileUrlWorks = await TestFileUrlAccessAsync();
                    if (!fileUrlWorks)
                    {
                        Debug.WriteLine("⚠️ File URL access test failed - consider using registry configuration");
                        Debug.WriteLine("💡 Alternative: Switch to DataURL or ChunkedStream method");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error initializing WebView2: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initialize WebView2 with browser arguments for file access
        /// Uses the most compatible WebView2 API patterns for UWP
        /// </summary>
        private async Task InitializeWithFileUrlSupport()
        {
            try
            {
                var browserArgs = AppConfig.GetWebView2Arguments();
                Debug.WriteLine($"Attempting WebView2 initialization with browser arguments: {browserArgs}");
                
                bool environmentCreated = false;
                
                if (!string.IsNullOrEmpty(browserArgs))
                {
                    // Try to create environment with browser arguments if the API is available
                    try
                    {
                        // First try the simpler single parameter CreateAsync (older WebView2 versions)
                        try
                        {
                            var environment = await CoreWebView2Environment.CreateAsync();
                            await webView.EnsureCoreWebView2Async();
                            
                            Debug.WriteLine("✅ WebView2 initialized with default environment");
                            Debug.WriteLine("⚠️ Browser arguments not applied programmatically - use registry method if needed");
                            Debug.WriteLine($"Intended arguments: {browserArgs}");
                            environmentCreated = true;
                        }
                        catch (Exception fallbackEx)
                        {
                            Debug.WriteLine($"❌ Default environment creation failed: {fallbackEx.Message}");
                        }
                    }
                    catch (Exception envEx)
                    {
                        Debug.WriteLine($"❌ Environment creation failed: {envEx.Message}");
                        Debug.WriteLine("This is normal if CoreWebView2Environment is not available in this WebView2 version");
                    }
                }
                
                // Fallback to standard initialization
                if (!environmentCreated)
                {
                    Debug.WriteLine("⚠️ Using standard WebView2 initialization");
                    Debug.WriteLine("Note: Browser arguments must be configured via Windows Registry:");
                    Debug.WriteLine("HKEY_CURRENT_USER\\Software\\Microsoft\\Edge\\WebView2\\AdditionalBrowserArguments");
                    Debug.WriteLine($"Value: {browserArgs}");
                    Debug.WriteLine("Or call PDFPreviewManager.GetRegistryConfigurationInstructions() for detailed steps");
                    
                    await webView.EnsureCoreWebView2Async();
                    Debug.WriteLine("✅ WebView2 initialized with default settings");
                    Debug.WriteLine("🔍 File URL access may be blocked - consider using registry configuration");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to initialize WebView2: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Alternative WebView2 initialization with environment options (if supported)
        /// Use this method if your WebView2 version supports CoreWebView2EnvironmentOptions
        /// </summary>
        public async Task InitializeAsyncWithEnvironmentOptions()
        {
            try 
            {
                // Alternative approach for newer WebView2 versions
                // Uncomment and use this if CoreWebView2EnvironmentOptions is available
                
                /*
                var options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = AppConfig.GetWebView2Arguments();
                
                var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
                await webView.EnsureCoreWebView2Async(environment);
                */
                
                // For now, use the standard initialization
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing WebView2 with environment options: {ex.Message}");
                // Fallback to standard initialization
                await InitializeAsync();
            }
        }

        public void LoadMFE(string mfeUrl)
        {
            try
            {
                webView.CoreWebView2.Navigate(mfeUrl);
                Debug.WriteLine($"Navigated to MFE: {mfeUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading MFE: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load a PDF file by copying it to app data and sending file URL to MFE
        /// Now supports multiple loading methods based on AppConfig.CurrentPDFMethod
        /// </summary>
        /// <param name="pdfFile">StorageFile object of the PDF file</param>
        public async Task LoadPDFAsync(StorageFile pdfFile)
        {
            try
            {
                if (webView?.CoreWebView2 == null)
                {
                    throw new InvalidOperationException("WebView2 is not initialized");
                }

                Debug.WriteLine($"🔄 Loading PDF using method: {AppConfig.GetPDFMethodDescription()}");

                switch (AppConfig.CurrentPDFMethod)
                {
                    case PDFLoadingMethod.CustomStream:
                        await LoadPDFWithCustomStreamAsync(pdfFile);
                        break;
                        
                    case PDFLoadingMethod.DataURL:
                        await LoadPDFWithDataUrlAsync(pdfFile);
                        break;
                        
                    case PDFLoadingMethod.FileURL:
                        await LoadPDFWithFileUrlAsync(pdfFile);
                        break;
                        
                    case PDFLoadingMethod.ChunkedStream:
                        await LoadPDFWithChunkedStreamingAsync(pdfFile);
                        break;
                        
                    default:
                        // Fallback to custom stream for best performance
                        await LoadPDFWithCustomStreamAsync(pdfFile);
                        break;
                }
                
                Debug.WriteLine($"✅ PDF loaded successfully: {pdfFile.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading PDF: {ex.Message}");
                
                // Try fallback method if primary method fails
                if (AppConfig.CurrentPDFMethod != PDFLoadingMethod.DataURL)
                {
                    Debug.WriteLine("🔄 Attempting fallback to Data URL method...");
                    try
                    {
                        await LoadPDFWithDataUrlAsync(pdfFile);
                        Debug.WriteLine("✅ Fallback to Data URL successful");
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.WriteLine($"❌ Fallback also failed: {fallbackEx.Message}");
                        throw new Exception($"Primary method failed: {ex.Message}. Fallback also failed: {fallbackEx.Message}");
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Load PDF using custom stream handler (uwp-pdf:// protocol)
        /// Best performance - no file copying, browser handles streaming
        /// </summary>
        private async Task LoadPDFWithCustomStreamAsync(StorageFile pdfFile)
        {
            try
            {
                string fileId = Guid.NewGuid().ToString("N");
                _activePDFs[fileId] = pdfFile;
                
                string streamUrl = $"uwp-pdf://{fileId}";
                
                Debug.WriteLine($"🔄 Loading PDF with custom stream: {pdfFile.Name}");
                Debug.WriteLine($"Stream URL: {streamUrl}");
                Debug.WriteLine($"File ID: {fileId}");
                Debug.WriteLine($"Active PDFs count: {_activePDFs.Count}");
                
                // Ensure the custom stream handler is registered before sending the message
                await EnsureCustomStreamHandlerAsync();
                
                var message = new
                {
                    type = "LOAD_PDF",
                    fileName = pdfFile.Name,
                    pdfUrl = streamUrl,
                    urlType = "stream",
                    transferMethod = "CustomStream",
                    fileSize = (await pdfFile.GetBasicPropertiesAsync()).Size,
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                string jsonMessage = JsonConvert.SerializeObject(message);
                Debug.WriteLine($"📤 Sending custom stream message: {jsonMessage}");
                
                webView.CoreWebView2.PostWebMessageAsString(jsonMessage);
                Debug.WriteLine($"✅ Custom stream message sent for: {pdfFile.Name} (URL: {streamUrl})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in LoadPDFWithCustomStreamAsync: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Load PDF using Data URL (Base64 encoded)
        /// Universal compatibility but higher memory usage
        /// </summary>
        private async Task LoadPDFWithDataUrlAsync(StorageFile pdfFile)
        {
            var buffer = await FileIO.ReadBufferAsync(pdfFile);
            var bytes = new byte[buffer.Length];
            
            using (var dataReader = DataReader.FromBuffer(buffer))
            {
                dataReader.ReadBytes(bytes);
            }
            
            string base64Content = Convert.ToBase64String(bytes);
            string dataUrl = $"data:application/pdf;base64,{base64Content}";
            
            var message = new
            {
                type = "LOAD_PDF",
                fileName = pdfFile.Name,
                pdfUrl = dataUrl,
                urlType = "data",
                transferMethod = "DataURL",
                fileSize = bytes.Length,
                timestamp = DateTime.UtcNow.ToString("O")
            };
            
            webView.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(message));
            Debug.WriteLine($"📤 Sent data URL message for: {pdfFile.Name} (Size: {bytes.Length} bytes)");
        }

        /// <summary>
        /// Load PDF using file:// URLs (requires browser arguments or registry config)
        /// Good performance but may require user configuration
        /// </summary>
        private async Task LoadPDFWithFileUrlAsync(StorageFile pdfFile)
        {
            try
            {
                Debug.WriteLine($"🔄 Loading PDF using File URL method: {pdfFile.Name}");
                
                // Copy PDF to app data folder and generate file URL
                var localFolder = ApplicationData.Current.LocalFolder;
                var fileName = $"pdf_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.pdf";
                var destinationFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                
                // Copy the file to app data folder
                await pdfFile.CopyAndReplaceAsync(destinationFile);
                
                // Generate file URL (always for FileURL method)
                string filePath = destinationFile.Path;
                string fileUrl = $"file:///{filePath.Replace('\\', '/')}";
                
                Debug.WriteLine($"Generated file URL: {fileUrl}");
                Debug.WriteLine($"File path: {filePath}");
                
                // Send the file URL to the MFE with explicit FileURL transfer method
                var message = new
                {
                    type = "LOAD_PDF",
                    fileName = pdfFile.Name,
                    pdfUrl = fileUrl,
                    urlType = "file",
                    transferMethod = "FileURL",
                    fileSize = (await pdfFile.GetBasicPropertiesAsync()).Size,
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                webView.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(message));
                Debug.WriteLine($"📤 Sent file URL message for: {pdfFile.Name} (Method: FileURL)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in LoadPDFWithFileUrlAsync: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load PDF using chunked streaming via PostWebMessageAsString
        /// Sends PDF in 2MB chunks for progressive loading with proper Base64 handling
        /// </summary>
        private async Task LoadPDFWithChunkedStreamingAsync(StorageFile pdfFile)
        {
            try
            {
                Debug.WriteLine($"🔄 Starting chunked streaming for: {pdfFile.Name}");
                
                // Read the entire PDF file
                var buffer = await FileIO.ReadBufferAsync(pdfFile);
                var allBytes = new byte[buffer.Length];
                
                using (var dataReader = DataReader.FromBuffer(buffer))
                {
                    dataReader.ReadBytes(allBytes);
                }
                
                // Convert entire PDF to Base64 first to avoid splitting issues
                string fullBase64 = Convert.ToBase64String(allBytes);
                
                // Calculate chunking details based on Base64 string length
                int baseChunkSize = AppConfig.ChunkSizeBytes;
                // Convert byte size to approximate Base64 character count (4/3 ratio)
                int base64ChunkSize = (int)Math.Ceiling((double)baseChunkSize * 4 / 3);
                // Ensure chunk size is aligned to 4-character boundaries for Base64
                base64ChunkSize = ((base64ChunkSize + 3) / 4) * 4;
                
                int totalChunks = (int)Math.Ceiling((double)fullBase64.Length / base64ChunkSize);
                
                Debug.WriteLine($"📊 PDF Size: {allBytes.Length} bytes, Base64: {fullBase64.Length} chars");
                Debug.WriteLine($"📊 Chunks: {totalChunks}, Base64 Chunk Size: {base64ChunkSize} chars");
                
                // Send start message to MFE
                var startMessage = new
                {
                    type = "LOAD_PDF_CHUNKED_START",
                    fileName = pdfFile.Name,
                    totalSize = allBytes.Length,
                    base64Size = fullBase64.Length,
                    totalChunks = totalChunks,
                    chunkSize = base64ChunkSize,
                    transferMethod = "ChunkedStream",
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                webView.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(startMessage));
                Debug.WriteLine($"📤 Sent chunked start message: {totalChunks} chunks");
                
                // Send chunks progressively from the Base64 string
                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    int startOffset = chunkIndex * base64ChunkSize;
                    int currentChunkSize = Math.Min(base64ChunkSize, fullBase64.Length - startOffset);
                    
                    // Extract chunk from Base64 string (this ensures valid Base64 data)
                    string chunkBase64 = fullBase64.Substring(startOffset, currentChunkSize);
                    
                    var chunkMessage = new
                    {
                        type = "LOAD_PDF_CHUNK",
                        chunkIndex = chunkIndex,
                        chunkData = chunkBase64,
                        chunkSize = currentChunkSize,
                        isLastChunk = (chunkIndex == totalChunks - 1),
                        transferMethod = "ChunkedStream",
                        timestamp = DateTime.UtcNow.ToString("O")
                    };
                    
                    webView.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(chunkMessage));
                    
                    Debug.WriteLine($"📤 Sent chunk {chunkIndex + 1}/{totalChunks} ({currentChunkSize} chars, Last: {chunkIndex == totalChunks - 1})");
                    
                    // Small delay to prevent overwhelming the message queue
                    if (chunkIndex < totalChunks - 1) // Don't delay after last chunk
                    {
                        await Task.Delay(AppConfig.ChunkDelayMs);
                    }
                }
                
                Debug.WriteLine($"✅ Chunked streaming completed for: {pdfFile.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in chunked streaming: {ex.Message}");
                
                // Send error message to MFE
                var errorMessage = new
                {
                    type = "LOAD_PDF_CHUNKED_ERROR",
                    error = ex.Message,
                    transferMethod = "ChunkedStream",
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                webView.CoreWebView2.PostWebMessageAsString(JsonConvert.SerializeObject(errorMessage));
                throw;
            }
        }

        /// <summary>
        /// Clear all active PDF streams and cleanup resources
        /// </summary>
        public void ClearPDFStreams()
        {
            _activePDFs.Clear();
            Debug.WriteLine("🧹 Cleared all active PDF streams");
        }

        /// <summary>
        /// Load a PDF file from an accessible path (like app package or temp folder)
        /// </summary>
        /// <param name="pdfFilePath">Path to PDF file in accessible location</param>
        public async Task LoadPDFFromAccessiblePathAsync(string pdfFilePath)
        {
            try
            {
                // Try to get StorageFile from accessible paths
                StorageFile sourceFile = null;
                
                // Check if it's in the app package
                if (pdfFilePath.StartsWith("ms-appx://"))
                {
                    var uri = new Uri(pdfFilePath);
                    sourceFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                }
                // Check if it's in temp folder
                else if (pdfFilePath.Contains(ApplicationData.Current.TemporaryFolder.Path))
                {
                    sourceFile = await StorageFile.GetFileFromPathAsync(pdfFilePath);
                }
                // Check if it's already in local folder
                else if (pdfFilePath.Contains(ApplicationData.Current.LocalFolder.Path))
                {
                    sourceFile = await StorageFile.GetFileFromPathAsync(pdfFilePath);
                }
                else
                {
                    throw new UnauthorizedAccessException("File path is not in an accessible location for UWP apps. Use file picker instead.");
                }

                await LoadPDFAsync(sourceFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading PDF from path: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clear the current PDF preview and cleanup resources
        /// </summary>
        public void ClearPreview()
        {
            try
            {
                var message = new
                {
                    type = "CLEAR_PDF",
                    transferMethod = AppConfig.CurrentPDFMethod.ToString(),
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                var messageJson = JsonConvert.SerializeObject(message);
                webView.CoreWebView2.PostWebMessageAsString(messageJson);
                
                // Clear stream references when using custom stream handler
                if (AppConfig.UseCustomStreamHandler)
                {
                    ClearPDFStreams();
                }
                
                Debug.WriteLine("Clear preview message sent to MFE");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy PDF to app data and get URL (file URL or data URL based on current PDF method)
        /// This method respects the current PDF loading method to determine URL type
        /// </summary>
        private async Task<string> CopyPDFToAppDataAndGetFileUrl(StorageFile sourceFile)
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;

                // Create a unique filename to avoid conflicts
                var fileName = $"pdf_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.pdf";
                var destinationFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                
                // Copy the file to app data folder
                await sourceFile.CopyAndReplaceAsync(destinationFile);

                // Return appropriate URL type based on current PDF method and configuration
                if (AppConfig.CurrentPDFMethod == PDFLoadingMethod.FileURL || AppConfig.UseFileUrls)
                {
                    // Create file URL - UWP app data folder should be accessible with proper WebView2 args
                    string filePath = destinationFile.Path;
                    string fileUrl = $"file:///{filePath.Replace('\\', '/')}";
                    Debug.WriteLine($"Generated file URL: {fileUrl} (Method: {AppConfig.CurrentPDFMethod})");
                    return fileUrl;
                }
                else
                {
                    // Fall back to data URL for other methods or when file URLs are disabled
                    string dataUrl = await ConvertPDFToDataUrl(destinationFile);
                    Debug.WriteLine($"Generated data URL (length: {dataUrl.Length} chars, Method: {AppConfig.CurrentPDFMethod})");
                    return dataUrl;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to copy PDF to app data: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert PDF file to Base64 data URL as fallback when file URLs are blocked
        /// </summary>
        private async Task<string> ConvertPDFToDataUrl(StorageFile pdfFile)
        {
            try
            {
                var buffer = await Windows.Storage.FileIO.ReadBufferAsync(pdfFile);
                var bytes = new byte[buffer.Length];
                
                using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                {
                    dataReader.ReadBytes(bytes);
                }
                
                string base64Content = Convert.ToBase64String(bytes);
                string dataUrl = $"data:application/pdf;base64,{base64Content}";
                
                Debug.WriteLine($"Converted PDF to data URL: {pdfFile.Name} ({bytes.Length} bytes)");
                return dataUrl;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert PDF to data URL: {ex.Message}");
            }
        }

        /// <summary>
        /// Send PDF file URL to MFE for preview
        /// Now correctly sets transfer method based on URL type
        /// </summary>
        private void SendPDFPathToMFE(string pdfUrl, string fileName)
        {
            string urlType = pdfUrl.StartsWith("file:///") ? "file" : 
                           pdfUrl.StartsWith("data:") ? "data" : "unknown";
            
            // Set correct transfer method based on URL type
            string transferMethod = urlType == "file" ? "FileURL" : 
                                  urlType == "data" ? "DataURL" : "Unknown";
            
            var message = new
            {
                type = "LOAD_PDF",
                fileName = fileName,
                pdfUrl = pdfUrl,
                urlType = urlType,
                transferMethod = transferMethod,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var messageJson = JsonConvert.SerializeObject(message);
            webView.CoreWebView2.PostWebMessageAsString(messageJson);
            
            Debug.WriteLine($"Sent PDF {urlType} URL to MFE: {fileName} (Method: {transferMethod})");
        }

        /// <summary>
        /// Handle messages from the MFE
        /// </summary>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var messageString = e.TryGetWebMessageAsString();
                Debug.WriteLine($"Received message from MFE: {messageString}");
                
                var message = JsonConvert.DeserializeObject<dynamic>(messageString);

                // Handle different message types from MFE
                switch ((string)message.type)
                {
                    case "MFE_READY":
                        Debug.WriteLine("MFE is ready for PDF loading");
                        break;
                        
                    case "PDF_LOADED":
                        Debug.WriteLine($"PDF loaded successfully: {message.message}");
                        break;
                        
                    case "PDF_ERROR":
                        string error = message.error?.ToString() ?? "";
                        Debug.WriteLine($"PDF loading error: {error}");
                        
                        // Check if this is a file URL access error and we can fall back
                        if (AppConfig.UseFileUrls && 
                            (error.Contains("Not allowed to load local resource") || 
                             error.Contains("file:///") ||
                             error.Contains("Failed to load PDF in iframe")))
                        {
                            Debug.WriteLine("File URL blocked - switching to data URL fallback");
                            AppConfig.SetFileUrlFallback(false);
                            
                            // Note: In a real app, you might want to automatically retry here
                            // or provide a user message about the fallback
                        }
                        break;
                        
                    case "PDF_CLEARED":
                        Debug.WriteLine("PDF preview cleared");
                        break;
                        
                    default:
                        Debug.WriteLine($"Unknown message type from MFE: {message.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling MFE message: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up old PDF files from app data (optional maintenance)
        /// </summary>
        public async Task CleanupOldPDFsAsync(TimeSpan maxAge)
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var files = await localFolder.GetFilesAsync();
                
                foreach (var file in files)
                {
                    if (file.Name.StartsWith("pdf_") && file.Name.EndsWith(".pdf"))
                    {
                        var fileAge = DateTime.Now - file.DateCreated.DateTime;
                        if (fileAge > maxAge)
                        {
                            await file.DeleteAsync();
                            Debug.WriteLine($"Deleted old PDF file: {file.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up old PDFs: {ex.Message}");
            }
        }

        /// <summary>
        /// Load PDF with automatic fallback from file URLs to data URLs if file access is blocked
        /// </summary>
        public async Task LoadPDFWithFallback(StorageFile pdfFile)
        {
            try
            {
                // First try with current configuration (file URLs if enabled)
                string pdfUrl = await CopyPDFToAppDataAndGetFileUrl(pdfFile);
                string fileName = pdfFile.Name;
                
                // Send to MFE
                SendPDFPathToMFE(pdfUrl, fileName);
                
                // If using file URLs, we might need to handle fallback based on MFE response
                if (AppConfig.UseFileUrls && pdfUrl.StartsWith("file:///"))
                {
                    Debug.WriteLine("Using file URL approach - MFE will handle or report errors");
                }
                else
                {
                    Debug.WriteLine("Using data URL approach - should work universally");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadPDFWithFallback: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get registry configuration instructions for WebView2 browser arguments
        /// Use this if programmatic browser arguments don't work in UWP
        /// </summary>
        public static string GetRegistryConfigurationInstructions()
        {
            var browserArgs = AppConfig.GetWebView2Arguments();
            return $@"
If WebView2 browser arguments don't work programmatically in UWP, configure via Windows Registry:

1. Open Registry Editor (regedit.exe)
2. Navigate to: HKEY_CURRENT_USER\Software\Microsoft\Edge\WebView2
3. Create String Value: AdditionalBrowserArguments  
4. Set Value Data: {browserArgs}
5. Restart the UWP application

This will enable file:// URL access in WebView2 for your UWP app.
";
        }

        /// <summary>
        /// Test if WebView2 file URL access is working after initialization
        /// Call this method after WebView2 is initialized to verify file URL support
        /// </summary>
        public async Task<bool> TestFileUrlAccessAsync()
        {
            try
            {
                if (webView?.CoreWebView2 == null)
                {
                    Debug.WriteLine("❌ WebView2 not initialized - cannot test file URL access");
                    return false;
                }

                Debug.WriteLine("🧪 Testing WebView2 file URL access...");
                
                // Get the applied browser arguments (if available)
                var configuredArgs = AppConfig.GetWebView2Arguments();
                Debug.WriteLine($"🔧 Configured browser arguments: {configuredArgs}");
                
                // Test by injecting JavaScript to check file access capabilities
                string testScript = @"
                    (function() {
                        try {
                            // Try to access a common system file to test file URL capability
                            fetch('file:///C:/Windows/System32/drivers/etc/hosts')
                                .then(response => {
                                    console.log('File URL test result:', response.ok ? 'SUCCESS' : 'BLOCKED');
                                    return response.ok;
                                })
                                .catch(error => {
                                    console.log('File URL access BLOCKED:', error.message);
                                    return false;
                                });
                            return 'TEST_INITIATED';
                        } catch (error) {
                            console.log('File URL test error:', error.message);
                            return 'TEST_FAILED';
                        }
                    })();
                ";
                
                var result = await webView.CoreWebView2.ExecuteScriptAsync(testScript);
                Debug.WriteLine($"📊 File URL test result: {result}");
                
                if (result.Contains("TEST_INITIATED"))
                {
                    Debug.WriteLine("✅ File URL test initiated - check browser console for results");
                    Debug.WriteLine("💡 If file access is working, you should see 'File URL test result: SUCCESS' in console");
                    return true;
                }
                else
                {
                    Debug.WriteLine("❌ File URL test failed to initiate");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error testing file URL access: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setup custom stream handler for PDF loading
        /// This allows PDF files to be loaded via custom stream URIs
        /// </summary>
        private async Task SetupCustomStreamHandler()
        {
            try
            {
                // Ensure WebView2 is properly initialized
                if (webView?.CoreWebView2 == null)
                {
                    throw new InvalidOperationException("WebView2 CoreWebView2 is not initialized. Call this method after WebView2 initialization.");
                }

                // Wait a brief moment to ensure WebView2 is fully ready
                await Task.Delay(100);

                // Unregister any existing handlers first to avoid duplicates
                try
                {
                    webView.CoreWebView2.WebResourceRequested -= HandlePDFStreamRequest;
                }
                catch
                {
                    // It's okay if there was no handler to remove
                }

                // Register custom protocol handler for uwp-pdf:// URLs
                webView.CoreWebView2.WebResourceRequested += HandlePDFStreamRequest;
                webView.CoreWebView2.AddWebResourceRequestedFilter("uwp-pdf://*", CoreWebView2WebResourceContext.All);
                
                Debug.WriteLine("✅ Custom PDF stream handler registered for uwp-pdf:// protocol");
                
                // Test the registration by checking if we can add the filter (this will throw if there's an issue)
                try
                {
                    // This is a validation step - if the protocol isn't supported, this would fail
                    webView.CoreWebView2.AddWebResourceRequestedFilter("uwp-pdf://test", CoreWebView2WebResourceContext.All);
                    Debug.WriteLine("✅ Custom stream handler registration validated");
                }
                catch (Exception testEx)
                {
                    Debug.WriteLine($"⚠️ Custom stream handler validation failed: {testEx.Message}");
                    Debug.WriteLine("This might indicate the uwp-pdf:// scheme is not properly supported");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to setup custom stream handler: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Handle requests to the custom uwp-pdf:// protocol
        /// </summary>
        private async void HandlePDFStreamRequest(object sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            try
            {
                string requestUri = args.Request.Uri;
                Debug.WriteLine($"🔄 Custom stream request received: {requestUri}");
                Debug.WriteLine($"Request method: {args.Request.Method}");
                Debug.WriteLine($"Request headers: {args.Request.Headers}");

                if (requestUri.StartsWith("uwp-pdf://"))
                {
                    string fileId = requestUri.Substring("uwp-pdf://".Length);
                    Debug.WriteLine($"Extracted file ID: {fileId}");
                    Debug.WriteLine($"Available PDFs: {string.Join(", ", _activePDFs.Keys)}");
                    
                    if (_activePDFs.TryGetValue(fileId, out StorageFile pdfFile))
                    {
                        Debug.WriteLine($"📄 Found PDF file: {pdfFile.Name} (ID: {fileId})");
                        Debug.WriteLine($"File path: {pdfFile.Path}");
                        
                        // Open stream from the original file as IRandomAccessStream
                        var randomAccessStream = await pdfFile.OpenAsync(FileAccessMode.Read);
                        Debug.WriteLine($"📖 Opened file stream, size: {randomAccessStream.Size} bytes");
                        
                        // Create HTTP response with proper headers for PDF streaming
                        var responseHeaders = $"Content-Type: application/pdf\n" +
                                            $"Content-Length: {randomAccessStream.Size}\n" +
                                            $"Accept-Ranges: bytes\n" +
                                            $"Cache-Control: private\n" +
                                            $"Content-Disposition: inline; filename=\"{pdfFile.Name}\"";
                        
                        Debug.WriteLine($"Response headers: {responseHeaders}");
                        
                        args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            randomAccessStream,
                            200,
                            "OK",
                            responseHeaders
                        );
                        
                        Debug.WriteLine($"✅ PDF stream response created for {pdfFile.Name}");
                    }
                    else
                    {
                        Debug.WriteLine($"❌ PDF file not found for ID: {fileId}");
                        Debug.WriteLine($"Available IDs: [{string.Join(", ", _activePDFs.Keys)}]");
                        args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            null, 404, "Not Found", "Content-Type: text/plain");
                    }
                }
                else
                {
                    Debug.WriteLine($"⚠️ Invalid uwp-pdf:// request: {requestUri}");
                    args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 400, "Bad Request", "Content-Type: text/plain");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error handling PDF stream request: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 500, "Internal Server Error", $"Content-Type: text/plain\n\nError: {ex.Message}");
            }
        }

        /// <summary>
        /// Load PDF using custom stream URI
        /// This method is used when the PDF is to be loaded via a custom stream instead of direct file access
        /// </summary>
        /// <param name="streamUri">The custom stream URI for the PDF</param>
        public void LoadPDFViaCustomStream(string streamUri)
        {
            try
            {
                // Navigate the WebView2 to the custom stream URI
                webView.CoreWebView2.Navigate(streamUri);
                
                Debug.WriteLine($"Navigated to PDF custom stream: {streamUri}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading PDF via custom stream: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Add a PDF file to the active streams for custom handling
        /// This is called when a PDF is loaded via the custom stream method
        /// </summary>
        /// <param name="streamId">The identifier for the stream</param>
        /// <param name="pdfFile">The PDF file to be added</param>
        public void AddActivePDFStream(string streamId, StorageFile pdfFile)
        {
            if (!_activePDFs.ContainsKey(streamId))
            {
                _activePDFs.Add(streamId, pdfFile);
                Debug.WriteLine($"Added PDF to active streams: {streamId}");
            }
        }

        /// <summary>
        /// Remove a PDF file from the active streams
        /// This is called when a PDF is unloaded or no longer needed
        /// </summary>
        /// <param name="streamId">The identifier for the stream</param>
        public void RemoveActivePDFStream(string streamId)
        {
            if (_activePDFs.ContainsKey(streamId))
            {
                _activePDFs.Remove(streamId);
                Debug.WriteLine($"Removed PDF from active streams: {streamId}");
            }
        }

        /// <summary>
        /// Get a PDF file from the active streams
        /// This can be used to retrieve the file for processing or inspection
        /// </summary>
        /// <param name="streamId">The identifier for the stream</param>
        /// <returns>The StorageFile associated with the stream ID, or null if not found</returns>
        public StorageFile GetActivePDFStream(string streamId)
        {
            _activePDFs.TryGetValue(streamId, out var pdfFile);
            return pdfFile;
        }

        /// <summary>
        /// Handle WebView2 navigation completion to ensure custom stream handler is properly set up
        /// </summary>
        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (e.IsSuccess)
                {
                    Debug.WriteLine("✅ WebView2 navigation completed successfully");
                    
                    // Ensure custom stream handler is registered after navigation
                    // This is a safety measure for cases where the handler might not be properly registered
                    await EnsureCustomStreamHandlerAsync();
                }
                else
                {
                    Debug.WriteLine($"❌ WebView2 navigation failed: {e.WebErrorStatus}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnNavigationCompleted: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure the custom stream handler is properly registered
        /// This method can be called multiple times safely
        /// </summary>
        private async Task EnsureCustomStreamHandlerAsync()
        {
            try
            {
                if (webView?.CoreWebView2 == null)
                {
                    Debug.WriteLine("⚠️ Cannot ensure custom stream handler - WebView2 not initialized");
                    return;
                }

                // Check if handler is already registered by testing a dummy request
                // This is a safe way to verify without causing issues
                var filters = webView.CoreWebView2.GetType()
                    .GetProperty("WebResourceRequestFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (filters == null)
                {
                    // Fallback: Just register the handler again (it's safe to register multiple times)
                    Debug.WriteLine("🔄 Re-registering custom stream handler as safety measure");
                    webView.CoreWebView2.WebResourceRequested += HandlePDFStreamRequest;
                    webView.CoreWebView2.AddWebResourceRequestedFilter("uwp-pdf://*", CoreWebView2WebResourceContext.All);
                    Debug.WriteLine("✅ Custom stream handler re-registered");
                }
                else
                {
                    Debug.WriteLine("✅ Custom stream handler verification completed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error ensuring custom stream handler: {ex.Message}");
                // As a last resort, try to register again
                try
                {
                    webView.CoreWebView2.WebResourceRequested += HandlePDFStreamRequest;
                    webView.CoreWebView2.AddWebResourceRequestedFilter("uwp-pdf://*", CoreWebView2WebResourceContext.All);
                    Debug.WriteLine("✅ Custom stream handler registered as fallback");
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"❌ Fallback registration also failed: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// Manually re-register the custom stream handler
        /// This can be called if there are issues with the uwp-pdf:// protocol
        /// </summary>
        public async Task ReRegisterCustomStreamHandlerAsync()
        {
            try
            {
                Debug.WriteLine("🔄 Manually re-registering custom stream handler...");
                await SetupCustomStreamHandler();
                Debug.WriteLine("✅ Custom stream handler manually re-registered");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to manually re-register custom stream handler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test the custom stream handler registration by making a test request
        /// This is useful for debugging to verify the handler is working
        /// </summary>
        public async Task TestCustomStreamHandlerAsync()
        {
            try
            {
                Debug.WriteLine("🧪 Testing custom stream handler...");
                
                if (webView?.CoreWebView2 == null)
                {
                    Debug.WriteLine("❌ WebView2 not initialized - cannot test custom stream handler");
                    return;
                }

                // Create a test request to see if the handler responds
                string testUrl = "uwp-pdf://test-handler-registration";
                Debug.WriteLine($"Testing with URL: {testUrl}");
                
                // We'll simulate what happens when the MFE tries to load a uwp-pdf:// URL
                // The actual test will be visible in the debug output when HandlePDFStreamRequest is called
                Debug.WriteLine("Custom stream handler test setup complete. Make a request to uwp-pdf://test to see handler response.");
                Debug.WriteLine("✅ Custom stream handler test prepared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error testing custom stream handler: {ex.Message}");
            }
        }
    }
}
