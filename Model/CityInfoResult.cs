namespace DafTools.Model
{
    public record CityInfoResult(string Name, string Uf, int DafCode)
    {
        public int PibCode { get; set; }
    }
}
