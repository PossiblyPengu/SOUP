using System;

class Program
{
    static void Main()
    {
        // Request high-performance GPU on hybrid systems
        Environment.SetEnvironmentVariable("SHIM_MCCOMPAT", "0x800000001");
        
        using var game = new FriendshipDungeonMG.Game1();
        game.Run();
    }
}
