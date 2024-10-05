using System.Data;
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

    // Start listening for responses (this runs in the background)
    public async Task StartListeningForResponses(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Listening for responses on port {ListeningPort}...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await _udpClient.ReceiveAsync();
            var sender = result.RemoteEndPoint;

            // Check if we received at least 1 byte for the struct type
            if (result.Buffer.Length >= 1)
            {
                // First byte is the enum for the struct type
                StructType structType = (StructType)result.Buffer[0];

                // Handle different struct types based on the command type
                switch (structType)
                {
                    case StructType.Command:
                        if (result.Buffer.Length == 1 + Marshal.SizeOf(typeof(Command))) // 1 byte for type + struct size
                        {
                            Command command = DeserializeStruct<Command>(result.Buffer, 1);
                            Console.WriteLine($"Received Command from {sender}: Direction = {command.Direction}, Speed = {command.Speed}, Stop = {command.Stop}");

                            // Notify UI
                            OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {command.Direction}, Speed = {command.Speed}, Stop = {command.Stop}");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid size for Command received from {sender}. Expected size: {1 + Marshal.SizeOf(typeof(Command))}");
                        }
                        break;

                    case StructType.LocationData:
                        if (result.Buffer.Length == 1 + Marshal.SizeOf(typeof(LocationData))) // 1 byte for type + struct size
                        {
                            LocationData locationData = DeserializeStruct<LocationData>(result.Buffer, 1);
                            Console.WriteLine($"Received LocationData from {sender}: X = {locationData.x}, Y = {locationData.y}");

                            // Notify UI
                            OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {locationData.x}, Speed = {locationData.y}");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid size for CommandType2 received from {sender}. Expected size: {1 + Marshal.SizeOf(typeof(LocationData))}");
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown command type received from {sender}: {structType}");
                        break;
                }
            }
            else
            {
                Console.WriteLine($"Received an invalid packet of size {result.Buffer.Length} from {sender}");
            }
        }
    }

    public async Task SendDataToEspDevices<T>(StructType type, T packet) where T : struct
    {
        byte[] packetBytes = SerializeStruct(type, packet);  // Serialize the struct

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpClient.SendAsync(packetBytes, packetBytes.Length, deviceEndpoint);  // Send the packet over UDP
            Console.WriteLine($"Command of type {type} sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
        }
    }


    // Helper method to serialize a struct into a byte array
    private byte[] SerializeStruct<T>(StructType type, T packet) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));         // Get the size of the struct
        byte[] packetBytes = new byte[size + 1];      // +1 byte for the type at the start

        packetBytes[0] = (byte)type;                  // First byte is the type

        IntPtr ptr = Marshal.AllocHGlobal(size);      // Allocate memory for the struct
        try
        {
            Marshal.StructureToPtr(packet, ptr, true);  // Convert struct to pointer
            Marshal.Copy(ptr, packetBytes, 1, size);    // Copy the struct starting from index 1
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);                 // Free the allocated memory
        }

        return packetBytes;
    }


    // Helper method to deserialize a struct from a byte array
    private T DeserializeStruct<T>(byte[] data, int offset) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(data, offset, ptr, size);  // Copy data starting from the offset
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

}
