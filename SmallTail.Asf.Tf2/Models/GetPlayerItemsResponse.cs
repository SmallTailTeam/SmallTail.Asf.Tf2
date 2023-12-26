using Newtonsoft.Json;

namespace SmallTail.Asf.TfBackpack.Models;

public class Result
{
    [JsonProperty("num_backpack_slots")]
    public int NumBackpackSlots { get; set; }
}

public class GetPlayerItemsResponse
{
    [JsonProperty("result")]
    public Result Result { get; set; }
}