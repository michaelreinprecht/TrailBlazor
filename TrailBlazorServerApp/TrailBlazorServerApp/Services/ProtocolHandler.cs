using System.Net;
using System.Runtime.InteropServices;
using TrailBlazorServerApp.Data;

public class ProtocolHandler
{
    private readonly UdpCommunicationService _udpService;
    private readonly List<IPEndPoint> _esp32Devices = new()
    {
        new IPEndPoint(IPAddress.Parse("192.168.4.100"), 4444) // ESP32C3 #1
        //new IPEndPoint(IPAddress.Parse("192.168.4.101"), 4444) // ESP32C3 #1
    };

    private byte LastReceivedSequenceNumber { get; set; } = byte.MaxValue;
    private bool IsFirstPacket { get; set; } = true;
    private byte SequenceNumber { get; set; }

    public event Action<string>? OnMessageReceived; // Notify UI when a message is processed

    public ProtocolHandler(UdpCommunicationService udpService)
    {
        _udpService = udpService;
        _udpService.OnPacketReceived += HandleIncomingPacket;
    }

    private async void HandleIncomingPacket(IPEndPoint sender, byte[] packetData)
    {
        if (packetData.Length >= Marshal.SizeOf(typeof(Header)))
        {
            Header receivedHeader = DeserializeStruct<Header>(packetData, 0);

            if (IsSequenceNumberValid(receivedHeader.SequenceNumber))
            {
                LastReceivedSequenceNumber = receivedHeader.SequenceNumber;
                int headerSize = Marshal.SizeOf(typeof(Header));
                int payloadSize = receivedHeader.Length;
                int actualPayloadLength = packetData.Length - headerSize;

                if (actualPayloadLength == payloadSize)
                {
                    await ProcessMessage(sender, receivedHeader, packetData, headerSize);
                }
                else
                {
                    Console.WriteLine($"Payload length mismatch: expected {payloadSize}, but got {actualPayloadLength}");
                }
            }
        }
    }

    private async Task ProcessMessage(IPEndPoint sender, Header header, byte[] packetData, int headerSize)
    {
        switch ((MessageType)header.MessageType)
        {
            case MessageType.ACK:
                OnMessageReceived?.Invoke($"Received ACK from {sender.Address}:{sender.Port}");
                break;
            case MessageType.ControlCommand:
                ControlCommand controlCommand = DeserializeStruct<ControlCommand>(packetData, headerSize);
                OnMessageReceived?.Invoke($"Control Command: Direction = {controlCommand.Direction}, Speed = {controlCommand.Speed}, Stop = {controlCommand.Stop}");
                break;
            case MessageType.MoveToCommand:
                MoveToCommand moveToCommand = DeserializeStruct<MoveToCommand>(packetData, headerSize);
                OnMessageReceived?.Invoke($"MoveTo Command: X = {moveToCommand.x}, Y = {moveToCommand.y}");
                break;
            default:
                OnMessageReceived?.Invoke($"Unknown Message Type from {sender.Address}:{sender.Port}");
                break;
        }

        if (header.IsFlagSet(Flag.ACK_Flag))
        {
            await SendAck(sender, header.VersionNumber);
        }
    }

    public async Task SendAck(IPEndPoint recipient, byte versionNumber)
    {
        Header ackHeader = new Header
        {
            VersionNumber = versionNumber,
            MessageType = (byte)MessageType.ACK,
            Flags = 0,
            SequenceNumber = SequenceNumber++,
            Length = 0
        };

        byte[] ackPacket = SerializeStruct(ackHeader);
        await _udpService.SendPacketAsync(ackPacket, recipient);
    }

    private bool IsSequenceNumberValid(byte sequenceNumber)
    {
        if (IsFirstPacket)
        {
            IsFirstPacket = false;
            return true;
        }

        return sequenceNumber > LastReceivedSequenceNumber || (LastReceivedSequenceNumber >= 245 && sequenceNumber <= 10);
    }

    public async Task SendControlCommandAsync(ControlCommand command)
    {
        Header header = new Header
        {
            VersionNumber = 1,
            MessageType = (byte)MessageType.ControlCommand,
            Flags = 0,
            SequenceNumber = SequenceNumber++,
            Length = (byte)Marshal.SizeOf(typeof(ControlCommand))
        };

        byte[] commandPacket = SerializeStruct(header)
            .Concat(SerializeStruct(command)).ToArray();

        foreach (var device in _esp32Devices)
        {
            await _udpService.SendPacketAsync(commandPacket, device);
        }
    }

    // Helper method to serialize a byte array from a struct

    public static byte[] SerializeStruct<T>(T packet) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] packetBytes = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(packet, ptr, true);
            Marshal.Copy(ptr, packetBytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        return packetBytes;
    }

    // Helper method to deserialize a struct from a byte array
    public static T DeserializeStruct<T>(byte[] data, int offset = 0) where T : struct
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
