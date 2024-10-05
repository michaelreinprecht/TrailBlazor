using System.Runtime.InteropServices;

namespace TrailBlazorServerApp.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]  // Ensure 1-byte alignment like in C
    public struct Command
    {
        public byte Direction;  
        public int Speed;        
        public byte Stop;   
    }

}
