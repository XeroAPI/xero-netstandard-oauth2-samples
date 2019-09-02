using System.Collections.Generic;

namespace XeroOAuth2Sample.Models
{
    public class OutstandingInvoicesViewModel
    {
        public string Name { get; set; }

        public Dictionary<string, int> Data { get; set; }
    }
}
