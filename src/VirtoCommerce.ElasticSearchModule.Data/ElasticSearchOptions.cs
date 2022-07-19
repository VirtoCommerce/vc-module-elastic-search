namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchOptions
    {
        public string Server { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string EnableHttpCompression { get; set; }
        public string EnableCompatibilityMode { get; set; }
        public string CertificateFingerprint { get; set; }
    }
}
