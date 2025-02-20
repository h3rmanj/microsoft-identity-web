﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Identity.Web.Test.Common;
using Newtonsoft.Json;

namespace Microsoft.Identity.Web.Test.LabInfrastructure
{
    /// <summary>
    /// Wrapper for lab service API.
    /// </summary>
    public class LabServiceApi
    {
        private readonly string _labAccessAppId;
        private readonly string _labAccessClientSecret;
        private string _labApiAccessToken;

        public LabServiceApi()
        {
            KeyVaultSecretsProvider keyVaultSecretsProvider = new();
            _labAccessAppId = keyVaultSecretsProvider.GetMsidLabSecret(TestConstants.LabVaultAppId).Value;
            _labAccessClientSecret = keyVaultSecretsProvider.GetMsidLabSecret(TestConstants.LabVaultAppSecret).Value;
        }

        public async Task<string> GetUserSecretAsync(string lab)
        {
            IDictionary<string, string> queryDict = new Dictionary<string, string>
            {
                { "secret", lab },
            };

            string result = await SendLabRequestAsync(LabApiConstants.LabUserCredentialEndpoint, queryDict).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<LabCredentialResponse>(result).Secret;
        }

        /// <summary>
        /// Returns a test user account for use in testing.
        /// </summary>
        /// <param name="query">Any and all parameters that the returned user should satisfy.</param>
        /// <returns>Users that match the given query parameters.</returns>
        public async Task<LabResponse> GetLabResponseFromApiAsync(UserQuery query)
        {
            // Fetch user
            string result = await RunQueryAsync(query).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new LabUserNotFoundException(query, "No lab user with specified parameters exists");
            }

            return CreateLabResponseFromResultStringAsync(result).Result;
        }

        internal async Task<LabResponse> CreateLabResponseFromResultStringAsync(string result)
        {
            LabUser[] userResponses = JsonConvert.DeserializeObject<LabUser[]>(result);

            var user = userResponses[0];

            var appResponse = await GetLabResponseAsync(LabApiConstants.LabAppEndpoint + user.AppId).ConfigureAwait(false);
            LabApp[] labApps = JsonConvert.DeserializeObject<LabApp[]>(appResponse);

            var labInfoResponse = await GetLabResponseAsync(LabApiConstants.LabInfoEndpoint + user.LabName).ConfigureAwait(false);
            Lab[] labs = JsonConvert.DeserializeObject<Lab[]>(labInfoResponse);

            user.TenantId = labs[0].TenantId;
            user.FederationProvider = labs[0].FederationProvider;

            return new LabResponse
            {
                User = user,
                App = labApps[0],
                Lab = labs[0],
            };
        }

        private Task<string> RunQueryAsync(UserQuery query)
        {
            IDictionary<string, string> queryDict = new Dictionary<string, string>();

            // Building user query
            // Required parameters will be set to default if not supplied by the test code
            queryDict.Add(LabApiConstants.MultiFactorAuthentication, query.MFA != null ? query.MFA.ToString() : MFA.None.ToString());
            queryDict.Add(LabApiConstants.ProtectionPolicy, query.ProtectionPolicy != null ? query.ProtectionPolicy.ToString() : ProtectionPolicy.None.ToString());

            if (query.UserType != null)
            {
                queryDict.Add(LabApiConstants.UserType, query.UserType.ToString());
            }

            if (query.HomeDomain != null)
            {
                queryDict.Add(LabApiConstants.HomeDomain, query.HomeDomain.ToString());
            }

            if (query.HomeUPN != null)
            {
                queryDict.Add(LabApiConstants.HomeUPN, query.HomeUPN.ToString());
            }

            if (query.B2CIdentityProvider != null)
            {
                queryDict.Add(LabApiConstants.B2CProvider, query.B2CIdentityProvider.ToString());
            }

            if (query.FederationProvider != null)
            {
                queryDict.Add(LabApiConstants.FederationProvider, query.FederationProvider.ToString());
            }

            if (query.AzureEnvironment != null)
            {
                queryDict.Add(LabApiConstants.AzureEnvironment, query.AzureEnvironment.ToString());
            }

            if (query.SignInAudience != null)
            {
                queryDict.Add(LabApiConstants.SignInAudience, query.SignInAudience.ToString());
            }

            return SendLabRequestAsync(LabApiConstants.LabEndPoint, queryDict);
        }

        private async Task<string> SendLabRequestAsync(string requestUrl, IDictionary<string, string> queryDict)
        {
            UriBuilder uriBuilder = new UriBuilder(requestUrl)
            {
                Query = string.Join("&", queryDict.Select(x => x.Key + "=" + x.Value.ToString(CultureInfo.InvariantCulture))),
            };

            return await GetLabResponseAsync(uriBuilder.ToString()).ConfigureAwait(false);
        }

        internal async Task<string> GetLabResponseAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(_labApiAccessToken))
            {
                _labApiAccessToken = await LabAuthenticationHelper.GetAccessTokenForLabAPIAsync(_labAccessAppId, _labAccessClientSecret).ConfigureAwait(false);
            }

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", string.Format(CultureInfo.InvariantCulture, "bearer {0}", _labApiAccessToken));
                return await httpClient.GetStringAsync(address).ConfigureAwait(false);
            }
        }
    }
}
