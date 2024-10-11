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
        new IPEndPoint(IPAddress.Parse("192.168.4.100"), 4444), // ESP32C3 #1
        //new IPEndPoint(IPAddress.Parse("192.168.4.101"), 4444), // ESP32C3 #2
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

            // Check if we received at least a header
            if (result.Buffer.Length >= Marshal.SizeOf(typeof(Header)))
            {
                // Deserialize the header
                Header receivedHeader = DeserializeStruct<Header>(result.Buffer, 0);
                Console.WriteLine($"Received header: Version = {receivedHeader.VersionNumber}, Message Type = {receivedHeader.MessageType}, Flags = {receivedHeader.Flags}");

                int headerSize = Marshal.SizeOf(typeof(Header));
                int expectedPayloadLength = receivedHeader.Length;
                int actualPayloadLength = result.Buffer.Length - headerSize;

                if (actualPayloadLength == expectedPayloadLength)
                {
                    // Handle the payload based on message type
                    switch ((MessageType)receivedHeader.MessageType)
                    {
                        case MessageType.ControlCommand:
                            if (expectedPayloadLength == Marshal.SizeOf(typeof(ControlCommand)))
                            {
                                ControlCommand command = DeserializeStruct<ControlCommand>(result.Buffer, headerSize);
                                Console.WriteLine($"Received Command from {sender}: Direction = {command.Direction}, Speed = {command.Speed}, Stop = {command.Stop}");

                                // Notify UI
                                OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {command.Direction}, Speed = {command.Speed}, Stop = {command.Stop}");
                            }
                            else
                            {
                                Console.WriteLine($"Invalid size for ControlCommand received from {sender}. Expected size: {Marshal.SizeOf(typeof(ControlCommand))}");
                            }
                            break;

                        case MessageType.MoveToCommand:
                            if (expectedPayloadLength == Marshal.SizeOf(typeof(MoveToCommand)))
                            {
                                MoveToCommand moveToCommand = DeserializeStruct<MoveToCommand>(result.Buffer, headerSize);
                                Console.WriteLine($"Received MoveToCommand from {sender}: X = {moveToCommand.x}, Y = {moveToCommand.y}");

                                // Notify UI
                                OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - X = {moveToCommand.x}, Y = {moveToCommand.y}");
                            }
                            else
                            {
                                Console.WriteLine($"Invalid size for MoveToCommand received from {sender}. Expected size: {Marshal.SizeOf(typeof(MoveToCommand))}");
                            }
                            break;

                        default:
                            Console.WriteLine($"Unknown message type received from {sender}: {receivedHeader.MessageType}");
                            break;
                    }
                }
                else
                {
                    Console.WriteLine($"Payload length mismatch: expected {expectedPayloadLength}, but got {actualPayloadLength}");
                }
            }
            else
            {
                Console.WriteLine($"Received an invalid packet of size {result.Buffer.Length} from {sender}");
            }
        }
    }

    public async Task SendDataToEspDevices<T>(MessageType type, T packet) where T : struct
    {
        int payloadSize = Marshal.SizeOf(typeof(T));

        Header header = new Header
        {
            VersionNumber = 1,
            MessageType = (byte)type,
            Flags = (byte)Flags.NACK, //Currently just never ask for an ACK -> TODO send ACK if this is Flags.ACK when receiving
            Length = (byte)payloadSize,
        };

        //Serialize header and packet
        byte[] packetBytes = SerializePacket(header, packet);

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpClient.SendAsync(packetBytes, packetBytes.Length, deviceEndpoint);  // Send the packet over UDP
            Console.WriteLine($"Command of type {type} sent to {deviceEndpoint.Address}:{deviceEndpoint.Port}");
        }
    }


    // Helper method to serialize a struct into a byte array
    private byte[] SerializePacket<T>(Header header, T packet) where T : struct
    {
        int headerSize = Marshal.SizeOf(typeof(Header));
        int payloadSize = Marshal.SizeOf(typeof(T));         // Get the size of the struct
        int packetLength = headerSize + payloadSize;
        byte[] packetBytes = new byte[packetLength];      // +1 byte for the type at the start


        IntPtr ptr = Marshal.AllocHGlobal(packetLength);      // Allocate memory for the struct
        try
        {
            // Copy the header into the byte array
            Marshal.StructureToPtr(header, ptr, true);
            Marshal.Copy(ptr, packetBytes, 0, headerSize);

            // Copy the payload into the byte array after the header
            Marshal.StructureToPtr(packet, ptr + headerSize, true);
            Marshal.Copy(ptr + headerSize, packetBytes, headerSize, payloadSize);
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
