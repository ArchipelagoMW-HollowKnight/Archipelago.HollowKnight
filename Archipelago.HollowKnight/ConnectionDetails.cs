namespace Archipelago.HollowKnight
{
    public record ConnectionDetails
    {
        public string ServerUrl { get; set; } = "archipelago.gg";
        public int ServerPort { get; set; } = 38281;
        public string SlotName { get; set; }
        public string ServerPassword { get; set; }
        public int ItemIndex { get; set; }
    }
}
