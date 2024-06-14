using Newtonsoft.Json;

namespace WalletFramework.Oid4Vc.Oid4Vci.Models.Authorization
{
    /// <summary>
    ///    Represents the authorization details.
    /// </summary>
    internal record AuthorizationDetails
    {
        /// <summary>
        ///    Gets the type of the credential.
        /// </summary>
        [JsonProperty("type")] 
        public string Type { get; } = "openid_credential";
        
        /// <summary>
        ///   Gets or Sets the format of the credential.
        /// </summary>
        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string? Format { get; }
        
        /// <summary>
        ///   Gets or Sets the verifiable credential type (vct).
        /// </summary>
        [JsonProperty("vct", NullValueHandling = NullValueHandling.Ignore)]
        public string? Vct { get; }
            
        /// <summary>
        ///  Gets or Sets the credential configuration id.
        /// </summary>
        [JsonProperty("credential_configuration_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? CredentialConfigurationId { get; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("locations", NullValueHandling = NullValueHandling.Ignore)]
        public string[]? Locations { get; }
        
        internal AuthorizationDetails(
            string? format, 
            string? vct, 
            string? credentialConfigurationId, 
            string[]? locations)
        {
            if (!string.IsNullOrWhiteSpace(format) && !string.IsNullOrWhiteSpace(credentialConfigurationId))
            {
                throw new ArgumentException("Both format and credentialConfigurationId cannot be present at the same time.");
            }
            
            Format = format;
            Vct = vct;
            CredentialConfigurationId = credentialConfigurationId;
            Locations = locations;
        }
    }
}
