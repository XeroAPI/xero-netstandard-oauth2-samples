using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Client;
using XeroOAuth2Sample.Example;
using XeroOAuth2Sample.Extensions;
using XeroOAuth2Sample.Models;

namespace XeroOAuth2Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly MemoryTokenStore _tokenStore;
        private readonly IXeroClient _xeroClient;
        private readonly IAccountingApi _accountingApi;

        public HomeController(MemoryTokenStore tokenStore, IXeroClient xeroClient, IAccountingApi accountingApi)
        {
            _tokenStore = tokenStore;
            _xeroClient = xeroClient;
            _accountingApi = accountingApi;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("OutstandingInvoices");
            }

            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> OutstandingInvoices()
        {
            var token = await _tokenStore.GetAccessTokenAsync(User.XeroUserId());

            var connections = await _xeroClient.GetConnectionsAsync(token);

            if (!connections.Any())
            {
                return RedirectToAction("NoTenants");
            }

            var data = new Dictionary<string, int>();

            foreach (var connection in connections)
            {
                var accessToken = token.AccessToken;
                var tenantId = connection.TenantId.ToString();

                var organisations = await _accountingApi.GetOrganisationsAsync(accessToken, tenantId);
                var organisationName = organisations._Organisations[0].Name;

                var outstandingInvoices = await _accountingApi.GetInvoicesAsync(accessToken, tenantId, statuses: "AUTHORISED", where: "Type == \"ACCREC\"");

                data[organisationName] = outstandingInvoices._Invoices.Count;
            }

            var model = new OutstandingInvoicesViewModel
            {
                Name = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}",
                Data = data
            };

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult NoTenants()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AddConnection()
        {
            // Signing out of this client app allows the user to be taken through the Xero Identity connection flow again, allowing more organisations to be connected
            // The user will not need to log in again because they're only signed out of our app, not Xero.
            await HttpContext.SignOutAsync(); 

            return RedirectToAction("SignUp");
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = "XeroSignUp")]
        public IActionResult SignUp()
        {
            return RedirectToAction("OutstandingInvoices");
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = "XeroSignIn")]
        public IActionResult SignIn()
        {
            return RedirectToAction("OutstandingInvoices");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
