using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct MoveToCommand
    {
        public int x;
        public int y;
    }

}
