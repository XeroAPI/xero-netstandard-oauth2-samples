using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroOAuth2Sample.Example;
using XeroOAuth2Sample.Extensions;

namespace XeroOAuth2Sample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();

            services.TryAddSingleton(new XeroConfiguration
            {
                ClientId = Configuration["Xero:ClientId"],
                ClientSecret = Configuration["Xero:ClientSecret"]
            });

            services.TryAddSingleton<IXeroClient, XeroClient>();
            services.TryAddSingleton<IAccountingApi, AccountingApi>();
            services.TryAddSingleton<MemoryTokenStore>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "XeroSignIn";
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "XeroIdentity";

                // Clean up cookies that don't match in local MemoryTokenStore.
                // In reality you wouldn't need this, as you'd be storing tokens in a real data store somewhere peripheral, so they won't go missing between restarts
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var tokenStore = context.HttpContext.RequestServices.GetService<MemoryTokenStore>();
                        var token = await tokenStore.GetAccessTokenAsync(context.Principal.XeroUserId());

                        if (token == null)
                        {
                            context.RejectPrincipal(); 
                        }
                    }
                };
            })
            .AddOpenIdConnect("XeroSignIn", options =>
            {
                options.Authority = "https://identity.xero.com";

                options.ClientId = Configuration["Xero:ClientId"];
                options.ClientSecret = Configuration["Xero:ClientSecret"];

                options.ResponseType = "code";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.CallbackPath = "/signin-oidc";

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = OnTokenValidated()
                };
            })
            .AddOpenIdConnect("XeroSignUp", options =>
            {
                options.Authority = "https://identity.xero.com";

                options.ClientId = Configuration["Xero:ClientId"];
                options.ClientSecret = Configuration["Xero:ClientSecret"];

                options.ResponseType = "code";

                options.Scope.Clear();
                options.Scope.Add("offline_access");
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("accounting.settings");
                options.Scope.Add("accounting.transactions");

                options.CallbackPath = "/signup-oidc";

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = OnTokenValidated()
                };
            });

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        private static Func<TokenValidatedContext, Task> OnTokenValidated()
        {
            return context =>
            {
                var tokenStore = context.HttpContext.RequestServices.GetService<MemoryTokenStore>();

                var token = new XeroOAuth2Token
                {
                    AccessToken = context.TokenEndpointResponse.AccessToken,
                    RefreshToken = context.TokenEndpointResponse.RefreshToken,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Convert.ToDouble(context.TokenEndpointResponse.ExpiresIn))
                };

                tokenStore.SetToken(context.Principal.XeroUserId(), token);

                return Task.CompletedTask;
            };
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
