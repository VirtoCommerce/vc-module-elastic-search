namespace VirtoCommerce.ElasticSearchModule.Data
{
    public class ElasticSearchOptions
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Key { get; set; }
        public bool EnableHttpCompression { get; set; } = false;
        public bool EnableCompatibilityMode { get; set; } = false;
        public string CertificateFingerprint { get; set; }
    }
}
