(function () {
    if (!window.signalR) {
        console.error("SignalR client library not found!");
        return;
    }

    // Tự bắt địa chỉ hiện tại (IP, cổng, https...)
    const chatHubUrl = `${location.protocol}//${location.host}/chatHub`;
    const notificationHubUrl = `${location.protocol}//${location.host}/notificationHub`;

    const chatConnection = new signalR.HubConnectionBuilder()
        .withUrl(chatHubUrl)
        .configureLogging(signalR.LogLevel.Information)
        .build();

    chatConnection.start()
        .then(() => console.log("✅ Connected to:", chatHubUrl))
        .catch(err => console.error("❌ ChatHub error:", err));

    const notificationConnection = new signalR.HubConnectionBuilder()
        .withUrl(notificationHubUrl)
        .configureLogging(signalR.LogLevel.Information)
        .build();

    notificationConnection.start()
        .then(() => console.log("✅ Connected to:", notificationHubUrl))
        .catch(err => console.error("❌ NotificationHub error:", err));
})();
