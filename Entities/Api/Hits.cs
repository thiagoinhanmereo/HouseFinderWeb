using System.Collections.Generic;

namespace HouseFinderWebBot.Api
{
    public class Hits
    {
        public int found { get; set; }
        public int start { get; set; }
        public ICollection<Hit> Hit { get; set; }
    }
}