# PDF Preview UWP with AWS MFE Integration (Multi-Method Support)

A UWP application that integrates with a web-based Micro Frontend (MFE) deployed on AWS to preview PDF files using multiple optimized loading methods.

## Architecture

- **UWP App**: Handles PDF file selection with configurable loading methods
- **WebView2**: Hosts the AWS-deployed MFE with optimal PDF streaming
- **MFE**: Web application that renders PDFs using multiple URL types
- **Communication**: JSON messages via WebView2 PostMessage API
- **Multi-Method Support**: Custom streams, data URLs, and file URLs

## Key Features

- **Custom Stream Handler**: Optimal performance with `uwp-pdf://` protocol (NEW)
- **Cloud MFE Compatible**: Works with MFE hosted on AWS or any cloud provider
- **Multiple Loading Methods**: Custom streams, data URLs, file URLs with automatic fallback
- **No File Copying**: Custom streams eliminate temporary file creation
- **Universal Compatibility**: Data URL fallback works without any configuration
- **Configurable Methods**: Switch between loading approaches via AppConfig
- **Enhanced Performance**: Browser-native streaming with range request support
- **Automatic Fallback**: Graceful degradation when primary method fails
- **Interactive UI Controls**: ComboBox for real-time method selection (NEW)

## User Interface

### Transfer Method Selection

The application now includes a **Transfer Method ComboBox** in the main UI that allows users to:

- **View Available Methods**: See all supported PDF transfer methods with descriptions
- **Switch Methods Dynamically**: Change transfer method without restarting the app
- **Real-time Feedback**: Get immediate status updates when methods change
- **Recommended Methods**: Clearly labeled recommended options for optimal performance

**Transfer Methods Available in UI:**
- 🚀 **Chunked Streaming (Recommended)** - 2MB chunks via PostWebMessage, best balance
- ⚡ **Custom Stream Handler** - uwp-pdf:// protocol, optimal for large files  
- 🌐 **Data URL (Base64)** - Universal compatibility, higher memory usage
- 📁 **File URL** - Direct file:// URLs, may require browser configuration

### UI Layout

```
[Load PDF] [Clear Preview] [Transfer Method: ▼ Chunked Streaming (Recommended)] [Status: Ready]
```

The combo box automatically:
- Selects the current method from `AppConfig.CurrentPDFMethod`
- Updates `AppConfig.CurrentPDFMethod` when selection changes
- Provides tooltips and descriptions for each method
- Shows recommendations for optimal user experience

## Setup Instructions

### 1. Configure MFE URLs

Update `AppConfig.cs` with your actual AWS deployment URLs:

```csharp
public const string AWS_PRODUCTION_URL = "https://your-actual-domain.amazonaws.com";
public const string AWS_STAGING_URL = "https://staging-your-domain.amazonaws.com";
```

### 2. Set Environment and PDF Loading Method

In `AppConfig.cs`, configure your environment and preferred PDF loading method:

```csharp
// For local development
public static EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType.Local;

// For production
public static EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType.Production;

// PDF Loading Method (NEW)
public static PDFLoadingMethod CurrentPDFMethod { get; set; } = PDFLoadingMethod.CustomStream;
```

**Available PDF Loading Methods:**
- `PDFLoadingMethod.CustomStream` - ⭐ **Recommended**: Best performance, no file copying
- `PDFLoadingMethod.ChunkedStream` - 🚀 **Progressive**: 2MB chunks via PostWebMessageAsString
- `PDFLoadingMethod.DataURL` - Universal compatibility, higher memory usage
- `PDFLoadingMethod.FileURL` - Legacy method, requires registry configuration

### 3. MFE Implementation

Your AWS-hosted MFE must implement the JavaScript integration code (see `sample-mfe-integration.js`).

### 4. WebView2 Configuration for File Access

The UWP application attempts to configure WebView2 with file access browser arguments:

```csharp
// Method 1: Environment with CoreWebView2EnvironmentOptions
var options = new CoreWebView2EnvironmentOptions()
{
    AdditionalBrowserArguments = "--allow-file-access-from-files --disable-web-security"
};
var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
await webView.EnsureCoreWebView2Async(environment);

// Method 2: Standard initialization + Registry configuration
await webView.EnsureCoreWebView2Async();
```

**If programmatic browser arguments don't work in UWP:**

1. Open Registry Editor (`regedit.exe`)
2. Navigate to: `HKEY_CURRENT_USER\Software\Microsoft\Edge\WebView2`
3. Create String Value: `AdditionalBrowserArguments`
4. Set Value Data: `--allow-file-access-from-files --disable-web-security`
5. Restart the UWP application

**Browser Arguments Explained:**
- `--allow-file-access-from-files`: Allows loading local files from file:// URLs
- `--disable-web-security`: Disables same-origin policy for local files

### 5. PDF Loading Workflow (Multi-Method Support)

The application now supports three PDF loading methods with automatic fallback:

#### **Method 1: Custom Stream Handler (Recommended)**

1. User selects PDF file via file picker
2. **No File Copying**: PDF remains in original location
3. **Stream Registration**: UWP registers `uwp-pdf://` protocol handler
4. **Unique URL Generation**: UWP generates stream URL: `uwp-pdf://a1b2c3d4e5f6g7h8`
5. UWP sends message to MFE with stream URL and metadata
6. **Direct Streaming**: MFE requests PDF via uwp-pdf:// URL
7. **WebView2 Handler**: Custom handler streams PDF directly from StorageFile
8. **Result**: Optimal performance with progressive loading and range request support

#### **Method 2: Data URLs (Universal Fallback)**

1. User selects PDF file via file picker
2. **File Reading**: UWP reads entire PDF into memory
3. **Base64 Encoding**: PDF converted to Base64 data URL
4. UWP sends message to MFE with data URL
5. **Direct Rendering**: MFE renders PDF from embedded data
6. **Result**: Universal compatibility, higher memory usage

#### **Method 3: File URLs (Legacy Method)**

1. User selects PDF file via file picker
2. **File Copying**: UWP copies file to app data folder: `ApplicationData.Current.LocalFolder`
3. **WebView2 Configuration**: Requires browser arguments or registry setup
4. **File URL Generation**: UWP generates file URL: `file:///C:/Users/.../AppData/Local/Packages/.../filename.pdf`
5. UWP sends message to MFE with file URL
6. **Browser Loading**: MFE loads PDF using file:// access (if permitted)
7. **Result**: Good performance when configured, may require user setup

## How to Use the Application

### Step-by-Step Usage Guide

1. **Launch the Application**: Start the UWP PDF Preview application

2. **Select Transfer Method**: 
   - Use the **Transfer Method** dropdown to choose your preferred PDF loading method
   - **Recommended**: Start with "Chunked Streaming (Recommended)" for best performance
   - The app will show status updates when you change methods

3. **Load a PDF**:
   - Click the **"Load PDF"** button
   - Select a PDF file from your computer using the file picker
   - The status bar will show progress: "Loading PDF: filename.pdf using [Method Name]..."

4. **View the PDF**:
   - The PDF will appear in the WebView2 control hosted by your MFE
   - Depending on your MFE implementation, you can zoom, scroll, and navigate pages

5. **Clear the Preview**:
   - Click **"Clear Preview"** to remove the current PDF and clean up resources

6. **Try Different Methods**:
   - If a PDF fails to load, try selecting a different transfer method from the dropdown
   - The application will automatically use the new method for subsequent PDF loads

### Transfer Method Recommendations

- **For Most Users**: Use **Chunked Streaming** - provides the best balance of performance and compatibility
- **For Large Files (>50MB)**: Try **Custom Stream Handler** for optimal memory usage
- **For Maximum Compatibility**: Use **Data URL (Base64)** if other methods fail
- **For Advanced Users**: **File URL** requires WebView2 configuration but offers good performance

### Troubleshooting

If PDFs fail to load:

1. **Try a Different Method**: Use the dropdown to select an alternative transfer method
2. **Check File Size**: Very large files may work better with Custom Stream Handler or Chunked Streaming
3. **Review Debug Output**: Check the Visual Studio output window for detailed error messages
4. **File URL Issues**: If using File URL method, verify WebView2 browser arguments are configured

## Message Protocol

The application communicates with the web app using JSON messages. The MFE supports both new and legacy message formats for compatibility.

### Message Attributes

All PDF loading messages now include a `transferMethod` attribute that indicates which transfer method is being used:
- `"CustomStream"` - Custom uwp-pdf:// protocol stream handler
- `"DataURL"` - Base64 encoded data URL
- `"FileURL"` - Direct file:// URL access
- `"ChunkedStream"` - Progressive chunked data transfer

This helps the MFE optimize its handling and provides better debugging information.

### Outgoing Messages (UWP → Web App)

**Load PDF (Custom Stream - Recommended Method):**
```json
{
    "type": "LOAD_PDF",
    "fileName": "document.pdf",
    "pdfUrl": "uwp-pdf://a1b2c3d4e5f6g7h8",
    "urlType": "stream",
    "transferMethod": "CustomStream",
    "fileSize": 2457600,
    "timestamp": "2024-07-09T14:30:22.123Z"
}
```

**Load PDF (Data URL - Universal Compatibility):**
```json
{
    "type": "LOAD_PDF",
    "fileName": "document.pdf",
    "pdfUrl": "data:application/pdf;base64,JVBERi0xLjQKMSAwIG9iago8PA...",
    "urlType": "data",
    "transferMethod": "DataURL",
    "fileSize": 2457600,
    "timestamp": "2024-07-09T14:30:22.123Z"
}
```

**Load PDF (File URL - Legacy Method):**
```json
{
    "type": "LOAD_PDF",
    "fileName": "document.pdf",
    "pdfUrl": "file:///C:/Users/user/AppData/Local/Packages/PDFPreviewUWP.../LocalState/pdf_20240709_143022_a1b2c3d4.pdf",
    "urlType": "file",
    "transferMethod": "FileURL",
    "timestamp": "2024-07-09T14:30:22.123Z"
}
```

**Load PDF (Chunked Stream - Progressive Method):**
```json
{
    "type": "LOAD_PDF_CHUNKED_START",
    "fileName": "document.pdf",
    "totalSize": 2457600,
    "totalChunks": 2,
    "chunkSize": 2097152,
    "transferMethod": "ChunkedStream",
    "timestamp": "2024-07-09T14:30:22.123Z"
}
```

**PDF Chunk Data:**
```json
{
    "type": "LOAD_PDF_CHUNK",
    "chunkIndex": 0,
    "chunkData": "JVBERi0xLjQKMSAwIG9iago8PA...",
    "chunkSize": 2097152,
    "isLastChunk": false,
    "transferMethod": "ChunkedStream",
    "timestamp": "2024-07-09T14:30:22.234Z"
}
```

**Clear PDF:**
```json
{
    "type": "CLEAR_PDF",
    "transferMethod": "ChunkedStream",
    "timestamp": "2024-07-09T14:30:22.345Z"
}
```

**Clear Preview (New Format):**
```json
{
    "type": "CLEAR_PDF"
}
```

**Clear Preview (Legacy Format):**
```json
{
    "type": "clearPreview"
}
```

Note: Messages are sent using `CoreWebView2.PostWebMessageAsString()` method in UWP.

### Incoming Messages (Web App → UWP)

**MFE Ready:**
```json
{
    "type": "MFE_READY",
    "message": "MFE initialized and ready for PDF loading"
}
```

**PDF Loaded:**
```json
{
    "type": "PDF_LOADED",
    "message": "PDF loaded in iframe successfully"
}
```

**PDF Cleared:**
```json
{
    "type": "PDF_CLEARED",
    "message": "PDF preview cleared"
}
```

**Error (with automatic fallback trigger):**
```json
{
    "type": "PDF_ERROR",
    "error": "Not allowed to load local resource: file:///C:/path/to/file.pdf",
    "method": "iframe"
}
```

**Note**: When the UWP app receives a file URL access error, it automatically switches to data URL mode for subsequent PDF loads.

**Legacy Messages (Backward Compatibility):**
```json
{
    "type": "ready",
    "message": "PDF Preview app is ready"
}
```

```json
{
    "type": "pdfLoaded", 
    "message": "PDF loaded successfully"
}
```

```json
{
    "type": "error",
    "message": "Error description"
}
```

## Architecture

- **MainPage.xaml**: UI layout with WebView2 and controls
- **MainPage.xaml.cs**: Main logic, WebView2 initialization, and message handling
- **PDFPreviewManager.cs**: Helper class for programmatic PDF operations
- **App.xaml.cs**: Application initialization and error handling

## Configuration

The application is configured to:
- Connect to `http://localhost:3000` by default
- Support PDF file selection from Documents Library and removable storage
- Handle WebView2 navigation and communication errors gracefully

## Performance Considerations

### PDF Loading Methods Comparison

**Custom Stream Handler (Recommended)**:
- ✅ **Optimal Performance**: Direct streaming, no file copying
- ✅ **Memory Efficient**: Browser manages memory automatically
- ✅ **Range Requests**: Supports seeking to specific PDF pages
- ✅ **No Configuration**: Works out-of-the-box in UWP
- ✅ **Large Files**: Excellent performance with multi-MB PDFs
- ✅ **Progressive Loading**: Start rendering before full download

**Chunked Streaming (Progressive Loading)**:
- ✅ **Progressive Loading**: PDF renders as chunks arrive
- ✅ **No File Copying**: Streams directly from original location
- ✅ **User Feedback**: Can show loading progress
- ✅ **Memory Management**: 2MB chunks prevent memory spikes
- ⚠️ **Message Queue**: Limited by PostWebMessageAsString throughput
- ⚠️ **Base64 Overhead**: ~33% larger data transfer
- ⚠️ **Complexity**: More complex error handling and state management

**Data URLs (Universal Fallback)**:
- ✅ **Universal Compatibility**: Works without any configuration
- ✅ **No Browser Security**: No file access restrictions
- ⚠️ **Higher Memory Usage**: Base64 encoding ~33% larger
- ⚠️ **Slower for Large Files**: Entire PDF loaded into memory
- ⚠️ **URL Length Limits**: Browser limits for very large files

**File URLs (Legacy Method)**:
- ✅ **Good Performance**: Fast loading once configured
- ✅ **Browser Streaming**: Efficient caching and streaming
- ❌ **Configuration Required**: Registry setup often needed in UWP
- ❌ **Security Restrictions**: Often blocked by browser security
- ⚠️ **File Copying**: Requires copying to app data folder

### Automatic Optimization

The application automatically:
1. **Uses Custom Stream Handler** by default for optimal performance
2. **Falls back to Data URLs** if streaming fails
3. **Provides configuration options** via AppConfig.CurrentPDFMethod
4. **Handles cleanup** of stream references and temporary files
5. **Provides detailed logging** about loading method and performance

## Troubleshooting

### Custom Stream Handler Issues
- **Stream Registration**: Check Visual Studio Output for "✅ Custom PDF stream handler registered" message
- **URL Format**: Ensure MFE handles `uwp-pdf://` URLs correctly
- **Stream Cleanup**: Verify `ClearPDFStreams()` is called when clearing previews
- **Error Handling**: Check for "❌ Error handling PDF stream request" messages in debug output
- **Fallback**: System automatically falls back to Data URLs if streaming fails

### WebView2 Not Loading
- Ensure WebView2 Runtime is installed
- Check if the MFE is accessible from the configured URL
- Verify network connectivity and DNS resolution

### PDF Loading Method Selection
- **Check Configuration**: Verify `AppConfig.CurrentPDFMethod` is set correctly
- **Method Fallback**: Monitor debug output for automatic fallback messages
- **Performance Issues**: Use CustomStream for large files, DataURL for compatibility
- **Test Different Methods**: Try switching methods if one doesn't work in your environment

### File Access Issues (File URL Method)
- **WebView2 browser arguments**: Check Visual Studio Output for configuration instructions
- **Registry configuration**: Use `HKEY_CURRENT_USER\Software\Microsoft\Edge\WebView2\AdditionalBrowserArguments`
- **File URL format**: Ensure proper formatting (`file:///` with forward slashes)
- **Permissions**: UWP app data folder is always accessible
- **Alternative**: Switch to CustomStream method to avoid file access issues

### Communication Issues
- Check browser developer tools in WebView2 for JavaScript errors
- Monitor Visual Studio Output window for debug messages
- Ensure JSON message format matches the expected protocol
- Verify the MFE implements the `UWPPDFIntegration` class correctly
- **New URL Types**: Ensure MFE handles `uwp-pdf://` and includes `urlType` in error messages

### MFE Integration Issues
- Verify the MFE includes `sample-mfe-integration.js` implementation
- Check that the MFE sends `MFE_READY` message on initialization
- Ensure iframe rendering works with file URLs
- Test message handling with both new and legacy formats
- Verify PDF.js is loaded if using PDF.js fallback method

## Dependencies

- Microsoft.NETCore.UniversalWindowsPlatform (6.2.14)
- Microsoft.UI.Xaml (2.8.6) - For modern WebView2 support in UWP
- Microsoft.Web.WebView2 (1.0.2210.55)
- Newtonsoft.Json (13.0.3) - For JSON serialization in UWP

## Capabilities

The application requires the following capabilities:
- Internet Client
- Private Network Client Server
- Documents Library
- Removable Storage

## UWP File Access and Security

### File Access Limitations

UWP applications have strict file access restrictions for security. The application cannot access arbitrary file system paths using `StorageFile.GetFileFromPathAsync()`. This will result in "Access Denied" errors for most file locations.

### Supported File Access Methods

1. **File Picker (Recommended for User Files)**:
   ```csharp
   var picker = new FileOpenPicker();
   picker.FileTypeFilter.Add(".pdf");
   var file = await picker.PickSingleFileAsync(); // Returns StorageFile
   ```

2. **App Package Files**:
   ```csharp
   var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/sample.pdf"));
   ```

3. **App Data Folders**:
   ```csharp
   var localFolder = ApplicationData.Current.LocalFolder;
   var file = await localFolder.GetFileAsync("filename.pdf");
   ```

### File URL Solution (Current Implementation)

To allow the web-based MFE to access local PDF files, we use direct file URLs:

```csharp
// Copy PDF to app data folder and generate file URL
private async Task<string> CopyPDFToAppDataAndGetFileUrl(StorageFile sourceFile)
{
    var localFolder = ApplicationData.Current.LocalFolder;
    
    // Generate unique filename
    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    string uniqueId = Guid.NewGuid().ToString("N")[..8];
    string fileName = $"pdf_{timestamp}_{uniqueId}.pdf";
    
    // Copy file to app data
    var copiedFile = await sourceFile.CopyAsync(localFolder, fileName, NameCollisionOption.ReplaceExisting);
    
    // Generate file URL
    string fileUrl = $"file:///{copiedFile.Path.Replace('\\', '/')}";
    return fileUrl;
}
```

This approach generates file URLs like `file:///C:/Users/.../AppData/Local/Packages/.../LocalState/filename.pdf`, which work universally with cloud-hosted MFEs without cross-origin restrictions.

### Legacy Virtual Host Mapping (Alternative)

For local development or when file URLs don't work, virtual host mapping can be used:

```csharp
// Map a virtual domain to the app's local data folder
webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
    "pdf-assets.uwp",                          // Virtual domain
    ApplicationData.Current.LocalFolder.Path,  // Local folder path
    CoreWebView2HostResourceAccessKind.Allow   // Access permission
);
```

This allows the MFE to access files using URLs like `https://pdf-assets.uwp/filename.pdf`, but requires the MFE to be hosted on the same origin as the WebView2 control.
