using System.Net;
using System.Net.Sockets;
using System.Text;
using TrailBlazorServerApp.Data;

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

    // Sending messages (ASCII) to multiple ESP devices
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
            var sender = result.RemoteEndPoint;

            // Convert bytes to Command struct
            var command = new Command
            {
                Direction = (char)result.Buffer[0],      // First byte is the direction char
                Stop = BitConverter.ToInt32(result.Buffer, 1) // Next 4 bytes are the stop int
            };

            Console.WriteLine($"Received command from {sender}: Direction = {command.Direction}, Stop = {command.Stop}");

            // Raise event to notify the UI
            OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {command.Direction}, Stop = {command.Stop}");
        }
    }


    //Sending commands (struct) to esp devices
    public async Task SendCommandToEspDevices(Command command)
    {
        var commandBytes = new byte[5]; // 1 byte for char + 4 bytes for int

        // Convert 'direction' (char) to byte
        commandBytes[0] = (byte)command.Direction;

        // Convert 'stop' (int) to bytes
        BitConverter.GetBytes(command.Stop).CopyTo(commandBytes, 1);

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpClient.SendAsync(commandBytes, commandBytes.Length, deviceEndpoint);
            Console.WriteLine($"Command sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
        }
    }

}
