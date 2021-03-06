﻿using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MvcCoreApiWithDocs.Models;
using Swashbuckle.AspNetCore.Swagger;
using System;
using IdentityServer4.AccessTokenValidation;

namespace MvcCoreApiWithDocs
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var readPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).
                RequireAuthenticatedUser().
                RequireClaim("scope", "read").Build();

            var writePolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).
                RequireAuthenticatedUser().
                RequireClaim("scope", "write").Build();

            services.AddSingleton<IContactRepository, InMemoryContactRepository>();
            services.AddMvcCore(opt =>
                {
                    opt.Filters.Add(new AuthorizeFilter("ReadPolicy"));
                }).AddAuthorization(o =>
                {
                    o.AddPolicy("ReadPolicy", readPolicy);
                    o.AddPolicy("WritePolicy", writePolicy);
                }).AddDataAnnotations().
                AddJsonFormatters().
                AddApiExplorer();

            // set up embedded identity server
            services.AddIdentityServer().
                AddTestClients().
                AddTestResources().
                AddDeveloperSigningCredential();

            services
                .AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
                .AddIdentityServerAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme, o =>
                {
                    o.Authority = "http://localhost:5000/openid";
                    o.RequireHttpsMetadata = false;
                });

            services.AddSwaggerGen(options => {
                options.SwaggerDoc("v1", new Info
                {
                    Title = "Contacts API",
                    Version = "v1",
                    Description = "Used to exchange contact information"
                });

                options.AddSecurityDefinition("openid", new OAuth2Scheme
                {
                    Type = "openid",
                    Flow = "Client Credentials",
                    Scopes = new Dictionary<string, string> { { "read", "Read access"}, {"write", "Write access"} },
                    TokenUrl = "http://localhost:5000/openid/token/connect"
                });

                options.OperationFilter<ScopesDefinitionOperationFilter>(new Dictionary<string, string> { { "ReadPolicy", "read" }, { "WritePolicy", "write" } });

                var xmlDocs = Path.Combine(AppContext.BaseDirectory, "MvcCoreApiWithDocs.xml");
                options.IncludeXmlComments(xmlDocs);
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.Map("/openid", id => {
                // use embedded identity server to issue tokens
                id.UseIdentityServer();
            });

            app.Map("/api", api => {
                // consume the JWT tokens in the API
                api.UseAuthentication();
                api.UseSwagger();
                api.UseMvc();
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "V1 Docs");
            });
        }
    }
}
