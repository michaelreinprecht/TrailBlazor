using System.Net;
using System.Net.Sockets;
using System.Text;

public class UdpCommunicationService
{
    private readonly List<IPEndPoint> _esp32Devices = new()
    {
        new IPEndPoint(IPAddress.Parse("192.168.4.101"), 4210), // ESP32C3 #1
        //new IPEndPoint(IPAddress.Parse("192.168.4.102"), 4210), // ESP32C3 #2
    };

    private const int ListeningPort = 4211; // Port to listen for ESP responses
    public event Action<string>? OnMessageReceived; // Event to notify when a message is received

    // Sending messages to multiple ESP devices
    public async Task SendMessageToEspDevices(string message)
    {
        using (var udpClient = new UdpClient())
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);

            foreach (var deviceEndpoint in _esp32Devices)
            {
                await udpClient.SendAsync(messageBytes, messageBytes.Length, deviceEndpoint);
                Console.WriteLine($"Message sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
            }
        }
    }

    // Start listening for responses (this runs in the background)
    public async Task StartListeningForResponses(CancellationToken cancellationToken)
    {
        using (var udpListener = new UdpClient(ListeningPort))
        {
            Console.WriteLine($"Listening for responses on port {ListeningPort}...");

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await udpListener.ReceiveAsync();
                var receivedMessage = Encoding.UTF8.GetString(result.Buffer);
                var sender = result.RemoteEndPoint;

                Console.WriteLine($"Received message from {sender}: {receivedMessage}");

                // Raise event to notify the UI
                OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - {receivedMessage}");
            }
        }
    }
}
