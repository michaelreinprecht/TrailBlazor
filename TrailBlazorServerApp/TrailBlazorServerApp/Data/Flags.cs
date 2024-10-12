namespace TrailBlazorServerApp.Data
{
    public enum Flags : byte
    {
        ACK_FLAG=0x00,    // The message with this flag requires acknowledgement
        NACK_FLAG =0x01,  // The message with this flag does not require acknowledgement
    }
}
