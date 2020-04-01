using System.Collections.Generic;

namespace XeroOAuth2Sample_MVC_PKCE.Models
{
    public class OutstandingInvoicesViewModel
    {
        public string Name { get; set; }

        public Dictionary<string, int> Data { get; set; }
    }
}
