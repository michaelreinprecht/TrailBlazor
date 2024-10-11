namespace TrailBlazorServerApp.Data
{
    public enum Flags : byte
    {
        ACK=0x00,    // The message with this flag requires acknowledgement
        NACK =0x01,  // The message with this flag does not require acknowledgement
    }
}
