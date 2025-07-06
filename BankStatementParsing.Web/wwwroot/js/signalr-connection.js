// SignalR connection for real-time updates
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/processingHub")
    .withAutomaticReconnect()
    .build();

// Connection status indicators
const statusIcon = document.getElementById('status-icon');
const statusText = document.getElementById('status-text');
const notificationToast = document.getElementById('notification-toast');
const toastTitle = document.getElementById('toast-title');
const toastBody = document.getElementById('toast-body');
const toastTime = document.getElementById('toast-time');

// Update connection status
function updateConnectionStatus(status, message) {
    statusText.textContent = message;
    statusIcon.className = `fas fa-circle ${status}`;
}

// Show toast notification
function showToast(title, message, type = 'info') {
    toastTitle.textContent = title;
    toastBody.textContent = message;
    toastTime.textContent = new Date().toLocaleTimeString();
    
    // Update toast icon based on type
    const toastHeader = document.querySelector('#notification-toast .toast-header i');
    switch (type) {
        case 'success':
            toastHeader.className = 'fas fa-check-circle text-success me-2';
            break;
        case 'error':
            toastHeader.className = 'fas fa-exclamation-circle text-danger me-2';
            break;
        case 'warning':
            toastHeader.className = 'fas fa-exclamation-triangle text-warning me-2';
            break;
        default:
            toastHeader.className = 'fas fa-info-circle text-primary me-2';
    }
    
    const toast = new bootstrap.Toast(notificationToast);
    toast.show();
}

// Connection event handlers
connection.onclose(async () => {
    updateConnectionStatus('text-danger', 'Disconnected');
    showToast('Connection Lost', 'Real-time updates disconnected. Attempting to reconnect...', 'warning');
});

connection.onreconnecting((error) => {
    updateConnectionStatus('text-warning', 'Reconnecting...');
});

connection.onreconnected((connectionId) => {
    updateConnectionStatus('text-success', 'Connected');
    showToast('Connected', 'Real-time updates are now active.', 'success');
    
    // Join user group (you might want to get the actual user ID)
    connection.invoke("JoinUserGroup", "1");
});

// Processing event handlers
connection.on("ProcessingStarted", function (fileName) {
    showToast('Processing Started', `Started processing: ${fileName}`, 'info');
    updateProcessingStatus(fileName, 'started');
});

connection.on("ProcessingProgress", function (fileName, progress, stage) {
    showToast('Processing Progress', `${fileName}: ${stage} (${progress}%)`, 'info');
    updateProcessingProgress(fileName, progress, stage);
});

connection.on("ProcessingCompleted", function (fileName, transactionCount, success) {
    if (success) {
        showToast('Processing Complete', `Successfully processed ${fileName} with ${transactionCount} transactions`, 'success');
    } else {
        showToast('Processing Failed', `Failed to process ${fileName}`, 'error');
    }
    updateProcessingStatus(fileName, success ? 'completed' : 'failed');
});

connection.on("ProcessingFailed", function (fileName, error) {
    showToast('Processing Failed', `Error processing ${fileName}: ${error}`, 'error');
    updateProcessingStatus(fileName, 'failed');
});

connection.on("TransactionAdded", function (transaction) {
    // Update transaction lists if visible
    if (typeof updateTransactionList === 'function') {
        updateTransactionList(transaction);
    }
    
    // Update dashboard metrics if visible
    if (typeof updateDashboardMetrics === 'function') {
        updateDashboardMetrics();
    }
});

connection.on("MetricsUpdated", function (metrics) {
    // Update dashboard metrics
    if (typeof updateDashboardMetrics === 'function') {
        updateDashboardMetrics(metrics);
    }
});

// Helper functions for updating UI
function updateProcessingStatus(fileName, status) {
    const statusElement = document.querySelector(`[data-file="${fileName}"] .processing-status`);
    if (statusElement) {
        statusElement.textContent = status;
        statusElement.className = `processing-status badge ${getStatusClass(status)}`;
    }
}

function updateProcessingProgress(fileName, progress, stage) {
    const progressElement = document.querySelector(`[data-file="${fileName}"] .progress-bar`);
    if (progressElement) {
        progressElement.style.width = `${progress}%`;
        progressElement.textContent = `${stage} - ${progress}%`;
    }
}

function getStatusClass(status) {
    switch (status) {
        case 'started':
            return 'bg-primary';
        case 'completed':
            return 'bg-success';
        case 'failed':
            return 'bg-danger';
        default:
            return 'bg-secondary';
    }
}

// Start the connection
connection.start().then(function () {
    updateConnectionStatus('text-success', 'Connected');
    showToast('Connected', 'Real-time updates are now active.', 'success');
    
    // Join user group (you might want to get the actual user ID)
    connection.invoke("JoinUserGroup", "1");
}).catch(function (err) {
    updateConnectionStatus('text-danger', 'Connection Failed');
    showToast('Connection Failed', 'Could not connect to real-time updates.', 'error');
    console.error(err.toString());
});

// Export connection for use in other scripts
window.processingConnection = connection;