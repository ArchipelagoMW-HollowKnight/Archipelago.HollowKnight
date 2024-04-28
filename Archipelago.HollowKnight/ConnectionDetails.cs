namespace Archipelago.HollowKnight
{
    public record ConnectionDetails
    {
        public string ServerUrl { get; set; }
        public int ServerPort { get; set; }
        public string SlotName { get; set; }
        public string ServerPassword { get; set; }
        public int ItemIndex { get; set; }

        public string RoomSeed { get; set; }
        public long Seed { get; set; }

        public bool AlwaysShowItems { get; set; }
    }
}
