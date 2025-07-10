// Sample MFE JavaScript code to handle UWP PDF preview integration
// This code should be included in your AWS-hosted MFE
// 
// This implementation supports:
// - File URLs (file:///) for cloud MFE compatibility (primary method)
// - Data URLs (data:application/pdf;base64,...) as fallback
// - Virtual host URLs (https://pdf-assets.uwp/...) for legacy support
// - Multiple rendering methods: iframe, PDF.js
//
// Updated: Uses file URL approach for maximum cloud compatibility

class UWPPDFIntegration {
    constructor() {
        this.initializeMessageHandler();
        this.notifyReady();
        
        // Chunked streaming state
        this.chunkingState = {
            isReceiving: false,
            chunks: [],
            expectedChunks: 0,
            fileName: '',
            totalSize: 0
        };
    }

    initializeMessageHandler() {
        // Listen for messages from UWP WebView2
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.addEventListener('message', (event) => {
                this.handleUWPMessage(event.data);
            });
        }
    }

    handleUWPMessage(data) {
        try {
            const message = typeof data === 'string' ? JSON.parse(data) : data;
            
            switch (message.type) {
                case 'LOAD_PDF':
                    const transferMethod = message.transferMethod || 'Unknown';
                    console.log(`Loading PDF: ${message.fileName || 'Unknown'} (${message.urlType || 'unknown'} URL, Method: ${transferMethod})`);
                    this.loadPDF(message.pdfUrl, message.urlType, transferMethod);
                    break;
                case 'LOAD_PDF_CHUNKED_START':
                    const chunkTransferMethod = message.transferMethod || 'ChunkedStream';
                    console.log(`Starting chunked PDF load: ${message.fileName} (${message.totalChunks} chunks, Method: ${chunkTransferMethod})`);
                    this.startChunkedPDFLoad(message);
                    break;
                case 'LOAD_PDF_CHUNK':
                    this.receiveChunk(message);
                    break;
                case 'LOAD_PDF_CHUNKED_ERROR':
                    console.error('Chunked loading error:', message.error);
                    this.resetChunkingState();
                    this.sendMessageToUWP({
                        type: 'PDF_ERROR',
                        error: `Chunked loading failed: ${message.error}`
                    });
                    break;
                case 'CLEAR_PDF':
                    this.clearPDF();
                    break;
                default:
                    console.log('Unknown message type from UWP:', message.type);
            }
        } catch (error) {
            console.error('Error handling UWP message:', error);
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: error.message
            });
        }
    }

    async loadPDF(pdfUrl, urlType = 'unknown', transferMethod = 'Unknown') {
        try {
            console.log(`Loading PDF from UWP: ${pdfUrl} (Type: ${urlType}, Method: ${transferMethod})`);
            
            // Handle different URL types for cloud MFE compatibility
            if (pdfUrl.startsWith('uwp-pdf://')) {
                // Custom stream handler - optimal performance
                await this.loadStreamPDF(pdfUrl, urlType, transferMethod);
            } else if (pdfUrl.startsWith('file:///')) {
                // File URLs - works with cloud-hosted MFE
                await this.loadFileSystemPDF(pdfUrl, transferMethod);
            } else if (pdfUrl.startsWith('https://pdf-assets.uwp/')) {
                // Virtual host mapping (legacy support for local MFE)
                await this.loadVirtualHostPDF(pdfUrl, transferMethod);
            } else if (pdfUrl.startsWith('data:application/pdf')) {
                // Base64 data URL (alternative approach)
                await this.loadDataUrlPDF(pdfUrl, transferMethod);
            } else {
                throw new Error('Unsupported PDF URL format: ' + pdfUrl);
            }
            
        } catch (error) {
            console.error('Error loading PDF:', error);
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: error.message,
                urlType: urlType,
                transferMethod: transferMethod
            });
        }
    }

    async loadStreamPDF(streamUrl, urlType, transferMethod = 'CustomStream') {
        console.log(`Loading PDF from custom stream: ${streamUrl} (Method: ${transferMethod})`);
        
        // Method 1: Direct iframe (recommended for custom streams)
        if (this.useIframeMethod()) {
            this.loadPDFInIframe(streamUrl, transferMethod);
            this.sendMessageToUWP({
                type: 'PDF_LOADED',
                message: 'PDF loaded via custom stream in iframe successfully',
                method: 'iframe',
                urlType: urlType,
                transferMethod: transferMethod
            });
            return;
        }
        
        // Method 2: PDF.js with fetch (also works with custom streams)
        if (window.pdfjsLib) {
            try {
                console.log('Attempting to load custom stream with PDF.js...');
                
                const response = await fetch(streamUrl);
                if (!response.ok) {
                    throw new Error(`Stream fetch error! status: ${response.status}`);
                }
                
                const arrayBuffer = await response.arrayBuffer();
                const loadingTask = window.pdfjsLib.getDocument(new Uint8Array(arrayBuffer));
                const pdf = await loadingTask.promise;
                
                console.log('PDF loaded via custom stream with PDF.js');
                this.renderPDFWithPDFJS(pdf);
                this.sendMessageToUWP({
                    type: 'PDF_LOADED',
                    message: 'PDF loaded via custom stream with PDF.js successfully',
                    method: 'pdfjs',
                    urlType: urlType,
                    transferMethod: transferMethod
                });
                return;
            } catch (pdfError) {
                console.warn('PDF.js custom stream loading failed:', pdfError);
                // Fallback to iframe
                this.loadPDFInIframe(streamUrl);
                return;
            }
        }
        
        // Fallback: Direct iframe
        this.loadPDFInIframe(streamUrl, transferMethod);
        this.sendMessageToUWP({
            type: 'PDF_LOADED',
            message: 'PDF loaded via custom stream with iframe fallback',
            method: 'iframe',
            urlType: urlType,
            transferMethod: transferMethod
        });
    }

    async loadFileSystemPDF(fileUrl, transferMethod = 'FileURL') {
        console.log(`Loading PDF from file system: ${fileUrl} (Method: ${transferMethod})`);
        
        // Method 1: Direct iframe (most reliable for file URLs)
        if (this.useIframeMethod()) {
            this.loadPDFInIframe(fileUrl, transferMethod);
            return;
        }
        
        // Method 2: PDF.js with fetch (may require additional permissions)
        if (window.pdfjsLib) {
            try {
                console.log('Attempting to load with PDF.js...');
                
                // Fetch the file content
                const response = await fetch(fileUrl);
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                
                const arrayBuffer = await response.arrayBuffer();
                const loadingTask = window.pdfjsLib.getDocument(new Uint8Array(arrayBuffer));
                const pdf = await loadingTask.promise;
                
                await this.renderPDF(pdf);
                
                this.sendMessageToUWP({
                    type: 'PDF_LOADED',
                    message: 'PDF loaded successfully with PDF.js',
                    pages: pdf.numPages,
                    method: 'pdfjs',
                    transferMethod: transferMethod
                });
                
            } catch (fetchError) {
                console.warn('PDF.js fetch failed, falling back to iframe:', fetchError);
                
                // Report specific file access errors back to UWP
                if (fetchError.message.includes('Not allowed to load local resource')) {
                    this.sendMessageToUWP({
                        type: 'PDF_ERROR',
                        error: `File URL access blocked: ${fetchError.message}`,
                        method: 'pdfjs',
                        transferMethod: transferMethod
                    });
                    return;
                }
                
                this.loadPDFInIframe(fileUrl, transferMethod);
            }
        } else {
            // Fallback to iframe if PDF.js not available
            this.loadPDFInIframe(fileUrl, transferMethod);
        }
    }

    async loadVirtualHostPDF(virtualUrl, transferMethod = 'VirtualHost') {
        // Legacy support for virtual host mapping (when MFE is local)
        console.log(`Loading PDF from virtual host: ${virtualUrl} (Method: ${transferMethod})`);
        this.loadPDFInIframe(virtualUrl, transferMethod);
    }

    async loadDataUrlPDF(dataUrl, transferMethod = 'DataURL') {
        // Support for Base64 data URLs
        console.log(`Loading PDF from data URL (Method: ${transferMethod})...`);
        if (window.pdfjsLib) {
            const loadingTask = window.pdfjsLib.getDocument(dataUrl);
            const pdf = await loadingTask.promise;
            await this.renderPDF(pdf);
            
            this.sendMessageToUWP({
                type: 'PDF_LOADED',
                message: 'PDF loaded from data URL',
                pages: pdf.numPages,
                method: 'pdfjs-dataurl',
                transferMethod: transferMethod
            });
        } else {
            this.loadPDFDataUrlInIframe(dataUrl, transferMethod);
        }
    }

    useIframeMethod() {
        // For cloud-hosted MFE, iframe is often more reliable for file URLs
        // You can make this configurable based on your MFE environment
        return true; // or check if running in cloud vs local
    }

    loadPDFInIframe(pdfUrl, transferMethod = 'Unknown') {
        console.log(`Loading PDF in iframe: ${pdfUrl} (Method: ${transferMethod})`);
        
        const container = document.getElementById('pdf-container');
        if (container) {
            // Add error detection for file URL access issues
            const iframe = document.createElement('iframe');
            iframe.src = pdfUrl;
            iframe.width = '100%';
            iframe.height = '100%';
            iframe.style.border = 'none';
            
            // Set up load/error handlers
            iframe.onload = () => {
                console.log('Iframe loaded successfully');
                this.onPDFLoaded('iframe');
            };
            
            iframe.onerror = (error) => {
                console.error('Iframe failed to load:', error);
                this.onPDFError('iframe');
            };
            
            // Additional error detection for file URL access
            setTimeout(() => {
                try {
                    // Try to access iframe content to detect security errors
                    if (iframe.contentDocument === null && pdfUrl.startsWith('file:///')) {
                        console.error('File URL access blocked by browser security');
                        this.sendMessageToUWP({
                            type: 'PDF_ERROR',
                            error: 'Not allowed to load local resource: ' + pdfUrl,
                            method: 'iframe',
                            transferMethod: transferMethod
                        });
                    }
                } catch (securityError) {
                    if (pdfUrl.startsWith('file:///')) {
                        console.error('Security error accessing file URL:', securityError);
                        this.sendMessageToUWP({
                            type: 'PDF_ERROR',
                            error: 'File URL access blocked: ' + securityError.message,
                            method: 'iframe',
                            transferMethod: transferMethod
                        });
                    }
                }
            }, 1000);
            
            container.innerHTML = '';
            container.appendChild(iframe);
        }
    }

    loadPDFDataUrlInIframe(dataUrl, transferMethod = 'DataURL') {
        console.log(`Loading PDF data URL in iframe (Method: ${transferMethod})...`);
        
        // Convert data URL to blob for iframe (more reliable)
        fetch(dataUrl)
            .then(response => response.blob())
            .then(blob => {
                const blobUrl = URL.createObjectURL(blob);
                const container = document.getElementById('pdf-container');
                
                if (container) {
                    container.innerHTML = `
                        <iframe 
                            src="${blobUrl}" 
                            width="100%" 
                            height="100%" 
                            style="border: none;"
                            onload="uwpPDFIntegration.onPDFLoaded('iframe-blob')"
                            onerror="uwpPDFIntegration.onPDFError('iframe-blob')">
                        </iframe>
                    `;
                }
            })
            .catch(error => {
                console.error('Error creating blob URL:', error);
                this.onPDFError('blob-creation');
            });
    }

    async renderPDF(pdf) {
        // Example PDF.js rendering (adapt to your needs)
        const container = document.getElementById('pdf-container');
        if (!container) return;

        container.innerHTML = ''; // Clear previous content

        for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
            const page = await pdf.getPage(pageNum);
            const canvas = document.createElement('canvas');
            const context = canvas.getContext('2d');
            
            const viewport = page.getViewport({ scale: 1.5 });
            canvas.height = viewport.height;
            canvas.width = viewport.width;
            
            container.appendChild(canvas);
            
            await page.render({
                canvasContext: context,
                viewport: viewport
            }).promise;
        }
    }

    clearPDF() {
        const container = document.getElementById('pdf-container');
        if (container) {
            container.innerHTML = '<p>No PDF loaded</p>';
        }
        
        this.sendMessageToUWP({
            type: 'PDF_CLEARED',
            message: 'PDF preview cleared'
        });
    }

    onPDFLoaded(method = 'unknown') {
        console.log(`PDF loaded successfully using ${method} method`);
        this.sendMessageToUWP({
            type: 'PDF_LOADED',
            message: `PDF loaded in ${method} successfully`,
            method: method
        });
    }

    onPDFError(method = 'unknown') {
        console.error(`Failed to load PDF using ${method} method`);
        this.sendMessageToUWP({
            type: 'PDF_ERROR',
            error: `Failed to load PDF in ${method}`,
            method: method
        });
    }

    sendMessageToUWP(message) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify(message));
        }
    }

    notifyReady() {
        // Let UWP know the MFE is ready
        setTimeout(() => {
            this.sendMessageToUWP({
                type: 'MFE_READY',
                message: 'MFE initialized and ready for PDF loading'
            });
        }, 1000);
    }

    // === Chunked Streaming Methods ===
    
    startChunkedPDFLoad(message) {
        console.log(`Starting chunked PDF load: ${message.fileName} (${message.totalChunks} chunks, ${message.totalSize} bytes)`);
        console.log(`Base64 size: ${message.base64Size} chars, Chunk size: ${message.chunkSize} chars`);
        
        // Reset and initialize chunking state
        this.chunkingState = {
            isReceiving: true,
            chunks: new Array(message.totalChunks),
            expectedChunks: message.totalChunks,
            fileName: message.fileName,
            totalSize: message.totalSize,
            base64Size: message.base64Size,
            receivedChunks: 0,
            chunkSize: message.chunkSize
        };
        
        // Notify UWP that we're ready to receive chunks
        this.sendMessageToUWP({
            type: 'CHUNKED_START_ACK',
            message: `Ready to receive ${message.totalChunks} chunks for ${message.fileName}`
        });
    }
    
    receiveChunk(message) {
        if (!this.chunkingState.isReceiving) {
            console.warn('Received chunk but not in receiving state');
            return;
        }
        
        const { chunkIndex, chunkData, isLastChunk } = message;
        
        // Enhanced validation
        if (chunkIndex < 0 || chunkIndex >= this.chunkingState.expectedChunks) {
            console.error(`Chunk index ${chunkIndex} out of range (0-${this.chunkingState.expectedChunks - 1})`);
            return;
        }
        
        if (!chunkData || chunkData.length === 0) {
            console.error(`Chunk ${chunkIndex} missing data`);
            return;
        }
        
        console.log(`Received chunk ${chunkIndex + 1}/${this.chunkingState.expectedChunks} (${chunkData.length} chars)`);
        
        // Store the chunk
        this.chunkingState.chunks[chunkIndex] = chunkData;
        this.chunkingState.receivedChunks++;
        
        // Update progress (optional - for UI feedback)
        const progress = (this.chunkingState.receivedChunks / this.chunkingState.expectedChunks) * 100;
        console.log(`Progress: ${progress.toFixed(1)}% (${this.chunkingState.receivedChunks}/${this.chunkingState.expectedChunks})`);
        
        // Check if all chunks received
        if (isLastChunk || this.chunkingState.receivedChunks === this.chunkingState.expectedChunks) {
            this.assembleAndLoadChunkedPDF();
        }
    }
    
    assembleAndLoadChunkedPDF() {
        try {
            console.log('Assembling chunked PDF...');
            
            // Validate all chunks are present
            const missingChunks = [];
            for (let i = 0; i < this.chunkingState.expectedChunks; i++) {
                if (!this.chunkingState.chunks[i] || 
                    this.chunkingState.chunks[i] === null || 
                    this.chunkingState.chunks[i] === undefined) {
                    missingChunks.push(i);
                }
            }
            
            if (missingChunks.length > 0) {
                throw new Error(`Missing chunks: [${missingChunks.join(', ')}]`);
            }
            
            // Log chunk details for debugging
            console.log(`Expected chunks: ${this.chunkingState.expectedChunks}, Received: ${this.chunkingState.receivedChunks}`);
            for (let i = 0; i < this.chunkingState.chunks.length; i++) {
                console.log(`Chunk ${i}: ${this.chunkingState.chunks[i] ? this.chunkingState.chunks[i].length : 'null'} chars`);
            }
            
            // Combine all chunks
            const combinedBase64 = this.chunkingState.chunks.join('');
            console.log(`Assembled PDF: ${combinedBase64.length} chars total`);
            
            // Validate the combined Base64 length matches expected
            if (this.chunkingState.base64Size && combinedBase64.length !== this.chunkingState.base64Size) {
                console.warn(`Base64 size mismatch: expected ${this.chunkingState.base64Size}, got ${combinedBase64.length}`);
            }
            
            // Validate Base64 format
            if (!this.isValidBase64(combinedBase64)) {
                throw new Error('Combined chunks do not form valid Base64 data');
            }
            
            const dataUrl = `data:application/pdf;base64,${combinedBase64}`;
            
            // Load the assembled PDF
            this.loadDataUrlPDF(dataUrl);
            
            // Store reference for debugging
            const fileName = this.chunkingState.fileName;
            const expectedChunks = this.chunkingState.expectedChunks;
            
            // Reset chunking state
            this.resetChunkingState();
            
            // Notify UWP of successful assembly
            this.sendMessageToUWP({
                type: 'PDF_LOADED',
                message: `Chunked PDF assembled and loaded: ${fileName}`,
                method: 'chunked',
                chunks: expectedChunks,
                transferMethod: 'ChunkedStream'
            });
            
        } catch (error) {
            console.error('Error assembling chunked PDF:', error);
            this.resetChunkingState();
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: `Failed to assemble chunked PDF: ${error.message}`,
                method: 'chunked',
                transferMethod: 'ChunkedStream'
            });
        }
    }
    
    // Helper method to validate Base64 format
    isValidBase64(str) {
        try {
            // Check if string is valid Base64
            return btoa(atob(str)) === str;
        } catch (err) {
            return false;
        }
    }
    
    resetChunkingState() {
        this.chunkingState = {
            isReceiving: false,
            chunks: [],
            expectedChunks: 0,
            fileName: '',
            totalSize: 0,
            receivedChunks: 0
        };
    }
}

// Initialize when page loads
let uwpPDFIntegration;
document.addEventListener('DOMContentLoaded', () => {
    uwpPDFIntegration = new UWPPDFIntegration();
});

// Example HTML structure for your MFE:
/*
<!DOCTYPE html>
<html>
<head>
    <title>PDF Preview MFE</title>
    <script src="https://cdn.jsdelivr.net/npm/pdfjs-dist@3.11.174/build/pdf.min.js"></script>
</head>
<body>
    <div id="pdf-container" style="width: 100%; height: 100vh; overflow: auto;">
        <p>Waiting for PDF from UWP app...</p>
    </div>
    <script src="uwp-pdf-integration.js"></script>
</body>
</html>
*/
