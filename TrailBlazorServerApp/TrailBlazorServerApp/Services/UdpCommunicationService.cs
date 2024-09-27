using System.Net;
using System.Net.Sockets;
using System.Text;

public class UdpCommunicationService
{
    private readonly List<IPEndPoint> _esp32Devices = new()
    {
        new IPEndPoint(IPAddress.Parse("192.168.4.9"), 4444), // ESP32C3 #1
        //new IPEndPoint(IPAddress.Parse("192.168.4.10"), 4444), // ESP32C3 #2
    };

    private const int ListeningPort = 3333; // Port to listen for ESP responses
    private readonly UdpClient _udpClient; // Reusable UdpClient for both sending and receiving

    public event Action<string>? OnMessageReceived; // Event to notify when a message is received

    public UdpCommunicationService()
    {
        // Bind the UdpClient to the listening port once
        _udpClient = new UdpClient(ListeningPort);
    }

    // Sending messages to multiple ESP devices
    public async Task SendMessageToEspDevices(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpClient.SendAsync(messageBytes, messageBytes.Length, deviceEndpoint);
            Console.WriteLine($"Message sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
        }
    }

    // Start listening for responses (this runs in the background)
    public async Task StartListeningForResponses(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Listening for responses on port {ListeningPort}...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await _udpClient.ReceiveAsync();
            var receivedMessage = Encoding.UTF8.GetString(result.Buffer);
            var sender = result.RemoteEndPoint;

            Console.WriteLine($"Received message from {sender}: {receivedMessage}");

            // Raise event to notify the UI
            OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - {receivedMessage}");
        }
    }
}
