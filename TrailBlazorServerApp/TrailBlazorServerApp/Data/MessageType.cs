namespace TrailBlazorServerApp.Data
{
    public enum MessageType : byte
    {
        ERR = 0x00,
        ACK = 0x01,
        ControlCommand = 0x03,  
        MoveToCommand = 0x04 
    }
}
