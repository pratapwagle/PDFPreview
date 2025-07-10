using System;

namespace PDFPreviewUWP
{
    /// <summary>
    /// Configuration class for MFE URLs and app settings
    /// </summary>
    public static class AppConfig
    {
        // MFE URLs - Update these with your actual AWS deployment URLs
        public const string AWS_PRODUCTION_URL = "https://your-mfe-domain.amazonaws.com";
        public const string AWS_STAGING_URL = "https://staging-your-mfe-domain.amazonaws.com";
        public const string LOCAL_DEVELOPMENT_URL = "http://localhost:3000";
        
        // Current environment - Change this to switch between environments
        public static EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType.Local;
        
        /// <summary>
        /// Get the MFE URL based on current environment
        /// </summary>
        public static string GetMFEUrl()
        {
            switch (CurrentEnvironment)
            {
                case EnvironmentType.Production:
                    return AWS_PRODUCTION_URL;
                case EnvironmentType.Staging:
                    return AWS_STAGING_URL;
                case EnvironmentType.Local:
                    return LOCAL_DEVELOPMENT_URL;
                default:
                    return LOCAL_DEVELOPMENT_URL;
            }
        }
        
        /// <summary>
        /// Check if running in production mode
        /// </summary>
        public static bool IsProduction => CurrentEnvironment == EnvironmentType.Production;
        
        /// <summary>
        /// Check if debugging features should be enabled
        /// </summary>
        public static bool EnableDebugging => CurrentEnvironment != EnvironmentType.Production;
        
        /// <summary>
        /// PDF Loading Method Configuration
        /// </summary>
        public static PDFLoadingMethod CurrentPDFMethod { get; set; } = PDFLoadingMethod.ChunkedStream;
        
        /// <summary>
        /// Check if file URLs should be used (recommended for cloud MFE)
        /// Note: In UWP, file URLs may be blocked by default, so we start with data URLs
        /// </summary>
        private static bool _useFileUrls = false; // Start with data URLs for UWP compatibility
        public static bool UseFileUrls => _useFileUrls;
        
        /// <summary>
        /// Check if custom stream handler should be used (best performance)
        /// </summary>
        public static bool UseCustomStreamHandler => CurrentPDFMethod == PDFLoadingMethod.CustomStream;
        
        /// <summary>
        /// Check if data URLs should be used (universal compatibility)
        /// </summary>
        public static bool UseDataUrls => CurrentPDFMethod == PDFLoadingMethod.DataURL;
        
        /// <summary>
        /// Check if chunked streaming should be used (progressive loading)
        /// </summary>
        public static bool UseChunkedStreaming => CurrentPDFMethod == PDFLoadingMethod.ChunkedStream;
        
        /// <summary>
        /// Chunk size for chunked streaming (2MB)
        /// </summary>
        public static int ChunkSizeBytes => 2 * 1024 * 1024; // 2MB chunks
        
        /// <summary>
        /// Delay between chunk sends (milliseconds) to prevent overwhelming message queue
        /// </summary>
        public static int ChunkDelayMs => 10;
        
        /// <summary>
        /// Set file URL fallback at runtime (used when file URLs are blocked)
        /// </summary>
        internal static void SetFileUrlFallback(bool useFileUrls)
        {
            _useFileUrls = useFileUrls;
        }
        
        /// <summary>
        /// Enable file URL testing (for users who want to try file URLs first)
        /// </summary>
        public static void EnableFileUrlTesting()
        {
            _useFileUrls = true;
        }
        
        /// <summary>
        /// Get WebView2 initialization arguments for file access
        /// Updated with comprehensive arguments for file URL support
        /// </summary>
        public static string GetWebView2Arguments()
        {
            if (UseFileUrls || CurrentPDFMethod == PDFLoadingMethod.FileURL)
            {
                return "--allow-file-access-from-files --disable-web-security --allow-running-insecure-content --disable-features=VizDisplayCompositor";
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Get information about WebView2 configuration for debugging
        /// </summary>
        public static string GetWebView2ConfigInfo()
        {
            return $"PDFMethod: {CurrentPDFMethod}, UseFileUrls: {UseFileUrls}, Environment: {CurrentEnvironment}, Debugging: {EnableDebugging}";
        }
        
        /// <summary>
        /// Get the optimal PDF loading method description
        /// </summary>
        public static string GetPDFMethodDescription()
        {
            switch (CurrentPDFMethod)
            {
                case PDFLoadingMethod.CustomStream:
                    return "Custom Stream Handler (Optimal Performance)";
                case PDFLoadingMethod.DataURL:
                    return "Data URLs (Universal Compatibility)";
                case PDFLoadingMethod.FileURL:
                    return "File URLs (Registry Configuration Required)";
                case PDFLoadingMethod.ChunkedStream:
                    return $"Chunked Streaming ({ChunkSizeBytes / (1024 * 1024)}MB chunks, Progressive Loading)";
                default:
                    return "Unknown Method";
            }
        }
    }
    
    public enum EnvironmentType
    {
        Local,
        Staging,
        Production
    }
    
    public enum PDFLoadingMethod
    {
        /// <summary>
        /// Custom stream handler with uwp-pdf:// protocol (Best performance, no file copying)
        /// </summary>
        CustomStream,
        
        /// <summary>
        /// Base64-encoded data URLs (Universal compatibility, higher memory usage)
        /// </summary>
        DataURL,
        
        /// <summary>
        /// Direct file:// URLs (Requires browser arguments/registry configuration)
        /// </summary>
        FileURL,
        
        /// <summary>
        /// Chunked streaming via PostWebMessageAsString (2MB chunks, progressive loading)
        /// </summary>
        ChunkedStream
    }
}
