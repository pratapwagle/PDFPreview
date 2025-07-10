import express from 'express';
import path from 'path';
import fs from 'fs';
import cors from 'cors';

const app = express();
const PORT = process.env.PORT || 3000;
const NODE_ENV = process.env.NODE_ENV || 'development';

// Enhanced logging for UWP integration
const logRequest = (req: express.Request, res: express.Response, next: express.NextFunction) => {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] ${req.method} ${req.url} - User-Agent: ${req.get('User-Agent')?.substring(0, 50) || 'Unknown'}`);
    next();
};

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static('public'));
app.use(logRequest);

// Serve static files
app.use('/public', express.static(path.join(__dirname, '../public')));

// Route to serve the main page
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, '../public/index.html'));
});

// Route to serve the UWP test page
app.get('/uwp-test', (req, res) => {
    res.sendFile(path.join(__dirname, '../public/uwp-test.html'));
});

// Route to serve the chunked streaming test page
app.get('/chunked-test', (req, res) => {
    res.sendFile(path.join(__dirname, '../public/chunked-streaming-test.html'));
});

// Route to serve the method validation test page
app.get('/method-test', (req, res) => {
    res.sendFile(path.join(__dirname, '../public/method-validation-test.html'));
});

// Route to serve the timing test page
app.get('/timing-test', (req, res) => {
    res.sendFile(path.join(__dirname, '../public/timing-test.html'));
});

// API endpoint to check if file exists and serve PDF
app.get('/api/pdf/*', (req, res) => {
    try {
        // Extract the file path from the URL, removing the /api/pdf/ prefix
        let filePath = req.url.substring('/api/pdf/'.length);
        
        // Decode URI components (handles spaces and special characters)
        filePath = decodeURIComponent(filePath);
        
        // Handle double encoding issues
        try {
            // Try to decode again in case of double encoding
            const testDecode = decodeURIComponent(filePath);
            if (testDecode !== filePath && testDecode.includes('\\')) {
                filePath = testDecode;
            }
        } catch (e) {
            // Ignore double decode errors
        }
        
        console.log('Original URL:', req.url);
        console.log('Extracted path:', req.url.substring('/api/pdf/'.length));
        console.log('Decoded file path:', filePath);
        
        // Validate file path format
        if (!filePath || filePath.length === 0) {
            console.log('Empty file path provided');
            return res.status(400).json({ error: 'No file path provided' });
        }
        
        // Check if file exists
        if (!fs.existsSync(filePath)) {
            console.log('File not found:', filePath);
            return res.status(404).json({ 
                error: 'File not found',
                filePath: filePath,
                suggestion: 'Please check that the file exists and the path is correct'
            });
        }
        
        // Check if file is a PDF
        if (path.extname(filePath).toLowerCase() !== '.pdf') {
            console.log('File is not a PDF:', filePath);
            return res.status(400).json({ 
                error: 'File is not a PDF',
                filePath: filePath,
                fileExtension: path.extname(filePath)
            });
        }
        
        console.log('✅ Serving PDF:', filePath);
        
        // Set appropriate headers for PDF
        res.setHeader('Content-Type', 'application/pdf');
        res.setHeader('Content-Disposition', 'inline');
        
        // Stream the PDF file
        const stream = fs.createReadStream(filePath);
        stream.on('error', (err) => {
            console.error('Error reading file:', err);
            if (!res.headersSent) {
                res.status(500).json({ 
                    error: 'Error reading file',
                    message: err.message,
                    filePath: filePath
                });
            }
        });
        stream.pipe(res);
        
    } catch (error) {
        console.error('Error in PDF endpoint:', error);
        if (!res.headersSent) {
            res.status(500).json({ 
                error: 'Internal server error',
                message: error instanceof Error ? error.message : 'Unknown error'
            });
        }
    }
});

// API endpoint to validate file path
app.post('/api/validate', (req, res) => {
    const { filePath } = req.body;
    
    if (!filePath) {
        return res.status(400).json({ error: 'File path is required' });
    }
    
    // Check if file exists
    if (!fs.existsSync(filePath)) {
        return res.status(404).json({ error: 'File not found' });
    }
    
    // Check if file is a PDF
    if (path.extname(filePath).toLowerCase() !== '.pdf') {
        return res.status(400).json({ error: 'File is not a PDF' });
    }
    
    res.json({ success: true, message: 'PDF file found' });
});

// UWP configuration endpoint
app.get('/api/uwp/config', (req, res) => {
    try {
        const configPath = path.join(__dirname, '../uwp-config.json');
        if (fs.existsSync(configPath)) {
            const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
            res.json({
                success: true,
                config: config,
                environment: NODE_ENV,
                serverInfo: {
                    port: PORT,
                    nodeVersion: process.version,
                    platform: process.platform
                }
            });
        } else {
            res.json({
                success: false,
                message: 'UWP configuration not found',
                environment: NODE_ENV
            });
        }
    } catch (error) {
        console.error('Error reading UWP config:', error);
        res.status(500).json({ 
            success: false, 
            error: 'Failed to read UWP configuration',
            message: error instanceof Error ? error.message : 'Unknown error'
        });
    }
});

// UWP status endpoint
app.get('/api/uwp/status', (req, res) => {
    res.json({
        status: 'running',
        environment: NODE_ENV,
        port: PORT,
        timestamp: new Date().toISOString(),
        uptime: process.uptime(),
        memoryUsage: process.memoryUsage(),
        uwpReady: true
    });
});

// API endpoint to get WebView2 configuration for file URL access
app.get('/api/uwp/webview2-config', (req, res) => {
    try {
        const webview2Config = {
            browserArguments: [
                '--allow-file-access-from-files',
                '--disable-web-security',
                '--allow-running-insecure-content',
                '--disable-features=VizDisplayCompositor'
            ],
            registryConfig: {
                path: 'HKEY_CURRENT_USER\\Software\\Microsoft\\Edge\\WebView2',
                valueName: 'AdditionalBrowserArguments',
                valueData: '--allow-file-access-from-files --disable-web-security --allow-running-insecure-content --disable-features=VizDisplayCompositor'
            },
            uwpImplementation: {
                programmatic: `
// In your UWP app, use this code:
var options = new CoreWebView2EnvironmentOptions();
options.AdditionalBrowserArguments = "--allow-file-access-from-files --disable-web-security --allow-running-insecure-content";
var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
await webView.EnsureCoreWebView2Async(environment);`,
                
                fallback: `
// If programmatic approach doesn't work, use registry:
// 1. Open Registry Editor (regedit.exe)
// 2. Navigate to: HKEY_CURRENT_USER\\Software\\Microsoft\\Edge\\WebView2
// 3. Create String Value: AdditionalBrowserArguments
// 4. Set Value: --allow-file-access-from-files --disable-web-security --allow-running-insecure-content
// 5. Restart your UWP application`
            },
            testUrls: [
                `http://localhost:${PORT}/file-url-test`,
                `http://localhost:${PORT}/method-test`
            ]
        };
        
        res.json({
            success: true,
            config: webview2Config,
            message: 'WebView2 configuration for file URL access',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error generating WebView2 config:', error);
        res.status(500).json({
            success: false,
            error: 'Failed to generate WebView2 configuration',
            message: error instanceof Error ? error.message : 'Unknown error'
        });
    }
});

// API endpoint to test file URL access
app.get('/file-url-test', (req, res) => {
    res.send(`
<!DOCTYPE html>
<html>
<head>
    <title>File URL Access Test</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .test-result { padding: 10px; margin: 10px 0; border-radius: 5px; }
        .pass { background: #d4edda; color: #155724; }
        .fail { background: #f8d7da; color: #721c24; }
        .info { background: #d1ecf1; color: #0c5460; }
    </style>
</head>
<body>
    <h1>File URL Access Test</h1>
    <div id="results"></div>
    
    <script>
        function addResult(test, message, status) {
            const div = document.createElement('div');
            div.className = 'test-result ' + status;
            div.innerHTML = '<strong>' + test + ':</strong> ' + message;
            document.getElementById('results').appendChild(div);
        }
        
        // Test 1: Check if we can access local files
        addResult('WebView2 Info', 'User Agent: ' + navigator.userAgent, 'info');
        
        // Test 2: Try to create a file URL (this will show if file access is enabled)
        try {
            const testFileUrl = 'file:///C:/Windows/System32/drivers/etc/hosts';
            addResult('File URL Test', 'Testing access to: ' + testFileUrl, 'info');
            
            fetch(testFileUrl)
                .then(response => {
                    if (response.ok) {
                        addResult('File URL Access', 'SUCCESS: File URLs are accessible!', 'pass');
                    } else {
                        addResult('File URL Access', 'RESTRICTED: File URLs are blocked (Status: ' + response.status + ')', 'fail');
                    }
                })
                .catch(error => {
                    addResult('File URL Access', 'BLOCKED: ' + error.message, 'fail');
                    addResult('Solution', 'Add WebView2 browser arguments: --allow-file-access-from-files --disable-web-security', 'info');
                });
        } catch (error) {
            addResult('File URL Test', 'ERROR: ' + error.message, 'fail');
        }
        
        // Test 3: WebView2 environment detection
        if (window.chrome && window.chrome.webview) {
            addResult('WebView2 Detection', 'SUCCESS: Running in WebView2 environment', 'pass');
        } else {
            addResult('WebView2 Detection', 'INFO: Not running in WebView2 (browser test)', 'info');
        }
    </script>
</body>
</html>
    `);
});

// Debug endpoint for testing file path encoding
app.get('/api/debug/path/:path(*)', (req, res) => {
    try {
        const rawPath = req.params.path;
        const decodedPath = decodeURIComponent(rawPath);
        
        console.log('Debug path test:');
        console.log('Raw path:', rawPath);
        console.log('Decoded path:', decodedPath);
        console.log('File exists:', fs.existsSync(decodedPath));
        
        res.json({
            rawPath: rawPath,
            decodedPath: decodedPath,
            exists: fs.existsSync(decodedPath),
            isFile: fs.existsSync(decodedPath) ? fs.statSync(decodedPath).isFile() : false,
            extension: path.extname(decodedPath),
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        res.status(500).json({
            error: 'Debug path test failed',
            message: error instanceof Error ? error.message : 'Unknown error'
        });
    }
});

// API endpoint to track timing metrics
app.post('/api/timing', (req, res) => {
    try {
        const { operation, durationMs, durationSec, method, pages, timestamp } = req.body;
        
        console.log(`⏱️ PDF Timing Report:`);
        console.log(`   Operation: ${operation || 'Unknown'}`);
        console.log(`   Duration: ${durationSec}s (${durationMs}ms)`);
        console.log(`   Method: ${method || 'Unknown'}`);
        console.log(`   Pages: ${pages || 'Unknown'}`);
        console.log(`   Timestamp: ${timestamp || new Date().toISOString()}`);
        
        // Store timing data (in production, this could go to a database or analytics service)
        const timingData = {
            operation,
            durationMs,
            durationSec,
            method,
            pages,
            timestamp: timestamp || new Date().toISOString(),
            serverTimestamp: new Date().toISOString()
        };
        
        res.json({
            success: true,
            message: 'Timing data recorded',
            data: timingData
        });
        
    } catch (error) {
        console.error('Error recording timing data:', error);
        res.status(500).json({
            success: false,
            error: 'Failed to record timing data',
            message: error instanceof Error ? error.message : 'Unknown error'
        });
    }
});

app.listen(PORT, () => {
    console.log(`🚀 PDF Preview Server started successfully!`);
    console.log(`📍 Server URL: http://localhost:${PORT}`);
    console.log(`🧪 UWP Test Page: http://localhost:${PORT}/uwp-test`);
    console.log(`🔧 Chunked Test Page: http://localhost:${PORT}/chunked-test`);
    console.log(`📊 Method Test Page: http://localhost:${PORT}/method-test`);
    console.log(`⏱️  Timing Test Page: http://localhost:${PORT}/timing-test`);
    console.log(`🔒 File URL Test: http://localhost:${PORT}/file-url-test`);
    console.log(`⚙️  Environment: ${NODE_ENV}`);
    console.log(`🔗 UWP WebView2 Ready for integration`);
    console.log(`📁 Serving static files from: ${path.join(__dirname, '../public')}`);
    
    if (NODE_ENV === 'development') {
        console.log(`🔧 Development mode - Auto-restart enabled`);
        console.log(`📊 UWP Config: http://localhost:${PORT}/api/uwp/config`);
        console.log(`📈 UWP Status: http://localhost:${PORT}/api/uwp/status`);
        console.log(`🔒 WebView2 Config: http://localhost:${PORT}/api/uwp/webview2-config`);
    }
    
    console.log(`\n✅ Ready for UWP WebView2 integration!`);
    console.log(`🧪 Test chunked streaming fixes at: http://localhost:${PORT}/chunked-test`);
    console.log(`🔍 Validate method behavior at: http://localhost:${PORT}/method-test`);
    console.log(`⏱️  Test timing performance at: http://localhost:${PORT}/timing-test`);
    console.log(`🔒 Test file URL access at: http://localhost:${PORT}/file-url-test`);
    console.log(`\n📋 For file URL support, get WebView2 config at: http://localhost:${PORT}/api/uwp/webview2-config`);
});
