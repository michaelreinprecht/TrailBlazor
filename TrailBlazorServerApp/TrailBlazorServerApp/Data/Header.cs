using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct Header
    {
        public byte VersionNumber;  // Version of the protocol
        public byte MessageType;    // Type of message (e.g., ControlCommand, MoveToCommand)
        public byte Flags;        // Optional flags
        public byte Length;
    }
}
