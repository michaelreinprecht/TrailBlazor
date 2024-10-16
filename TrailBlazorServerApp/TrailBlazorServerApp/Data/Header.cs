using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct Header
    {
        public byte VersionNumber;  // Version of the protocol
        public byte MessageType;    // Type of message (e.g., ControlCommand, MoveToCommand)
        public byte SequenceNumber;
        public byte Length;
        public uint Flags;

        public void SetFlag(Flag flag)
        {
            if (!IsFlagSet(flag))
            {
                Flags |= (uint)flag;
            }
        }

        public void ClearFlag(Flag flag)
        {
            if (IsFlagSet(flag))
            {
                Flags &= unchecked((uint)~flag);
            }
        }

        public bool IsFlagSet(Flag flag)
        {
            return (Flags & (uint)flag) != 0;
        }
    }
}
