﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.Resource;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.IdentityModel.Extensions.AspNetCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Identity.Web
{
    /// <summary>
    /// Extensions for <see cref="AuthenticationBuilder"/> for startup initialization of web APIs.
    /// </summary>
    public static partial class MicrosoftIdentityWebApiAuthenticationBuilderExtensions
    {
        /// <summary>
        /// Protects the web API with Microsoft identity platform (formerly Azure AD v2.0).
        /// This method expects the configuration file will have a section, named "AzureAd" as default, with the necessary settings to initialize authentication options.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to which to add this configuration.</param>
        /// <param name="configuration">The configuration instance.</param>
        /// <param name="configSectionName">The configuration section with the necessary settings to initialize authentication options.</param>
        /// <param name="jwtBearerScheme">The JWT bearer scheme name to be used. By default it uses "Bearer".</param>
        /// <param name="subscribeToJwtBearerMiddlewareDiagnosticsEvents">
        /// Set to true if you want to debug, or just understand the JWT bearer events.
        /// </param>
        /// <returns>The authentication builder to chain.</returns>
        public static MicrosoftIdentityWebApiAuthenticationBuilderWithConfiguration AddMicrosoftIdentityWebApi(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        string configSectionName = Constants.AzureAd,
        string jwtBearerScheme = JwtBearerDefaults.AuthenticationScheme,
        bool subscribeToJwtBearerMiddlewareDiagnosticsEvents = false)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configSectionName == null)
            {
                throw new ArgumentNullException(nameof(configSectionName));
            }

            IConfigurationSection configurationSection = configuration.GetSection(configSectionName);

            return builder.AddMicrosoftIdentityWebApi(
                configurationSection,
                jwtBearerScheme,
                subscribeToJwtBearerMiddlewareDiagnosticsEvents);
        }

        /// <summary>
        /// Protects the web API with Microsoft identity platform (formerly Azure AD v2.0).
        /// This method expects the configuration file will have a section, named "AzureAd" as default, with the necessary settings to initialize authentication options.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to which to add this configuration.</param>
        /// <param name="configurationSection">The configuration second from which to fill-in the options.</param>
        /// <param name="jwtBearerScheme">The JWT bearer scheme name to be used. By default it uses "Bearer".</param>
        /// <param name="subscribeToJwtBearerMiddlewareDiagnosticsEvents">
        /// Set to true if you want to debug, or just understand the JWT bearer events.
        /// </param>
        /// <returns>The authentication builder to chain.</returns>
        public static MicrosoftIdentityWebApiAuthenticationBuilderWithConfiguration AddMicrosoftIdentityWebApi(
            this AuthenticationBuilder builder,
            IConfigurationSection configurationSection,
            string jwtBearerScheme = JwtBearerDefaults.AuthenticationScheme,
            bool subscribeToJwtBearerMiddlewareDiagnosticsEvents = false)
        {
            if (configurationSection == null)
            {
                throw new ArgumentNullException(nameof(configurationSection));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            AddMicrosoftIdentityWebApiImplementation(
                builder,
                options => configurationSection.Bind(options),
                options => configurationSection.Bind(options),
                jwtBearerScheme,
                subscribeToJwtBearerMiddlewareDiagnosticsEvents);

            return new MicrosoftIdentityWebApiAuthenticationBuilderWithConfiguration(
                builder.Services,
                jwtBearerScheme,
                options => configurationSection.Bind(options),
                options => configurationSection.Bind(options),
                configurationSection);
        }

        /// <summary>
        /// Protects the web API with Microsoft identity platform (formerly Azure AD v2.0).
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to which to add this configuration.</param>
        /// <param name="configureJwtBearerOptions">The action to configure <see cref="JwtBearerOptions"/>.</param>
        /// <param name="configureMicrosoftIdentityOptions">The action to configure the <see cref="MicrosoftIdentityOptions"/>.</param>
        /// <param name="jwtBearerScheme">The JWT bearer scheme name to be used. By default it uses "Bearer".</param>
        /// <param name="subscribeToJwtBearerMiddlewareDiagnosticsEvents">
        /// Set to true if you want to debug, or just understand the JWT bearer events.</param>
        /// <returns>The authentication builder to chain.</returns>
        public static MicrosoftIdentityWebApiAuthenticationBuilder AddMicrosoftIdentityWebApi(
            this AuthenticationBuilder builder,
            Action<JwtBearerOptions> configureJwtBearerOptions,
            Action<MicrosoftIdentityOptions> configureMicrosoftIdentityOptions,
            string jwtBearerScheme = JwtBearerDefaults.AuthenticationScheme,
            bool subscribeToJwtBearerMiddlewareDiagnosticsEvents = false)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configureJwtBearerOptions == null)
            {
                throw new ArgumentNullException(nameof(configureJwtBearerOptions));
            }

            if (configureMicrosoftIdentityOptions == null)
            {
                throw new ArgumentNullException(nameof(configureMicrosoftIdentityOptions));
            }

            AddMicrosoftIdentityWebApiImplementation(
                builder,
                configureJwtBearerOptions,
                configureMicrosoftIdentityOptions,
                jwtBearerScheme,
                subscribeToJwtBearerMiddlewareDiagnosticsEvents);

            return new MicrosoftIdentityWebApiAuthenticationBuilder(
                builder.Services,
                jwtBearerScheme,
                configureJwtBearerOptions,
                configureMicrosoftIdentityOptions,
                null);
        }

        private static void AddMicrosoftIdentityWebApiImplementation(
            AuthenticationBuilder builder,
            Action<JwtBearerOptions> configureJwtBearerOptions,
            Action<MicrosoftIdentityOptions> configureMicrosoftIdentityOptions,
            string jwtBearerScheme,
            bool subscribeToJwtBearerMiddlewareDiagnosticsEvents)
        {
            builder.AddJwtBearer(jwtBearerScheme, configureJwtBearerOptions);
            builder.Services.Configure(jwtBearerScheme, configureMicrosoftIdentityOptions);

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();
            builder.Services.TryAddSingleton<MicrosoftIdentityIssuerValidatorFactory>();
            builder.Services.AddRequiredScopeAuthorization();
            builder.Services.AddOptions<AadIssuerValidatorOptions>();

            if (subscribeToJwtBearerMiddlewareDiagnosticsEvents)
            {
                builder.Services.AddSingleton<IJwtBearerMiddlewareDiagnostics, JwtBearerMiddlewareDiagnostics>();
            }

            // Change the authentication configuration to accommodate the Microsoft identity platform endpoint (v2.0).
            builder.Services.AddOptions<JwtBearerOptions>(jwtBearerScheme)
                .Configure<IServiceProvider, IOptionsMonitor<MergedOptions>, IOptionsMonitor<MicrosoftIdentityOptions>, IOptions<MicrosoftIdentityOptions>>((
                options,
                serviceProvider,
                mergedOptionsMonitor,
                msIdOptionsMonitor,
                msIdOptions) =>
                {
                    MicrosoftIdentityBaseAuthenticationBuilder.SetIdentityModelLogger(serviceProvider);
                    MergedOptions mergedOptions = mergedOptionsMonitor.Get(jwtBearerScheme);
                    MergedOptions.UpdateMergedOptionsFromJwtBearerOptions(options, mergedOptions);
                    MergedOptions.UpdateMergedOptionsFromMicrosoftIdentityOptions(msIdOptions.Value, mergedOptions);
                    MergedOptions.UpdateMergedOptionsFromMicrosoftIdentityOptions(msIdOptionsMonitor.Get(jwtBearerScheme), mergedOptions);

                    MergedOptionsValidation.Validate(mergedOptions);

                    if (string.IsNullOrWhiteSpace(options.Authority))
                    {
                        options.Authority = AuthorityHelpers.BuildAuthority(mergedOptions);
                    }

                    // This is a Microsoft identity platform web API
                    options.Authority = AuthorityHelpers.EnsureAuthorityIsV2(options.Authority);

                    if (options.TokenValidationParameters.AudienceValidator == null
                     && options.TokenValidationParameters.ValidAudience == null
                     && options.TokenValidationParameters.ValidAudiences == null)
                    {
                        RegisterValidAudience registerAudience = new RegisterValidAudience();
                        registerAudience.RegisterAudienceValidation(
                            options.TokenValidationParameters,
                            mergedOptions);
                    }

                    // If the developer registered an IssuerValidator, do not overwrite it
                    if (options.TokenValidationParameters.ValidateIssuer && options.TokenValidationParameters.IssuerValidator == null)
                    {
                        // Instead of using the default validation (validating against a single tenant, as we do in line of business apps),
                        // we inject our own multi-tenant validation logic (which even accepts both v1.0 and v2.0 tokens)
                        MicrosoftIdentityIssuerValidatorFactory microsoftIdentityIssuerValidatorFactory =
                        serviceProvider.GetRequiredService<MicrosoftIdentityIssuerValidatorFactory>();

                        options.TokenValidationParameters.IssuerValidator =
                        microsoftIdentityIssuerValidatorFactory.GetAadIssuerValidator(options.Authority).Validate;
                    }

                    // If you provide a token decryption certificate, it will be used to decrypt the token
                    if (mergedOptions.TokenDecryptionCertificates != null)
                    {
                        DefaultCertificateLoader.UserAssignedManagedIdentityClientId = mergedOptions.UserAssignedManagedIdentityClientId;
                        IEnumerable<X509Certificate2?> certificates = DefaultCertificateLoader.LoadAllCertificates(mergedOptions.TokenDecryptionCertificates);
                        IEnumerable<X509SecurityKey> keys = certificates.Select(c => new X509SecurityKey(c));
                        options.TokenValidationParameters.TokenDecryptionKeys = keys;
                    }

                    if (options.Events == null)
                    {
                        options.Events = new JwtBearerEvents();
                    }

                    // When an access token for our own web API is validated, we add it to MSAL.NET's cache so that it can
                    // be used from the controllers.

                    if (!mergedOptions.AllowWebApiToBeAuthorizedByACL)
                    {
                        ChainOnTokenValidatedEventForClaimsValidation(options.Events, jwtBearerScheme);
                    }

                    if (subscribeToJwtBearerMiddlewareDiagnosticsEvents)
                    {
                        var diagnostics = serviceProvider.GetRequiredService<IJwtBearerMiddlewareDiagnostics>();

                        diagnostics.Subscribe(options.Events);
                    }
                });
        }

        /// <summary>
        /// In order to ensure that the Web API only accepts tokens from tenants where it has been consented and provisioned, a token that
        /// has neither Roles nor Scopes claims should be rejected. To enforce that rule, add an event handler to the beginning of the
        /// <see cref="JwtBearerEvents.OnTokenValidated"/> handler chain that rejects tokens that don't meet the rules.
        /// </summary>
        /// <param name="events">The <see cref="JwtBearerEvents"/> object to modify.</param>
        /// <param name="jwtBearerScheme">The JWT bearer scheme name to be used. By default it uses "Bearer".</param>
        internal static void ChainOnTokenValidatedEventForClaimsValidation(JwtBearerEvents events, string jwtBearerScheme)
        {
            var tokenValidatedHandler = events.OnTokenValidated;
            events.OnTokenValidated = async context =>
            {
                if (!context!.Principal!.Claims.Any(x => x.Type == ClaimConstants.Scope
                        || x.Type == ClaimConstants.Scp
                        || x.Type == ClaimConstants.Roles
                        || x.Type == ClaimConstants.Role))
                {
                    context.Fail(string.Format(CultureInfo.InvariantCulture, IDWebErrorMessage.NeitherScopeOrRolesClaimFoundInToken, jwtBearerScheme));
                }

                await tokenValidatedHandler(context).ConfigureAwait(false);
            };
        }
    }
}
