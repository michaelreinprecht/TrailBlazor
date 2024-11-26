using System.Net;
using System.Runtime.InteropServices;
using TrailBlazorServerApp.Data;

public class AppProtocolService
{
    private static byte LastReceivedSequenceNumber { get; set; } = byte.MaxValue;
    public static byte SequenceNumber { get; set; } = byte.MinValue;
    private bool IsFirstPacket { get; set; } = true;

    private readonly UdpCommunicationService _udpService;
    private readonly List<IPEndPoint> _esp32Devices = new()
    {
        new IPEndPoint(IPAddress.Parse("192.168.4.100"), 4444), // ESP32C3 #1
        //new IPEndPoint(IPAddress.Parse("192.168.4.101"), 4444), // ESP32C3 #2
    };

    public event Action<string>? OnMessageReceived; // Event to notify when a message is received
    public event Action<bool>? OnAckReceived;

    public AppProtocolService(UdpCommunicationService udpService)
    {
        _udpService = udpService;
        _udpService.OnDataReceived += HandleReceivedData; // Subscribe to the raw data event
    }

    public async Task StartListening(CancellationToken cancellationToken)
    {
        if (_udpService != null)
        {
            await _udpService.StartListening(cancellationToken);
        } else
        {
            Console.WriteLine("Missing udpService to start Listening!");
        }
    }

    private void HandleReceivedData(IPEndPoint sender, byte[] data)
    {
        if (data.Length >= Marshal.SizeOf(typeof(Header)))
        {
            Header receivedHeader = DeserializeStruct<Header>(data, 0);
            Console.WriteLine($"Received header: Version = {receivedHeader.VersionNumber}, Message Type = {receivedHeader.MessageType}, Flags = {receivedHeader.Flags}, SequenceNumber = {receivedHeader.SequenceNumber}");

            //if (IsSequenceNumberValid(receivedHeader.SequenceNumber))
            //{
            //    LastReceivedSequenceNumber = receivedHeader.SequenceNumber;

                // Handle message types here based on your application logic...
                ProcessMessage(sender, receivedHeader, data);
            //}
            //else
            //{
            //    Console.WriteLine($"Ignoring packet with sequence number {receivedHeader.SequenceNumber}");
            //}
        }
        else
        {
            Console.WriteLine($"Received an invalid packet of size {data.Length} from {sender}");
        }
    }

    bool IsSequenceNumberValid(byte sequenceNumber)
    {
        if (IsFirstPacket)
        {
            IsFirstPacket = false;
            return true;
        }
        // If the new sequence number is greater than the last one, it's valid
        if (sequenceNumber > LastReceivedSequenceNumber)
        {
            return true;
        }
        // Special case for wraparound
        byte distance = (byte)(LastReceivedSequenceNumber - sequenceNumber);
        if (distance < 127)
        {
            return true;
        }
        // Otherwise, ignore the packet
        return false;
    }

    private async void ProcessMessage(IPEndPoint sender, Header header, byte[] data)
    {
        // If an ACK_FLAG is set, send an acknowledgment response, but never send an ACK for an ACK
        if (header.IsFlagSet(Flag.ACK_Flag) && header.MessageType != (byte)MessageType.ACK)
        {
            Console.WriteLine("ACK flag set, sending acknowledgment response");
            await SendProtocolMessage(MessageType.ACK);
        }

        int headerSize = Marshal.SizeOf(typeof(Header));
        int expectedPayloadLength = header.Length;
        int actualPayloadLength = data.Length - headerSize;

        if (actualPayloadLength == expectedPayloadLength)
        {
            // Handle the payload based on message type
            switch ((MessageType)header.MessageType)
            {
                case MessageType.ERR:
                    //TODO: Handle error messages
                    Console.WriteLine($"Received error message!");
                    OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Received error message!");
                    break;
                case MessageType.ACK:
                    //TODO: Handle ack messages
                    Console.WriteLine($"Received ACK message!");
                    OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Received ACK message!");
                    break;
                case MessageType.ControlCommand:
                    if (expectedPayloadLength == Marshal.SizeOf(typeof(ControlCommand)))
                    {
                        ControlCommand controlCommand = DeserializeStruct<ControlCommand>(data, headerSize);
                        Console.WriteLine($"Received ControlCommand from {sender}: Direction = {controlCommand.Direction}, Speed = {controlCommand.Speed}, Stop = {controlCommand.Stop}");

                        // Notify UI
                        OnMessageReceived?.Invoke($"From {sender.Address}:{sender.Port} - Direction = {controlCommand.Direction}, Speed = {controlCommand.Speed}, Stop = {controlCommand.Stop}");
                    }
                    else
                    {
                        Console.WriteLine($"Invalid size for ControlCommand received from {sender}. Expected size: {Marshal.SizeOf(typeof(ControlCommand))}");
                    }
                    break;

                case MessageType.MoveToCommand:
                    if (expectedPayloadLength == Marshal.SizeOf(typeof(MoveToCommand)))
                    {
                        MoveToCommand moveToCommand = DeserializeStruct<MoveToCommand>(data, headerSize);
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
                    Console.WriteLine($"Unknown message type received from {sender}: {header.MessageType}");
                    break;
            }
        }
        else
        {
            Console.WriteLine($"Payload length mismatch: expected {expectedPayloadLength}, but got {actualPayloadLength}");
        }


    }

    public async Task SendProtocolMessage<T>(string targetIP, MessageType type, T payload, HashSet<Flag>? flags = null) where T : struct
    {
        IPEndPoint targetDevice = new IPEndPoint(IPAddress.Parse(targetIP), 4444);

        Header header = CreateHeader(type, payload, flags);
        byte[] packetBytes = SerializePacket(header, payload);

        await _udpService.SendDataAsync(packetBytes, targetDevice);
    }

    public async Task SendProtocolMessage(MessageType type, HashSet<Flag>? flags = null)
    {
        Header header = CreateHeader(type, flags);
        byte[] packetBytes = SerializePacket(header);

        foreach (var deviceEndpoint in _esp32Devices)
        {
            await _udpService.SendDataAsync(packetBytes, deviceEndpoint);
        }
    }

    private Header CreateHeader<T>(MessageType type, T payload, HashSet<Flag>? flags = null) where T : struct
    {
        Header header = new Header
        {
            VersionNumber = 1,
            MessageType = (byte)type,
            Flags = byte.MinValue,
            SequenceNumber = SequenceNumber++,
            Length = (byte)Marshal.SizeOf(typeof(T)),
        };
        if (flags != null)
        {
            foreach (Flag flag in flags)
            {
                header.SetFlag(flag);
            }
        }
        return header;
    }

    private Header CreateHeader(MessageType type, HashSet<Flag>? flags = null)
    {
        Header header = new Header
        {
            VersionNumber = 1,
            MessageType = (byte)type,
            Flags = byte.MinValue,
            SequenceNumber = SequenceNumber++,
            Length = 0,
        };
        if (flags != null)
        {
            foreach (Flag flag in flags)
            {
                header.SetFlag(flag);
            }
        }
        return header;
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

    // Overloaded method to serialize only the header into a byte array (for ACK messages)
    private byte[] SerializePacket(Header header)
    {
        int headerSize = Marshal.SizeOf(typeof(Header));
        byte[] packetBytes = new byte[headerSize];

        IntPtr ptr = Marshal.AllocHGlobal(headerSize); // Allocate memory for the header only
        try
        {
            // Copy the header into the byte array
            Marshal.StructureToPtr(header, ptr, true);
            Marshal.Copy(ptr, packetBytes, 0, headerSize);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr); // Free the allocated memory
        }

        return packetBytes;
    }

    private T DeserializeStruct<T>(byte[] data, int offset) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(data, offset, ptr, size);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
