using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct Header
    {
        public byte VersionNumber;  // Version of the protocol
        public byte MessageType;    // Type of message (e.g., ControlCommand, MoveToCommand)
        public byte Length;
        public byte Flags;

        // Method to set the ACK flag in the Flags field
        public void SetAckFlag(bool ack)
        {
            if (ack)
            {
                Flags |= (byte)1 << 0; // Set bit 0 to 1
            }
            else
            {
                Flags &= unchecked((byte)~(1 << 0)); // Clear bit 0
            }
        }

        // Method to check if the ACK flag is set
        public bool IsAckFlagSet()
        {
            return (Flags & (1 << 0)) != 0; // Check if bit 0 is set
        }
    }
}
