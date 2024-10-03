using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct Command
    {
        public byte Direction;  // Same as 'char' in C
        public int Stop;        // Same as 'int' in C
        public bool StopBool;   // Same as 'bool' in C
    }

}
