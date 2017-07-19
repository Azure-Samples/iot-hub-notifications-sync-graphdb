namespace SyncGraphDbApp
{
    using Newtonsoft.Json;

    struct Location
    {
        [JsonProperty("building")]
        public string Building { get; set; }

        [JsonProperty("floor")]
        public string Floor { get; set; }

        [JsonProperty("room")]
        public string Room { get; set; }

        public override string ToString()
        {
            return $"building: {Building}, floor: {Floor}, room: {Room}";
        }
    }
}
