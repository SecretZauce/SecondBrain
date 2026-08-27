namespace SecretZauce.SecondBrain.Editor
{
    /// <summary>
    /// Where to send someone who wants Pro.
    ///
    /// Pro is not enabled by a scripting define: the Pro package ships the free code alongside
    /// its own, so Pro is active whenever its assembly is present. To ask whether it is active,
    /// check <c>ProFeature.Provider != null</c>.
    /// </summary>
    public static class ProLicenseUtils
    {
        /// <summary>
        /// Unity Asset Store page for SecondBrain PRO.
        /// </summary>
        public const string ASSET_STORE_URL = "https://assetstore.unity.com/packages/slug/383598";
    }
}
