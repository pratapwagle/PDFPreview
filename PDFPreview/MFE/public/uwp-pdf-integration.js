// UWP PDF Integration JavaScript - Standalone version
// This code can be included in any AWS-hosted MFE for UWP integration
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
        this.isInUWP = this.checkUWPEnvironment();
        this.initializeMessageHandler();
        this.notifyReady();
        
        // Chunked streaming state
        this.chunkingState = {
            isReceiving: false,
            chunks: [],
            expectedChunks: 0,
            fileName: '',
            totalSize: 0,
            receivedChunks: 0
        };
        
        // Timing state for performance measurement
        this.timingState = {
            requestStartTime: null,
            previewCompleteTime: null,
            lastRequestDuration: null
        };
        
        // Initialize timing display
        this.initializeTimingDisplay();
        
        // Initialize UI timing tracking
        this.uiTimingState = {
            totalLoads: 0,
            durations: [],
            currentOperation: null
        };
        this.initializeUITimingDisplay();
    }

    checkUWPEnvironment() {
        return !!(window.chrome && window.chrome.webview);
    }

    initializeMessageHandler() {
        // Listen for messages from UWP WebView2
        if (this.isInUWP) {
            console.log('Initializing UWP message handler');
            window.chrome.webview.addEventListener('message', (event) => {
                this.handleUWPMessage(event.data);
            });
        }
    }

    handleUWPMessage(data) {
        try {
            console.log('Received UWP message:', data);
            const message = typeof data === 'string' ? JSON.parse(data) : data;
            
            switch (message.type) {
                case 'LOAD_PDF':
                    this.loadPDF(message.pdfUrl);
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
                    this.sendMessageToUWP({
                        type: 'MESSAGE_ERROR',
                        error: `Unknown message type: ${message.type}`,
                        receivedMessage: message
                    });
            }
        } catch (error) {
            console.error('Error handling UWP message:', error);
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: error.message
            });
        }
    }

    async loadPDF(pdfUrl) {
        try {
            console.log('Loading PDF from UWP:', pdfUrl);
            
            // Start timing measurement
            this.startTiming('PDF Load');
            
            if (!pdfUrl) {
                throw new Error('No PDF URL provided');
            }
            
            // Handle different URL types for cloud MFE compatibility
            if (pdfUrl.startsWith('file:///')) {
                // File URLs - works with cloud-hosted MFE (primary method)
                await this.loadFileSystemPDF(pdfUrl);
            } else if (pdfUrl.startsWith('https://pdf-assets.uwp/')) {
                // Virtual host mapping (legacy support for local MFE)
                await this.loadVirtualHostPDF(pdfUrl);
            } else if (pdfUrl.startsWith('data:application/pdf')) {
                // Base64 data URL (alternative approach)
                await this.loadDataUrlPDF(pdfUrl);
            } else {
                throw new Error('Unsupported PDF URL format: ' + pdfUrl);
            }
            
        } catch (error) {
            console.error('Error loading PDF:', error);
            this.updateTimingDisplay(`❌ PDF Load Failed: ${error.message}`, 'error');
            this.updateUITimingError('PDF Load', error.message);
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: error.message
            });
        }
    }

    async loadFileSystemPDF(fileUrl) {
        console.log('Loading PDF from file system:', fileUrl);
        
        // Notify UWP that we're starting to load
        this.sendMessageToUWP({
            type: 'PDF_LOADING',
            message: 'Starting to load PDF from file system',
            url: fileUrl,
            method: 'file-system'
        });
        
        // Method 1: Direct iframe (most reliable for file URLs)
        if (this.useIframeMethod()) {
            this.loadPDFInIframe(fileUrl);
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
                
                // Complete timing measurement
                const duration = this.completeTiming('PDF Preview');
                
                this.sendMessageToUWP({
                    type: 'PDF_LOADED',
                    message: 'PDF loaded successfully with PDF.js',
                    pages: pdf.numPages,
                    method: 'pdfjs',
                    loadTimingMs: duration ? Math.round(duration) : null
                });
                
            } catch (fetchError) {
                console.warn('PDF.js fetch failed, falling back to iframe:', fetchError);
                this.loadPDFInIframe(fileUrl);
            }
        } else {
            // Fallback to iframe if PDF.js not available
            this.loadPDFInIframe(fileUrl);
        }
    }

    async loadVirtualHostPDF(virtualUrl) {
        // Legacy support for virtual host mapping (when MFE is local)
        console.log('Loading PDF from virtual host:', virtualUrl);
        
        this.sendMessageToUWP({
            type: 'PDF_LOADING',
            message: 'Loading PDF from virtual host',
            url: virtualUrl,
            method: 'virtual-host'
        });
        
        this.loadPDFInIframe(virtualUrl);
    }

    async loadDataUrlPDF(dataUrl) {
        // Support for Base64 data URLs
        console.log('Loading PDF from data URL...');
        if (window.pdfjsLib) {
            try {
                const loadingTask = window.pdfjsLib.getDocument(dataUrl);
                const pdf = await loadingTask.promise;
                await this.renderPDF(pdf);
                
                // Complete timing measurement
                const duration = this.completeTiming('PDF Preview');
                
                this.sendMessageToUWP({
                    type: 'PDF_LOADED',
                    message: 'PDF loaded from data URL',
                    pages: pdf.numPages,
                    method: 'pdfjs-dataurl',
                    loadTimingMs: duration ? Math.round(duration) : null
                });
            } catch (error) {
                console.warn('PDF.js data URL failed, falling back to iframe:', error);
                this.loadPDFDataUrlInIframe(dataUrl);
            }
        } else {
            this.loadPDFDataUrlInIframe(dataUrl);
        }
    }

    useIframeMethod() {
        // For cloud-hosted MFE, iframe is often more reliable for file URLs
        // This can be made configurable based on your MFE environment
        // return true for cloud deployment, can be false for local testing
        return true; // Default to iframe method for maximum compatibility
    }

    loadPDFInIframe(pdfUrl) {
        console.log('Loading PDF in iframe:', pdfUrl);
        
        const container = document.getElementById('pdf-container');
        if (container) {
            container.innerHTML = `
                <iframe 
                    src="${pdfUrl}" 
                    width="100%" 
                    height="100%" 
                    style="border: none;"
                    onload="uwpPDFIntegration.onPDFLoaded('iframe')"
                    onerror="uwpPDFIntegration.onPDFError('iframe')">
                </iframe>
            `;
        } else {
            console.warn('PDF container element not found. Expected element with id="pdf-container"');
            this.sendMessageToUWP({
                type: 'PDF_ERROR',
                error: 'PDF container element not found'
            });
        }
    }

    loadPDFDataUrlInIframe(dataUrl) {
        console.log('Loading PDF data URL in iframe...');
        
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
                } else {
                    console.warn('PDF container element not found');
                    this.onPDFError('container-missing');
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
        if (!container) {
            console.warn('PDF container element not found');
            return;
        }

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
        
        // Complete timing measurement
        const duration = this.completeTiming('PDF Preview');
        
        this.sendMessageToUWP({
            type: 'PDF_LOADED',
            message: `PDF loaded in ${method} successfully`,
            method: method,
            loadTimingMs: duration ? Math.round(duration) : null
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
        if (this.isInUWP) {
            console.log('Sending message to UWP:', message);
            window.chrome.webview.postMessage(JSON.stringify(message));
        }
    }

    notifyReady() {
        // Let UWP know the MFE is ready
        if (this.isInUWP) {
            setTimeout(() => {
                this.sendMessageToUWP({
                    type: 'MFE_READY',
                    message: 'MFE initialized and ready for PDF loading'
                });
                console.log('Notified UWP that MFE is ready');
            }, 1000);
        }
    }

    // === Chunked Streaming Methods ===
    
    startChunkedPDFLoad(message) {
        console.log(`Starting chunked PDF load: ${message.fileName} (${message.totalChunks} chunks, ${message.totalSize} bytes)`);
        console.log(`Base64 size: ${message.base64Size} chars, Chunk size: ${message.chunkSize} chars`);
        
        // Start timing measurement for chunked loading
        this.startTiming('Chunked PDF Load');
        
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
            
            // Complete timing measurement for chunked loading
            const duration = this.completeTiming('Chunked PDF Preview');
            
            // Reset chunking state
            this.resetChunkingState();
            
            // Notify UWP of successful assembly
            this.sendMessageToUWP({
                type: 'PDF_LOADED',
                message: `Chunked PDF assembled and loaded: ${fileName}`,
                method: 'chunked',
                chunks: expectedChunks,
                transferMethod: 'ChunkedStream',
                loadTimingMs: duration ? Math.round(duration) : null
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
    
    initializeTimingDisplay() {
        // Create timing display element if it doesn't exist
        if (!document.getElementById('pdf-timing-display')) {
            console.log('Creating timing display element...');
            const timingDisplay = document.createElement('div');
            timingDisplay.id = 'pdf-timing-display';
            timingDisplay.style.cssText = `
                position: fixed;
                top: 10px;
                right: 10px;
                background: rgba(0, 0, 0, 0.8);
                color: white;
                padding: 8px 12px;
                border-radius: 6px;
                font-family: 'Segoe UI', Arial, sans-serif;
                font-size: 12px;
                z-index: 10000;
                box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
                min-width: 200px;
                display: block;
                border: 2px solid #007acc;
            `;
            timingDisplay.innerHTML = '⏱️ PDF Preview Timing Ready';
            document.body.appendChild(timingDisplay);
            
            console.log('Timing display created and added to body');
            
            // Show it briefly to confirm it's working
            setTimeout(() => {
                timingDisplay.style.display = 'none';
                console.log('Initial timing display hidden');
            }, 3000);
        } else {
            console.log('Timing display element already exists');
        }
    }
    
    startTiming(operation = 'PDF Load') {
        this.timingState.requestStartTime = performance.now();
        this.updateTimingDisplay(`🔄 ${operation} started...`, 'loading');
        this.updateUITimingStart(operation);
        console.log(`⏱️ Started timing for: ${operation}`);
    }
    
    completeTiming(operation = 'PDF Preview') {
        if (this.timingState.requestStartTime) {
            this.timingState.previewCompleteTime = performance.now();
            this.timingState.lastRequestDuration = this.timingState.previewCompleteTime - this.timingState.requestStartTime;
            
            const durationMs = Math.round(this.timingState.lastRequestDuration);
            const durationSec = (this.timingState.lastRequestDuration / 1000).toFixed(2);
            
            this.updateTimingDisplay(`✅ ${operation}: ${durationSec}s (${durationMs}ms)`, 'success');
            this.updateUITimingComplete(operation, durationMs, parseFloat(durationSec));
            console.log(`⏱️ ${operation} completed in: ${durationSec}s (${durationMs}ms)`);
            
            // Send timing info to UWP
            this.sendMessageToUWP({
                type: 'PDF_TIMING',
                operation: operation,
                durationMs: durationMs,
                durationSec: parseFloat(durationSec),
                timestamp: new Date().toISOString()
            });
            
            // Send timing data to server for analytics
            this.sendTimingToServer(operation, durationMs, parseFloat(durationSec));
            
            // Auto-hide after 5 seconds
            setTimeout(() => {
                this.hideTimingDisplay();
            }, 5000);
            
            return this.timingState.lastRequestDuration;
        }
        return null;
    }
    
    updateTimingDisplay(message, status = 'info') {
        console.log(`Updating timing display: ${message} (${status})`);
        const display = document.getElementById('pdf-timing-display');
        if (display) {
            display.style.display = 'block';
            display.innerHTML = message;
            
            // Update colors based on status
            switch (status) {
                case 'loading':
                    display.style.background = 'rgba(0, 123, 255, 0.9)'; // Blue
                    break;
                case 'success':
                    display.style.background = 'rgba(40, 167, 69, 0.9)'; // Green
                    break;
                case 'error':
                    display.style.background = 'rgba(220, 53, 69, 0.9)'; // Red
                    break;
                default:
                    display.style.background = 'rgba(0, 0, 0, 0.8)'; // Dark
            }
            console.log('Timing display updated successfully');
        } else {
            console.error('Timing display element not found!');
            // Try to recreate it
            this.initializeTimingDisplay();
            // Try again
            const newDisplay = document.getElementById('pdf-timing-display');
            if (newDisplay) {
                newDisplay.style.display = 'block';
                newDisplay.innerHTML = message;
                console.log('Timing display recreated and updated');
            }
        }
    }
    
    hideTimingDisplay() {
        const display = document.getElementById('pdf-timing-display');
        if (display) {
            display.style.display = 'none';
        }
    }
    
    showTimingStats() {
        if (this.timingState.lastRequestDuration) {
            const durationSec = (this.timingState.lastRequestDuration / 1000).toFixed(2);
            const durationMs = Math.round(this.timingState.lastRequestDuration);
            this.updateTimingDisplay(`📊 Last: ${durationSec}s (${durationMs}ms)`, 'info');
        }
    }
    
    async sendTimingToServer(operation, durationMs, durationSec) {
        try {
            // Only send to server if we're not in UWP environment (to avoid CORS issues)
            if (!this.isInUWP && window.location.hostname === 'localhost') {
                const response = await fetch('/api/timing', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        operation: operation,
                        durationMs: durationMs,
                        durationSec: durationSec,
                        method: 'uwp-integration',
                        timestamp: new Date().toISOString(),
                        userAgent: navigator.userAgent
                    })
                });
                
                if (response.ok) {
                    console.log('⏱️ Timing data sent to server successfully');
                } else {
                    console.warn('⏱️ Failed to send timing data to server:', response.status);
                }
            }
        } catch (error) {
            console.warn('⏱️ Error sending timing data to server:', error.message);
        }
    }

    // === UI Timing Display Methods ===
    
    initializeUITimingDisplay() {
        // Show the timing section
        const timingSection = document.getElementById('timingSection');
        if (timingSection) {
            timingSection.style.display = 'block';
            console.log('UI timing display initialized');
        }
    }
    
    updateUITimingStart(operation) {
        this.uiTimingState.currentOperation = operation;
        
        // Update current operation
        const operationStatus = document.getElementById('operationStatus');
        const operationDetails = document.getElementById('operationDetails');
        const currentOperationItem = document.getElementById('currentOperation');
        
        if (operationStatus) operationStatus.textContent = operation;
        if (operationDetails) operationDetails.textContent = `Started at ${new Date().toLocaleTimeString()}`;
        if (currentOperationItem) {
            currentOperationItem.className = 'timing-item loading';
        }
        
        console.log(`UI: Started ${operation}`);
    }
    
    updateUITimingComplete(operation, durationMs, durationSec) {
        // Update statistics
        this.uiTimingState.totalLoads++;
        this.uiTimingState.durations.push(durationMs);
        
        // Calculate average
        const avgDuration = this.uiTimingState.durations.reduce((a, b) => a + b, 0) / this.uiTimingState.durations.length;
        const avgDurationSec = (avgDuration / 1000).toFixed(2);
        
        // Update current operation (show as complete)
        const operationStatus = document.getElementById('operationStatus');
        const operationDetails = document.getElementById('operationDetails');
        const currentOperationItem = document.getElementById('currentOperation');
        
        if (operationStatus) operationStatus.textContent = `✅ ${operation}`;
        if (operationDetails) operationDetails.textContent = `Completed in ${durationSec}s`;
        if (currentOperationItem) {
            currentOperationItem.className = 'timing-item success';
        }
        
        // Update last timing
        const lastDuration = document.getElementById('lastDuration');
        const lastMethod = document.getElementById('lastMethod');
        
        if (lastDuration) lastDuration.textContent = `${durationSec}s`;
        if (lastMethod) lastMethod.textContent = `${operation} (${durationMs}ms)`;
        
        // Update average timing
        const avgDurationEl = document.getElementById('avgDuration');
        const totalLoads = document.getElementById('totalLoads');
        
        if (avgDurationEl) avgDurationEl.textContent = `${avgDurationSec}s`;
        if (totalLoads) totalLoads.textContent = `${this.uiTimingState.totalLoads} loads completed`;
        
        console.log(`UI: Completed ${operation} - ${durationSec}s (Average: ${avgDurationSec}s)`);
        
        // Reset current operation after 3 seconds
        setTimeout(() => {
            if (operationStatus) operationStatus.textContent = 'Ready';
            if (operationDetails) operationDetails.textContent = 'Waiting for next PDF load request';
            if (currentOperationItem) {
                currentOperationItem.className = 'timing-item';
            }
        }, 3000);
    }
    
    updateUITimingError(operation, error) {
        const operationStatus = document.getElementById('operationStatus');
        const operationDetails = document.getElementById('operationDetails');
        const currentOperationItem = document.getElementById('currentOperation');
        
        if (operationStatus) operationStatus.textContent = `❌ ${operation}`;
        if (operationDetails) operationDetails.textContent = `Error: ${error}`;
        if (currentOperationItem) {
            currentOperationItem.className = 'timing-item error';
        }
        
        console.log(`UI: Error in ${operation} - ${error}`);
        
        // Reset after 5 seconds
        setTimeout(() => {
            if (operationStatus) operationStatus.textContent = 'Ready';
            if (operationDetails) operationDetails.textContent = 'Waiting for next PDF load request';
            if (currentOperationItem) {
                currentOperationItem.className = 'timing-item';
            }
        }, 5000);
    }
}

// Initialize when page loads
let uwpPDFIntegration;
document.addEventListener('DOMContentLoaded', () => {
    uwpPDFIntegration = new UWPPDFIntegration();
});

// Debug functions for testing (accessible from browser console)
window.debugTimingDisplay = {
    show: () => {
        if (uwpPDFIntegration) {
            uwpPDFIntegration.updateTimingDisplay('🧪 Debug Test Display', 'info');
        }
    },
    test: () => {
        if (uwpPDFIntegration) {
            uwpPDFIntegration.startTiming('Debug Test');
            setTimeout(() => {
                uwpPDFIntegration.completeTiming('Debug Test Complete');
            }, 1000);
        }
    },
    testUI: () => {
        if (uwpPDFIntegration) {
            uwpPDFIntegration.updateUITimingStart('UI Debug Test');
            setTimeout(() => {
                uwpPDFIntegration.updateUITimingComplete('UI Debug Test', 1500, 1.5);
            }, 1500);
        }
    },
    testError: () => {
        if (uwpPDFIntegration) {
            uwpPDFIntegration.updateUITimingStart('Error Test');
            setTimeout(() => {
                uwpPDFIntegration.updateUITimingError('Error Test', 'Simulated error for testing');
            }, 500);
        }
    },
    recreate: () => {
        if (uwpPDFIntegration) {
            // Remove existing display
            const existing = document.getElementById('pdf-timing-display');
            if (existing) existing.remove();
            // Recreate it
            uwpPDFIntegration.initializeTimingDisplay();
        }
    }
};

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
