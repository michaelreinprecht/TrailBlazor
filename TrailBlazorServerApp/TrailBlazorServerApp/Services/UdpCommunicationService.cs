using System.Net;
using System.Net.Sockets;

public class UdpCommunicationService
{
    private readonly UdpClient _udpClient;
    private const int ListeningPort = 3333; // Port to listen for ESP responses

    public event Action<IPEndPoint, byte[]>? OnDataReceived; // Event to notify when raw data is received

    public UdpCommunicationService()
    {
        _udpClient = new UdpClient(ListeningPort);
    }

    // Start listening for incoming data packets
    public async Task StartListening(CancellationToken cancellationToken)
    {
        Console.WriteLine("UDP service is listening for data...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await _udpClient.ReceiveAsync();
            OnDataReceived?.Invoke(result.RemoteEndPoint, result.Buffer); // Notify subscribers with the raw data
        }
    }

    // Send raw data to a specified endpoint
    public async Task SendDataAsync(byte[] data, IPEndPoint endpoint)
    {
        await _udpClient.SendAsync(data, data.Length, endpoint);
        Console.WriteLine($"Data sent to {endpoint.Address}:{endpoint.Port}");
    }
}

