// WebView2 API Reference for UWP PDF Preview Project
// This file documents the correct WebView2 API methods used in this project

/*
CORRECT WebView2 API Usage:

1. Navigation:
   ✅ webView.CoreWebView2.Navigate(string uri)
   ❌ webView.CoreWebView2.NavigateAsync(string uri) // Does not exist

2. Messaging:
   ✅ webView.CoreWebView2.PostWebMessageAsString(string webMessageAsString)
   ✅ webView.CoreWebView2.PostWebMessageAsJson(string webMessageAsJson)
   
3. Event Handling:
   ✅ webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
   
4. Settings:
   ✅ webView.CoreWebView2.Settings.AreDevToolsEnabled = bool;
   ✅ webView.CoreWebView2.Settings.AreHostObjectsAllowed = bool;
   
5. Virtual Host Mapping:
   ✅ webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
        string hostName, 
        string folderPath, 
        CoreWebView2HostResourceAccessKind accessKind);

6. Initialization:
   ✅ await webView.EnsureCoreWebView2Async(); // This one IS async

Important Notes:
- Most CoreWebView2 methods are synchronous
- Only EnsureCoreWebView2Async() is async and should be awaited
- Navigate() is synchronous, not NavigateAsync()
- PostWebMessageAsString() is synchronous
*/
