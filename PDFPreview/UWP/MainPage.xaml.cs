using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using System.Linq;

namespace PDFPreviewUWP
{
    /// <summary>
    /// Helper class to represent PDF transfer methods in the combo box
    /// </summary>
    public class PDFTransferMethodItem
    {
        public PDFLoadingMethod Method { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsRecommended { get; set; }
        
        public override string ToString()
        {
            return IsRecommended ? $"{DisplayName} (Recommended)" : DisplayName;
        }
    }

    public sealed partial class MainPage : Page
    {
        private PDFPreviewManager pdfManager;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize transfer method combo box first
                InitializeTransferMethodComboBox();
                
                // Initialize PDF manager with WebView2 control
                pdfManager = new PDFPreviewManager(PDFWebView);
                await pdfManager.InitializeAsync();
                
                // Show registry configuration info if file URLs are enabled
                if (AppConfig.UseFileUrls)
                {
                    Debug.WriteLine("🔧 File URL mode enabled");
                    Debug.WriteLine("If PDFs don't load, you may need to configure WebView2 via registry:");
                    Debug.WriteLine(PDFPreviewManager.GetRegistryConfigurationInstructions());
                    
                    // Optional: Test file URL access after initialization
                    // var fileUrlWorks = await pdfManager.TestFileUrlAccess();
                    // Debug.WriteLine($"File URL access test: {(fileUrlWorks ? "✅ Passed" : "❌ Failed")}");
                }
                
                // Load the MFE using configuration
                string mfeUrl = AppConfig.GetMFEUrl();
                UpdateStatus($"Loading MFE from: {AppConfig.CurrentEnvironment} (Using {GetCurrentMethodDisplayName()})");
                pdfManager.LoadMFE(mfeUrl);
                UpdateStatus("MFE loaded successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing: {ex.Message}");
                Debug.WriteLine($"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Initialize the transfer method combo box with available PDF transfer methods
        /// </summary>
        private void InitializeTransferMethodComboBox()
        {
            var transferMethods = new List<PDFTransferMethodItem>
            {
                new PDFTransferMethodItem
                {
                    Method = PDFLoadingMethod.ChunkedStream,
                    DisplayName = "Chunked Streaming",
                    Description = "2MB chunks via PostWebMessage - Best balance of performance and compatibility",
                    IsRecommended = true
                },
                new PDFTransferMethodItem
                {
                    Method = PDFLoadingMethod.CustomStream,
                    DisplayName = "Custom Stream Handler",
                    Description = "uwp-pdf:// protocol - Optimal for large files, best performance",
                    IsRecommended = false
                },
                new PDFTransferMethodItem
                {
                    Method = PDFLoadingMethod.DataURL,
                    DisplayName = "Data URL (Base64)",
                    Description = "Base64 encoded data URLs - Universal compatibility, higher memory usage",
                    IsRecommended = false
                },
                new PDFTransferMethodItem
                {
                    Method = PDFLoadingMethod.FileURL,
                    DisplayName = "File URL",
                    Description = "Direct file:// URLs - May require browser configuration",
                    IsRecommended = false
                }
            };

            TransferMethodComboBox.ItemsSource = transferMethods;
            
            // Select the current method from AppConfig
            var currentMethodItem = transferMethods.FirstOrDefault(m => m.Method == AppConfig.CurrentPDFMethod);
            if (currentMethodItem != null)
            {
                TransferMethodComboBox.SelectedItem = currentMethodItem;
            }
            else
            {
                // Default to the recommended method if current method not found
                TransferMethodComboBox.SelectedItem = transferMethods.First(m => m.IsRecommended);
            }
        }

        /// <summary>
        /// Handle transfer method selection change
        /// </summary>
        private void TransferMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TransferMethodComboBox.SelectedItem is PDFTransferMethodItem selectedItem)
            {
                // Update the configuration
                AppConfig.CurrentPDFMethod = selectedItem.Method;
                
                // Update the status to show the new method
                string methodName = GetCurrentMethodDisplayName();
                UpdateStatus($"Transfer method changed to: {methodName}");
                
                Debug.WriteLine($"🔄 PDF transfer method changed to: {selectedItem.Method} - {selectedItem.Description}");
                
                // Show additional info for methods that might need special configuration
                switch (selectedItem.Method)
                {
                    case PDFLoadingMethod.FileURL:
                        Debug.WriteLine("⚠️ File URL method may require WebView2 browser arguments or registry configuration");
                        Debug.WriteLine("See PDFPreviewManager.GetRegistryConfigurationInstructions() for setup details");
                        break;
                        
                    case PDFLoadingMethod.CustomStream:
                        Debug.WriteLine("ℹ️ Custom stream handler provides the best performance for large files");
                        break;
                        
                    case PDFLoadingMethod.ChunkedStream:
                        Debug.WriteLine($"ℹ️ Chunked streaming will use {AppConfig.ChunkSizeBytes / (1024 * 1024)}MB chunks with {AppConfig.ChunkDelayMs}ms delay");
                        break;
                        
                    case PDFLoadingMethod.DataURL:
                        Debug.WriteLine("ℹ️ Data URL method works universally but may use more memory for large files");
                        break;
                }
            }
        }

        /// <summary>
        /// Get the display name of the current transfer method
        /// </summary>
        private string GetCurrentMethodDisplayName()
        {
            return AppConfig.CurrentPDFMethod switch
            {
                PDFLoadingMethod.ChunkedStream => "Chunked Streaming",
                PDFLoadingMethod.CustomStream => "Custom Stream Handler",
                PDFLoadingMethod.DataURL => "Data URL (Base64)",
                PDFLoadingMethod.FileURL => "File URL",
                _ => "Unknown Method"
            };
        }

        private async void LoadPDFButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                picker.FileTypeFilter.Add(".pdf");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    string methodName = GetCurrentMethodDisplayName();
                    UpdateStatus($"Loading PDF: {file.Name} using {methodName}...");
                    
                    // Use the selected method from the combo box (already set in AppConfig.CurrentPDFMethod)
                    await pdfManager.LoadPDFAsync(file);
                    
                    UpdateStatus($"PDF loaded successfully using {methodName}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading PDF: {ex.Message}");
                Debug.WriteLine($"PDF loading error: {ex}");
                
                // Suggest trying a different method if the current one failed
                var currentMethod = GetCurrentMethodDisplayName();
                Debug.WriteLine($"💡 Consider trying a different transfer method if {currentMethod} continues to fail");
            }
        }

        private void ClearPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            pdfManager.ClearPreview();
            UpdateStatus("Preview cleared");
        }

        private async void TestStreamHandlerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Testing custom stream handler...");
                
                if (pdfManager == null)
                {
                    UpdateStatus("Error: PDF manager not initialized");
                    return;
                }

                // Test the custom stream handler registration
                await pdfManager.TestCustomStreamHandlerAsync();
                
                // Also re-register the handler to ensure it's working
                await pdfManager.ReRegisterCustomStreamHandlerAsync();
                
                UpdateStatus("Custom stream handler test completed - check debug output");
                Debug.WriteLine("🧪 Custom stream handler test completed. Check the debug output above for details.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Stream handler test failed: {ex.Message}");
                Debug.WriteLine($"❌ Stream handler test error: {ex}");
            }
        }

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
        }
    }
}
