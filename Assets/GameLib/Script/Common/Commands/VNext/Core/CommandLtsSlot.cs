#nullable enable

namespace Game.Commands.VNext
{
    public enum CommandLtsSlot
    {
        None = 0,
        Scope = 10,
        Actor = 20,
        CommandRoot = 30,
        RootActor = 40,
        CallerActor = 50,
        ContextA = 100,
        ContextB = 110,
        ContextC = 120,
        ContextD = 130,
    }

    static class CommandLtsSlotUtility
    {
        public const int SlotCount = 8;

        public static bool IsContextSlot(CommandLtsSlot slot)
        {
            return slot is CommandLtsSlot.ContextA or CommandLtsSlot.ContextB or CommandLtsSlot.ContextC or CommandLtsSlot.ContextD;
        }

        public static int ToStorageIndex(CommandLtsSlot slot)
        {
            return slot switch
            {
                CommandLtsSlot.Actor => 0,
                CommandLtsSlot.CommandRoot => 1,
                CommandLtsSlot.RootActor => 2,
                CommandLtsSlot.CallerActor => 3,
                CommandLtsSlot.ContextA => 4,
                CommandLtsSlot.ContextB => 5,
                CommandLtsSlot.ContextC => 6,
                CommandLtsSlot.ContextD => 7,
                _ => -1,
            };
        }
    }
}
