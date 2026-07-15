// Real-time classroom WebSocket client
// Handles connection, message routing, and state management

class ClassroomClient {
    constructor(classroomId, dotnetHelper) {
        this.classroomId = classroomId;
        this.dotnetHelper = dotnetHelper;
        this.ws = null;
        this.connected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectDelay = 1000;
        this.messageHandlers = {};
    }

    // Connect to the classroom WebSocket
    async connect() {
        try {
            const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            // No token param - auth comes via cookie
            const url = `${protocol}//${window.location.host}/api/classroom/${this.classroomId}/ws`;

            this.ws = new WebSocket(url);
            this.ws.onopen = () => this.onOpen();
            this.ws.onmessage = (event) => this.onMessage(event);
            this.ws.onerror = (event) => this.onError(event);
            this.ws.onclose = () => this.onClose();

            return new Promise((resolve, reject) => {
                const timeout = setTimeout(() => reject(new Error('Connection timeout')), 5000);
                const checkConnection = () => {
                    if (this.connected) {
                        clearTimeout(timeout);
                        resolve();
                    } else {
                        setTimeout(checkConnection, 100);
                    }
                };
                checkConnection();
            });
        } catch (error) {
            console.error('Failed to connect:', error);
            throw error;
        }
    }

    onOpen() {
        console.log('Connected to classroom');
        this.connected = true;
        this.reconnectAttempts = 0;
        this.emit('connected');
    }

    onMessage(event) {
        try {
            const message = JSON.parse(event.data);
            console.log('Received message:', message.type);

            switch (message.type) {
                case 'chat':
                    this.emit('chat', message);
                    break;
                case 'directive':
                    this.emit('directive', message);
                    break;
                case 'state':
                    this.emit('state', message);
                    break;
                case 'user_joined':
                    this.emit('user_joined', message);
                    break;
                case 'user_left':
                    this.emit('user_left', message);
                    break;
                case 'system':
                    this.emit('system', message);
                    break;
                default:
                    console.warn('Unknown message type:', message.type);
            }
        } catch (error) {
            console.error('Failed to parse message:', error);
        }
    }

    onError(event) {
        console.error('WebSocket error:', event);
        this.emit('error', event);
    }

    onClose() {
        console.log('Disconnected from classroom');
        this.connected = false;
        this.emit('disconnected');

        // Attempt to reconnect
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
            console.log(`Attempting to reconnect in ${delay}ms...`);
            setTimeout(() => this.connect().catch(err => console.error('Reconnect failed:', err)), delay);
        }
    }

    // Send a chat message
    sendChat(content) {
        if (!this.connected) {
            console.error('Not connected');
            return;
        }

        const message = {
            type: 'chat',
            content: content,
            timestamp: new Date().toISOString()
        };

        this.ws.send(JSON.stringify(message));
    }

    // Send a directive (teacher only)
    sendDirective(toUser, action, payload) {
        if (!this.connected) {
            console.error('Not connected');
            return;
        }

        const message = {
            type: 'directive',
            toUser: toUser,
            action: action,
            payload: payload
        };

        this.ws.send(JSON.stringify(message));
    }

    // Register event handler
    on(eventType, handler) {
        if (!this.messageHandlers[eventType]) {
            this.messageHandlers[eventType] = [];
        }
        this.messageHandlers[eventType].push(handler);
    }

    // Emit event to handlers
    emit(eventType, data) {
        if (this.messageHandlers[eventType]) {
            this.messageHandlers[eventType].forEach(handler => {
                try {
                    handler(data);
                } catch (error) {
                    console.error(`Error in ${eventType} handler:`, error);
                }
            });
        }

        // Call .NET methods based on message type
        if (this.dotnetHelper) {
            try {
                switch (eventType) {
                    case 'chat':
                        this.dotnetHelper.invokeMethodAsync('OnChatMessage', data.senderName || 'Unknown', data.content || '');
                        break;
                    case 'directive':
                        this.dotnetHelper.invokeMethodAsync('OnDirective', data.action || data.tool || '', data.payload || data.data || {});
                        break;
                    case 'user_joined':
                        this.dotnetHelper.invokeMethodAsync('OnUserJoined', data.userName || '');
                        break;
                    case 'user_left':
                        this.dotnetHelper.invokeMethodAsync('OnUserLeft', data.userName || '');
                        break;
                }
            } catch (error) {
                console.warn(`Failed to invoke .NET method for ${eventType}:`, error);
            }
        }
    }

    // Disconnect cleanly
    disconnect() {
        if (this.ws) {
            this.maxReconnectAttempts = 0;  // Prevent reconnection
            this.ws.close();
        }
    }

    isConnected() {
        return this.connected;
    }
}

// Global instance management
window.classroomClients = {};

window.initClassroomClient = async function(classroomId, dotnetHelper) {
    try {
        const client = new ClassroomClient(classroomId, dotnetHelper);
        await client.connect();
        window.classroomClients[classroomId] = client;
        console.log(`Initialized classroom client for ${classroomId}`);
        return client;
    } catch (error) {
        console.error(`Failed to initialize classroom client for ${classroomId}:`, error);
        return null;
    }
};

window.getClassroomClient = function(classroomId) {
    return window.classroomClients[classroomId];
};

window.sendChat = function(classroomId, content) {
    const client = window.classroomClients[classroomId];
    if (client) {
        client.sendChat(content);
    } else {
        console.warn(`No client found for classroom ${classroomId}`);
    }
};

window.sendDirective = function(classroomId, toUser, action, payload) {
    const client = window.classroomClients[classroomId];
    if (client) {
        client.sendDirective(toUser, action, payload);
    } else {
        console.warn(`No client found for classroom ${classroomId}`);
    }
};

window.disconnectClassroom = function(classroomId) {
    const client = window.classroomClients[classroomId];
    if (client) {
        client.disconnect();
        delete window.classroomClients[classroomId];
        console.log(`Disconnected classroom client for ${classroomId}`);
    }
};
