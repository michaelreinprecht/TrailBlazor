using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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

            // Make sure we received the right amount of bytes
            if (result.Buffer.Length == Marshal.SizeOf(typeof(Command)))
            {
                Command command;

                // Pin the incoming byte array in memory and convert it to the Command struct
                IntPtr ptr = Marshal.AllocHGlobal(result.Buffer.Length);
                try
                {
                    Marshal.Copy(result.Buffer, 0, ptr, result.Buffer.Length);
                    command = Marshal.PtrToStructure<Command>(ptr);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                Console.WriteLine($"Received command from {sender}: Direction = {command.Direction}, Stop = {command.Stop}, StopBool = {command.StopBool}");

                // Raise event to notify the UI
                OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {command.Direction}, Stop = {command.Stop}, StopBool = {command.StopBool}");
            }
            else
            {
                Console.WriteLine($"Received {result.Buffer.Length} bytes from {sender}, expected {Marshal.SizeOf(typeof(Command))} bytes.");
            }
        }
    }



    //Sending commands (struct) to esp devices
    public async Task SendCommandToEspDevices(Command command)
    {
        //Test if struct size matches, remove later
        Console.WriteLine($"Size of Command: {Marshal.SizeOf(typeof(Command))}");  // Log size of struct

        // Calculate the size of the struct
        int size = Marshal.SizeOf(command);
        byte[] commandBytes = new byte[size];

        // Pin the command struct in memory and copy it to byte array
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(command, ptr, true);
            Marshal.Copy(ptr, commandBytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpClient.SendAsync(commandBytes, commandBytes.Length, deviceEndpoint);
            Console.WriteLine($"Command sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
        }
    }


}
