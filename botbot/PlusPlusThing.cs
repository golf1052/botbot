using System.Collections.Generic;

namespace botbot
{
    public class PlusPlusThing
    {
        public string Id { get; set; }
        public int Score { get; set; }
        public Dictionary<string, int> Reasons { get; set; }

        public PlusPlusThing()
        {
            Reasons = new Dictionary<string, int>();
        }
    }
}
